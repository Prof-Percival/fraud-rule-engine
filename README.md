# Fraud Rule Engine Service

A service that evaluates categorised transaction events against a set of fraud rules,
produces an explainable risk assessment for each one, persists the result, and exposes it
over an HTTP API.

Built for the Capitec technical assessment, brief 2.

> Status: planning complete, implementation in progress. The build sequence is in
> `PLAN.md` and the design decisions are recorded as ADRs in `docs/adr/`. Commands in
> this README are the intended interface and are verified as each step lands.

## Quick start

Docker is the only thing you need installed. The .NET SDK is not required on the host,
because it lives in the build stage of the image.

```bash
docker compose up --build
```

That compiles the service, starts PostgreSQL, applies migrations and serves the API on
<http://localhost:8080>. First build pulls the base images and takes a few minutes.
Subsequent builds are cached.

If PostgreSQL is already installed on this machine it holds port 5432, and the stack cannot publish the
database on a port something else owns. Put `POSTGRES_PORT=55432` in a `.env` file next to
`compose.yaml` before the first run and the two stay out of each other's way. Nothing inside the stack
is affected, because the API reaches the database by service name rather than through the host.

Check it came up:

```bash
curl http://localhost:8080/health/ready
```

Run the full test suite, again with nothing installed but Docker:

```bash
docker compose --profile test run --rm tests
```

Stop it again, keeping the data:

```bash
docker compose stop
```

Start it back up with `docker compose up -d`. That creates whatever is missing and starts whatever
already exists, so it is safe to run repeatedly. Nothing above deletes the database.

Identical commands work in PowerShell on Windows, Terminal on macOS and a Linux shell.

## What it does

A transaction event arrives already categorised by an upstream service. This service:

1. Validates it and rejects it with a useful problem response if it is malformed
2. Loads the customer context it needs to evaluate history dependent rules
3. Runs every enabled rule against the transaction
4. Aggregates the rule outcomes into a risk score and one of three decisions
5. Persists the event, the assessment, and the outcome of every rule that ran
6. Returns the assessment, and makes it queryable afterwards

Each assessment records which rules fired, why, with the values that caused it, and which
version of the rule set produced the result. An assessment from six months ago can still be
explained, because the configuration that produced it is stored alongside it.

### Decisions

| Decision | Meaning |
|---|---|
| `Approve` | Score below the review threshold |
| `Review` | Score warrants a human looking at it |
| `Decline` | Score above the decline threshold |

Three states rather than a fraud flag, because in practice most flagged transactions go to
a human rather than being blocked outright.

### Rules

| Rule | Criteria |
|---|---|
| `HighValueTransaction` | Amount above a per currency threshold |
| `HighRiskCategory` | Transaction category on the watchlist |
| `DeniedMerchant` | Merchant on the deny list |
| `UnusualHour` | Outside the customer's expected activity window, in their local time |
| `TransactionVelocity` | Too many transactions inside a rolling window |
| `ImpossibleTravel` | Geographically implausible sequence of transactions |
| `FirstTimeMerchantHighValue` | High value at a merchant the customer has never used |
| `AmountEscalation` | Materially above the customer's rolling average |

Current thresholds and enablement for every rule are visible at `GET /api/v1/rules`.

## Prerequisites

**To build, run and test: Docker only.**

| Platform | Install |
|---|---|
| Windows | `winget install Docker.DockerDesktop` |
| macOS | `brew install --cask docker` |

On Windows, use the WSL2 backend, which is the Docker Desktop default. Nothing else is
needed and no .NET installation is required.

If you would rather not use Docker at all, there are step by step instructions for running
the service directly against a local PostgreSQL under
[running without Docker](#without-docker).

**To develop: add the .NET 10 SDK.**

Needed for IDE work, debugging, and generating EF Core migrations with `dotnet ef`. Not
needed to run or test the service.

| Platform | Install |
|---|---|
| Windows | `winget install Microsoft.DotNet.SDK.10` |
| macOS | `brew install --cask dotnet-sdk` |

Confirm with `dotnet --version`, which should report 10.x.

.NET 10 is the current LTS release, supported until November 2028. .NET 8 and .NET 9 both
reach end of support on 10 November 2026, so neither was a sensible target for new work.

<details>
<summary>Container runtime alternatives</summary>

Docker Desktop requires a paid licence for larger organisations. Finch and Podman both
build this `Dockerfile` and run this `compose.yaml` unchanged. Substitute the CLI name:

```bash
brew install --cask finch
finch vm init      # first run only, downloads a VM
finch vm start
finch compose up --build
```

Everything below is written with `docker` because that is what a reviewer will most likely
have.
</details>

## How the container is built

`Dockerfile` is multi stage:

| Stage | Base image | Purpose |
|---|---|---|
| `build` | `mcr.microsoft.com/dotnet/sdk:10.0` | Restore, build, publish |
| `test` | `build` | Runs `dotnet test` |
| `final` | `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` | Runtime |

The runtime image is Ubuntu Chiseled, which is distroless. No shell, no package manager,
and a non root user by default. Smaller image and a much smaller attack surface than the
full runtime image, which matters more than usual for something sitting in a payment path.

The build stage restores against `Directory.Packages.props` before copying the source, so a
source only change does not invalidate the restore layer.

## Running

### Where the database lives

The service reaches its database through one connection string and has no opinion beyond that, so where
it runs is a choice made at startup rather than a property of the design.

| You want | Command | Database used |
|---|---|---|
| Nothing installed, one command | `docker compose up -d --build` | PostgreSQL in a container, data in a named volume |
| The schema in a server you already run | `docker compose --profile host-db up -d --build api-host-db` | That server, created on first start if absent |
| The service on the host, not in a container | `dotnet run --project src/FraudRuleEngine.Api` | Whatever `ConnectionStrings__Default` points at |

The default is the container because a reviewer should not have to install PostgreSQL, create a role and
match a version before the thing will start, and because it gives everyone the same server rather than
whatever happens to be on the machine.

Worth being precise about one thing: the data is not in the container. It is in a named volume, and the
container in front of it is disposable. Removing and recreating the container leaves the data where it
was, which is why `docker compose down` keeps it and only `down -v` does not.

None of this is how the database runs in production. There it is a managed service with backups, failover
and an upgrade path, and the only thing the application knows about it is the connection string it was
given. See Deploying below.

### With Docker, which is the supported path

```bash
docker compose up --build          # foreground, logs to console
docker compose up -d --build       # background
docker compose logs -f api         # follow the API logs
```

Starting and stopping, and what each one does to the data:

| Command | Effect | Data |
|---|---|---|
| `docker compose up -d` | Creates anything missing, starts anything already there. Safe to repeat | kept |
| `docker compose stop` | Stops the containers, leaves them in place | kept |
| `docker compose start` | Starts the containers stopped above | kept |
| `docker compose restart` | Stop then start | kept |
| `docker compose down` | Stops and removes the containers, leaves the volume | kept |
| `docker compose down -v` | Removes the containers **and the volume** | **destroyed** |

`docker compose stop` then `docker compose up -d` is the routine cycle. `down -v` is the only command
here that loses data, and it is the one to use when you want a genuinely empty database to start again
from.

Services in the compose stack:

| Service | Port | Notes |
|---|---|---|
| `api` | 8080 | Waits for the database healthcheck before starting |
| `db` | 5432, overridable | PostgreSQL 17, data in a named volume that survives a restart |
| `tests` | none | Only starts under the `test` profile |
| `pgadmin` | 5050 | Only starts under the `tools` profile. See below |

### Using a PostgreSQL you already have

By default the stack runs its own PostgreSQL in a container. That server is separate from any PostgreSQL
installed on the machine, so its `fraudengine` database is listed under its own entry in a client rather
than alongside databases in the local server. A database only ever appears under the server that holds
it, so to see it among the ones already there, the service has to write into that server instead:

```bash
docker compose --profile host-db up -d --build api-host-db
```

That runs the API against a PostgreSQL on the host and does not start the `db` service at all. The
database and its schema are created on first start if they are not there, so nothing needs preparing
beyond an account that may create one. Settings come from `.env`:

```
HOST_POSTGRES_PORT=5432
HOST_POSTGRES_DB=fraudengine
HOST_POSTGRES_USER=postgres
HOST_POSTGRES_PASSWORD=your-password
```

Two things to expect. The host server has to accept a connection from the container, which is a different
address from `localhost`, so `pg_hba.conf` usually needs a line permitting the Docker network and a
reload afterwards. And if the default stack is still running it already holds port 8080, so either stop
it first or set `API_PORT` to something else.

### Looking at the data

The container's database keeps its data in a named volume, so it is still there after a restart and after
`docker compose down`. Two ways to inspect it.

**pgAdmin in the stack**, which needs nothing installed:

```bash
docker compose --profile tools up -d pgadmin
```

Open <http://localhost:5050>. The server is already registered as "Fraud rule engine"; expand it and
enter the password `fraudengine` when asked. The tables are under
`Databases > fraudengine > Schemas > public > Tables`.

This connects over the compose network rather than through the host, so it works whatever else is
installed on the machine.

**A pgAdmin or client installed on your machine**, connecting to the published port:

| Setting | Value |
|---|---|
| Host | `localhost` |
| Port | `5432` |
| Database | `fraudengine` |
| Username | `fraudengine` |
| Password | `fraudengine` |

If a client connects but shows no `fraudengine` database, it is almost certainly talking to a different
PostgreSQL. A server installed on the machine already holds port 5432, and a client pointed at
`localhost:5432` reaches that one instead of the container. Check with:

```bash
docker compose ps db          # is the container up, and on which host port
```

To move the container off the contested port, put `POSTGRES_PORT=55432` in a `.env` file next to
`compose.yaml`, run `docker compose up -d`, and connect the client to `55432`. Nothing inside the stack
changes, because the API reaches the database by service name on the compose network rather than through
the host. Changing the port does recreate the database container, so readiness reports 503 for a few
seconds while the connection is re established, then returns to 200 on its own. The data is in the
volume and is not affected.

To reach it from `psql` without installing anything:

```bash
docker compose exec db psql -U fraudengine -d fraudengine -c "\dt"
docker compose exec db psql -U fraudengine -d fraudengine -c "select decision, count(*) from fraud_assessments group by decision;"
```

Migrations are applied automatically on startup outside production. In production they run
as a separate step, because an application that migrates its own schema on boot will fight
itself the moment it runs more than one replica.

OpenAPI document is at <http://localhost:8080/openapi/v1.json> and the browsable UI at
<http://localhost:8080/scalar/v1>. Outside production <http://localhost:8080> goes to the reference as
well, so there is no versioned path to remember.

Both exist in development only, so in production they are not served and the root is not a redirect.
Note also that an unauthenticated request to a path that does not exist comes back as 401 rather than
404, because authorization is a fallback policy and applies to unmatched routes too. A caller without a
key cannot use the difference to work out which paths are real.

### Without Docker

Everything works without Docker. It is more steps, because you install and run the two
things the container would otherwise have handled for you: the .NET 10 SDK and PostgreSQL.

**1. Install the .NET 10 SDK**

| Platform | Install |
|---|---|
| Windows | `winget install Microsoft.DotNet.SDK.10` |
| macOS | `brew install --cask dotnet-sdk` |
| Linux | See <https://learn.microsoft.com/dotnet/core/install/linux> |

Verify with `dotnet --version`, which should report 10.x. Open a new terminal first, since
the installer changes `PATH`.

**2. Install and start PostgreSQL 17**

Windows, which installs a service that starts automatically:

```powershell
winget install PostgreSQL.PostgreSQL.17
```

macOS:

```bash
brew install postgresql@17
brew services start postgresql@17
```

Linux, using Debian or Ubuntu as the example:

```bash
sudo apt install postgresql-17
sudo systemctl enable --now postgresql
```

The Windows installer asks you to set a password for the built in `postgres` superuser during
setup. Make a note of it, because step 3 needs it and there is no way to recover it later
short of editing `pg_hba.conf`.

If you would rather not install anything, a hosted PostgreSQL instance works just as well.
Nothing in the service cares where the database runs, only that a connection string reaches
it.

**3. Create a role and a database for the service**

The default superuser differs by platform, so getting into `psql` differs too. Pick your line:

| Platform | Open psql as an administrator |
|---|---|
| Windows | `psql -U postgres` then enter the password from step 2 |
| macOS via Homebrew | `psql postgres` |
| Linux | `sudo -u postgres psql` |

Homebrew is the one that catches people out. It does not create a `postgres` role at all. It
creates a superuser named after your macOS account and trusts local connections, so there is
no password to supply and `psql -U postgres` fails with "role postgres does not exist".

On Windows, `psql.exe` is not on `PATH` by default. It lives in
`C:\Program Files\PostgreSQL\17\bin`, so either add that to `PATH` or use the SQL Shell
shortcut the installer creates. pgAdmin, also installed, will run the same statements if you
prefer a window to a prompt.

Once you have a prompt, create a dedicated role and a database it owns:

```sql
CREATE ROLE fraudengine WITH LOGIN PASSWORD 'fraudengine';
CREATE DATABASE fraudengine OWNER fraudengine;
\q
```

These are throwaway local development credentials and they are in the README on purpose, so
the connection string below works without you having to invent anything. Do not reuse them
anywhere that matters.

The `OWNER` clause matters and is not decoration. From PostgreSQL 15 onwards, an ordinary role
cannot create objects in the `public` schema of a database it does not own, so migrations fail
with "permission denied for schema public". Making the role the owner avoids that.

**4. Check you can actually connect**

Worth thirty seconds here rather than debugging it through the application later:

```bash
psql "postgresql://fraudengine:fraudengine@localhost:5432/fraudengine" -c "select version();"
```

That prints the server version if the role, password, database and port are all correct, and
tells you which of them is wrong if not. See the troubleshooting table below for what the
common failures mean.

**5. Point the service at it**

Set the connection string as an environment variable. The double underscore is how .NET maps
an environment variable onto a nested configuration key, in this case
`ConnectionStrings:Default`.

PowerShell:

```powershell
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=fraudengine;Username=fraudengine;Password=fraudengine"
```

bash or zsh:

```bash
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=fraudengine;Username=fraudengine;Password=fraudengine"
```

Note that this only lasts for the current terminal session. A new terminal needs it again, or
put it in your shell profile.

**6. Apply the schema**

```bash
dotnet tool restore
dotnet ef database update \
  --project src/FraudRuleEngine.Infrastructure \
  --startup-project src/FraudRuleEngine.Api
```

**7. Run it**

```bash
dotnet restore
dotnet build
dotnet run --project src/FraudRuleEngine.Api
```

The API comes up on <http://localhost:8080>. Confirm with
`curl http://localhost:8080/health/ready`, which reports unhealthy if it cannot reach the
database, so it verifies the whole chain above rather than just that the process started.

**When PostgreSQL will not cooperate**

| What you see | What it means |
|---|---|
| `connection refused` | The server is not running, or not on 5432. `brew services list`, `systemctl status postgresql`, or Services on Windows |
| `password authentication failed for user "fraudengine"` | The role exists but the password differs from step 3. Reset it with `ALTER ROLE fraudengine WITH PASSWORD 'fraudengine';` |
| `role "fraudengine" does not exist` | Step 3 did not run, or it ran against a different server than the one you are now connecting to |
| `role "postgres" does not exist` | You are on Homebrew. Use `psql postgres`, which connects as your macOS account |
| `database "fraudengine" does not exist` | The `CREATE DATABASE` line did not run. The `\q` at the end of the block is easy to paste too early |
| `permission denied for schema public` during migrations | The role does not own the database. `ALTER DATABASE fraudengine OWNER TO fraudengine;` |
| `54320` or `5433` in use instead of 5432 | Another PostgreSQL is already installed. Either point the connection string at the other port or stop the one you are not using |

On macOS, `brew services list` tells you whether the server is up. On Windows it is a Windows
service called `postgresql-x64-17` and shows up in `services.msc`.

**Testing without Docker**

Unit tests need nothing beyond the SDK:

```bash
dotnet test UnitTests.slnf
```

`UnitTests.slnf` is a solution filter holding everything except the integration test project. It
selects by project rather than by test trait because a filter that matches nothing in a project
counts as a failed run, so excluding the integration tests with `--filter` reports failure on a
suite that entirely passed.

The integration tests need a PostgreSQL instance and will use the one from
`ConnectionStrings__Default` if it is set. Without that variable they try to start their own
through Testcontainers, which does need a container runtime, so set the variable if you have
no Docker:

```bash
dotnet test
```

The integration tests create and drop schemas, so give them their own database rather than the
one the application uses. From a `psql` prompt opened as in step 3:

```sql
CREATE DATABASE fraudengine_tests OWNER fraudengine;
```

Then point the tests at it for the duration of the run:

```bash
ConnectionStrings__Default="Host=localhost;Port=5432;Database=fraudengine_tests;Username=fraudengine;Password=fraudengine" dotnet test
```

Setting it inline like that rather than exporting it avoids the mistake of leaving the variable
pointing at the test database and then wondering where the application's data went.

## Testing

Everything in one command, no local SDK:

```bash
docker compose --profile test run --rm tests
```

That runs unit and integration tests against the compose PostgreSQL instance and exits with
the test result as its exit code, so it works unchanged in CI.

Unit tests only, no database and no compose stack. Building this stage runs them, and a failure
fails the build:

```bash
docker build --target test .
```

It runs `UnitTests.slnf`, because a build has no database and no way to reach one. A `TEST_TARGET`
build argument can point it at another target, but the integration tests cannot pass here whatever
it is set to, so use the compose command above to run everything.

With the SDK installed locally:

```bash
dotnet test                                                     # everything
dotnet test UnitTests.slnf                                      # unit only
dotnet test tests/FraudRuleEngine.IntegrationTests               # integration only
dotnet test UnitTests.slnf --coverage                            # with coverage
```

Coverage comes from the test platform's own collector, so it is `--coverage` rather than the
`--collect` argument the older runner took. Add `--coverage-output <path>` to choose where the
Cobertura file lands.

### How the integration tests get a database

Integration tests use a real PostgreSQL instance rather than the EF Core in memory
provider. The in memory provider would not exercise the unique constraint that enforces
idempotency, nor the keyset pagination query, and those are two of the things most worth
testing.

The database comes from one of two places, and the fixture picks without configuration:

- If `ConnectionStrings__Default` is set, it uses that. This is the compose and CI path.
- Otherwise it starts a throwaway PostgreSQL container through Testcontainers. This is the
  path when running `dotnet test` from an IDE.

So the same test code works from a developer machine and from inside the compose stack.

## Using the API

All data endpoints require an `X-Api-Key` header. The development key is
`dev-local-key-0123456789`, set in `appsettings.Development.json`, and it is the only key that
ships. Health probes are unauthenticated, as are the OpenAPI document and its browsable
reference, which exist in development only.

A request without a key, or with one that is not configured, comes back as 401. Each client is
rate limited separately, keyed on the client its API key belongs to and on an allowance that can be
set for that client alone, and exceeding it returns 429 with a `Retry-After` header. Both are
`application/problem+json`, the same shape as every other error. See Configuration for how the
allowances are set and changed.

There is a `docs/requests.http` file covering every endpoint with realistic payloads. It
runs directly in Visual Studio, Rider and the VS Code REST Client extension, which is the
easiest way to exercise the API on Windows.

Evaluate a transaction:

```bash
curl -X POST http://localhost:8080/api/v1/transactions/evaluate \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: dev-local-key-0123456789" \
  -d '{
    "eventId": "8f2a1c74-6b3e-4d19-9a52-1e7c4f8b2d63",
    "transactionId": "TXN-000123",
    "customerId": "CUST-4471",
    "accountId": "ACC-9920",
    "amount": 48500.00,
    "currency": "ZAR",
    "category": "CashWithdrawal",
    "channel": "Atm",
    "merchantId": "MERCH-771",
    "merchantName": "ATM Sandton City",
    "countryCode": "ZA",
    "occurredAt": "2026-09-01T02:14:00Z"
  }'
```

Returns the assessment, the score, the decision, and every rule that ran with the reason
it did or did not fire.

In PowerShell, `curl` is an alias for `Invoke-WebRequest` and will not accept these flags.
Use `curl.exe` explicitly, or use `docs/requests.http`.

Query assessments:

```bash
curl -H "X-Api-Key: dev-local-key-0123456789" \
  "http://localhost:8080/api/v1/assessments?decision=Review&minScore=60&pageSize=25"
```

Paging is keyset based. Responses carry a `nextCursor`, which is passed back as
`?cursor=...`. Offset paging was deliberately replaced, because it degrades on large tables
and it repeats or skips rows when inserts land between page requests.

Summary counts:

```bash
curl -H "X-Api-Key: dev-local-key-0123456789" http://localhost:8080/api/v1/assessments/summary
```

### Endpoints

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/v1/transactions/evaluate` | Evaluate one transaction |
| POST | `/api/v1/transactions/batch` | Bulk ingest, per item results |
| GET | `/api/v1/assessments` | Filter and page assessments |
| GET | `/api/v1/assessments/{id}` | Single assessment with rule detail |
| GET | `/api/v1/assessments/summary` | Counts by decision, most triggered rules |
| GET | `/api/v1/customers/{id}/assessments` | Assessments for one customer |
| GET | `/api/v1/rules` | Rule catalogue and live configuration |
| GET | `/health/live` | Liveness, unauthenticated |
| GET | `/health/ready` | Readiness including database, unauthenticated |

Errors are `application/problem+json` per RFC 9457.

## Configuration

Two sections carry the settings worth changing without a rebuild.

`RuleSet` holds the rule thresholds, the scoring weights and the decision bands, plus a version
label. It is validated at startup, so a weight that is not positive, a review threshold that is not
below the decline threshold, or a rule left with no currency thresholds fails the boot with a
message naming the setting rather than running with a rule quietly disabled. Every assessment is
stamped with the version label plus a fingerprint of the values actually in force, so a stored
assessment can be traced back to the numbers that produced it.

`ApiKey` holds the accepted keys, each mapped to a client name, the window, a default allowance, and an
allowance per client where one client should not be treated like the rest:

```json
"ApiKey": {
  "Window": "00:01:00",
  "RequestsPerWindow": 6000,
  "Keys": {
    "a-long-key-for-partner-a": "partner-a",
    "a-long-key-for-the-loader": "bulk-loader"
  },
  "Clients": {
    "partner-a": { "RequestsPerWindow": 1200 },
    "bulk-loader": { "RequestsPerWindow": 60000 }
  }
}
```

A client with no entry under `Clients` runs on `RequestsPerWindow`. Throttling one noisy caller is a
matter of lowering its number, and lifting a limit for one integration raises only theirs.

The allowance is resolved by **client**, not by key, which matters during a rotation: a client holding
two valid keys at once shares one allowance rather than being handed twice the rate for as long as the
rotation lasts.

Validated the same way as the rest. No keys, a key shorter than sixteen characters, a key with no client
name, an allowance that is not positive, or an allowance naming a client no key maps to all fail the
boot. That last one exists because a typo would otherwise read as working configuration while the client
quietly stayed on the default.

The default of 6,000 a minute is derived rather than chosen: a single instance measured about 190
evaluations per second, so roughly half of one instance's capacity is 100 per second. It is per instance,
which is a stated limit further down.

**Changing an allowance while the service runs.** The limiter reads its numbers through
`IOptionsMonitor`, and the effective allowance forms part of the rate limiting partition key, so a
changed value applies to a caller already sending rather than only to one that turns up later. It also
means a client mid way through being throttled gets a fresh allowance the moment its number changes,
which is the quickest way to release one during development.

That only works for configuration sources that support reloading. A JSON file does, including a
Kubernetes ConfigMap or Secret mounted as a file, since the platform updates the file in place.
**Environment variables are read once at startup and cannot change**, so a deployment that wants to
retune a client without a restart has to supply that section as a mounted file rather than as
`ApiKey__Clients__...`.

The development keys are in `appsettings.Development.json`, deliberately spanning a range of allowances
so throttling can be exercised without editing anything:

| Key | Client | Allowance per minute |
|---|---|---|
| `dev-local-key-0123456789` | `local-development` | 20,000 |
| `dev-bulk-loader-key-0001` | `bulk-loader` | 60,000 |
| `dev-partner-a-key-000001` | `partner-a` | 1,200 |
| `dev-partner-b-key-000001` | `partner-b` | 1,200 |
| `dev-analyst-tool-key-001` | `analyst-tool` | 300 |
| `dev-throttled-key-000001` | `throttled-client` | 20 |
| `dev-tight-key-0000000001` | `tight-client` | 3 |
| `dev-default-key-00000001` | `on-the-default` | whatever the default is |

Use the bulk loader key for importing, the tight one to see a 429 on demand, and the last one to check
what an unlisted client gets. Since the limiter holds its counters in memory, `docker compose restart
api` clears every one of them.

### Where settings come from

Configuration is read in the usual precedence order, with each source overriding the one before it:
`appsettings.json`, then `appsettings.{Environment}.json`, then environment variables. Nothing in the
service reads a secret from a file it owns, so a deployment supplies values as environment variables
and no code changes between environments.

Nesting maps to a double underscore, so `ConnectionStrings:Default` becomes
`ConnectionStrings__Default` and `RuleSet:Scoring:ReviewThreshold` becomes
`RuleSet__Scoring__ReviewThreshold`.

One consequence worth stating: `appsettings.Development.json` is the only file carrying a connection
string and an API key, and it is loaded only when the environment is Development. Running with
`ASPNETCORE_ENVIRONMENT=Production` and no environment variables therefore does not fall back to the
development values. It fails to start, reporting that no API keys are configured. A missing credential
stops the deployment rather than quietly accepting a key that was meant for a laptop.

## Deploying

The image is the deployment artefact. It serves plain HTTP on port 8080, runs as an unprivileged user,
and contains no shell or package manager. TLS is expected to terminate at the ingress or load balancer
in front of it, which is the normal arrangement and keeps certificate handling out of the application.

### Credentials

Supply them as environment variables from whatever secret store the platform provides, so the values
never sit in an image or a repository:

| Platform | How the value arrives |
|---|---|
| ECS or Fargate | `secrets` in the task definition, sourced from Secrets Manager or Parameter Store |
| Kubernetes | a `Secret` projected into the pod as environment variables, or mounted by a CSI driver |
| Azure Container Apps or App Service | a Key Vault reference in the application settings |

The variables a deployment has to set:

```
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Default=Host=...;Port=5432;Database=...;Username=...;Password=...
ApiKey__Keys__<the-key>=<the-client-it-belongs-to>
OTEL_EXPORTER_OTLP_ENDPOINT=http://collector:4317   # optional
```

Rotating a key means adding the new one alongside the old, redeploying, moving callers across, then
removing the old one. Two keys can be valid at once because the section holds a set rather than a
single value, so rotation needs no downtime.

### Schema changes

The service does not migrate its own schema outside Development. An application that migrates on boot
fights itself the moment it runs more than one replica, and a failed migration takes the process down
instead of leaving something an operator can retry.

Apply the schema as a separate step before the new version rolls out, from a generated script:

```bash
dotnet ef migrations script --idempotent \
  --project src/FraudRuleEngine.Infrastructure \
  --startup-project src/FraudRuleEngine.Infrastructure \
  --output schema.sql
```

The script is idempotent, so it can be applied repeatedly and skips anything already present, which is
what makes it safe to run from a pipeline step or a one shot job.

### Readiness and rollout

Point the orchestrator's liveness probe at `/health/live` and its readiness probe at `/health/ready`.
Liveness runs no checks and answers whether the process is alive, so a brief database outage does not
cause a restart loop. Readiness includes the database, so an instance that cannot reach it is taken out
of rotation and put back when it recovers. Both are unauthenticated, so a probe needs no credential.

## Observability

Logs are structured, one line per request with method, path, status and elapsed time, as readable
text in development and JSON in production. Every response carries an `X-Correlation-Id`, taken
from the trace id, so a log line, its trace and the reply all point at the same request. Request
bodies are never logged, which keeps transaction identifiers and amounts out of the logs.

Traces and metrics use OpenTelemetry, covering requests, database calls and the runtime, alongside
the fraud specific metrics: transactions assessed, the decision split, per rule trigger rate and
assessment latency. A jump in one rule's trigger rate is an incident signal.

Exporting is opt in. Set `OTEL_EXPORTER_OTLP_ENDPOINT` to send to a collector; without it the
instrumentation still runs and nothing tries to reach an endpoint that is not there. In development
`Telemetry__Console=true` prints spans and metrics to the console instead.

## Architecture

```
FraudRuleEngine.Domain          rules, scoring, entities. No external dependencies.
FraudRuleEngine.Application     use cases and the ports they depend on.
FraudRuleEngine.Infrastructure  EF Core persistence, enrichment, reference data.
FraudRuleEngine.Api             HTTP surface and composition root.
```

Dependencies point inward. The domain project has no package references, so persistence
concerns cannot leak into the rules. The compiler enforces the layering rather than a
convention document doing it.

Every rule implements one interface:

```csharp
public interface IFraudRule
{
    RuleId Id { get; }
    RuleOutcome Evaluate(FraudEvaluationContext context);
}
Any setting can be overridden by an environment variable using the standard double underscore form,
which is how the container is configured:

```bash
env RuleSet__Scoring__ReviewThreshold=45 \
    ApiKey__Keys__some-long-key-value-here=some-client \
    dotnet run --project src/FraudRuleEngine.Api
```

Note the `env` prefix. A key containing a hyphen is not a valid shell variable name, so setting it
as a bare `NAME=value` prefix fails with "command not found". Compose and Kubernetes take these
names directly and need no such workaround.t constraint is the central design decision. It keeps evaluation latency predictable,
avoids one database round trip per rule, and makes every rule test a plain in memory unit
test with no mocking. It also over fetches, since a transaction caught by the amount
threshold still pays for the history load. That tradeoff and the plan for tiering the
enrichment if it ever mattered are recorded in ADR 0003.

Adding a rule is one class, one DI registration and one test file. No existing rule is
touched.

Design decisions are recorded as ADRs in `docs/adr/`, including the ones about what was
deliberately left out and why.

## Known limits

Stated plainly because they are design decisions rather than oversights.

Rate limiting counts requests in the memory of one instance, so the allowance is per instance
rather than per client across the deployment. Two replicas admit twice the configured rate.
That is deliberate for this scope, since it needs no shared state and still stops a single
caller saturating the instance it reaches. Enforcing one allowance across replicas needs a
shared counter, in Redis or at the ingress, which is where this would go next.

The synchronous evaluation path holds at a few hundred requests per second on modest
hardware. The rules themselves are microseconds; the enrichment read is the bottleneck.
Caching the slow moving parts of the context handles the next increment, and moving
assessment detail writes out of the request path handles the one after that.

Past that the architecture needs to change rather than be tuned. Velocity and escalation
rules want streaming per customer aggregates maintained by a stream processor, with the
engine reading a materialised customer profile instead of querying transaction history.
That means broker based ingestion and a state store. It is the right answer at that scale
and the wrong first version, since it does not change the rule engine design, which is
what this brief is about.

Also out of scope, with reasoning in `docs/adr/`: a rules DSL for analyst managed rules,
ML scoring, case management workflow, and multi tenancy.
