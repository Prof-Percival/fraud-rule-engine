# 0010. Serilog for logs, OpenTelemetry for traces and metrics

Date: 2026-09-04

Status: Accepted

## Context

A fraud engine is operated, not just run. When flag volume jumps overnight, the first questions
are which rule is firing more, whether the decision split has moved, and whether a particular
request can be followed end to end. Answering those needs structured logs, traces that cross the
database boundary, and metrics that mean something in this domain, all correlated so one request
can be found across the three.

## Decision

Logs go through Serilog. One line per request carrying method, path, status and elapsed time, as
readable text in development and as JSON in production so a pipeline can parse it. Every request
carries a correlation id, and it is returned in an `X-Correlation-Id` header.

Traces and metrics go through OpenTelemetry, with instrumentation for ASP.NET Core, Npgsql and the
runtime. The correlation id is the trace id, so a log line, its trace and the reply all point at
one request.

Exporters are opt in. The OTLP exporter is added only when an endpoint is configured, and a console
exporter is a development switch. The instrumentation runs regardless, so the data is available to
whatever listens, including `dotnet-counters`, without a collector having to be present.

The fraud specific metrics are transactions assessed, the decision split, per rule trigger rate,
and assessment latency. They come from a `Meter` created with the in-box diagnostics type, so the
application layer takes on no telemetry dependency and OpenTelemetry picks the instruments up by
name. They are recorded only for a genuine assessment, so a retried duplicate does not inflate them.

Two health probes. Liveness runs no checks and reports whether the process is up. Readiness runs a
database check, so an unreachable database returns the service as not ready without killing it.

The effective rule set and its version are logged once at startup, so the numbers behind a stamped
version fingerprint are recoverable later.

## Consequences

A request can be followed from its log line, through its trace and the database spans beneath it, to
the reply, all by one id. A shift in the decision split or a single rule's trigger rate shows in the
metrics before anyone reads a log.

The service runs the same with or without a collector. Nothing tries to reach an exporter that is
not there, so a reviewer sees clean output, and a production deployment turns exporting on by
setting an endpoint.

The domain and application layers stay free of any telemetry package. The metrics live in the
application layer because that is where an assessment happens, but they depend only on the base
class library.

Structured logging is more setup than the default provider, and the OpenTelemetry packages are a
real dependency surface. Both are accepted, because operating a fraud service blind is the larger
cost.

## On personal data

The transaction model carries no card number, so there is no PAN to mask. The identifiers it does
carry, customer and account, are kept out of the logs by never logging the request body and by
correlating on the trace id rather than on anything from the payload. If a card number were ever
added to the model, it would have to be masked to its last four before any logging, and that belongs
with the change that introduces it.

## Alternatives

**The built in JSON console logger.** It produces structured logs with no extra dependency, and it
was close. Rejected because the request logging middleware, the enrichment and the correlation
handling are work that would then be hand rolled, and Serilog is the more common production choice
so it reads as familiar.

**A Prometheus scrape endpoint.** It would let a reviewer see metrics in a browser without a
collector, which is appealing. Rejected for now because the exporter is prerelease, and this
codebase treats warnings as errors and keeps to stable dependencies. OTLP plus the console switch
covers the same need from stable packages.

**Logging the request body.** It would make request logs richer. Rejected: the body carries the
transaction identifiers and amounts, and a fraud service should not be spilling those into its logs.
