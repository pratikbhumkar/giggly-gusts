using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GigglyGusts.Host.Tests;

/// <summary>
/// HTTP integration tests for <c>GET /weather</c> (controllers + DI + mock provider).
/// </summary>
public sealed class WeatherEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WeatherEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_weather_allowlisted_city_returns_200_contract_and_cache_header()
    {
        var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/weather?city=Sydney", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("public", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("max-age=120", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);

        var doc = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotNull(doc);
        Assert.Equal("Sydney", doc!["city"]?.GetValue<string>());
        Assert.Equal(22.5, doc["tempC"]?.GetValue<double>());
        Assert.Equal("Partly cloudy", doc["condition"]?.GetValue<string>());
        Assert.Equal("fallback", doc["source"]?.GetValue<string>());
        var correlationId = doc["correlationId"]?.GetValue<string>();
        Assert.False(string.IsNullOrEmpty(correlationId));
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var hdr));
        Assert.Equal(correlationId, hdr.First());
    }

    [Fact]
    public async Task Get_weather_empty_city_returns_400_problem_details_and_no_store()
    {
        var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/weather?city=", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.False(string.IsNullOrEmpty(problem.CorrelationId));
    }

    [Fact]
    public async Task Get_weather_non_au_city_returns_400()
    {
        var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/weather?city=Paris", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_weather_respects_incoming_correlation_header()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "abc-123");
        using var response = await client.GetAsync(new Uri("/weather?city=Perth", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var hdr));
        Assert.Equal("abc-123", hdr.First());
        var doc = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("abc-123", doc?["correlationId"]?.GetValue<string>());
    }

    private sealed class ProblemDetailsDto
    {
        public string? Title { get; set; }

        public int? Status { get; set; }

        public string? CorrelationId { get; set; }
    }
}
