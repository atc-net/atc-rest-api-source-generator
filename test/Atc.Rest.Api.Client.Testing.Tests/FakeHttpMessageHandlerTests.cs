namespace Atc.Rest.Api.Client.Testing.Tests;

public sealed class FakeHttpMessageHandlerTests
{
    [Fact]
    public async Task Request_DisposedByTheClient_ItsBodyCanStillBeRead()
    {
        // Generated clients dispose the request after sending it; the recorded body must survive that
        using var handler = new FakeHttpMessageHandler(() => HttpResponseMessageFactory.NoContent());
        using var client = handler.CreateClient();

        using (var request = new HttpRequestMessage(HttpMethod.Post, "/pets") { Content = new StringContent("{\"name\":\"Fido\"}", Encoding.UTF8, "application/json") })
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        var recorded = handler.Request(0);
        Assert.Equal("{\"name\":\"Fido\"}", await recorded.ReadBodyAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Fido", (await recorded.ReadBodyAsAsync<Pet>(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web), TestContext.Current.CancellationToken))!.Name);
    }

    [Fact]
    public async Task RespondTo_WithAMethod_AnswersOnlyThatMethod()
    {
        using var handler = new FakeHttpMessageHandler(() => HttpResponseMessageFactory.StatusCode(HttpStatusCode.NotFound));
        handler
            .RespondTo(HttpMethod.Post, "/pets", () => HttpResponseMessageFactory.StatusCode(HttpStatusCode.Created))
            .RespondTo(HttpMethod.Get, "/pets", () => HttpResponseMessageFactory.StatusCode(HttpStatusCode.OK));
        using var client = handler.CreateClient();

        using var body = new StringContent("{}");
        using var created = await client.PostAsync(new Uri("/pets", UriKind.Relative), body, TestContext.Current.CancellationToken);
        using var listed = await client.GetAsync(new Uri("/pets", UriKind.Relative), TestContext.Current.CancellationToken);
        using var deleted = await client.DeleteAsync(new Uri("/pets", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
    }

    [Fact]
    public async Task RespondTo_WithoutAMethod_AnswersEveryMethod()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.RespondTo("/pets", () => HttpResponseMessageFactory.StatusCode(HttpStatusCode.Accepted));
        using var client = handler.CreateClient();

        using var response = await client.DeleteAsync(new Uri("/api/pets", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Requests_ConcurrentSends_AreAllRecorded()
    {
        using var handler = new FakeHttpMessageHandler(() => HttpResponseMessageFactory.NoContent());
        using var client = handler.CreateClient();

        await Parallel.ForAsync(0, 200, TestContext.Current.CancellationToken, async (i, ct) =>
        {
            using var response = await client.GetAsync(new Uri($"/items/{i}", UriKind.Relative), ct);
        });

        Assert.Equal(200, handler.Requests.Count);
        Assert.Equal(200, handler.Requests.Select(r => r.GetPath()).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Responses_InOrder_TheLastOneRepeats()
    {
        using var handler = new FakeHttpMessageHandler(
            () => HttpResponseMessageFactory.StatusCode(HttpStatusCode.ServiceUnavailable),
            () => HttpResponseMessageFactory.StatusCode(HttpStatusCode.OK));
        using var client = handler.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            using var response = await client.GetAsync(new Uri("/x", UriKind.Relative), TestContext.Current.CancellationToken);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK, HttpStatusCode.OK], statuses);
    }

    [Fact]
    public async Task FromRequests_ResponseSeesTheRequest()
    {
        using var handler = FakeHttpMessageHandler.FromRequests(request => HttpResponseMessageFactory.RawJson($"\"{request.GetQueryParameter("q")}\""));
        using var client = handler.CreateClient();

        var body = await client.GetStringAsync(new Uri("/search?q=fido", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal("\"fido\"", body);
    }

    [Fact]
    public async Task NoResponseConfigured_Throws()
    {
        using var handler = new FakeHttpMessageHandler();
        using var client = handler.CreateClient();

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync(new Uri("/x", UriKind.Relative), TestContext.Current.CancellationToken));
    }

    private sealed record Pet(string Name);
}