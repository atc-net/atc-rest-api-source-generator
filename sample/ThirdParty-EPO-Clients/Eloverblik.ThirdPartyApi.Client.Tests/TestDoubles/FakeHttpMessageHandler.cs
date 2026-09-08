namespace Eloverblik.ThirdPartyApi.Client.Tests.TestDoubles;

/// <summary>
/// Records every outgoing request and replays a queue of canned responses.
/// The last response is reused once the queue is exhausted.
/// </summary>
public sealed class FakeHttpMessageHandler(
    params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public HttpClient CreateClient(
        string baseAddress = "https://api.eloverblik.dk")
        => new(this, disposeHandler: false) { BaseAddress = new Uri(baseAddress) };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);

        return Task.FromResult(responses[Math.Min(Requests.Count - 1, responses.Length - 1)]());
    }
}
