# giggly-gusts

ASP.NET Core host for a **weather-style API** take-home: **thin vertical slices** for app and (from Phase 2 onward) **Terraform**, with **CI** as the safety net. Full architecture and delivery order live under **[`docs/`](./docs/README.md)**.

## Requirements

- [.NET SDK 8](https://dotnet.microsoft.com/download) matching [`global.json`](./global.json) (currently **8.0.100**, roll-forward **latestPatch**).
- [Docker](https://docs.docker.com/get-docker/) (only for the container workflow in Phase 5+).
- [Terraform 1.10.5](https://developer.hashicorp.com/terraform/downloads) (only for `infra/`).

## Run locally

### Default (Development)

`ASPNETCORE_ENVIRONMENT` and `DOTNET_ENVIRONMENT` default to **Development** when you run from the SDK (see [`launchSettings.json`](./src/GigglyGusts.Host/Properties/launchSettings.json)). From the repo root:

```bash
dotnet run --project src/GigglyGusts.Host/GigglyGusts.Host.csproj
```

Then (default **http** profile uses port **5025**):

```bash
curl -sS -i http://localhost:5025/health
```

You should see JSON with **`"environment":"Development"`** and a **`diagnostics`** object (non-secret) because [`appsettings.Development.json`](./src/GigglyGusts.Host/appsettings.Development.json) sets **`Health:IncludeDiagnostics`** to **true**.

### `GET /weather` (mock or live)

The endpoint is the same in both modes; only the **`source`** field on a successful response changes (`live` vs `fallback`). The success body shape is **`city`**, **`tempC`**, **`condition`**, **`source`**; the correlation id is **header-only** on successful responses — the **`X-Correlation-Id`** response header is the canonical surface (echoed from the request when present, generated otherwise). The **400** ProblemDetails error path still carries **`correlationId`** in its `extensions` because that path is `no-store`.

**Australia-only rule:** `city` is **trimmed** and compared using a **normalized uppercase key** against an **allowlist** in [`AustralianCityCatalog`](./src/GigglyGusts.Host/Weather/AustralianCityCatalog.cs): **Sydney, Melbourne, Brisbane, Perth, Adelaide, Hobart, Darwin, Canberra**. Any other value (including non-AU cities) returns **400** with **ProblemDetails** (and **`Cache-Control: no-store`**). Successful responses use **`Cache-Control: private, max-age=120`** — **`private`** (not `public`) so shared caches (CDNs, proxies) don't store responses keyed alongside a per-request **`X-Correlation-Id`** header.

#### Mock only (default)

`USE_OPEN_METEO=false`. No outbound HTTP; the static AU table answers every valid request with **`source=fallback`**.

```bash
dotnet run --project src/GigglyGusts.Host/GigglyGusts.Host.csproj

curl -sS -i "http://localhost:5025/weather?city=Sydney"     # 200, source=fallback
curl -sS -i "http://localhost:5025/weather?city="            # 400, no-store, ProblemDetails
curl -sS -i "http://localhost:5025/weather?city=Paris"       # 400, no-store, ProblemDetails
```

#### Live Open-Meteo (Phase 6)

Flip the kill-switch on and the host calls Open-Meteo for each valid city; on bounded failure it falls back to the AU table:

```bash
Weather__UseOpenMeteo=true \
  dotnet run --project src/GigglyGusts.Host/GigglyGusts.Host.csproj

curl -sS "http://localhost:5025/weather?city=Sydney"   # 200, source=live (Open-Meteo)
```

**Live path failure policy (Option A, documented).** When the Open-Meteo pipeline exhausts its retry budget or hits a defensive non-retryable failure (e.g. malformed JSON), the controller **does not** return 5xx. Instead, the composite provider serves the static AU fallback with **`source=fallback`**, preserving the Phase 4 contract for clients. The router differentiates by **`OpenMeteoFailedException.IsTransient`**: transient exhaustion (e.g. 5xx / timeouts after retries) is logged at **`Warning`**, while non-transient failures (malformed JSON, incomplete payload, `upstream_4xx_*` — i.e. protocol drift or config bugs that retries cannot fix) still fall back but log at **`Error`** so operators can spot them. User cancellations (`HttpContext.RequestAborted`) are propagated as cancellation, never converted to a fallback. Maintenance is handled separately (see below).

#### Maintenance mode

`MAINTENANCE_MODE=true` short-circuits every **`/weather*`** request with **`503 Service Unavailable`** + **ProblemDetails** + **`Cache-Control: no-store`** **before** any provider runs. **`/health`** and **`/swagger`** stay reachable.

```bash
Weather__MaintenanceMode=true \
  dotnet run --project src/GigglyGusts.Host/GigglyGusts.Host.csproj

curl -sS -i "http://localhost:5025/weather?city=Sydney"   # 503 + ProblemDetails
curl -sS -i "http://localhost:5025/health"                 # 200 (probes still work)
```

#### Configuration keys

All keys live under the **`Weather`** section. They can be set via [`appsettings.*.json`](./src/GigglyGusts.Host/appsettings.json), env vars (using the `Weather__Foo__Bar` separator), or Terraform-passed Lambda env vars (see [`infra/compute.tf`](./infra/compute.tf)). **No secrets** in any of these.

| Key | Env var | Default | Meaning |
|-----|---------|---------|---------|
| `Weather:UseOpenMeteo` | `Weather__UseOpenMeteo` | `false` | Kill-switch for the live provider; `false` keeps the API on the static AU table. |
| `Weather:MaintenanceMode` | `Weather__MaintenanceMode` | `false` | `true` short-circuits weather routes to 503; evaluated **before** `UseOpenMeteo` (matches the truth table in [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) §10.2). |
| `Weather:OpenMeteo:BaseUrl` | `Weather__OpenMeteo__BaseUrl` | `https://api.open-meteo.com` | Live provider base URL. Override for staging or offline mocks. |
| `Weather:Http:AttemptTimeoutMs` | `Weather__Http__AttemptTimeoutMs` | `1500` | Per-attempt HTTP timeout (drives `HttpClient.Timeout`). |
| `Weather:Http:MaxRetries` | `Weather__Http__MaxRetries` | `2` | Retries **after** the first attempt (total attempts = 1 + this). |
| `Weather:Http:BackoffBaseMs` | `Weather__Http__BackoffBaseMs` | `100` | Backoff base for `base * 2^attempt`, then full jitter. |
| `Weather:Http:BackoffMaxMs` | `Weather__Http__BackoffMaxMs` | `1000` | Hard cap on the backoff window. |
| `Weather:Http:RetryOn429` | `Weather__Http__RetryOn429` | `false` | `true` enables a single retry on 429 honouring `Retry-After` (capped at `BackoffMaxMs`); `false` treats 429 as non-retryable. |

**Retry classification:** transient (retryable) — connection errors, per-attempt `TaskCanceledException` (from `HttpClient.Timeout`), 5xx, **and** 429 when `RetryOn429=true`. Non-retryable — user cancellation (`RequestAborted`), 4xx (other than the documented 429 rule), 200 with malformed/incomplete JSON.

**Circuit breaker — deferred.** A process-level circuit breaker (open / half-open / closed) around the live path is **not** implemented in Phase 6; the bounded retry budget + fallback already keeps single requests cheap. A breaker around the retry policy is reserved for a later phase per the architecture doc.

**Runtime flag updates.** `Weather:UseOpenMeteo`, `Weather:MaintenanceMode`, and the retry knobs are all read via [`IOptionsMonitor`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptionsmonitor-1) per request through [`WeatherProviderRouter`](./src/GigglyGusts.Host/Weather/WeatherProviderRouter.cs) and [`MaintenanceModeMiddleware`](./src/GigglyGusts.Host/Middleware/MaintenanceModeMiddleware.cs). Within a single request the captured value is stable (no mid-request flips); new requests pick up the latest values without a process restart. The named `HttpClient` configuration (base URL + per-attempt timeout) is snapshotted when `IHttpClientFactory` builds the client, which is per-attempt, so attempt-timeout changes also take effect on the next request.

### Swagger / OpenAPI

OpenAPI JSON is at **`/swagger/v1/swagger.json`** and the UI at **`/swagger`**. The **`http`** / **`https`** launch profiles open **`/swagger`** by default (see [`launchSettings.json`](./src/GigglyGusts.Host/Properties/launchSettings.json)).

### Run as a container (Phase 5)

The repository [`Dockerfile`](./Dockerfile) is a multi-stage build that publishes the host project and uses the **ASP.NET 8 runtime** image plus the **[AWS Lambda Web Adapter](https://github.com/awslabs/aws-lambda-web-adapter)** extension. This keeps a single image **Lambda-compatible** (the adapter forwards Lambda invocations to Kestrel on port **8080**) while still serving **plain HTTP** locally via `docker run -p`. The deviation from `public.ecr.aws/lambda/dotnet:8` is documented inline at the top of the `Dockerfile` and in [ADR 0001](./docs/design/adr/0001-lambda-container-compute.md).

```bash
docker build -t giggly-gusts-api:dev .
docker run --rm -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  giggly-gusts-api:dev
```

Then in another shell:

```bash
curl -sS http://localhost:8080/health
curl -sS "http://localhost:8080/weather?city=Sydney"
```

**Note: `EXPOSE 8080` is for local Docker only.** Inside Lambda the **`EXPOSE`** directive is ignored — the Web Adapter intercepts incoming events and proxies them to Kestrel on the same port. The Lambda function's **`image_uri`** is what matters in AWS.

#### Architecture pinning (must match Lambda)

[`infra/compute.tf`](./infra/compute.tf) pins **`architectures = ["arm64"]`** on the Lambda function, so the image **must** be `linux/arm64`. To keep CI honest, the `docker` job:

- runs `docker/setup-qemu-action@v3` (arm64 emulation),
- passes `platforms: linux/arm64` to `docker/build-push-action@v6`,
- asserts the built image's architecture is `arm64` via `docker image inspect`,
- smoke-tests `/health` + `/weather?city=Sydney` against the running arm64 container under QEMU.

On Apple Silicon, `docker build .` is `linux/arm64` by default — same as CI. On x86_64 hosts, build for arm64 explicitly:

```bash
docker buildx build --platform linux/arm64 --load -t giggly-gusts-api:dev .
```

#### Troubleshooting

- `connection refused` on `:8080` immediately after `docker run` — the .NET runtime is still starting. Retry after a few seconds; CI's smoke step polls for up to ~90s because arm64 .NET startup under QEMU on an amd64 runner is slow.
- Slow first build — Buildx pulls the SDK and runtime images and emulates arm64 on amd64 hosts; subsequent builds reuse the local layer cache (and the GitHub Actions cache in CI).
- "exec format error" or similar — usually means the image was built for the wrong architecture. Rebuild with `--platform linux/arm64`.

### Production configuration locally

Run with **Production** so [`appsettings.Production.json`](./src/GigglyGusts.Host/appsettings.Production.json) is selected and diagnostics stay off:

```bash
ASPNETCORE_ENVIRONMENT=Production DOTNET_ENVIRONMENT=Production \
  dotnet run --project src/GigglyGusts.Host/GigglyGusts.Host.csproj
```

`GET /health` should report **`"environment":"Production"`** and **omit** **`diagnostics`**.

**`/health` response policy:** The top-level **`environment`** field (ASP.NET Core host environment name) is **always returned by design** for every environment — it is not a secret and helps operators and probes confirm which profile is active. Only the nested **`diagnostics`** object is gated by **`Health:IncludeDiagnostics`** (on in Development by default, off in Production).

Base [`appsettings.json`](./src/GigglyGusts.Host/appsettings.json) holds shared defaults; environment files override **logging** and **health** display only — **no secrets** in any file.

## Tests

```bash
dotnet test GigglyGusts.sln
```

## CI

[`.github/workflows/ci.yml`](./.github/workflows/ci.yml) runs on **push** and **pull_request** to **`main`**:

- **`ci`:** **restore** → **`dotnet format --verify-no-changes`** → **build** → **test** (SDK from `global.json`). The job sets **`ASPNETCORE_ENVIRONMENT=Development`** and **`DOTNET_ENVIRONMENT=Development`**, plus the Phase 6 defaults **`Weather__UseOpenMeteo=false`** and **`Weather__MaintenanceMode=false`** so the default test process never reaches Open-Meteo; live-path coverage runs against `HttpMessageHandler` fakes inside the test suite.
- **`docker`:** runs **after `ci`**, builds [`Dockerfile`](./Dockerfile) with `docker/setup-buildx-action` + `docker/build-push-action` (cache via `type=gha`), tags the image with the commit SHA, then runs a quick smoke test that asserts **`/health`** and **`/weather?city=Sydney`** respond inside the container. **No registry push** on the default workflow.
- **`terraform`:** **`terraform fmt -check -recursive`**, **`init -backend=false`**, **`validate`**, **`plan`** in **`infra/`** — **no `apply`** on the default pipeline. A **LocalStack** service runs in that job so **`terraform plan`** can target the AWS provider **without real `AWS_*` credentials**; the job sets **`TF_VAR_use_localstack=true`**, **`TF_VAR_localstack_endpoint`**, and **`TF_VAR_container_image`** (placeholder public Lambda base image URI). LocalStack services include **`ecr`** so the new `aws_ecr_repository` (Phase 5) plans cleanly.
- **Token & concurrency:** workflow **`permissions`** are limited to **`contents: read`** (clone) and **`actions: write`** (NuGet **cache** save/restore). **`concurrency`** dedupes runs per ref and **`cancel-in-progress: true`** cancels an in-flight run when a newer commit is pushed to the same branch/PR.

### CI strategy — Mode A vs Mode B

| Mode | Required for green PRs? | What runs | AWS credentials | Image reference |
|------|-------------------------|-----------|-----------------|-----------------|
| **A (default)** | **Yes** | `ci` → `docker build` + smoke test → `terraform plan` against LocalStack | **No** real keys | **Placeholder**: `public.ecr.aws/lambda/dotnet:8` (or any pinned tag via `TF_VAR_container_image`) — **not** equivalent to a production deploy |
| **B (optional)** | **No** | Mode A **plus** `aws-actions/configure-aws-credentials` (OIDC), ECR login, `docker push` to ECR, then `terraform plan`/`apply` with the **real** image URI/digest | Yes (OIDC role) | Real ECR URI; for deploys, pin to **`@sha256:<digest>`** so the Lambda function references an immutable artifact |

Mode B is **not** wired into this repo's default workflow on purpose: a take-home should not require maintainers to provision an AWS account before contributors can PR. Treat Mode A's `terraform plan` as a **structural** check; a real deploy must go through Mode B against the actual ECR repository defined in [`infra/ecr.tf`](./infra/ecr.tf).

## Phase 3 (completed)

- **Application:** environment-aware **`/health`** JSON with optional **`diagnostics`** from config.
- **Terraform:** **`infra/modules/naming/`** for shared naming and tags (still used by root config).

## Phase 4 (completed)

- **Application:** **`GET /weather?city={city}`** via **`ControllerBase`** + DI; **`IWeatherProvider`** with **`MockWeatherProvider`** only; **AU allowlist** and **ProblemDetails** on **400**; correlation id header + body field; unit + integration tests.
- **Terraform:** Lambda execution role, basic execution policy attachment, log group, and **`aws_lambda_function`** with **`package_type = Image`**.

## Phase 5 (completed)

- **Container:** repo-root [`Dockerfile`](./Dockerfile) (multi-stage, ASP.NET 8 runtime + AWS Lambda Web Adapter), [`.dockerignore`](./.dockerignore).
- **Terraform:** **`aws_ecr_repository`** + lifecycle policy.
- **CI:** **`docker`** job builds + smoke-tests after `ci`; default workflow does not push to ECR or `apply`.

## Phase 6 (this slice)

- **Live provider:** [`OpenMeteoWeatherProvider`](./src/GigglyGusts.Host/Weather/OpenMeteoWeatherProvider.cs) behind `IHttpClientFactory` named client `open-meteo`. Resilience is delegated to a **Polly v8 `ResiliencePipeline`** built per call from the current options — bounded retries, exponential backoff with built-in jitter, a `DelayGenerator` that honours `Retry-After` when 429-retry is on, and native `CancellationToken` propagation. Per-attempt timeout comes from `HttpClient.Timeout`.
- **Fallback policy:** [`WeatherProviderRouter`](./src/GigglyGusts.Host/Weather/WeatherProviderRouter.cs) downgrades to **`source=fallback`** when the live pipeline exhausts retries; user cancellations propagate.
- **Correlation surface:** successful **`/weather`** responses carry the correlation id only in the **`X-Correlation-Id`** response header, never in the body. The success cache header is therefore **`Cache-Control: private, max-age=120`** so CDNs / shared proxies don't cache the per-request header. Error responses (400 / 503 ProblemDetails) keep `correlationId` in `extensions` because those paths are `no-store`.
- **Feature flags:** **`Weather:UseOpenMeteo`** picks live vs mock inside the router; **`Weather:MaintenanceMode`** short-circuits weather routes to **503** via [`MaintenanceModeMiddleware`](./src/GigglyGusts.Host/Middleware/MaintenanceModeMiddleware.cs) (evaluated first, beats `UseOpenMeteo`).
- **Tests:** unit tests assert observable outcomes (attempt counts, mapping for happy / malformed / incomplete payloads, 429 rules, cancellation propagation) — the retry / backoff math itself is Polly's responsibility and isn't re-asserted. Integration tests cover live success, 5xx → retry → fallback, timeout → retry → fallback, garbage JSON → fallback, and maintenance mode (no outbound HTTP).
- **Terraform:** Lambda environment variables for every new key, **`aws_lambda_function.publish = true`**, **`aws_lambda_alias "live"`**, and **`aws_lambda_provisioned_concurrency_config`** that activates when **`var.provisioned_concurrency_count > 0`** (default `0` — non-prod / take-home).
- **CI:** `ci` job explicitly exports `Weather__UseOpenMeteo=false` and `Weather__MaintenanceMode=false` so tests never depend on Open-Meteo uptime.

## Docs and phased delivery

| Resource | Purpose |
|----------|---------|
| [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) | Target AWS shape, resilience, observability, environments. |
| [`docs/PHASES.md`](./docs/PHASES.md) | **Phased plan** — heartbeat, **infra + CI**, env-aware app + Terraform structure (**Phase 3**), then mock API and AWS in later phases. |
| [`docs/README.md`](./docs/README.md) | Index of diagrams, design notes, ADR folder. |

Terraform layout details: **[`infra/README.md`](./infra/README.md)**.
