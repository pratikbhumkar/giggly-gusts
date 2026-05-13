using GigglyGusts.Host.Configuration;
using GigglyGusts.Host.Health;
using GigglyGusts.Host.Middleware;
using GigglyGusts.Host.Weather;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<HealthDisplayOptions>(
    builder.Configuration.GetSection(HealthDisplayOptions.SectionName));
builder.Services.AddControllers();
builder.Services.AddSingleton<IWeatherProvider, MockWeatherProvider>();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "GigglyGusts API",
            Version = "v1",
            Description = "Weather-style HTTP API. Mock weather only in this phase (no Open-Meteo).",
        });
});
var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "GigglyGusts v1");
    });
}

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

app.MapControllers();

app.Run();

public partial class Program;
