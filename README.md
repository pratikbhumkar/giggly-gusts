# giggly-gusts

HTTP API service for availability checks. Run it locally to confirm the host is up; clients use the health endpoint below.

## Prerequisites

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). The application targets `net8.0` (`src/GigglyGusts.Host`).

The repo pins the SDK channel in **`global.json`** (`8.0.100` minimum with `rollForward: latestPatch` so any current 8.0.x patch can be used locally; CI installs a matching SDK from that file).

## Build

From the repository root (recommended — builds the host and tests):

```bash
dotnet restore GigglyGusts.sln
dotnet build GigglyGusts.sln
```

To build only the API host:

```bash
dotnet build src/GigglyGusts.Host/GigglyGusts.Host.csproj
```

Build output is under each project’s `bin/` directory (for example `src/GigglyGusts.Host/bin/Debug/net8.0/` for a default Debug build).

## Tests

From the repository root:

```bash
dotnet test GigglyGusts.sln
```

## Continuous integration

On every **push** and **pull request** targeting **`main`**, GitHub Actions runs **`ci`** with **no cloud credentials** and **no infrastructure apply**:

1. Checkout  
2. **actions/setup-dotnet** using **`global.json`** (pinned SDK channel)  
3. **`dotnet restore`** on **`GigglyGusts.sln`**  
4. **`dotnet format GigglyGusts.sln --verify-no-changes --no-restore`** (style must match **`.editorconfig`**)  
5. **`dotnet build GigglyGusts.sln --no-restore`**  
6. **`dotnet test GigglyGusts.sln --no-build`**

NuGet packages are cached between runs (`~/.nuget/packages`) to speed up restore.

**Branch for CI-related changes:** implement and land CI and quality-gate work on **`contract/phase-2-ci`**, then open a pull request into **`main`** (do not commit Phase 2 CI work only on `main` without team agreement).

**Before you push**, run format locally so PRs stay green:

```bash
dotnet restore GigglyGusts.sln
dotnet format GigglyGusts.sln --verify-no-changes --no-restore
```

## Run (local)

```bash
cd src/GigglyGusts.Host
dotnet run
```

By default the **http** launch profile serves HTTP at **http://localhost:5025**. If you use another profile or edit `Properties/launchSettings.json`, use the URLs shown when the app starts.

## Using the API

### Health

| Item | Value |
|------|--------|
| Method | `GET` |
| Path | `/health` |
| Success | `200 OK` |
| Body | JSON: `{"status":"ok"}` |
| Content-Type | `application/json; charset=utf-8` |

**Example**

```bash
curl -i http://localhost:5025/health
```

You should see `HTTP/1.1 200` (or `HTTP/1.1 200 OK`) and a response body of `{"status":"ok"}`.

## Notes

- **Base URL:** For local development, use the host and port printed at startup (default HTTP base is `http://localhost:5025`).
- **HTTPS:** The **https** profile in `Properties/launchSettings.json` uses different ports (`https://localhost:7110` and HTTP on `5025` in the template). Use the URL that matches how you started the app.
- **Configuration:** Optional settings live in `appsettings.json` and `appsettings.Development.json` next to the host project; no secrets should be committed to the repository.
