namespace GigglyGusts.Host.Weather.Resilience;

/// <summary>
/// Single failure type thrown by the Open-Meteo pipeline.
/// </summary>
/// <remarks><see cref="IsTransient"/> gates Polly's retry; <see cref="RetryAfter"/> feeds its <c>DelayGenerator</c>; <see cref="Reason"/> is logged at fallback.</remarks>
public sealed class OpenMeteoFailedException : Exception
{
    public OpenMeteoFailedException(
        string reason,
        bool isTransient,
        TimeSpan? retryAfter = null,
        Exception? inner = null)
        : base(reason, inner)
    {
        Reason = reason;
        IsTransient = isTransient;
        RetryAfter = retryAfter;
    }

    /// <summary>Short reason code (e.g. <c>upstream_5xx_502</c>, <c>malformed_json</c>).</summary>
    public string Reason { get; }

    /// <summary>True when the failure is retryable (5xx, network, attempt timeout, 429-with-retry).</summary>
    public bool IsTransient { get; }

    /// <summary>Optional server-supplied wait (<c>Retry-After</c>) used by Polly's delay generator.</summary>
    public TimeSpan? RetryAfter { get; }
}
