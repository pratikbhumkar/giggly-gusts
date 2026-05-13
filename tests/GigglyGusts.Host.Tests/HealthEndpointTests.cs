using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace GigglyGusts.Host.Tests;

/// <summary>
/// Integration tests for the public HTTP surface of the host.
/// </summary>
public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static JsonSerializerOptions JsonOptions => new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task Get_health_development_includes_environment_and_diagnostics()
    {
        var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var payload = await response.Content.ReadFromJsonAsync<HealthApiDto>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("ok", payload.Status);
        Assert.Equal("Development", payload.Environment);
        Assert.NotNull(payload.Diagnostics);
        Assert.Equal("non-production", payload.Diagnostics.Profile);
    }

    [Fact]
    public async Task Get_health_production_omits_diagnostics_and_shows_production()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, Environments.Production);
        });

        var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HealthApiDto>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("ok", payload.Status);
        Assert.Equal("Production", payload.Environment);
        Assert.Null(payload.Diagnostics);
    }

    private sealed class HealthApiDto
    {
        public string Status { get; set; } = string.Empty;

        public string Environment { get; set; } = string.Empty;

        public DiagnosticsApiDto? Diagnostics { get; set; }
    }

    private sealed class DiagnosticsApiDto
    {
        public string Profile { get; set; } = string.Empty;
    }
}
