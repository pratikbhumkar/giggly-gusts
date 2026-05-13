namespace GigglyGusts.Host.Weather;

/// <summary>
/// Australia-only scope: allowlisted capital / major cities (see README). Non-listed cities are rejected (400).
/// Aligns with <c>docs/ARCHITECTURE.md</c> geography: supported cities / validation (allowlist path).
/// </summary>
/// <remarks>Provides AU monthly fallback values (when <c>USE_OPEN_METEO=false</c> or after live failure) plus lat/lon for the Open-Meteo forecast endpoint.</remarks>
public static class AustralianCityCatalog
{
    private static readonly IReadOnlyDictionary<string, CityForecast> ByNormalizedKey =
        new Dictionary<string, CityForecast>(StringComparer.Ordinal)
        {
            ["SYDNEY"] = new CityForecast("Sydney", 22.5, "Partly cloudy", -33.8688, 151.2093),
            ["MELBOURNE"] = new CityForecast("Melbourne", 17.0, "Showers clearing", -37.8136, 144.9631),
            ["BRISBANE"] = new CityForecast("Brisbane", 26.0, "Fine", -27.4698, 153.0251),
            ["PERTH"] = new CityForecast("Perth", 24.0, "Sunny", -31.9523, 115.8613),
            ["ADELAIDE"] = new CityForecast("Adelaide", 20.0, "Mild", -34.9285, 138.6007),
            ["HOBART"] = new CityForecast("Hobart", 14.0, "Cool with drizzle", -42.8821, 147.3272),
            ["DARWIN"] = new CityForecast("Darwin", 31.0, "Humid, storms possible", -12.4634, 130.8456),
            ["CANBERRA"] = new CityForecast("Canberra", 16.0, "Crisp, mostly sunny", -35.2809, 149.1300),
        };

    public static bool TryGet(string normalizedCityKey, out CityForecast forecast)
    {
        return ByNormalizedKey.TryGetValue(normalizedCityKey, out forecast!);
    }

    public sealed record CityForecast(
        string DisplayName,
        double TempC,
        string Condition,
        double Latitude,
        double Longitude);
}
