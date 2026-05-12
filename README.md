# giggly-gusts

HTTP API service for availability checks. Run it locally to confirm the host is up; clients use the health endpoint below.

## Prerequisites

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). The application targets `net8.0` (`src/GigglyGusts.Host`).

## Build

From the repository root:

```bash
dotnet build src/GigglyGusts.Host/GigglyGusts.Host.csproj
```

A successful build produces the host under `src/GigglyGusts.Host/bin/Debug/net8.0/` (or `Release` if you pass `-c Release`).

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
