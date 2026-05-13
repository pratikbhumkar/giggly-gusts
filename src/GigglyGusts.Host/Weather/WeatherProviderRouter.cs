using GigglyGusts.Host.Configuration;
using GigglyGusts.Host.Weather.Resilience;
using Microsoft.Extensions.Options;

namespace GigglyGusts.Host.Weather;

/// <summary>
/// Routes lookups to <see cref="OpenMeteoWeatherProvider"/> when <c>UseOpenMeteo</c> is on, and
/// falls back to <see cref="MockWeatherProvider"/> on <see cref="OpenMeteoFailedException"/>.
/// </summary>
public sealed class WeatherProviderRouter(
    OpenMeteoWeatherProvider live,
    MockWeatherProvider mock,
    IOptionsMonitor<WeatherOptions> options,
    ILogger<WeatherProviderRouter> logger) : IWeatherProvider
{
    public async Task<WeatherLookupResult?> LookupAsync(string normalizedCityKey, CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.UseOpenMeteo)
        {
            return await mock.LookupAsync(normalizedCityKey, cancellationToken);
        }

        try
        {
            return await live.LookupAsync(normalizedCityKey, cancellationToken);
        }
        catch (OpenMeteoFailedException ex)
        {
            logger.LogWarning("Live weather failed, serving fallback. Reason={Reason}", ex.Reason);
            return await mock.LookupAsync(normalizedCityKey, cancellationToken);
        }
    }
}
