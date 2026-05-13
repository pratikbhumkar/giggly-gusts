using Microsoft.Extensions.Options;

namespace GigglyGusts.Host.Tests.Fakes;

/// <summary>
/// Minimal <see cref="IOptionsMonitor{TOptions}"/> backed by a fixed instance, for unit tests
/// that do not need change-notifications.
/// </summary>
public sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T> where T : class
{
    public StaticOptionsMonitor(T value)
    {
        CurrentValue = value;
    }

    public T CurrentValue { get; }

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
