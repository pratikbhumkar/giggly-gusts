using GigglyGusts.Host.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace GigglyGusts.Host.Tests.Fixtures;

internal static class WeatherTestFactoryExtensions
{
    /// <summary>
    /// Returns a factory with weather settings overridden via in-memory configuration and the
    /// Open-Meteo HTTP client wired to a shared <see cref="FakeHttpMessageHandler"/> instance.
    /// </summary>
    public static WebApplicationFactory<Program> WithFakeOpenMeteo(
        this WebApplicationFactory<Program> baseFactory,
        FakeHttpMessageHandler handler,
        IDictionary<string, string?> weatherSettings)
    {
        return baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(weatherSettings!);
            });

            builder.ConfigureServices(services =>
            {
                services.Configure<HttpClientFactoryOptions>(GigglyGusts.Host.Weather.OpenMeteoWeatherProvider.HttpClientName, options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = handler);
                });
            });
        });
    }
}
