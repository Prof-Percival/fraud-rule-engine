# 0006. API key authentication for this submission

Date: 2026-09-04

Status: Accepted

## Context

The data endpoints expose fraud assessments: what was flagged, for which customer, at what score. That is not public information, so the service needs to know who is calling before it answers. The callers are other services inside the bank rather than end users, and there is no browser, no login screen and no user identity involved.

## Decision

A shared API key in an `X-Api-Key` header. Keys are configuration, each mapped to a client name, validated at startup.

Authorization uses a fallback policy rather than metadata attached to each route, so an endpoint added later is covered by default and opting out has to be written down. The health probes and, in development, the OpenAPI document and its browsable reference are the explicit exceptions.

## Consequences

A caller needs one header, which is the whole integration cost. Nothing to negotiate, no token endpoint, no clock skew, and a reviewer can exercise the API with one curl flag.

Keys map to a client name rather than standing alone, which is what makes the rest work: traffic is attributed to a client in the logs, the rate limiter partitions on it, and one client's key can be revoked by removing a line of configuration without disturbing anyone else. The key itself is never logged, not even on a rejection, because a rejected key is often a valid key sent to the wrong environment.

Comparison runs in fixed time against every configured key, so the time taken does not reveal how much of a key was correct.

What an API key does not give is expiry, rotation without a deploy, scopes, or any proof that the caller is who the key says. A leaked key is valid until someone removes it, and every holder of a key is equally trusted. Those are real limitations and the reason this is recorded as a decision for this submission rather than a recommendation for production.

## What would change in production

Keys would move to a secret manager rather than configuration, with rotation on a schedule and two valid keys during an overlap window. Per client scopes would separate a service that only reads assessments from one that submits transactions.

Beyond that the answer is workload identity rather than a shared secret: mutual TLS where the platform supports it, or OAuth2 client credentials against the bank's identity provider, which brings expiry, revocation and scopes without a shared secret sitting in configuration.

## Alternatives

**OAuth2 client credentials.** The right answer for service to service traffic in a bank, and rejected here only on scope: it needs an authorization server for a reviewer to run, and configuring one would add more moving parts than the fraud engine itself. The token validation would be a handful of lines, so this is a deferral rather than a dead end.

**Mutual TLS.** Strong, and it removes the shared secret entirely. Rejected because certificate provisioning is a deployment concern that a reviewer cannot reasonably be asked to set up, and it would make running the service locally considerably harder for no gain in what the brief is assessing.

**No authentication at all.** Rejected. The endpoints return customer level fraud outcomes, and shipping those unauthenticated would be the wrong instinct to show even in an exercise.
