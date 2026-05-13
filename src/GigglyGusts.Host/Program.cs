using GigglyGusts.Host.Configuration;
using GigglyGusts.Host.Health;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<HealthDisplayOptions>(
    builder.Configuration.GetSection(HealthDisplayOptions.SectionName));
var app = builder.Build();

app.MapGet(
    "/health",
    (IHostEnvironment env, IOptions<HealthDisplayOptions> healthOptions) =>
    {
        var opts = healthOptions.Value;
        DiagnosticsPayload? diagnostics = opts.IncludeDiagnostics
            ? new DiagnosticsPayload(env.EnvironmentName)
            : null;

        return Results.Json(new HealthResponse("ok", env.EnvironmentName, diagnostics));
    });

app.Run();

public partial class Program;
