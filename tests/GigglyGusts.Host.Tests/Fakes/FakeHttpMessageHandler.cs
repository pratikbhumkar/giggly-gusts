using System.Net;

namespace GigglyGusts.Host.Tests.Fakes;

/// <summary>
/// Scripted <see cref="HttpMessageHandler"/>: each <see cref="HttpClient.SendAsync(HttpRequestMessage)"/>
/// pops the next response (or invokes the next factory) from a queue.
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _scripts = new();
    private int _calls;

    public int CallCount => _calls;

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    private readonly List<HttpRequestMessage> _requests = new();

    public FakeHttpMessageHandler EnqueueResponse(HttpStatusCode status, string? body = null, string? contentType = "application/json")
    {
        _scripts.Enqueue((_, _) =>
        {
            var response = new HttpResponseMessage(status);
            if (body is not null)
            {
                response.Content = new StringContent(body, System.Text.Encoding.UTF8, contentType ?? "application/json");
            }
            return Task.FromResult(response);
        });
        return this;
    }

    public FakeHttpMessageHandler EnqueueException(Exception ex)
    {
        _scripts.Enqueue((_, _) => Task.FromException<HttpResponseMessage>(ex));
        return this;
    }

    public FakeHttpMessageHandler EnqueueScript(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> script)
    {
        _scripts.Enqueue(script);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        _requests.Add(request);
        if (_scripts.Count == 0)
        {
            throw new InvalidOperationException("FakeHttpMessageHandler ran out of scripted responses.");
        }

        return _scripts.Dequeue()(request, cancellationToken);
    }
}
