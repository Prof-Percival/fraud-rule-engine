# 0004. No rules DSL or third party rules engine

Date: 2026-09-01

Status: Accepted

## Context

The brief asks for a set of rules applied per transaction. A common instinct at that phrasing is
to reach for a rules engine, either a library such as NRules built on a Rete network, or a small
domain specific language so that analysts can change rules without a deploy. Both are real options
and both carry weight, so the choice is worth recording rather than defaulting into.

## Decision

Rules are plain C# types behind one interface, `IFraudRule`. There is no DSL and no third party
rules engine.

```csharp
public interface IFraudRule
{
    RuleId Id { get; }
    RuleOutcome Evaluate(FraudEvaluationContext context);
}
```

The thresholds a rule uses are configuration, bound and validated at startup, which is the part
analysts actually tune. The logic of a rule stays in code.

## Consequences

A rule is a unit tested pure function with the full language available: loops, the type system, the
debugger, ordinary refactoring. There is no rule runtime to learn, no rule store to keep consistent
with the code, and no expression evaluator to sanitise against untrusted input.

Changing what a rule *does*, rather than the numbers it uses, needs a deploy. For a set of eight
rules that change rarely, that is an acceptable trade, and the configuration split in ADR 0009 means
the frequently tuned part, the thresholds, does not.

The scoring policy adding the outcomes is also plain code, so the whole path from transaction to
decision can be read top to bottom without stepping through a framework.

## Alternatives

**A rules engine library such as NRules.** A Rete network pays off when many rules share facts and
re-evaluate as facts change, across hundreds or thousands of rules. Here there are eight, each
evaluated once against one pre loaded context, so the matching algorithm solves a problem this
service does not have, in exchange for a dependency and a second mental model layered over the
domain.

**A custom DSL for analysts.** The appeal is changing rule logic without a deploy. The cost is a
language to design, parse, sandbox and version, plus an editor and a safety story for what happens
when a bad rule is published. That is a product in itself and larger than the brief. The middle
ground, configurable thresholds over fixed logic, captures most of the benefit for a fraction of the
cost.

**Configuration driven rules.** Expressing whole rules in configuration rather than code. Rejected
for the same reason as a DSL in miniature: it drifts toward an untyped, untestable language in JSON.
Thresholds belong in configuration; branching logic belongs in code.
