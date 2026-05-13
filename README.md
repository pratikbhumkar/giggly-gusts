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
- **`terraform`:** **`terraform fmt -check -recursive`**, **`init -backend=false`**, **`validate`**, **`plan`** in **`infra/`** — no **apply**, no required **AWS** / **OIDC** secrets.
- **Token & concurrency:** workflow **`permissions`** are limited to **`contents: read`** (clone) and **`actions: write`** (NuGet **cache** save/restore). **`concurrency`** dedupes runs per ref and **`cancel-in-progress: true`** cancels an in-flight run when a newer commit is pushed to the same branch/PR.

## Phase 3 (this slice)

- **Application:** environment-aware **`/health`** JSON ( **`environment`** from **`IHostEnvironment.EnvironmentName`**, optional **`diagnostics`** from config) differs between **Development** and **Production** using **`appsettings.*.json`** — no secrets in responses or logs.
- **Terraform:** **`infra/modules/naming/`** child module (variables / locals / outputs only) for **name prefix**, **standard tag map**, and **planned** Lambda / log group / ECR / API Gateway **name strings**; root **`main.tf`** wires the module. **No `provider "aws"`**, no **`aws_*`** resources or data sources — **AWS resources land in Phase 4+**.

## Docs and phased delivery

| Resource | Purpose |
|----------|---------|
| [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) | Target AWS shape, resilience, observability, environments. |
| [`docs/PHASES.md`](./docs/PHASES.md) | **Phased plan** — heartbeat, **infra + CI**, env-aware app + Terraform structure (**Phase 3**), then mock API and AWS in later phases. |
| [`docs/README.md`](./docs/README.md) | Index of diagrams, design notes, ADR folder. |

Terraform layout details: **[`infra/README.md`](./infra/README.md)**.
