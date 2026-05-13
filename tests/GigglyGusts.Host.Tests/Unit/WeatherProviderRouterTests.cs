using System.Net;
using GigglyGusts.Host.Configuration;
using GigglyGusts.Host.Tests.Fakes;
using GigglyGusts.Host.Weather;
using Microsoft.Extensions.Logging;
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
    public async Task Transient_live_failure_logs_warning_and_serves_fallback()
    {
        var handler = new FakeHttpMessageHandler();
        // 5xx is transient, so Polly will retry MaxRetries times before the router catches.
        handler.EnqueueResponse(HttpStatusCode.BadGateway);
        handler.EnqueueResponse(HttpStatusCode.BadGateway);
        handler.EnqueueResponse(HttpStatusCode.BadGateway);

        var routerLog = new TestLogger<WeatherProviderRouter>();
        var router = BuildRouter(
            handler,
            new MutableOptionsMonitor(new WeatherOptions
            {
                UseOpenMeteo = true,
                Http = new WeatherOptions.HttpResilienceSettings
                {
                    AttemptTimeoutMs = 200,
                    MaxRetries = 2,
                    BackoffBaseMs = 0,
                    BackoffMaxMs = 1,
                },
            }),
            routerLog);

        var result = await router.LookupAsync("SYDNEY", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("fallback", result!.Source);
        Assert.Equal(3, handler.CallCount);
        Assert.Equal(1, routerLog.Entries.Count(e => e.Level == LogLevel.Warning));
        Assert.Equal(0, routerLog.Entries.Count(e => e.Level == LogLevel.Error));
    }

    [Fact]
    public async Task Non_transient_live_failure_logs_error_and_serves_fallback()
    {
        var handler = new FakeHttpMessageHandler();
        // 200 OK with garbage body -> malformed_json -> IsTransient=false. No retries.
        handler.EnqueueResponse(HttpStatusCode.OK, "{not json", contentType: "application/json");

        var routerLog = new TestLogger<WeatherProviderRouter>();
        var router = BuildRouter(
            handler,
            new MutableOptionsMonitor(new WeatherOptions
            {
                UseOpenMeteo = true,
                Http = new WeatherOptions.HttpResilienceSettings
                {
                    AttemptTimeoutMs = 200,
                    BackoffBaseMs = 0,
                    BackoffMaxMs = 1,
                },
            }),
            routerLog);

        var result = await router.LookupAsync("MELBOURNE", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("fallback", result!.Source);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(1, routerLog.Entries.Count(e => e.Level == LogLevel.Error));
        Assert.Equal(0, routerLog.Entries.Count(e => e.Level == LogLevel.Warning));
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

    private static WeatherProviderRouter BuildRouter(
        FakeHttpMessageHandler handler,
        MutableOptionsMonitor monitor,
        ILogger<WeatherProviderRouter>? routerLogger = null)
    {
        var live = new OpenMeteoWeatherProvider(
            new SingleHandlerHttpClientFactory(handler, new Uri("https://test.open-meteo.invalid"), TimeSpan.FromMilliseconds(200)),
            monitor,
            NullLogger<OpenMeteoWeatherProvider>.Instance);

        return new WeatherProviderRouter(
            live,
            new MockWeatherProvider(),
            monitor,
            routerLogger ?? NullLogger<WeatherProviderRouter>.Instance);
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
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
