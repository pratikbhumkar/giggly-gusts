# giggly-gusts

ASP.NET Core host for a **weather-style API** take-home: **thin vertical slices** for app and (from Phase 2 onward) **Terraform**, with **CI** as the safety net. Full architecture and delivery order live under **[`docs/`](./docs/README.md)**.

## Requirements

- [.NET SDK 8](https://dotnet.microsoft.com/download) matching [`global.json`](./global.json) (currently **8.0.100**, roll-forward **latestPatch**).

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

### `GET /weather` (mock provider only)

**Open-Meteo and other live weather HTTP clients are not used in this phase** — responses come from an in-process **`MockWeatherProvider`** behind **`IWeatherProvider`** (swap-friendly for a later phase).

**Australia-only rule:** `city` is **trimmed** and compared using a **normalized uppercase key** against an **allowlist** in [`AustralianCityCatalog`](./src/GigglyGusts.Host/Weather/AustralianCityCatalog.cs): **Sydney, Melbourne, Brisbane, Perth, Adelaide, Hobart, Darwin, Canberra**. Any other value (including non-AU cities) returns **400** with **ProblemDetails** (and **`Cache-Control: no-store`**). Successful responses use **`Cache-Control: public, max-age=120`**.

Success JSON (stable field names): **`city`**, **`tempC`**, **`condition`**, **`source`** (`fallback` for mock, matching **`live` \| `fallback`** in [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md)), **`correlationId`** (matches **`X-Correlation-Id`** when the client sends one).

```bash
# Valid allowlisted city (200 + JSON contract)
curl -sS -i "http://localhost:5025/weather?city=Sydney"

# Invalid: empty city after trim (400 + ProblemDetails, no-store)
curl -sS -i "http://localhost:5025/weather?city="

# Non-allowlisted / non-AU example (400 + ProblemDetails, no-store)
curl -sS -i "http://localhost:5025/weather?city=Paris"
```

### Swagger / OpenAPI

OpenAPI JSON is at **`/swagger/v1/swagger.json`** and the UI at **`/swagger`**. The **`http`** / **`https`** launch profiles open **`/swagger`** by default (see [`launchSettings.json`](./src/GigglyGusts.Host/Properties/launchSettings.json)).

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

- **`ci`:** **restore** → **`dotnet format --verify-no-changes`** → **build** → **test** (SDK from `global.json`). The job sets **`ASPNETCORE_ENVIRONMENT=Development`** and **`DOTNET_ENVIRONMENT=Development`** so CI matches the default local story.
- **`terraform`:** **`terraform fmt -check -recursive`**, **`init -backend=false`**, **`validate`**, **`plan`** in **`infra/`** — **no `apply`** on the default pipeline. A **LocalStack** service runs in that job so **`terraform plan`** can target the AWS provider **without real `AWS_*` credentials**; the job sets **`TF_VAR_use_localstack=true`**, **`TF_VAR_localstack_endpoint`**, and **`TF_VAR_container_image`** (placeholder public Lambda base image URI for the Lambda **container** resource).
- **Token & concurrency:** workflow **`permissions`** are limited to **`contents: read`** (clone) and **`actions: write`** (NuGet **cache** save/restore). **`concurrency`** dedupes runs per ref and **`cancel-in-progress: true`** cancels an in-flight run when a newer commit is pushed to the same branch/PR.

## Phase 3 (completed)

- **Application:** environment-aware **`/health`** JSON with optional **`diagnostics`** from config.
- **Terraform:** **`infra/modules/naming/`** for shared naming and tags (still used by root config).

## Phase 4 (this slice)

- **Application:** **`GET /weather?city={city}`** via **`ControllerBase`** + DI; **`IWeatherProvider`** with **`MockWeatherProvider`** only; **AU allowlist** and **ProblemDetails** on **400**; correlation id header + body field; unit tests (no **`WebApplicationFactory`**) and integration tests (**`WebApplicationFactory<Program>`**) cover rules and HTTP.
- **Terraform:** **`aws_iam_role`** (Lambda execution) + **`aws_iam_role_policy_attachment`** (basic execution), **`aws_cloudwatch_log_group`**, **`aws_lambda_function`** with **`package_type = Image`** and **`image_uri = var.container_image`**. **Plan-only** in default CI (LocalStack + **`TF_VAR_*`** as above); nothing is deployed by the main workflow.

## Docs and phased delivery

| Resource | Purpose |
|----------|---------|
| [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) | Target AWS shape, resilience, observability, environments. |
| [`docs/PHASES.md`](./docs/PHASES.md) | **Phased plan** — heartbeat, **infra + CI**, env-aware app + Terraform structure (**Phase 3**), then mock API and AWS in later phases. |
| [`docs/README.md`](./docs/README.md) | Index of diagrams, design notes, ADR folder. |

Terraform layout details: **[`infra/README.md`](./infra/README.md)**.
