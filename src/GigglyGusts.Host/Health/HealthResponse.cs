namespace GigglyGusts.Host.Health;

/// <summary>
/// Serializable health payload; shape differs by environment and <see cref="Configuration.HealthDisplayOptions"/>.
/// </summary>
public sealed record HealthResponse(
    string Status,
    string Environment,
    DiagnosticsPayload? Diagnostics);

/// <summary>
/// Extra health fields when <c>Health:IncludeDiagnostics</c> is true; values mirror the running host (no secrets).
/// </summary>
public sealed record DiagnosticsPayload(string HostEnvironment);
