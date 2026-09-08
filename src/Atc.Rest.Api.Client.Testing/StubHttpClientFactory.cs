namespace Atc.Rest.Api.Client.Testing;

/// <summary>
/// An <see cref="IHttpClientFactory"/> that hands out a pre-built <see cref="HttpClient"/>
/// and records the names it was asked for.
/// </summary>
/// <remarks>
/// Clients generated in <c>EndpointPerOperation</c> mode resolve their transport through
/// <see cref="IHttpClientFactory"/> using a generated <c>Constants.HttpClientName</c>, so
/// this is the seam that lets an endpoint be exercised without a real server. Asserting on
/// <see cref="RequestedNames"/> also proves the endpoint asked for the expected named client.
/// </remarks>
/// <example>
/// <code>
/// using var handler = new FakeHttpMessageHandler(
///     () => HttpResponseMessageFactory.Json(new { id = 1 }));
///
/// var factory = new StubHttpClientFactory(handler.CreateClient());
/// var endpoint = new GetItemEndpoint(factory);
///
/// await endpoint.ExecuteAsync(cancellationToken: CancellationToken.None);
///
/// factory.RequestedNames.Should().ContainSingle().Which.Should().Be(Constants.HttpClientName);
/// </code>
/// </example>
public sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient client;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubHttpClientFactory"/> class.
    /// </summary>
    /// <param name="client">The client to return for every requested name.</param>
    public StubHttpClientFactory(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        this.client = client;
    }

    /// <summary>
    /// Gets every client name that was requested, in order.
    /// </summary>
    public List<string> RequestedNames { get; } = [];

    /// <inheritdoc />
    public HttpClient CreateClient(string name)
    {
        RequestedNames.Add(name);
        return client;
    }
}