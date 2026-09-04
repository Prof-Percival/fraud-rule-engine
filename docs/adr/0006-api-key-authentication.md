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

Keys are hashed once when the configuration is read and resolved by a single lookup, so a request costs one hash regardless of how many keys exist, and only the digests are held in memory.

What an API key does not give is expiry, rotation without a deploy, scopes, or any proof that the caller is who the key says. A leaked key is valid until someone removes it, and every holder of a key is equally trusted. Those are real limitations and the reason this is recorded as a decision for this submission rather than a recommendation for production.

## Keys identify services, not people

An API key authenticates a calling application. It is the wrong instrument for a person, because it carries no individual identity, no expiry, and no way to withdraw one person's access without breaking everyone who shares the key.

So an analyst never holds one. An analyst signs in to an internal tool with the bank's single sign on, and that tool calls this service with its own credential. The analyst's identity comes from their session and is what belongs in an audit record of who looked at whose assessment; the API key only says which application is calling. Any future endpoint that exposes something a named human should be accountable for needs that user identity carried through, not a shared key.

## What would change in production

Keys would come from a secret manager rather than configuration, issued when a service is onboarded and injected by the deployment platform, with rotation on a schedule and two keys valid during an overlap window so nothing breaks mid rotation.

Per client scopes would separate a service that only reads assessments from one that submits transactions, which a single shared key cannot express.

Beyond that the answer is workload identity rather than a shared secret: mutual TLS where the platform supports it, or OAuth2 client credentials against the bank's identity provider, which brings expiry, revocation and scopes without a secret sitting in configuration.

## Alternatives

**OAuth2 client credentials.** The right answer for service to service traffic in a bank, and rejected here only on scope: it needs an authorization server for a reviewer to run, and configuring one would add more moving parts than the fraud engine itself. The token validation would be a handful of lines, so this is a deferral rather than a dead end.

**Mutual TLS.** Strong, and it removes the shared secret entirely. Rejected because certificate provisioning is a deployment concern that a reviewer cannot reasonably be asked to set up, and it would make running the service locally considerably harder for no gain in what the brief is assessing.

**No authentication at all.** Rejected. The endpoints return customer level fraud outcomes, and shipping those unauthenticated would be the wrong instinct to show even in an exercise.
