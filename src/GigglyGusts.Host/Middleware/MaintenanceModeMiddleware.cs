using GigglyGusts.Host.Configuration;
using Microsoft.Extensions.Options;

namespace GigglyGusts.Host.Middleware;

/// <summary>
/// Branches off <c>/weather*</c> and, when <see cref="WeatherOptions.MaintenanceMode"/> is true,
/// short-circuits with a <c>503</c> <c>application/problem+json</c> body and
/// <c>Cache-Control: no-store</c>. <c>/health</c> and <c>/swagger</c> are unaffected because they
/// never enter the branch. Per <c>docs/ARCHITECTURE.md §10.2</c>, <c>MAINTENANCE_MODE</c> beats
/// <c>USE_OPEN_METEO</c>.
/// </summary>
public static class MaintenanceModeMiddleware
{
    public static IApplicationBuilder UseWeatherMaintenanceMode(this IApplicationBuilder app)
    {
        return app.UseWhen(
            ctx => ctx.Request.Path.StartsWithSegments("/weather"),
            branch => branch.Use(async (HttpContext ctx, RequestDelegate next) =>
            {
                var options = ctx.RequestServices
                    .GetRequiredService<IOptionsMonitor<WeatherOptions>>()
                    .CurrentValue;

                if (!options.MaintenanceMode)
                {
                    await next(ctx);
                    return;
                }

                var correlationId = ctx.Items[CorrelationIdMiddleware.HttpContextItemKey] as string
                    ?? Guid.NewGuid().ToString("N");

                ctx.Response.Headers.CacheControl = "no-store";

                await Results.Problem(
                    detail: "Weather service is temporarily unavailable for maintenance.",
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Service in maintenance",
                    type: "https://tools.ietf.org/html/rfc7231#section-6.6.4",
                    extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId })
                    .ExecuteAsync(ctx);
            }));
    }
}
