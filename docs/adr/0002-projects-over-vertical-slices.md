# 0002. Separate projects per layer rather than vertical slices

Date: 2026-09-01
Status: Accepted

## Context

ADR 0001 settled on layering with dependencies pointing inward. That still leaves the
question of how the layers are physically arranged. Two reasonable options:

1. Four compiled projects, one per layer
2. One project, folders per feature, each feature holding its own endpoint, handler,
   domain logic and persistence

Option 2 is vertical slice architecture, and it has real advantages. Related code sits
together, a feature can be read end to end without moving between projects, and changes stay
local. For services that are mostly independent CRUD features it is usually the better
choice, because the coupling it avoids is coupling that actually hurts.

## Decision

Four projects, one per layer.

## Consequences

The layering is enforced by the compiler. `FraudRuleEngine.Domain` has no package
references, so a rule cannot take a dependency on `DbContext` even by accident. In a single
project arrangement nothing stops that except review discipline, and the value of this
particular boundary is that it holds without discipline.

Rule tests run with no infrastructure at all, and it is structurally impossible for that to
regress. That is the property being protected.

The cost is that the layers cut across features. Adding a rule touches only the domain
project, but adding an endpoint touches three. Accepted, because the shape of this service
is not a set of independent features.

## Alternatives

**Vertical slices in a single project.** Rejected on the shape of the problem. Slices work
when features are independent. Here they are not. Eight rules, one scoring policy, one
decision policy and one evaluation pipeline all operate on the same model, and every rule
shares the same evaluation context. Cutting that into per feature slices would either
duplicate the shared core across slices or produce a shared folder that is the domain layer
with a different name.

The honest summary is that vertical slices organise around features and this service has one
feature with a lot of depth. Layering matches that better.

**Two projects, domain and host.** Simpler, and it was tempting. Rejected because the
evaluation flow has genuine orchestration in it, being enrich, evaluate, score and persist,
and that sequence needs to be unit testable without an HTTP host. Which means it needs a
home that is neither the domain nor the host.
