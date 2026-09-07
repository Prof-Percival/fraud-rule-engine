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

## The allowance belongs to a client, not to the service

The limit began as one number applied to everyone, and measurement showed how little that number meant: a single instance served about 190 evaluations a second, while the allowance stood at 100 a minute, or under one percent of what the instance could do. A number nobody derived is a number nobody can defend, so the default is now half of measured capacity and every client may be given its own.

Allowances resolve by client rather than by key. During a rotation a client holds two valid keys at once, and resolving by key would hand it twice the rate for as long as that lasted.

The effective allowance forms part of the rate limiting partition key, because a partition's limiter is constructed once and then reused for the life of the process. Without that, retuning a client would apply only to callers that had not yet appeared, which is the kind of setting that looks applied and is not.

Reloading is therefore real, but only from sources that support it. A file does, including a ConfigMap or Secret mounted as one; environment variables are read at startup and cannot change. A deployment that wants to retune a client without restarting has to mount that section rather than pass it as a variable.

A fixed window remains the mechanism, and it has a known shape: a caller may spend a full allowance at the end of one window and again at the start of the next, and a legitimate burst inside a window is refused even where the average would have been fine. A token bucket expresses sustained rate and burst separately and is the better fit for machine to machine traffic. It was not adopted here because per client allowances addressed the problem that prompted the change, and swapping the algorithm as well would have been two decisions in one commit.

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
