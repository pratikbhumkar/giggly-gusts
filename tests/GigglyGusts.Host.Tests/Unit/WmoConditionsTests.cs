using GigglyGusts.Host.Weather.Resilience;
using Xunit;

namespace GigglyGusts.Host.Tests.Unit;

public sealed class WmoConditionsTests
{
    [Theory]
    [InlineData(0, "Clear sky")]
    [InlineData(2, "Partly cloudy")]
    [InlineData(48, "Fog")]
    [InlineData(63, "Rain")]
    [InlineData(82, "Rain showers")]
    [InlineData(95, "Thunderstorm")]
    [InlineData(99, "Thunderstorm with hail")]
    public void Describe_known_codes(int code, string expected)
        => Assert.Equal(expected, WmoConditions.Describe(code));

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(12345)]
    public void Describe_unknown_codes_returns_unknown(int code)
        => Assert.Equal("Unknown", WmoConditions.Describe(code));
}
