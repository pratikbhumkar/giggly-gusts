using System.ComponentModel.DataAnnotations;

namespace GigglyGusts.Host.Configuration;

/// <summary>
/// All weather-pipeline configuration bound from the <c>Weather</c> section.
/// </summary>
/// <remarks>Read via <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> so Terraform / env updates take effect without code changes; no secrets; field names match <c>docs/ARCHITECTURE.md §5.9 / §10</c>.</remarks>
public sealed class WeatherOptions
{
    public const string SectionName = "Weather";

    /// <summary>Provider kill-switch: when true, call Open-Meteo (with fallback on failure); when false, mock only.</summary>
    public bool UseOpenMeteo { get; init; }

    /// <summary>When true, short-circuit weather routes with a documented 503 response without calling any provider.</summary>
    public bool MaintenanceMode { get; init; }

    /// <summary>IMemoryCache TTL for successful live lookups (seconds). 0 disables the decorator.</summary>
    [Range(0, 86_400)]
    public int CacheSeconds { get; init; } = 120;

    public OpenMeteoSettings OpenMeteo { get; init; } = new();

    public HttpResilienceSettings Http { get; init; } = new();

    public sealed class OpenMeteoSettings
    {
        public string BaseUrl { get; init; } = "https://api.open-meteo.com";
    }

    public sealed class HttpResilienceSettings
    {
        /// <summary>Per-attempt timeout (also used as <see cref="System.Net.Http.HttpClient.Timeout"/>).</summary>
        public int AttemptTimeoutMs { get; init; } = 1500;

        /// <summary>Number of retries AFTER the first attempt (total attempts = 1 + MaxRetries).</summary>
        public int MaxRetries { get; init; } = 2;

        public int BackoffBaseMs { get; init; } = 100;

        public int BackoffMaxMs { get; init; } = 1000;

        /// <summary>
        /// Documented 429 rule: when true, treat 429 like other transients (single retry with backoff,
        /// honour <c>Retry-After</c> seconds capped at <see cref="BackoffMaxMs"/>); when false, do not retry.
        /// </summary>
        public bool RetryOn429 { get; init; }
    }
}
