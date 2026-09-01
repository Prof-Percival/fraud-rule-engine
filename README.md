# Fraud Rule Engine Service

A service that evaluates categorised transaction events against a set of fraud rules,
produces an explainable risk assessment for each one, persists the result, and exposes it
over an HTTP API.

Built for the Capitec technical assessment, brief 2.

> Status: planning complete, implementation in progress. The build sequence is in
> `PLAN.md` and the design decisions are recorded as ADRs in `docs/adr/`. Commands in
> this README are the intended interface and are verified as each step lands.

## Quick start

Docker is the only thing you need installed. The .NET SDK is not required on the host,
because it lives in the build stage of the image.

```bash
docker compose up --build
```

That compiles the service, starts PostgreSQL, applies migrations and serves the API on
<http://localhost:8080>. First build pulls the base images and takes a few minutes.
Subsequent builds are cached.

Check it came up:

```bash
curl http://localhost:8080/health/ready
```

Run the full test suite, again with nothing installed but Docker:

```bash
docker compose --profile test run --rm tests
```

Stop everything and discard the database volume:

```bash
docker compose down -v
```

Identical commands work in PowerShell on Windows, Terminal on macOS and a Linux shell.

## What it does

A transaction event arrives already categorised by an upstream service. This service:

1. Validates it and rejects it with a useful problem response if it is malformed
2. Loads the customer context it needs to evaluate history dependent rules
3. Runs every enabled rule against the transaction
4. Aggregates the rule outcomes into a risk score and one of three decisions
5. Persists the event, the assessment, and the outcome of every rule that ran
6. Returns the assessment, and makes it queryable afterwards

Each assessment records which rules fired, why, with the values that caused it, and which
version of the rule set produced the result. An assessment from six months ago can still be
explained, because the configuration that produced it is stored alongside it.

### Decisions

| Decision | Meaning |
|---|---|
| `Approve` | Score below the review threshold |
| `Review` | Score warrants a human looking at it |
| `Decline` | Score above the decline threshold |

Three states rather than a fraud flag, because in practice most flagged transactions go to
a human rather than being blocked outright.

### Rules

| Rule | Criteria |
|---|---|
| `HighValueTransaction` | Amount above a per currency threshold |
| `HighRiskCategory` | Transaction category on the watchlist |
| `DeniedMerchant` | Merchant on the deny list |
| `UnusualHour` | Outside the customer's expected activity window, in their local time |
| `TransactionVelocity` | Too many transactions inside a rolling window |
| `ImpossibleTravel` | Geographically implausible sequence of transactions |
| `FirstTimeMerchantHighValue` | High value at a merchant the customer has never used |
| `AmountEscalation` | Materially above the customer's rolling average |

Current thresholds and enablement for every rule are visible at `GET /api/v1/rules`.

## Prerequisites

**To build, run and test: Docker only.**

| Platform | Install |
|---|---|
| Windows | `winget install Docker.DockerDesktop` |
| macOS | `brew install --cask docker` |

On Windows, use the WSL2 backend, which is the Docker Desktop default. Nothing else is
needed and no .NET installation is required.

If you would rather not use Docker at all, there are step by step instructions for running
the service directly against a local PostgreSQL under
[running without Docker](#without-docker).

**To develop: add the .NET 10 SDK.**

Needed for IDE work, debugging, and generating EF Core migrations with `dotnet ef`. Not
needed to run or test the service.

| Platform | Install |
|---|---|
| Windows | `winget install Microsoft.DotNet.SDK.10` |
| macOS | `brew install --cask dotnet-sdk` |

Confirm with `dotnet --version`, which should report 10.x.

.NET 10 is the current LTS release, supported until November 2028. .NET 8 and .NET 9 both
reach end of support on 10 November 2026, so neither was a sensible target for new work.

<details>
<summary>Container runtime alternatives</summary>

Docker Desktop requires a paid licence for larger organisations. Finch and Podman both
build this `Dockerfile` and run this `compose.yaml` unchanged. Substitute the CLI name:

```bash
brew install --cask finch
finch vm init      # first run only, downloads a VM
finch vm start
finch compose up --build
```

Everything below is written with `docker` because that is what a reviewer will most likely
have.
</details>

## How the container is built

`Dockerfile` is multi stage:

| Stage | Base image | Purpose |
|---|---|---|
| `build` | `mcr.microsoft.com/dotnet/sdk:10.0` | Restore, build, publish |
| `test` | `build` | Runs `dotnet test` |
| `final` | `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` | Runtime |

The runtime image is Ubuntu Chiseled, which is distroless. No shell, no package manager,
and a non root user by default. Smaller image and a much smaller attack surface than the
full runtime image, which matters more than usual for something sitting in a payment path.

The build stage restores against `Directory.Packages.props` before copying the source, so a
source only change does not invalidate the restore layer.

## Running

### With Docker, which is the supported path

```bash
docker compose up --build          # foreground, logs to console
docker compose up -d --build       # background
docker compose logs -f api         # follow the API logs
docker compose down -v             # stop and drop the database volume
```

Services in the compose stack:

| Service | Port | Notes |
|---|---|---|
| `api` | 8080 | Waits for the database healthcheck before starting |
| `db` | 5432 | PostgreSQL 17, data in a named volume |
| `tests` | none | Only starts under the `test` profile |

Migrations are applied automatically on startup outside production. In production they run
as a separate step, because an application that migrates its own schema on boot will fight
itself the moment it runs more than one replica.

OpenAPI document is at <http://localhost:8080/openapi/v1.json> and the browsable UI at
<http://localhost:8080/scalar/v1>.

### Without Docker

Everything works without Docker. It is more steps, because you install and run the two
things the container would otherwise have handled for you: the .NET 10 SDK and PostgreSQL.

**1. Install the .NET 10 SDK**

| Platform | Install |
|---|---|
| Windows | `winget install Microsoft.DotNet.SDK.10` |
| macOS | `brew install --cask dotnet-sdk` |
| Linux | See <https://learn.microsoft.com/dotnet/core/install/linux> |

Verify with `dotnet --version`, which should report 10.x. Open a new terminal first, since
the installer changes `PATH`.

**2. Install and start PostgreSQL 17**

Windows, which installs a service that starts automatically:

```powershell
winget install PostgreSQL.PostgreSQL.17
```

macOS:

```bash
brew install postgresql@17
brew services start postgresql@17
```

If you would rather not install it at all, a hosted PostgreSQL instance works equally well.
Anything reachable on a connection string is fine, and nothing in the service depends on
where the database runs.

**3. Create the database**

```bash
createdb fraudengine
```

On Windows, `createdb.exe` lives under `C:\Program Files\PostgreSQL\17\bin`, or use pgAdmin,
which the installer includes.

**4. Point the service at it**

Set the connection string as an environment variable. The double underscore is how .NET maps
an environment variable onto a nested configuration key.

PowerShell:

```powershell
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=fraudengine;Username=postgres;Password=yourpassword"
```

bash or zsh:

```bash
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=fraudengine;Username=postgres;Password=yourpassword"
```

**5. Apply the schema**

```bash
dotnet tool restore
dotnet ef database update \
  --project src/FraudRuleEngine.Infrastructure \
  --startup-project src/FraudRuleEngine.Api
```

**6. Run it**

```bash
dotnet restore
dotnet build
dotnet run --project src/FraudRuleEngine.Api
```

The API comes up on <http://localhost:8080>. Confirm with
`curl http://localhost:8080/health/ready`, which reports unhealthy if it cannot reach the
database, so it also verifies steps 2 through 4.

**Testing without Docker**

Unit tests need nothing beyond the SDK:

```bash
dotnet test --filter Category!=Integration
```

The integration tests need a PostgreSQL instance and will use the one from
`ConnectionStrings__Default` if it is set. Without that variable they try to start their own
through Testcontainers, which does need a container runtime, so set the variable if you have
no Docker:

```bash
dotnet test
```

Note that the integration tests create and drop schemas, so point them at a scratch database
rather than one holding anything you care about.

## Testing

Everything in one command, no local SDK:

```bash
docker compose --profile test run --rm tests
```

That runs unit and integration tests against the compose PostgreSQL instance and exits with
the test result as its exit code, so it works unchanged in CI.

Unit tests only, no database and no compose stack:

```bash
docker build --target test --build-arg TEST_FILTER=Category!=Integration .
```

With the SDK installed locally:

```bash
dotnet test                                                    # everything
dotnet test --filter Category!=Integration                     # unit only
dotnet test --collect:"XPlat Code Coverage"                     # with coverage
```

### How the integration tests get a database

Integration tests use a real PostgreSQL instance rather than the EF Core in memory
provider. The in memory provider would not exercise the unique constraint that enforces
idempotency, nor the keyset pagination query, and those are two of the things most worth
testing.

The database comes from one of two places, and the fixture picks without configuration:

- If `ConnectionStrings__Default` is set, it uses that. This is the compose and CI path.
- Otherwise it starts a throwaway PostgreSQL container through Testcontainers. This is the
  path when running `dotnet test` from an IDE.

So the same test code works from a developer machine and from inside the compose stack.

## Using the API

All data endpoints require an API key header. The development key is in
`appsettings.Development.json`. Health probes are unauthenticated.

There is a `docs/requests.http` file covering every endpoint with realistic payloads. It
runs directly in Visual Studio, Rider and the VS Code REST Client extension, which is the
easiest way to exercise the API on Windows.

Evaluate a transaction:

```bash
curl -X POST http://localhost:8080/api/v1/transactions/evaluate \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: dev-local-key" \
  -d '{
    "eventId": "8f2a1c74-6b3e-4d19-9a52-1e7c4f8b2d63",
    "transactionId": "TXN-000123",
    "customerId": "CUST-4471",
    "accountId": "ACC-9920",
    "amount": 48500.00,
    "currency": "ZAR",
    "category": "CashWithdrawal",
    "channel": "Atm",
    "merchantId": "MERCH-771",
    "merchantName": "ATM Sandton City",
    "countryCode": "ZA",
    "occurredAt": "2026-09-01T02:14:00Z"
  }'
```

Returns the assessment, the score, the decision, and every rule that ran with the reason
it did or did not fire.

In PowerShell, `curl` is an alias for `Invoke-WebRequest` and will not accept these flags.
Use `curl.exe` explicitly, or use `docs/requests.http`.

Query assessments:

```bash
curl -H "X-Api-Key: dev-local-key" \
  "http://localhost:8080/api/v1/assessments?decision=Review&minScore=60&limit=25"
```

Paging is keyset based. Responses carry a `nextCursor`, which is passed back as
`?cursor=...`. Offset paging was deliberately replaced, because it degrades on large tables
and it repeats or skips rows when inserts land between page requests.

Summary counts:

```bash
curl -H "X-Api-Key: dev-local-key" http://localhost:8080/api/v1/assessments/summary
```

### Endpoints

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/v1/transactions/evaluate` | Evaluate one transaction |
| POST | `/api/v1/transactions/batch` | Bulk ingest, per item results |
| GET | `/api/v1/assessments` | Filter and page assessments |
| GET | `/api/v1/assessments/{id}` | Single assessment with rule detail |
| GET | `/api/v1/assessments/summary` | Counts by decision, most triggered rules |
| GET | `/api/v1/customers/{id}/assessments` | Assessments for one customer |
| GET | `/api/v1/rules` | Rule catalogue and live configuration |
| GET | `/health/live` | Liveness, unauthenticated |
| GET | `/health/ready` | Readiness including database, unauthenticated |

Errors are `application/problem+json` per RFC 9457.

## Architecture

```
FraudRuleEngine.Domain          rules, scoring, entities. No external dependencies.
FraudRuleEngine.Application     use cases and the ports they depend on.
FraudRuleEngine.Infrastructure  EF Core persistence, enrichment, reference data.
FraudRuleEngine.Api             HTTP surface and composition root.
```

Dependencies point inward. The domain project has no package references, so persistence
concerns cannot leak into the rules. The compiler enforces the layering rather than a
convention document doing it.

Every rule implements one interface:

```csharp
public interface IFraudRule
{
    RuleId Id { get; }
    RuleOutcome Evaluate(FraudEvaluationContext context);
}
```

`Evaluate` is synchronous and performs no IO. Rules cannot query the database. Everything
a rule might need is loaded once into `FraudEvaluationContext` before evaluation starts:
the customer's recent transaction window, their rolling average, the merchants they have
used before, and the relevant reference data.

That constraint is the central design decision. It keeps evaluation latency predictable,
avoids one database round trip per rule, and makes every rule test a plain in memory unit
test with no mocking. It also over fetches, since a transaction caught by the amount
threshold still pays for the history load. That tradeoff and the plan for tiering the
enrichment if it ever mattered are recorded in ADR 0003.

Adding a rule is one class, one DI registration and one test file. No existing rule is
touched.

Design decisions are recorded as ADRs in `docs/adr/`, including the ones about what was
deliberately left out and why.

## Known limits

Stated plainly because they are design decisions rather than oversights.

The synchronous evaluation path holds at a few hundred requests per second on modest
hardware. The rules themselves are microseconds; the enrichment read is the bottleneck.
Caching the slow moving parts of the context handles the next increment, and moving
assessment detail writes out of the request path handles the one after that.

Past that the architecture needs to change rather than be tuned. Velocity and escalation
rules want streaming per customer aggregates maintained by a stream processor, with the
engine reading a materialised customer profile instead of querying transaction history.
That means broker based ingestion and a state store. It is the right answer at that scale
and the wrong first version, since it does not change the rule engine design, which is
what this brief is about.

Also out of scope, with reasoning in `docs/adr/`: a rules DSL for analyst managed rules,
ML scoring, case management workflow, and multi tenancy.
