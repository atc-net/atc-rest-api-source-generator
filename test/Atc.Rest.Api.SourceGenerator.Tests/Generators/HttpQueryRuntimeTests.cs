namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Runtime tests for the OpenAPI 3.2 <c>QUERY</c> method against a real Kestrel host.
/// </summary>
/// <remarks>
/// Everything else about QUERY support is verified at compile time — snapshots of the generated
/// source, and compilation of that source. None of it proves the shape actually works when a
/// request hits a running server, which is the whole point of choosing QUERY over GET: the
/// criteria have to survive as a request body because they are too large for a URL.
/// <para>
/// These tests use the same endpoint registration the generator emits —
/// <c>MapMethods(pattern, ["QUERY"], handler)</c> with an <c>[FromBody]</c>-bound parameter — so
/// they exercise the real ASP.NET Core behaviour rather than a stand-in.
/// </para>
/// </remarks>
public class HttpQueryRuntimeTests
{
    /// <summary>
    /// The motivating requirement: look up 1000 records by id. As a GET this is roughly 40 KB of
    /// query string against Kestrel's 8 KB default request-line limit, so it fails with 414 before
    /// reaching a handler. As a QUERY the ids travel in the body.
    /// </summary>
    [Fact]
    public async Task QueryEndpoint_Accepts1000GuidsInTheRequestBody()
    {
        var ids = Enumerable
            .Range(0, 1000)
            .Select(_ => Guid.NewGuid())
            .ToList();

        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        // Exactly the registration EndpointMapHelper.BuildSingleLineMapCall emits for a
        // non-standard verb, with the body binding the generated Parameters record uses.
        app.MapMethods(
            "/resources",
            ["QUERY"],
            ([FromBody] ResourceQuery request) => Results.Ok(request.Ids.Count));

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var address = new Uri(app.Urls.First());
            using var client = new HttpClient { BaseAddress = address };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Query, "/resources")
            {
                Content = JsonContent.Create(new ResourceQuery { Ids = ids }),
            };

            using var response = await client.SendAsync(requestMessage, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var echoedCount = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Equal("1000", echoedCount);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
            await app.DisposeAsync();
        }
    }

    /// <summary>
    /// The same 1000 ids as a GET query string, to demonstrate why QUERY is needed at all.
    /// Kestrel rejects the request line before any handler runs.
    /// </summary>
    [Fact]
    public async Task GetEndpoint_Rejects1000GuidsInTheQueryString()
    {
        var ids = Enumerable
            .Range(0, 1000)
            .Select(_ => Guid.NewGuid())
            .ToList();

        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        app.MapGet("/resources", () => Results.Ok("reached the handler"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var address = new Uri(app.Urls.First());
            using var client = new HttpClient { BaseAddress = address };

            var queryString = string.Join("&", ids.Select(id => $"id={id}"));
            var relativeUrl = $"/resources?{queryString}";

            // ~40 KB of request line against an 8 KB default limit.
            Assert.True(
                relativeUrl.Length > 8192,
                $"Expected the URL to exceed Kestrel's 8 KB request-line limit, but it was {relativeUrl.Length} bytes.");

            using var response = await client.GetAsync(new Uri(relativeUrl, UriKind.Relative), TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.RequestUriTooLong, response.StatusCode);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
            await app.DisposeAsync();
        }
    }

    /// <summary>
    /// Stands in for the generated request-body model — a component schema carrying the id set.
    /// </summary>
    internal sealed class ResourceQuery
    {
        public List<Guid> Ids { get; set; } = [];
    }
}