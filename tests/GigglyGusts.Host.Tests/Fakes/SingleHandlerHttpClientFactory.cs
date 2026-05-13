namespace GigglyGusts.Host.Tests.Fakes;

/// <summary>
/// <see cref="IHttpClientFactory"/> stub that always returns an <see cref="HttpClient"/>
/// wired to a single fake handler — enough for the live-provider unit tests.
/// </summary>
public sealed class SingleHandlerHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    private readonly Uri _baseAddress;
    private readonly TimeSpan _timeout;

    public SingleHandlerHttpClientFactory(HttpMessageHandler handler, Uri baseAddress, TimeSpan timeout)
    {
        _handler = handler;
        _baseAddress = baseAddress;
        _timeout = timeout;
    }

    public HttpClient CreateClient(string name)
    {
        return new HttpClient(_handler, disposeHandler: false)
        {
            BaseAddress = _baseAddress,
            Timeout = _timeout,
        };
    }
}
