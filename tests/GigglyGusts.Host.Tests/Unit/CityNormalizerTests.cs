using GigglyGusts.Host.Weather;
using Xunit;

namespace GigglyGusts.Host.Tests.Unit;

public sealed class CityNormalizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("  Sydney ", "SYDNEY")]
    [InlineData("melbourne", "MELBOURNE")]
    public void NormalizeForLookup_trims_and_uppercases(string? raw, string expected)
    {
        Assert.Equal(expected, CityNormalizer.NormalizeForLookup(raw));
    }
}
