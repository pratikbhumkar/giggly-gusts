using GigglyGusts.Host.Configuration;
using Microsoft.Extensions.Options;

namespace GigglyGusts.Host.Weather;

/// <summary>
/// Composition root for the weather pipeline: binds <see cref="WeatherOptions"/>, registers shared
/// dependencies, and chooses the provider implementation from configuration (USE_OPEN_METEO).
/// </summary>
public static class WeatherServiceCollectionExtensions
{
    public static IServiceCollection AddWeatherPipeline(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<WeatherOptions>()
            .Bind(configuration.GetSection(WeatherOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<MockWeatherProvider>();
        services.AddSingleton<OpenMeteoWeatherProvider>();

        services.AddHttpClient(OpenMeteoWeatherProvider.HttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptionsMonitor<WeatherOptions>>().CurrentValue;
            client.BaseAddress = new Uri(opts.OpenMeteo.BaseUrl);
            client.Timeout = TimeSpan.FromMilliseconds(opts.Http.AttemptTimeoutMs);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("giggly-gusts/0.1 (+https://github.com/pratikbhumkar/giggly-gusts)");
        });

        services.AddSingleton<IWeatherProvider, WeatherProviderRouter>();

        return services;
    }
}
