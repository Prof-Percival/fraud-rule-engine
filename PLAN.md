# Fraud Rule Engine Service: Build Plan

The plan for the fraud rule engine. It covers what the brief asks for, the architecture,
the order the work is done in, and the reasoning behind the main choices.

## 1. What the brief asks for

> Create a system that processes categorized transaction events and flags potential fraud.
> Apply a set of fraud rules per transaction based on different criteria and then store them
> in a data store. Allow the retrieval of this data via an API.

Four obligations, worth separating because each drives different design work:

1. **Process categorized transaction events.** Input is already categorized. Something upstream
   did the categorisation, which means this service consumes a contract it does not own. Input
   validation matters, unknown categories cannot crash the engine, and the same event may
   arrive twice.
2. **Apply a set of rules based on different criteria.** Plural, and explicitly varied. A single
   amount threshold does not satisfy this. The rule set needs stateless rules, rules that need
   customer history, rules driven by configuration, and rules driven by reference data. That
   variety is what calls for a real engine design rather than a method full of if statements.
3. **Store them in a data store.** The flagged results, and because a result cannot be explained
   without the input and the rule set that produced it, all three are persisted.
4. **Retrieval via an API.** The query patterns drive the indexes and the pagination strategy.

Plus the three cross cutting requirements: production grade, a runnable Dockerfile, and a
README covering build, run and test.

## 2. Scope boundary

A production fraud platform is event driven off a broker, has a rules DSL so analysts can
change rules without a deploy, does ML scoring alongside deterministic rules, and runs a case
management workflow for the analysts who review flags. None of that is in scope here. Each
exclusion is recorded in an ADR with the reasoning and what would change if it were in scope.

**In scope**

- Synchronous evaluation endpoint, single transaction, returns the assessment
- Batch ingestion endpoint for bulk event processing
- Eight rules spanning four different kinds of criteria
- Deterministic risk scoring and a decision policy with configurable thresholds
- Full persistence of events, assessments and individual rule outcomes
- Query API with filtering, keyset pagination and a summary aggregate endpoint
- Idempotent ingestion
- Rule set versioning so historical assessments stay reproducible
- Observability: structured logs, OpenTelemetry traces and metrics, health probes
- API key authentication and per client rate limiting
- Containerised, non root, with compose bringing up Postgres alongside it
- Unit tests on every rule and on the scoring policy, integration tests against real Postgres

**Deliberately out of scope, each with an ADR**

- Message broker ingestion. HTTP plus an internal queue shows the same reasoning without
  adding infrastructure a reviewer has to run.
- Rules DSL or a rules engine library such as NRules. Discussed in ADR 0004.
- ML or behavioural scoring
- Case management and analyst workflow
- Multi tenancy
- Real time streaming aggregates. Aggregates are computed from the transaction table.

## 3. Architecture

Four projects, clean architecture layering, dependencies pointing inward.

```
FraudRuleEngine.Domain          no external dependencies at all
FraudRuleEngine.Application     depends on Domain
FraudRuleEngine.Infrastructure  depends on Application and Domain
FraudRuleEngine.Api             depends on all three, composition root only
```

The reason for four projects rather than one is that the compiler then enforces the
layering. The domain project cannot reference EF Core because the reference is not there, so
persistence concerns cannot leak into the rules. ADR 0002 records the alternative, vertical
slices in a single project, and why it was not chosen for this shape of problem.

### 3.1 The engine design

The rules sit behind one interface:

```csharp
public interface IFraudRule
{
    RuleId Id { get; }
    RuleOutcome Evaluate(FraudEvaluationContext context);
}
```

Three things make this work.

**Rules are pure functions.** `Evaluate` is synchronous and does no IO. No rule reaches for a
database. If rules could do IO, eight rules would mean eight round trips per transaction, the
rules would be awkward to test, and evaluation latency would become unpredictable. Making them
pure means every rule test is a plain in memory unit test with no mocks.

**The context carries pre loaded enrichment.** Because rules cannot fetch data, an enrichment
step loads it first. `FraudEvaluationContext` holds the transaction plus everything any rule
might need: a window of the customer's recent transactions, their rolling amount average, the
set of merchants they have used before, and the relevant reference data, all loaded in one
pass before evaluation begins.

The tradeoff is that this over fetches. A transaction that trips the amount rule still pays for
the history load. That is accepted because the fetch is a single indexed query on a bounded
window, and the alternative, lazy loading per rule, reintroduces IO into the rules. If
profiling showed it mattered, the fix is to split enrichment into tiers and load the expensive
tier only when a cheap rule has already raised suspicion.

**Rule outcomes are explainable.** Every outcome carries the rule id, whether it triggered, a
score contribution, a severity, and a human readable reason with the actual values that caused
it. In a fraud system a flag that cannot be explained is close to useless, because a human has
to action it and a regulator may ask about it.

Composition:

```
FraudRuleEvaluator
  runs the enabled rules, collects outcomes, hands them to
IRiskScoringPolicy
  aggregates score contributions and applies thresholds, producing
FraudAssessment
  { TransactionId, RiskScore, Decision, RuleOutcomes[], RuleSetVersion, EvaluatedAt }
```

Decision is `Approve`, `Review` or `Decline`. Three states rather than a boolean, because most
flagged transactions go to a human, not straight to a block.

Evaluation runs the rules sequentially, not in parallel. Eight in memory predicate evaluations
take microseconds, so parallelising would add scheduling overhead and non deterministic
ordering for nothing, and stable ordering keeps two stored assessments comparable.

### 3.2 Rule set

Eight rules, chosen so the set spans four different kinds of criteria. The spread is the point,
not the count.

| Rule | Criteria type | Needs |
|---|---|---|
| HighValueTransaction | Stateless threshold | Config per currency |
| HighRiskCategory | Reference data | Category watchlist |
| DeniedMerchant | Reference data | Merchant deny list |
| UnusualHour | Config driven window | Config |
| TransactionVelocity | Customer history | Recent transaction window |
| ImpossibleTravel | Customer history, geo | Recent transactions with country |
| FirstTimeMerchantHighValue | Customer history | Known merchant set |
| AmountEscalation | Customer aggregate | Rolling average |

The first four need nothing but the transaction and configuration. The last four need the
enrichment context, which is what makes it worth building.

### 3.3 Persistence

EF Core with Npgsql against PostgreSQL.

```
transaction_events   the ingested event, unique on (event_id)
fraud_assessments    one per evaluated transaction, FK to transaction_events
rule_outcomes        one row per rule per assessment, FK to fraud_assessments
merchant_denylist    reference data
```

Design points:

- **`rule_outcomes` stores every rule that ran, not only the ones that triggered.** It costs
  more rows, and it is the right call: "which rules did not fire on this transaction" is a
  question analysts genuinely ask, and it cannot be answered later if only hits are stored.
- **`fraud_assessments.rule_set_version` identifies the configuration that produced it.** Rules
  and thresholds change. Without this, an assessment from three months ago cannot be explained.
  This is an audit requirement in a bank, not a nice to have.
- **Money as `numeric(19,4)` with the currency stored beside it.** Never float.
- **All timestamps `timestamptz`, UTC throughout.** The unusual hour rule needs local time, so
  the conversion happens in the rule using the transaction's offset, not in storage.
- **Indexes driven by the actual query patterns** in section 3.4, not sprinkled across columns.

### 3.4 API surface

Version prefix `/api/v1` from the start. Cheap now, expensive to add later.

```
POST /api/v1/transactions/evaluate        evaluate one, returns the assessment
POST /api/v1/transactions/batch           bulk ingest, returns per item results
GET  /api/v1/assessments                  filter and page
GET  /api/v1/assessments/{id}             single, with full rule outcome detail
GET  /api/v1/assessments/summary          counts by decision, most triggered rules
GET  /api/v1/customers/{id}/assessments   customer view
GET  /api/v1/rules                        rule catalogue and live config
GET  /health/live  GET /health/ready      probes, unauthenticated
```

`GET /assessments` filters on customer, decision, minimum score and a time range. Those four
filters plus the customer endpoint are what the indexes are built for.

Pagination is keyset, ordered on `(evaluated_at desc, id desc)` with an opaque cursor. Offset
paging degrades as the offset grows, and it skips or repeats rows when new data lands between
page requests, which on a table taking continuous inserts is a correctness problem rather than
just a performance one.

Errors are `application/problem+json` per RFC 9457, one exception handler mapping domain
exceptions to status codes, no try catch scattered through the endpoints.

The `/rules` endpoint lets an operator see which rules are live and at what thresholds without
reading the source or the database.

### 3.5 Cross cutting

- **Configuration.** Rule thresholds and enablement bind to strongly typed options with
  validation on startup, so a bad threshold fails the boot rather than silently disabling a
  rule. The version stamped on each assessment derives from the bound config.
- **Idempotency.** Producers retry, so the same `event_id` will arrive twice. A unique
  constraint on `event_id` guards it, and a duplicate returns the original assessment with 200
  rather than creating a second one. The constraint carries this rather than an application
  level read before write, because two concurrent requests would both pass that read.
- **Observability.** Serilog for structured JSON logs, OpenTelemetry for traces and metrics.
  Metrics a fraud service actually needs: evaluations per second, decision distribution, per
  rule trigger rate, evaluation latency. A spike in one rule's trigger rate is an incident
  signal.
- **PII.** Card numbers masked to last four before anything is logged, customer identifiers not
  written into log messages. A logged PAN is a reportable incident.
- **Security.** API key on all data endpoints, health probes open. Per client rate limiting
  with the fixed window limiter. ADR 0006 covers why not OAuth2 or mTLS here.

## 4. Build order

The sequence the work is done in. Each abstraction is introduced at the point where it is
needed, which keeps the diffs small and the history readable.

### Commit style

Subject line is a sentence describing what the commit does, starting with a capital letter, no
trailing full stop, kept under about seventy characters, no `feat:` or `chore:` prefixes. Where
a change needs more than the subject, the body is prose: what the problem was, what was chosen,
and what was rejected.

### Foundations

1. **Initialise the solution and build configuration.** Solution file, the four source projects
   wired up with references pointing inward, `Directory.Build.props` with warnings as errors and
   nullable enabled, `Directory.Packages.props` for central package versions, a
   `tests/Directory.Build.props` holding the shared test configuration, `global.json` pinning
   the SDK and selecting the test runner, `.editorconfig`, `.gitignore`.

   `FraudRuleEngine.Domain.Tests` is created here with two architecture tests asserting the
   domain references nothing from persistence, hosting or observability, which turns the
   dependency rule in ADR 0001 into something the build enforces. The other two test projects
   are created at the step where they first have something to test, since an empty test project
   is speculative and the test platform treats a project with zero tests as a failure.
2. **Record the first architecture decisions.** ADRs 0001 to 0003.

### Domain model

3. **Add the transaction event model and money type.** `TransactionEvent`, `Money` as a value
   object that refuses arithmetic across mismatched currencies, the `Channel` and
   `TransactionCategory` enums, and typed identifiers such as `CustomerId`.
4. **Cover money arithmetic and currency mismatch with tests.** Money is where a silent bug does
   the most damage, so it is the right place to start.

### Rules and the engine

5. **Add the rule interface and the stateless rules.** `IFraudRule`, the `FraudRuleEvaluator`,
   and the rules that need only the transaction and configuration: high value, high risk
   category, denied merchant and unusual hour. The unusual hour rule handles local time from the
   transaction's own offset, since the question is what time it was for the customer.
6. **Introduce the evaluation context and the history rules.** The velocity rule cannot be a
   pure function without customer history, so the enrichment context appears here. Velocity and
   impossible travel read the recent window; impossible travel needs a distance calculation kept
   in the domain as a small geo helper with its own tests.
7. **Add the customer aggregate rules.** First time merchant and amount escalation, which read
   the rolling average and known merchant set. Rule set complete at eight.

### Scoring and decisions

8. **Add the risk scoring policy and decision thresholds.** Weighted aggregation of rule
   outcomes, thresholds for the three decision states, and unit coverage including the exact
   boundaries.

### Application layer

9. **Add the evaluate transaction use case.** The handler that orchestrates enrichment,
   evaluation, scoring and persistence. Ports are defined here as interfaces with no
   implementations yet, which keeps the step testable on its own.

### Persistence

10. **Add EF Core persistence and the initial migration.** DbContext, entity configurations in
    separate classes rather than attributes on the domain types, the initial migration, the
    store implementations, and the indexes the query patterns need.

### API

11. **Add the evaluation endpoint.** Minimal API endpoint, request validation, the mapping from
    domain exceptions to problem details, OpenAPI.
12. **Add assessment retrieval endpoints.** Filtering, the single and summary endpoints, the
    customer view, and keyset pagination with an opaque cursor.
13. **Add batch ingestion.** Bulk path with per item results, so one malformed event does not
    fail the whole batch.
14. **Make ingestion idempotent on event id.** A unique constraint, the migration for it, and a
    duplicate returning the existing assessment. The constraint carries this rather than a read
    before write, which does not hold under concurrency.

### Hardening

15. **Move rule thresholds into validated configuration.** Thresholds bind to options validated
    at startup, so a bad value fails the boot instead of silently disabling a rule. The rule set
    version derives from the configuration and is stamped onto every assessment.
16. **Add structured logging, tracing and metrics.** Serilog, OpenTelemetry, correlation ids,
    the fraud specific metrics, health probes, and card number masking.
17. **Add API key authentication and rate limiting.**

### Delivery

18. **Add the Dockerfile and compose stack.** Build, test and final stages. Chiseled runtime
    image, non root user, compose with Postgres behind a healthcheck, a `tests` service on a
    `test` profile, and migrations applied on startup outside production only. The target is that
    a reviewer with only Docker installed can build, run and test the whole thing.
19. **Add integration tests against PostgreSQL.** Real database, real HTTP pipeline through
    `WebApplicationFactory`, covering the idempotency path, the pagination cursor across a page
    boundary, and a full evaluate then retrieve round trip. The fixture resolves its database
    from `ConnectionStrings__Default` when set, and otherwise starts a throwaway Postgres through
    Testcontainers, so the same test code runs in CI and from an IDE.
20. **Add the build and test workflow, request collection and README.** A CI workflow, a
    `requests.http` collection covering every endpoint, and the README with operational notes.

## 5. Testing strategy

- **Domain unit tests.** Every rule, every boundary. No mocks, because the rules are pure.
- **Scoring policy tests.** Threshold boundaries explicitly, including the exact values.
- **Application tests.** Use case orchestration with hand written fakes rather than a mock
  framework, because fakes read better in tests that describe behaviour.
- **Integration tests.** Testcontainers spinning real Postgres. Migrations, the unique
  constraint rejecting a duplicate, keyset paging across a boundary.
- **API tests.** `WebApplicationFactory` through the real pipeline including auth and error
  mapping.

xUnit as the framework. Assertions with Shouldly rather than FluentAssertions, since
FluentAssertions moved to a paid licence for commercial use at version 8.

Coverage is not a target. Rules and scoring are pure logic and should be at or near full
coverage; wiring code is not chased for its own sake.

## 6. Where this design breaks

The limits, recorded as design decisions rather than things left unexamined.

**The synchronous evaluation endpoint is the first constraint.** Every request does an
enrichment query, eight rule evaluations and two writes. The rules are microseconds; the
database is everything. At a few hundred requests per second on modest hardware this holds.
Beyond that, the enrichment read is the bottleneck.

**First fix is caching the enrichment.** Customer aggregates and known merchant sets change
slowly, so a short lived cache absorbs most of the read load. The cost is staleness, which
matters for velocity specifically, so the window aggregates need different treatment from the
slow moving data.

**Second fix is splitting the write path.** Persisting the assessment does not need to be inside
the request. Write the decision, queue the detail, and endpoint latency drops to the enrichment
read plus one insert.

**Beyond that the architecture changes rather than gets tuned.** Velocity and escalation want a
streaming aggregate maintained per customer rather than recomputed per request, which means
events off a broker, a stateful stream processor keeping windowed counters, and the engine
reading a materialised customer profile. At that point ingestion is asynchronous and the
synchronous endpoint becomes a thin read against a precomputed profile. It is not built that way
now because it needs a broker, a stream processor and a state store that a reviewer would have
to run, none of which change the rule engine design that is the subject of the brief.

## 7. Repository layout

```
fraud-rule-engine/
  README.md
  PLAN.md
  Dockerfile
  compose.yaml
  global.json
  Directory.Build.props
  Directory.Packages.props
  .editorconfig
  .dockerignore
  FraudRuleEngine.sln
  docs/
    requests.http
    adr/
      README.md
      0001-layered-architecture.md
      0002-projects-over-vertical-slices.md
      0003-rules-as-pure-functions.md
      0004-no-rules-dsl-or-engine-library.md
      0005-http-ingestion-not-a-broker.md
      0006-api-key-authentication.md
      0007-postgresql-as-the-data-store.md
      0008-keyset-pagination.md
  src/
    FraudRuleEngine.Domain/
      Transactions/
      Rules/
      Scoring/
      Assessments/
    FraudRuleEngine.Application/
      Evaluation/
      Abstractions/
    FraudRuleEngine.Infrastructure/
      Persistence/
        Configurations/
        Migrations/
      Enrichment/
      ReferenceData/
    FraudRuleEngine.Api/
      Endpoints/
      Contracts/
      Middleware/
      Configuration/
  tests/
    FraudRuleEngine.Domain.Tests/
    FraudRuleEngine.Application.Tests/
    FraudRuleEngine.IntegrationTests/
```

## 8. Before starting

Only Docker is needed to build, run and test the service, since the SDK lives in the build
stage of the image. See the README for details.

A local .NET 10 SDK is needed for IDE work, debugging, and generating EF Core migrations with
`dotnet ef`.
