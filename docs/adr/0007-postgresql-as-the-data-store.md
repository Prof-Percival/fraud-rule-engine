# 0007. PostgreSQL as the data store

Date: 2026-09-01

Status: Accepted

## Context

The service persists three things that hang together: the ingested event, the assessment made from
it, and one row per rule that ran. It then serves queries over them: filter assessments by customer,
decision, score and time, page through the results, and aggregate a summary. The store has to hold
money exactly, enforce a uniqueness constraint for idempotency, and support the indexes those queries
need.

## Decision

PostgreSQL, through EF Core with the Npgsql provider.

## Consequences

The data is relational and the queries are relational: joins across event, assessment and outcomes,
range and equality filters, and grouped aggregates for the summary. A relational store answers those
directly, and PostgreSQL brings the specifics this domain needs: `numeric(19,4)` for money with no
floating point drift, a real unique constraint to carry idempotency rather than an application check,
and composite indexes ordered to match the keyset pagination in ADR 0008.

EF Core keeps the entity configuration in one place and generates the migrations, and it is the
idiomatic data access for a .NET service, which the brief asks the submission to be written in. The
domain stays free of it, per ADR 0001, so the dependency sits at the edge.

A single PostgreSQL node is a scaling ceiling. For this service that is far off, and the read side
would reach for replicas or caching before the store itself became the constraint.

## Alternatives

**A document store such as MongoDB.** Storing each assessment as a document is a tempting fit for the
nested outcomes. Rejected because the query side, ad hoc filters and grouped aggregates across
assessments, is exactly what a relational engine does best and a document store does with more effort,
and because the data has genuine relationships rather than being independent documents.

**SQLite.** Zero infrastructure, which is appealing for a reviewer. Rejected as the primary store
because it is not a production database for a concurrent service, and leaning on it would send the
wrong signal about how the service is meant to run.

**A cloud specific managed database.** Rejected on portability. It would tie the submission to one
provider and stop a reviewer running it locally with nothing but a container.
