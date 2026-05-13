namespace GigglyGusts.Host.Health;

/// <summary>
/// Serializable health payload; shape differs by environment and <see cref="Configuration.HealthDisplayOptions"/>.
/// </summary>
public sealed record HealthResponse(
    string Status,
    string Environment,
    DiagnosticsPayload? Diagnostics);

public sealed record DiagnosticsPayload(string Profile);
