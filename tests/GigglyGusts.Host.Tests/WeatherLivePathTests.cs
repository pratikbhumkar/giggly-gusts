using System.Net;
using System.Text.Json.Nodes;
using GigglyGusts.Host.Tests.Fakes;
using GigglyGusts.Host.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GigglyGusts.Host.Tests;

/// <summary>
/// End-to-end HTTP tests for the Open-Meteo live path with the network faked.
/// Asserts the documented failure policy (Option A): retries are bounded and bounded failures
/// downgrade to <c>source=fallback</c> rather than surfacing a 5xx.
/// </summary>
public sealed class WeatherLivePathTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Dictionary<string, string?> UseOpenMeteoOn = new()
    {
        ["Weather:UseOpenMeteo"] = "true",
        ["Weather:MaintenanceMode"] = "false",
        ["Weather:OpenMeteo:BaseUrl"] = "https://test.open-meteo.invalid",
        ["Weather:Http:AttemptTimeoutMs"] = "200",
        ["Weather:Http:MaxRetries"] = "2",
        ["Weather:Http:BackoffBaseMs"] = "1",
        ["Weather:Http:BackoffMaxMs"] = "5",
        ["Weather:Http:RetryOn429"] = "false",
    };

    private readonly WebApplicationFactory<Program> _baseFactory;

    public WeatherLivePathTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    [Fact]
    public async Task Live_success_returns_source_live_and_phase4_contract()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """{"current":{"temperature_2m":19.5,"weather_code":2}}""");

        using var factory = _baseFactory.WithFakeOpenMeteo(handler, UseOpenMeteoOn);
        var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/weather?city=Sydney", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("Sydney", doc["city"]!.GetValue<string>());
        Assert.Equal(19.5, doc["tempC"]!.GetValue<double>());
        Assert.Equal("Partly cloudy", doc["condition"]!.GetValue<string>());
        Assert.Equal("live", doc["source"]!.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(doc["correlationId"]!.GetValue<string>()));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Live_5xx_triggers_retries_then_falls_back()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.BadGateway);
        handler.EnqueueResponse(HttpStatusCode.ServiceUnavailable);
        handler.EnqueueResponse(HttpStatusCode.InternalServerError);

        using var factory = _baseFactory.WithFakeOpenMeteo(handler, UseOpenMeteoOn);
        var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/weather?city=Perth", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("Perth", doc["city"]!.GetValue<string>());
        Assert.Equal("fallback", doc["source"]!.GetValue<string>());
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task Timeout_triggers_retries_then_falls_back()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueException(new TaskCanceledException("timeout", new TimeoutException()));
        handler.EnqueueException(new TaskCanceledException("timeout", new TimeoutException()));
        handler.EnqueueException(new TaskCanceledException("timeout", new TimeoutException()));

        using var factory = _baseFactory.WithFakeOpenMeteo(handler, UseOpenMeteoOn);
        var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/weather?city=Brisbane", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("Brisbane", doc["city"]!.GetValue<string>());
        Assert.Equal("fallback", doc["source"]!.GetValue<string>());
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task Garbage_json_falls_back_without_extra_retries()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "{not json", contentType: "application/json");

        using var factory = _baseFactory.WithFakeOpenMeteo(handler, UseOpenMeteoOn);
        var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/weather?city=Adelaide", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("Adelaide", doc["city"]!.GetValue<string>());
        Assert.Equal("fallback", doc["source"]!.GetValue<string>());
        Assert.Equal(1, handler.CallCount);
    }
}
