using GigglyGusts.Host.Configuration;
using GigglyGusts.Host.Weather;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace GigglyGusts.Host.Tests.Unit;

public sealed class CachingWeatherProviderTests
{
    [Fact]
    public async Task Cache_hit_skips_inner_call()
    {
        var inner = new RecordingProvider(_ => new WeatherLookupResult("Sydney", 22.5, "Partly cloudy", "live"));
        var decorator = BuildDecorator(inner, cacheSeconds: 120);

        var first = await decorator.LookupAsync("SYDNEY", CancellationToken.None);
        var second = await decorator.LookupAsync("SYDNEY", CancellationToken.None);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task Different_cities_dont_collide()
    {
        var inner = new RecordingProvider(key => new WeatherLookupResult(key, 18.0, "Clear sky", "live"));
        var decorator = BuildDecorator(inner, cacheSeconds: 120);

        var sydney1 = await decorator.LookupAsync("SYDNEY", CancellationToken.None);
        var melbourne = await decorator.LookupAsync("MELBOURNE", CancellationToken.None);
        var sydney2 = await decorator.LookupAsync("SYDNEY", CancellationToken.None);

        Assert.Equal("SYDNEY", sydney1!.CityDisplay);
        Assert.Equal("MELBOURNE", melbourne!.CityDisplay);
        Assert.Same(sydney1, sydney2);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task Fallback_results_are_not_cached()
    {
        var inner = new RecordingProvider(_ => new WeatherLookupResult("Sydney", 22.5, "Partly cloudy", "fallback"));
        var decorator = BuildDecorator(inner, cacheSeconds: 120);

        var first = await decorator.LookupAsync("SYDNEY", CancellationToken.None);
        var second = await decorator.LookupAsync("SYDNEY", CancellationToken.None);

        Assert.Equal("fallback", first!.Source);
        Assert.Equal("fallback", second!.Source);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task Null_results_are_not_cached()
    {
        var inner = new RecordingProvider(_ => null);
        var decorator = BuildDecorator(inner, cacheSeconds: 120);

        var first = await decorator.LookupAsync("UNKNOWN", CancellationToken.None);
        var second = await decorator.LookupAsync("UNKNOWN", CancellationToken.None);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task CacheSeconds_zero_bypasses_cache()
    {
        var inner = new RecordingProvider(_ => new WeatherLookupResult("Sydney", 22.5, "Partly cloudy", "live"));
        var decorator = BuildDecorator(inner, cacheSeconds: 0);

        await decorator.LookupAsync("SYDNEY", CancellationToken.None);
        await decorator.LookupAsync("SYDNEY", CancellationToken.None);
        await decorator.LookupAsync("SYDNEY", CancellationToken.None);

        Assert.Equal(3, inner.CallCount);
    }

    private static CachingWeatherProvider BuildDecorator(IWeatherProvider inner, int cacheSeconds)
    {
        return new CachingWeatherProvider(
            inner,
            new MemoryCache(new MemoryCacheOptions()),
            new MutableOptionsMonitor(new WeatherOptions { CacheSeconds = cacheSeconds }));
    }

    private sealed class RecordingProvider : IWeatherProvider
    {
        private readonly Func<string, WeatherLookupResult?> _factory;

        public RecordingProvider(Func<string, WeatherLookupResult?> factory)
        {
            _factory = factory;
        }

        public int CallCount { get; private set; }

        public Task<WeatherLookupResult?> LookupAsync(string normalizedCityKey, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_factory(normalizedCityKey));
        }
    }

    private sealed class MutableOptionsMonitor : IOptionsMonitor<WeatherOptions>
    {
        private WeatherOptions _value;

        public MutableOptionsMonitor(WeatherOptions value)
        {
            _value = value;
        }

        public WeatherOptions CurrentValue => _value;

        public WeatherOptions Get(string? name) => _value;

        public IDisposable? OnChange(Action<WeatherOptions, string?> listener) => null;

        public void Replace(WeatherOptions next) => _value = next;
    }
}
