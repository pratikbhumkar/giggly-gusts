namespace GigglyGusts.Host.Configuration;

/// <summary>
/// Controls optional non-secret fields on the health endpoint for environment-aware demos.
/// </summary>
public sealed class HealthDisplayOptions
{
    public const string SectionName = "Health";

    /// <summary>
    /// When true, the health JSON includes a small diagnostics object (never secrets).
    /// </summary>
    public bool IncludeDiagnostics { get; init; }
}
