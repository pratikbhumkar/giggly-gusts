using GigglyGusts.Host.Weather;
using Xunit;

namespace GigglyGusts.Host.Tests.Unit;

public sealed class MockWeatherProviderTests
{
    private readonly MockWeatherProvider _provider = new();

    [Fact]
    public async Task LookupAsync_known_city_returns_fallback_source()
    {
        var result = await _provider.LookupAsync("SYDNEY", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("Sydney", result.CityDisplay);
        Assert.Equal(22.5, result.TempC);
        Assert.Equal("Partly cloudy", result.Condition);
        Assert.Equal("fallback", result.Source);
    }

    [Fact]
    public async Task LookupAsync_unknown_city_returns_null()
    {
        var result = await _provider.LookupAsync("PARIS", CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task LookupAsync_empty_key_returns_null()
    {
        var result = await _provider.LookupAsync(string.Empty, CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task LookupAsync_case_insensitive_catalog_key()
    {
        var result = await _provider.LookupAsync("BRISBANE", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("Brisbane", result!.CityDisplay);
    }
}
