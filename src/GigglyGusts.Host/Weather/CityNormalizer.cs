namespace GigglyGusts.Host.Weather;

/// <summary>
/// Normalizes the <c>city</c> query parameter (trim, case rules) before allowlist checks.
/// </summary>
public static class CityNormalizer
{
    /// <summary>
    /// Trims and folds to uppercase ASCII for allowlist matching (AU English city names only in Phase 4).
    /// </summary>
    public static string NormalizeForLookup(string? raw)
    {
        if (raw is null)
        {
            return string.Empty;
        }

        return raw.Trim().ToUpperInvariant();
    }
}
