using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GigglyGusts.Host.Tests;

/// <summary>
/// Ensures OpenAPI + Swagger UI are wired and only exposed in Development.
/// </summary>
public sealed class SwaggerEndpointTests
{
    [Fact]
    public async Task Swagger_openapi_json_is_served_in_development()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
        });

        var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Weather", body, StringComparison.Ordinal);
        Assert.Contains("/weather", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Swagger_ui_is_served_in_development()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
        });

        var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/swagger/index.html", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "text/html",
            response.Content.Headers.ContentType?.MediaType ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Swagger_openapi_json_is_not_served_in_production()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
        });

        var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
