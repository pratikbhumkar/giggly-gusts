using System.Net;
using System.Text.Json.Nodes;
using GigglyGusts.Host.Tests.Fakes;
using GigglyGusts.Host.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GigglyGusts.Host.Tests;

public sealed class WeatherMaintenanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public WeatherMaintenanceTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    [Fact]
    public async Task Maintenance_mode_returns_503_problem_details_and_does_not_call_open_meteo()
    {
        var handler = new FakeHttpMessageHandler();

        using var factory = _baseFactory.WithFakeOpenMeteo(handler, new Dictionary<string, string?>
        {
            ["Weather:UseOpenMeteo"] = "true",
            ["Weather:MaintenanceMode"] = "true",
            ["Weather:OpenMeteo:BaseUrl"] = "https://test.open-meteo.invalid",
            ["Weather:Http:AttemptTimeoutMs"] = "200",
            ["Weather:Http:MaxRetries"] = "2",
            ["Weather:Http:BackoffBaseMs"] = "1",
            ["Weather:Http:BackoffMaxMs"] = "5",
        });

        var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/weather?city=Sydney", UriKind.Relative));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var doc = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(503, doc["status"]!.GetValue<int>());
        Assert.False(string.IsNullOrEmpty(doc["correlationId"]?.GetValue<string>()));

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Maintenance_mode_does_not_break_health_endpoint()
    {
        var handler = new FakeHttpMessageHandler();
        using var factory = _baseFactory.WithFakeOpenMeteo(handler, new Dictionary<string, string?>
        {
            ["Weather:MaintenanceMode"] = "true",
            ["Weather:UseOpenMeteo"] = "false",
        });

        var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
