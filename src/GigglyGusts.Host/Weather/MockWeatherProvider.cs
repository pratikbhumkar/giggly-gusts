namespace GigglyGusts.Host.Weather;

/// <summary>
/// Static AU allowlist weather; <see cref="WeatherLookupResult.Source"/> is <c>fallback</c> per architecture (no Open-Meteo yet).
/// </summary>
public sealed class MockWeatherProvider : IWeatherProvider
{
    public Task<WeatherLookupResult?> LookupAsync(string normalizedCityKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(normalizedCityKey))
        {
            return Task.FromResult<WeatherLookupResult?>(null);
        }

        if (!AustralianCityCatalog.TryGet(normalizedCityKey, out var row))
        {
            return Task.FromResult<WeatherLookupResult?>(null);
        }

        // Architecture §1: source live | fallback — mock path matches static table semantics.
        var result = new WeatherLookupResult(row.DisplayName, row.TempC, row.Condition, "fallback");
        return Task.FromResult<WeatherLookupResult?>(result);
    }
}
