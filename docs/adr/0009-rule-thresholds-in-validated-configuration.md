# 0009. Rule thresholds live in validated configuration

Date: 2026-09-03

Status: Accepted

## Context

Every rule held its numbers as compiled fields: the high value thresholds per currency, the
velocity count and window, the impossible travel speed ceiling, the scoring weights, the
review and decline thresholds. These are the figures a fraud analyst tunes in response to
what the engine is catching and missing. Tuning any of them meant a code change, a build and
a deploy, which is too slow a loop for numbers that move with fraud patterns.

Two failure modes matter more than the inconvenience. A threshold set wrong in a way that
disables a rule fails silently: the engine runs, returns verdicts, and quietly stops catching
a class of fraud, with nothing to signal it. And the rule set version stamped on each
assessment was a compiled constant, so once the numbers could change, an assessment could
claim a version whose thresholds it was never scored under. In a regulated setting that
breaks the ability to explain a historical decision.

## Decision

Thresholds bind from a `RuleSet` configuration section into strongly typed options, validated
at startup. A bad value fails the boot with a message naming the setting, rather than starting
a service that scores wrong.

The domain stays free of the configuration framework. Rules and the scoring policy take their
values as constructor arguments and guard them, so they cannot exist in an incoherent state
however they are built. The composition root binds the options, validates them, and constructs
the domain objects from them. Options and validation live at the edge; the invariants live in
the domain.

Validation runs with `ValidateOnStart`. The checks are the ones an operator can act on: a
weight has to be positive, the review threshold has to sit below the decline threshold, a
window has to move forward, a rule cannot be left with no currency thresholds.

The rule set version derives from the effective configuration. It is the operator label with a
fingerprint of the actual values appended, read through `IOptionsMonitor` so a reload is
reflected. Any change to the numbers changes the fingerprint.

## Consequences

Thresholds are tuned in configuration, and in an environment that supports reload, without a
deploy. The numbers a fraud analyst owns are no longer buried in compiled fields.

A misconfiguration fails the boot where a deployment notices it, instead of surfacing as a
rule that quietly does nothing. The domain guards back the startup validation, so the invariant
holds even in a test that constructs a rule directly.

The version on an assessment names the numbers it was actually scored under. An operator can
change a threshold and forget to bump the label, and the fingerprint still moves, so two
different rule sets can never share a stored version. Correlating a version to its full set of
values relies on the effective configuration being recorded at startup, which observability
adds next.

The composition root now carries the options shape, the validator and the mapping to domain
types. That is the right place for it, since it is already the only layer that knows every
other, but it is more code there than before.

## Alternatives

**A plain operator label for the version.** Rejected. Nothing stops the label going stale when
a threshold changes, which reintroduces the exact problem of an assessment claiming a version
it was not scored under. The fingerprint removes the discipline requirement.

**A full snapshot of the thresholds on every assessment.** The audit need is to explain an old
assessment, which a serialised copy of the numbers on each row would satisfy. Rejected for now
because it repeats the same configuration across millions of rows to capture something that
changes rarely. The fingerprint identifies the configuration compactly, and the values behind
a fingerprint belong in a startup record written once per change, not on every assessment.

**Validation with data annotations on the options.** Attributes cover a required field or a
range, but not a relationship like review below decline, or a rule needing at least one
currency. A single validator keeps every check in one readable place and reports them
together.

**Keeping the thresholds compiled.** Rejected. It is the status quo this reverses, and it makes
the numbers an analyst owns depend on an engineer and a release.
