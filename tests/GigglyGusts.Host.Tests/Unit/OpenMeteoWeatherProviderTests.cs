using System.Net;
using GigglyGusts.Host.Configuration;
using GigglyGusts.Host.Tests.Fakes;
using GigglyGusts.Host.Weather;
using GigglyGusts.Host.Weather.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GigglyGusts.Host.Tests.Unit;

public sealed class OpenMeteoWeatherProviderTests
{
    private const string SydneyKey = "SYDNEY";

    /// <summary>
    /// Resilience comes from Polly; tests assert observable outcomes (attempt count, success vs
    /// give-up, propagated cancellation) rather than Polly internals (jitter math, delay values).
    /// Backoff bounds are forced to ~0 so the suite still runs in well under a second.
    /// </summary>
    private static OpenMeteoWeatherProvider BuildProvider(
        FakeHttpMessageHandler handler,
        WeatherOptions? options = null)
    {
        options ??= new WeatherOptions
        {
            UseOpenMeteo = true,
            Http = new WeatherOptions.HttpResilienceSettings
            {
                AttemptTimeoutMs = 100,
                MaxRetries = 2,
                BackoffBaseMs = 0,
                BackoffMaxMs = 1,
                RetryOn429 = false,
            },
        };

        var factory = new SingleHandlerHttpClientFactory(
            handler,
            new Uri(options.OpenMeteo.BaseUrl),
            TimeSpan.FromMilliseconds(options.Http.AttemptTimeoutMs));

        var monitor = new StaticOptionsMonitor<WeatherOptions>(options);
        return new OpenMeteoWeatherProvider(factory, monitor, NullLogger<OpenMeteoWeatherProvider>.Instance);
    }

    [Fact]
    public async Task Maps_happy_payload_to_live_result()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"current":{"temperature_2m":24.7,"weather_code":2}}""");

        var provider = BuildProvider(handler);
        var result = await provider.LookupAsync(SydneyKey, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Sydney", result!.CityDisplay);
        Assert.Equal(24.7, result.TempC);
        Assert.Equal("Partly cloudy", result.Condition);
        Assert.Equal("live", result.Source);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Unknown_au_city_returns_null_without_http()
    {
        var handler = new FakeHttpMessageHandler();
        var provider = BuildProvider(handler);

        var result = await provider.LookupAsync("PARIS", CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, handler.CallCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task Retries_on_5xx_until_giveup(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(status);
        handler.EnqueueResponse(status);
        handler.EnqueueResponse(status);

        var provider = BuildProvider(handler);

        await Assert.ThrowsAsync<OpenMeteoFailedException>(
            () => provider.LookupAsync(SydneyKey, CancellationToken.None));
        Assert.Equal(3, handler.CallCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Does_not_retry_on_4xx(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(status);

        var provider = BuildProvider(handler);

        await Assert.ThrowsAsync<OpenMeteoFailedException>(
            () => provider.LookupAsync(SydneyKey, CancellationToken.None));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Default_does_not_retry_on_429()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.TooManyRequests);

        var provider = BuildProvider(handler);

        await Assert.ThrowsAsync<OpenMeteoFailedException>(
            () => provider.LookupAsync(SydneyKey, CancellationToken.None));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Retries_on_429_when_enabled_and_recovers()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueScript((req, ct) =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            resp.Headers.Add("Retry-After", "1");
            return Task.FromResult(resp);
        });
        handler.EnqueueResponse(HttpStatusCode.OK, """{"current":{"temperature_2m":18.0,"weather_code":3}}""");

        var options = new WeatherOptions
        {
            UseOpenMeteo = true,
            Http = new WeatherOptions.HttpResilienceSettings
            {
                AttemptTimeoutMs = 500,
                MaxRetries = 2,
                BackoffBaseMs = 0,
                BackoffMaxMs = 2,
                RetryOn429 = true,
            },
        };

        var provider = BuildProvider(handler, options);

        var result = await provider.LookupAsync(SydneyKey, CancellationToken.None);

        Assert.Equal("live", result!.Source);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task Network_error_is_retried_then_recovers()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("dns down"));
        handler.EnqueueResponse(HttpStatusCode.OK, """{"current":{"temperature_2m":15.0,"weather_code":0}}""");

        var provider = BuildProvider(handler);
        var result = await provider.LookupAsync(SydneyKey, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("live", result!.Source);
        Assert.Equal("Clear sky", result.Condition);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task Per_attempt_timeout_is_retried()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueException(new TaskCanceledException("timeout", new TimeoutException()));
        handler.EnqueueResponse(HttpStatusCode.OK, """{"current":{"temperature_2m":21.0,"weather_code":1}}""");

        var provider = BuildProvider(handler);
        var result = await provider.LookupAsync(SydneyKey, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("live", result!.Source);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task Malformed_json_is_not_retried()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "{not json", contentType: "application/json");

        var provider = BuildProvider(handler);

        var ex = await Assert.ThrowsAsync<OpenMeteoFailedException>(
            () => provider.LookupAsync(SydneyKey, CancellationToken.None));
        Assert.Equal("malformed_json", ex.Reason);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Incomplete_payload_is_not_retried()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"current":{"temperature_2m":null,"weather_code":2}}""");

        var provider = BuildProvider(handler);

        var ex = await Assert.ThrowsAsync<OpenMeteoFailedException>(
            () => provider.LookupAsync(SydneyKey, CancellationToken.None));
        Assert.Equal("incomplete_payload", ex.Reason);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task User_cancellation_is_propagated_not_swallowed()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueScript(async (req, ct) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"current":{"temperature_2m":1,"weather_code":0}}"""),
            };
        });

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var provider = BuildProvider(handler);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => provider.LookupAsync(SydneyKey, cts.Token));
    }
}
