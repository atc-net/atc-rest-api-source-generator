namespace Eloverblik.ThirdPartyApi.Client.Tests.TestDoubles;

/// <summary>
/// Hands out a single <see cref="HttpClient"/> for the generated client name.
/// Generated endpoints resolve their transport through <see cref="IHttpClientFactory"/>,
/// so this is the seam that lets an endpoint be exercised without a real server.
/// </summary>
public sealed class StubHttpClientFactory(
    HttpClient client) : IHttpClientFactory
{
    public List<string> RequestedNames { get; } = [];

    public HttpClient CreateClient(
        string name)
    {
        RequestedNames.Add(name);
        return client;
    }
}