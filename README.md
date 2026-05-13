# giggly-gusts

ASP.NET Core host for a **weather-style API** take-home: **thin vertical slices** for app and (from Phase 2 onward) **Terraform**, with **CI** as the safety net. Full architecture and delivery order live under **[`docs/`](./docs/README.md)**.

## Requirements

- [.NET SDK 8](https://dotnet.microsoft.com/download) matching [`global.json`](./global.json) (currently **8.0.100**, roll-forward **latestPatch**).

## Run locally

From the repo root:

```bash
dotnet run --project src/GigglyGusts.Host/GigglyGusts.Host.csproj
```

Then (default **http** profile uses port **5025** — see [`launchSettings.json`](./src/GigglyGusts.Host/Properties/launchSettings.json)):

```bash
curl -sS -i http://localhost:5025/health
```

## Tests

```bash
dotnet test GigglyGusts.sln
```

## CI

[`.github/workflows/ci.yml`](./.github/workflows/ci.yml) runs on **push** and **pull_request** to **`main`**: **restore** → **`dotnet format --verify-no-changes`** → **build** → **test** (SDK pinned via `global.json`).

## Docs and phased delivery

| Resource | Purpose |
|----------|---------|
| [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) | Target AWS shape, resilience, observability, environments. |
| [`docs/PHASES.md`](./docs/PHASES.md) | **Phased plan:** **Phase 1** — heartbeat only (this repo’s committed baseline). **Phase 2+** — add **`infra/`**, **`terraform fmt` / `validate` / `plan`** in CI alongside the app, then mock API, **Docker → ECR → Lambda (container)** in **Phase 5**, Open-Meteo, optional **`apply`**, hardening. |
| [`docs/README.md`](./docs/README.md) | Index of diagrams, design notes, ADR folder. |
