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

- **`ci`:** **restore** → **`dotnet format --verify-no-changes`** → **build** → **test** (SDK from `global.json`). The job sets **`ASPNETCORE_ENVIRONMENT=Development`** and **`DOTNET_ENVIRONMENT=Development`** so CI matches the default local story.
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

## Phase 5 (this slice)

- **Container:** repo-root [`Dockerfile`](./Dockerfile) (multi-stage, ASP.NET 8 runtime + AWS Lambda Web Adapter), [`.dockerignore`](./.dockerignore) to keep the build context lean. Same `/health` and `/weather` surface inside the container.
- **Terraform:** added **`aws_ecr_repository`** + lifecycle policy ([`infra/ecr.tf`](./infra/ecr.tf)); the existing `aws_lambda_function` already consumes **`var.container_image`** so `terraform plan` reacts to image-reference changes.
- **CI:** new **`docker`** job builds the image and smoke-tests `/health` + `/weather` after `ci` passes; **`terraform`** job continues to plan against LocalStack (now including `ecr`). **No `terraform apply`** and **no ECR push** on the default workflow — see Mode A / Mode B above.
- **Decision record:** packaging and compute choice tracked in [ADR 0001 — Lambda container from ECR (Accepted)](./docs/design/adr/0001-lambda-container-compute.md).

## Docs and phased delivery

| Resource | Purpose |
|----------|---------|
| [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) | Target AWS shape, resilience, observability, environments. |
| [`docs/PHASES.md`](./docs/PHASES.md) | **Phased plan** — heartbeat, **infra + CI**, env-aware app + Terraform structure (**Phase 3**), then mock API and AWS in later phases. |
| [`docs/README.md`](./docs/README.md) | Index of diagrams, design notes, ADR folder. |

Terraform layout details: **[`infra/README.md`](./infra/README.md)**.
