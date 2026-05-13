using System.Text.Json.Serialization;

namespace GigglyGusts.Host.Weather;

/// <summary>
/// Public JSON contract for <c>GET /weather</c> (see docs/ARCHITECTURE.md, goals table).
/// </summary>
public sealed class WeatherApiResponse
{
    [JsonPropertyName("city")]
    public string City { get; init; } = string.Empty;

    [JsonPropertyName("tempC")]
    public double TempC { get; init; }

    [JsonPropertyName("condition")]
    public string Condition { get; init; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;
}
