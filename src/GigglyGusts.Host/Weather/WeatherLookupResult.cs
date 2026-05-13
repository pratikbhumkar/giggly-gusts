namespace GigglyGusts.Host.Weather;

/// <summary>
/// Successful weather lookup from a provider (mock or future live path).
/// </summary>
public sealed record WeatherLookupResult(
    string CityDisplay,
    double TempC,
    string Condition,
    string Source);
