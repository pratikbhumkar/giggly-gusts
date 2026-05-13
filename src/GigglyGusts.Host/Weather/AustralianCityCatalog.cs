namespace GigglyGusts.Host.Weather;

/// <summary>
/// Australia-only scope: allowlisted capital / major cities (see README). Non-listed cities are rejected (400).
/// Aligns with <c>docs/ARCHITECTURE.md</c> geography: supported cities / validation (allowlist path for Phase 4 mock).
/// </summary>
public static class AustralianCityCatalog
{
    private static readonly IReadOnlyDictionary<string, CityForecast> ByNormalizedKey =
        new Dictionary<string, CityForecast>(StringComparer.Ordinal)
        {
            ["SYDNEY"] = new CityForecast("Sydney", 22.5, "Partly cloudy"),
            ["MELBOURNE"] = new CityForecast("Melbourne", 17.0, "Showers clearing"),
            ["BRISBANE"] = new CityForecast("Brisbane", 26.0, "Fine"),
            ["PERTH"] = new CityForecast("Perth", 24.0, "Sunny"),
            ["ADELAIDE"] = new CityForecast("Adelaide", 20.0, "Mild"),
            ["HOBART"] = new CityForecast("Hobart", 14.0, "Cool with drizzle"),
            ["DARWIN"] = new CityForecast("Darwin", 31.0, "Humid, storms possible"),
            ["CANBERRA"] = new CityForecast("Canberra", 16.0, "Crisp, mostly sunny"),
        };

    public static bool TryGet(string normalizedCityKey, out CityForecast forecast)
    {
        return ByNormalizedKey.TryGetValue(normalizedCityKey, out forecast!);
    }

    public sealed record CityForecast(string DisplayName, double TempC, string Condition);
}
