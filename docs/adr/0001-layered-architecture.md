# 0001. Layered architecture with dependencies pointing inward

Date: 2026-09-01
Status: Accepted

## Context

The service has a genuine domain at its centre: fraud rules, risk scoring, and a decision
policy. It also has substantial infrastructure around that centre, being persistence, an
HTTP surface, reference data lookups and configuration.

The risk in a service shaped like this is that the domain logic ends up entangled with EF
Core and ASP.NET types. Once a rule takes a `DbContext` or a `HttpContext`, it stops being
testable in isolation and starts being testable only through the whole stack. In a rule
engine that is particularly costly, because the rules are the part most in need of dense,
fast, readable tests.

## Decision

Four layers, dependencies pointing inward only.

```
Api            composition root, HTTP concerns
Infrastructure persistence, external data
Application    use cases, ports
Domain         rules, scoring, entities
```

`Domain` has no package references at all. `Application` references only `Domain`.
`Infrastructure` and `Api` sit outside both. Ports are declared in `Application` as
interfaces and implemented in `Infrastructure`, so the direction of the dependency is
inverted relative to the direction of the call.

## Consequences

Rule tests need no mocks, no database and no host. They construct a transaction, construct
a context, call `Evaluate`, and assert. That is the main thing being bought.

Persistence concerns cannot leak inward, because the reference needed to leak them does not
exist. The compiler enforces the boundary rather than a code review convention.

The cost is indirection. Reading a request end to end means moving through an endpoint, a
handler, a port and an implementation. For a service this size that is a real cost and it is
accepted, because the domain here is the valuable part and protecting it is worth some
navigation overhead.

There is also a mapping cost. API contracts are separate types from domain models, so
translation happens at the boundary. Deliberate. Leaking domain types into the HTTP contract
would make the public API change every time the model changes.

## Alternatives

**Single project, folders for separation.** Less ceremony and appropriate for a smaller
service. Rejected because nothing prevents the layering being violated except discipline,
and the point of the boundary here is that it holds without discipline.

**Domain and infrastructure only, no application layer.** Endpoints would orchestrate
directly. Rejected because the evaluation flow has real orchestration in it, being enrich,
evaluate, score, persist, and that sequence belongs in a testable unit rather than in an
HTTP handler.
