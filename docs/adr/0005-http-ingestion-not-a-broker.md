# 0005. HTTP ingestion rather than a message broker

Date: 2026-09-01

Status: Accepted

## Context

Transaction events have to get into the service. In a production fraud platform they would usually
arrive over a message broker such as Kafka or SQS: the producer publishes, the engine consumes, and
the two scale and fail independently. The brief asks for an API, and a reviewer has to be able to run
the thing, so the ingestion path is a decision with a trade behind it.

## Decision

Events arrive over HTTP: a synchronous endpoint that evaluates one transaction and returns the
assessment, and a batch endpoint for bulk submission. There is no broker.

The evaluation itself lives in an application layer use case that knows nothing about HTTP. The
endpoint is a thin adapter over it.

## Consequences

A reviewer needs nothing but the service and its database. There is no broker to stand up, no topic
to create, no consumer group to reason about. The synchronous endpoint also gives an immediate
verdict, which is the natural shape for a caller deciding whether to let a transaction through.

Because the use case is transport agnostic, moving to a broker later is an added consumer that calls
the same handler, not a rewrite. The domain and application layers do not change.

What HTTP does not give is back pressure, replay, or independent scaling of ingestion from
evaluation. Under a sustained spike the caller waits or sheds load rather than a broker absorbing it.
That is accepted for the scope here and called out in the plan as the first thing that changes when
throughput outgrows a synchronous path.

## Alternatives

**Broker ingestion now.** The production shape, and rejected only on scope: it adds infrastructure a
reviewer must run and shifts the verdict from synchronous to asynchronous, which changes the API
contract for no gain at this size. The design keeps the door open by isolating the use case from the
transport.

**An internal in process queue behind the HTTP endpoint.** Accept quickly, process in the
background. Rejected because it adds the hardest part of asynchronous processing, durability and
retry, without the part that justifies it, a real broker, and it would make the batch endpoint's per
item result harder to return honestly.
