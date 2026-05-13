using System.Net;
using GigglyGusts.Host.Configuration;
using GigglyGusts.Host.Tests.Fakes;
using GigglyGusts.Host.Weather;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GigglyGusts.Host.Tests.Unit;

public sealed class WeatherProviderRouterTests
{
    [Fact]
    public async Task UseOpenMeteo_false_routes_to_mock_without_calling_open_meteo()
    {
        var handler = new FakeHttpMessageHandler();
        var router = BuildRouter(handler, new MutableOptionsMonitor(new WeatherOptions { UseOpenMeteo = false }));

        var result = await router.LookupAsync("SYDNEY", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("fallback", result!.Source);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task UseOpenMeteo_true_routes_to_live_with_fallback()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"current":{"temperature_2m":17.3,"weather_code":2}}""");

        var router = BuildRouter(handler, new MutableOptionsMonitor(new WeatherOptions
        {
            UseOpenMeteo = true,
            Http = new WeatherOptions.HttpResilienceSettings { AttemptTimeoutMs = 200, BackoffBaseMs = 0, BackoffMaxMs = 1 },
        }));

        var result = await router.LookupAsync("MELBOURNE", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("live", result!.Source);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Flips_provider_on_next_request_when_flag_changes_at_runtime()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"current":{"temperature_2m":11.0,"weather_code":3}}""");

        var monitor = new MutableOptionsMonitor(new WeatherOptions { UseOpenMeteo = false });
        var router = BuildRouter(handler, monitor);

        var first = await router.LookupAsync("PERTH", CancellationToken.None);
        Assert.Equal("fallback", first!.Source);
        Assert.Equal(0, handler.CallCount);

        monitor.Replace(new WeatherOptions
        {
            UseOpenMeteo = true,
            Http = new WeatherOptions.HttpResilienceSettings { AttemptTimeoutMs = 200, BackoffBaseMs = 0, BackoffMaxMs = 1 },
        });

        var second = await router.LookupAsync("PERTH", CancellationToken.None);
        Assert.Equal("live", second!.Source);
        Assert.Equal(1, handler.CallCount);
    }

    private static WeatherProviderRouter BuildRouter(FakeHttpMessageHandler handler, MutableOptionsMonitor monitor)
    {
        var live = new OpenMeteoWeatherProvider(
            new SingleHandlerHttpClientFactory(handler, new Uri("https://test.open-meteo.invalid"), TimeSpan.FromMilliseconds(200)),
            monitor,
            NullLogger<OpenMeteoWeatherProvider>.Instance);

        return new WeatherProviderRouter(
            live,
            new MockWeatherProvider(),
            monitor,
            NullLogger<WeatherProviderRouter>.Instance);
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
