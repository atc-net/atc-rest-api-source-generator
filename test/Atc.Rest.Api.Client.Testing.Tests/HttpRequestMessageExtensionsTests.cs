namespace Atc.Rest.Api.Client.Testing.Tests;

public sealed class HttpRequestMessageExtensionsTests
{
    [Fact]
    public void PathAndQuery_AreReadFromTheUri()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/pets/7?tag=a%20b&flag");

        Assert.Equal("/pets/7", request.GetPath());
        Assert.Equal("a b", request.GetQueryParameter("tag"));
        Assert.Equal(string.Empty, request.GetQueryParameter("flag"));
        Assert.Null(request.GetQueryParameter("missing"));
    }

    [Fact]
    public void Headers_AreReadFromTheRequestAndItsContent()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost/pets")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Correlation-Id", "abc");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token-1");

        Assert.Equal("abc", request.GetHeader("X-Correlation-Id"));
        Assert.StartsWith("application/json", request.GetHeader("Content-Type"), StringComparison.Ordinal);
        Assert.Equal("token-1", request.GetBearerToken());
    }

    [Fact]
    public async Task ReadBody_WithoutContent_IsEmpty()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/pets");

        Assert.Equal(string.Empty, await request.ReadBodyAsync(TestContext.Current.CancellationToken));
        Assert.Null(await request.ReadBodyAsAsync<object>(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void StubHttpClientFactory_ReturnsTheClientAndRecordsTheName()
    {
        using var client = new HttpClient();
        var factory = new StubHttpClientFactory(client);

        Assert.Same(client, factory.CreateClient("Pets"));
        Assert.Equal(["Pets"], factory.RequestedNames);
    }
}