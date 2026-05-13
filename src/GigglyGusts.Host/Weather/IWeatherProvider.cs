namespace GigglyGusts.Host.Weather;

/// <summary>
/// Weather resolution abstraction; Phase 4 uses a mock; Open-Meteo plugs in later.
/// </summary>
public interface IWeatherProvider
{
    /// <summary>
    /// Returns a forecast for a supported AU city key (see <see cref="AustralianCityCatalog"/>), or null if invalid.
    /// </summary>
    Task<WeatherLookupResult?> LookupAsync(string normalizedCityKey, CancellationToken cancellationToken);
}
