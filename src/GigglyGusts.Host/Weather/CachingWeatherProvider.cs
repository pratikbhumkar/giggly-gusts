using GigglyGusts.Host.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GigglyGusts.Host.Weather;

/// <summary>
/// IMemoryCache decorator on <see cref="IWeatherProvider"/>. Caches successful live lookups
/// by normalized city key for <see cref="WeatherOptions.CacheSeconds"/>; null and fallback bypass.
/// </summary>
public sealed class CachingWeatherProvider(
    IWeatherProvider inner,
    IMemoryCache cache,
    IOptionsMonitor<WeatherOptions> options) : IWeatherProvider
{
    public async Task<WeatherLookupResult?> LookupAsync(
        string normalizedCityKey,
        CancellationToken cancellationToken)
    {
        var ttl = TimeSpan.FromSeconds(options.CurrentValue.CacheSeconds);
        if (ttl <= TimeSpan.Zero)
        {
            return await inner.LookupAsync(normalizedCityKey, cancellationToken);
        }

        var key = $"weather:{normalizedCityKey}";
        if (cache.Get<WeatherLookupResult>(key) is { } cached)
        {
            return cached;
        }

        var fresh = await inner.LookupAsync(normalizedCityKey, cancellationToken);
        if (fresh is { Source: not "fallback" })
        {
            cache.Set(key, fresh, ttl);
        }

        return fresh;
    }
}
