using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using GigglyGusts.Host.Configuration;
using GigglyGusts.Host.Weather.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace GigglyGusts.Host.Weather;

/// <summary>
/// Live <see cref="IWeatherProvider"/> against Open-Meteo's current-weather endpoint.
/// </summary>
/// <remarks>Failures throw <see cref="OpenMeteoFailedException"/>; Polly retries only the transient ones and honours <c>Retry-After</c> on 429.</remarks>
public sealed class OpenMeteoWeatherProvider(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<WeatherOptions> options,
    ILogger<OpenMeteoWeatherProvider> logger) : IWeatherProvider
{
    public const string HttpClientName = "open-meteo";

    public async Task<WeatherLookupResult?> LookupAsync(string normalizedCityKey, CancellationToken cancellationToken)
    {
        if (!AustralianCityCatalog.TryGet(normalizedCityKey, out var city))
        {
            return null;
        }

        var pipeline = BuildPipeline(options.CurrentValue.Http, city.DisplayName);
        return await pipeline.ExecuteAsync(
            async ct => await CallAndMapAsync(city, ct),
            cancellationToken);
    }

    private ResiliencePipeline BuildPipeline(WeatherOptions.HttpResilienceSettings http, string city)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<OpenMeteoFailedException>(ex => ex.IsTransient),
                MaxRetryAttempts = Math.Max(0, http.MaxRetries),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(Math.Max(0, http.BackoffBaseMs)),
                MaxDelay = TimeSpan.FromMilliseconds(Math.Max(1, http.BackoffMaxMs)),
                DelayGenerator = args => ValueTask.FromResult<TimeSpan?>(
                    args.Outcome.Exception is OpenMeteoFailedException { RetryAfter: { } wait }
                        ? TimeSpan.FromMilliseconds(Math.Min(http.BackoffMaxMs, Math.Max(0, wait.TotalMilliseconds)))
                        : null),
                OnRetry = args =>
                {
                    var reason = (args.Outcome.Exception as OpenMeteoFailedException)?.Reason
                        ?? args.Outcome.Exception?.GetType().Name;
                    logger.LogInformation(
                        "Open-Meteo transient failure. City={City} Attempt={Attempt} Reason={Reason} DelayMs={DelayMs}",
                        city,
                        args.AttemptNumber + 1,
                        reason,
                        args.RetryDelay.TotalMilliseconds);
                    return default;
                },
            })
            .Build();
    }

    private async Task<WeatherLookupResult> CallAndMapAsync(
        AustralianCityCatalog.CityForecast city,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var path = BuildPath(city);

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(path, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OpenMeteoFailedException("attempt_timeout", isTransient: true, inner: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new OpenMeteoFailedException("network_error", isTransient: true, inner: ex);
        }

        using (response)
        {
            return await HandleResponseAsync(city, response, cancellationToken);
        }
    }

    private async Task<WeatherLookupResult> HandleResponseAsync(
        AustralianCityCatalog.CityForecast city,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        var retryOn429 = options.CurrentValue.Http.RetryOn429;

        OpenMeteoFailedException? failure = status switch
        {
            429 => new OpenMeteoFailedException(
                "rate_limited",
                isTransient: retryOn429,
                retryAfter: retryOn429 ? response.Headers.RetryAfter?.Delta : null),
            >= 500 and <= 599 => new OpenMeteoFailedException($"upstream_5xx_{status}", isTransient: true),
            >= 400 and <= 499 => new OpenMeteoFailedException($"upstream_4xx_{status}", isTransient: false),
            _ when !response.IsSuccessStatusCode => new OpenMeteoFailedException($"upstream_unexpected_{status}", isTransient: false),
            _ => null,
        };

        if (failure is not null)
        {
            throw failure;
        }

        OpenMeteoCurrentResponse? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<OpenMeteoCurrentResponse>(cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new OpenMeteoFailedException("malformed_json", isTransient: false, inner: ex);
        }

        if (payload?.Current is null || payload.Current.TemperatureC is null || payload.Current.WeatherCode is null)
        {
            throw new OpenMeteoFailedException("incomplete_payload", isTransient: false);
        }

        var condition = WmoConditions.Describe(payload.Current.WeatherCode.Value);
        return new WeatherLookupResult(city.DisplayName, payload.Current.TemperatureC.Value, condition, "live");
    }

    private static string BuildPath(AustralianCityCatalog.CityForecast city)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"/v1/forecast?latitude={city.Latitude}&longitude={city.Longitude}&current=temperature_2m,weather_code&timezone=auto");
    }

    private sealed record OpenMeteoCurrentResponse(
        [property: JsonPropertyName("current")] OpenMeteoCurrent? Current);

    private sealed record OpenMeteoCurrent(
        [property: JsonPropertyName("temperature_2m")] double? TemperatureC,
        [property: JsonPropertyName("weather_code")] int? WeatherCode);
}
