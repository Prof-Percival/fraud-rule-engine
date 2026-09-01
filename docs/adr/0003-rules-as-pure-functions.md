# 0003. Rules are pure synchronous functions with no IO

Date: 2026-09-01
Status: Accepted

## Context

Rules need different data. Some need only the transaction and a threshold. Others need the
customer's recent transaction history, their rolling average amount, or the set of merchants
they have used before.

The obvious way to give a rule what it needs is to let it fetch it:

```csharp
public async Task<RuleOutcome> EvaluateAsync(TransactionEvent txn, CancellationToken ct)
{
    var recent = await _repository.GetRecentAsync(txn.CustomerId, _window, ct);
    return recent.Count > _threshold ? RuleOutcome.Triggered(...) : RuleOutcome.Clear(...);
}
```

This works and it is what most implementations do. It has three problems that compound as
the rule set grows.

Latency becomes a function of the rule count. Eight rules that each query means eight round
trips per transaction, several of them fetching overlapping data. At any real throughput the
database sees a multiple of the request rate.

Every rule test needs a mocked repository. The tests stop being about fraud logic and start
being about mock setup. When a rule has four branches, that is four mock configurations to
express something that is really just a predicate over data.

Rule evaluation stops being deterministic and becomes dependent on database state at the
moment each individual rule happens to run. Two rules reading the same window a few
milliseconds apart can see different data.

## Decision

`IFraudRule.Evaluate` is synchronous, takes everything it needs as an argument, and performs
no IO.

```csharp
public interface IFraudRule
{
    RuleId Id { get; }
    RuleOutcome Evaluate(FraudEvaluationContext context);
}
```

`FraudEvaluationContext` is assembled once per transaction before evaluation begins, by an
enrichment step in the application layer. It holds the transaction, the customer's recent
transaction window, their aggregates, their known merchant set, and the relevant reference
data.

Rules are therefore pure functions from context to outcome.

## Consequences

Every rule test is a plain unit test. Build a context, call `Evaluate`, assert on the
outcome. No mocks, no fixtures, no async. This is the main thing being bought, and it is
what makes it realistic to cover every branch of every rule properly.

One enrichment pass replaces N per rule queries. Evaluation latency becomes one read plus
some microseconds of predicate evaluation, and it does not grow when a rule is added.

All rules see one consistent snapshot of the customer's state, so evaluation is
deterministic and reproducible. Given the same context, the same rule set produces the same
assessment every time. That is what makes replaying a historical assessment meaningful.

Evaluation runs sequentially. There is no reason to parallelise in memory predicates, and
sequential execution keeps rule ordering deterministic.

**The cost is over fetching.** The enrichment step does not know which rules will fire, so
it loads everything any rule might need. A transaction that trips the amount threshold on
the first rule still pays for the history load. This is accepted because the enrichment is a
single indexed query over a bounded time window, which is cheap and predictable, and
predictable is worth more here than optimal.

**The context is a growing surface.** Every new rule that needs new data widens
`FraudEvaluationContext` and the enrichment step that fills it. At eight rules this is
comfortable. At fifty it would need splitting into per rule data requirements, with the
enrichment step composing the union of what the enabled rules declare they need.

## If the over fetching became a problem

The fix is tiered enrichment, not abandoning purity. Split the context into a cheap tier
loaded always and an expensive tier loaded only when a cheap rule has already raised
suspicion, then evaluate in two passes. Rules stay pure functions and only the enrichment
step changes.

Second option is caching the slow moving parts. Known merchant sets and rolling averages
change slowly, so they tolerate a short lived cache. Velocity windows do not and would stay
uncached.

Neither is built now, because at the current rule count the enrichment query is not the
constraint worth optimising.

## Alternatives

**Async rules that query their own data.** Rejected for the three reasons in the context
section. This is the common implementation and the tradeoff is worth being explicit about:
it is more convenient to write one rule that way, and it gets progressively worse with every
rule added.

**Lazy loading on the context.** A context exposing properties that fetch on first access,
so only what is used gets loaded. Rejected because it reintroduces IO behind a property
access, which means synchronous blocking or an async property, and it makes latency depend
on which rules happen to fire. It also makes the tests worse again, since the context now
needs mocking.
