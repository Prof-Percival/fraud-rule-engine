# Architecture Decision Records

An ADR is a short document recording one significant decision: what the situation was, what
was decided, and what the consequences are. One decision per file, numbered, and never
edited after the fact. If a decision is reversed later, a new ADR supersedes the old one and
the old one stays in place with its status changed.

The point is that six months later the reasoning is still available. Code shows what was
decided. It does not show what the alternatives were or why they lost.

Format used here is deliberately small. Context, Decision, Consequences, Alternatives.
Anything longer stops being written.

## Index

| ADR | Decision | Status |
|---|---|---|
| [0001](0001-layered-architecture.md) | Layered architecture with dependencies pointing inward | Accepted |
| [0002](0002-projects-over-vertical-slices.md) | Separate projects per layer rather than vertical slices | Accepted |
| [0003](0003-rules-as-pure-functions.md) | Rules are pure synchronous functions with no IO | Accepted |
| 0004 | No rules DSL or third party rules engine | Planned |
| 0005 | HTTP ingestion rather than a message broker | Planned |
| 0006 | API key authentication for this submission | Planned |
| 0007 | PostgreSQL as the data store | Planned |
| 0008 | Keyset pagination rather than offset | Planned |
| [0009](0009-rule-thresholds-in-validated-configuration.md) | Rule thresholds live in validated configuration | Accepted |

Records 0004 through 0008 are written as the corresponding work lands, in the sequence set
out in `PLAN.md`. 0009 was not foreseen in that list: moving the thresholds into configuration
turned out to be a decision worth its own record, so it took the next free number rather than
displacing the reserved ones.
