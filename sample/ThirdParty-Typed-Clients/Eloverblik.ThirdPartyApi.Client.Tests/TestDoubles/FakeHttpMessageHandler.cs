namespace Eloverblik.ThirdPartyApi.Client.Tests.TestDoubles;

/// <summary>
/// Records every outgoing request and replays a queue of canned responses.
/// The last response is reused once the queue is exhausted, so a test that
/// only cares about a single call does not have to enumerate every retry.
/// </summary>
public sealed class FakeHttpMessageHandler(
    params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>
    /// Awaited before answering - lets a test hold a request open to observe
    /// cancellation or concurrency behaviour.
    /// </summary>
    public Task ResponseGate { get; set; } = Task.CompletedTask;

    public HttpClient CreateClient(
        string baseAddress = "https://api.eloverblik.dk")
        => new(this, disposeHandler: false) { BaseAddress = new Uri(baseAddress) };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);

        await ResponseGate.WaitAsync(cancellationToken);

        return responses[Math.Min(Requests.Count - 1, responses.Length - 1)]();
    }
}
