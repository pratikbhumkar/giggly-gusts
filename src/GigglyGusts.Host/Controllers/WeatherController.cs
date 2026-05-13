using GigglyGusts.Host.Middleware;
using GigglyGusts.Host.Weather;
using Microsoft.AspNetCore.Mvc;

namespace GigglyGusts.Host.Controllers;

[ApiController]
[Route("weather")]
public sealed class WeatherController : ControllerBase
{
    private const int SuccessCacheSeconds = 120;

    private readonly IWeatherProvider _weatherProvider;
    private readonly ILogger<WeatherController> _logger;

    public WeatherController(IWeatherProvider weatherProvider, ILogger<WeatherController> logger)
    {
        _weatherProvider = weatherProvider;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(WeatherApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAsync([FromQuery] string? city, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        // Trim and upper-invariant fold so the allowlist match is whitespace- and case-insensitive.
        var normalized = (city ?? string.Empty).Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(normalized))
        {
            _logger.LogWarning("Weather request rejected: empty city. CorrelationId={CorrelationId}", correlationId);
            return BadRequestWithNoStore(CreateProblem(correlationId, "Missing city", "Query parameter 'city' is required."));
        }

        var lookup = await _weatherProvider.LookupAsync(normalized, cancellationToken);
        if (lookup is null)
        {
            _logger.LogWarning(
                "Weather request rejected: city not in AU allowlist. CityKey={CityKey} CorrelationId={CorrelationId}",
                normalized,
                correlationId);
            return BadRequestWithNoStore(
                CreateProblem(
                    correlationId,
                    "Unsupported city",
                    "Only allowlisted Australian cities are supported in this phase (see README)."));
        }

        // private so shared caches (CDN/proxy) don't store responses paired with a per-request
        // X-Correlation-Id header; correlationId is no longer in the body.
        Response.Headers.CacheControl = $"private, max-age={SuccessCacheSeconds}";
        var body = new WeatherApiResponse
        {
            City = lookup.CityDisplay,
            TempC = lookup.TempC,
            Condition = lookup.Condition,
            Source = lookup.Source,
        };

        _logger.LogInformation(
            "Weather lookup success. City={City} Source={Source} CorrelationId={CorrelationId}",
            lookup.CityDisplay,
            lookup.Source,
            correlationId);

        return Ok(body);
    }

    private string GetCorrelationId()
    {
        if (HttpContext.Items.TryGetValue(CorrelationIdMiddleware.HttpContextItemKey, out var value)
            && value is string id
            && !string.IsNullOrEmpty(id))
        {
            return id;
        }

        return Guid.NewGuid().ToString("N");
    }

    private static ProblemDetails CreateProblem(string correlationId, string title, string detail)
    {
        return new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = StatusCodes.Status400BadRequest,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Extensions = { ["correlationId"] = correlationId },
        };
    }

    private BadRequestObjectResult BadRequestWithNoStore(ProblemDetails problem)
    {
        Response.Headers.CacheControl = "no-store";
        return BadRequest(problem);
    }
}
