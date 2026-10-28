namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The host's own middleware, added through <c>ApiMiddlewareOptions.AfterErrorHandling</c>, runs inside the error
/// handling <c>Use{Api}Api()</c> adds: what it throws is a ProblemDetails 500 and what it answers empty gets a
/// ProblemDetails body - on the package's host and on the plain host alike.
/// </summary>
public partial class ErrorResponseBodyRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostMiddleware_AfterErrorHandling_RunsInsideTheErrorHandling(
        bool plainHost)
    {
        await using var host = await ErrorBodyHost.StartAsync(plainHost: plainHost, hostMiddleware: true);
        using var http = new HttpClient { BaseAddress = host.BaseAddress };

        foreach (var (path, status) in new[] { ("host-middleware/throws", 500), ("host-middleware/forbids", 403) })
        {
            using var response = await http.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var label = $"{(plainHost ? "plain host" : "Atc.Rest.MinimalApi host")}, {path}: {(int)response.StatusCode} {response.Content.Headers.ContentType}\n{body}";

            Assert.Equal(status, (int)response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.True(TryReadStatus(body, out var problemStatus) && problemStatus == status, label);
            Assert.DoesNotContain("boom from host middleware", body, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Until 3.1 <c>Map{Api}Api()</c> also added the middleware: a host that still calls only it must not start
    /// without its error handling - it fails at startup and names the call it misses.
    /// </summary>
    [Fact]
    public async Task MapApi_WithoutUseApi_FailsAtStartupAndNamesTheMissingCall()
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => ErrorBodyHost.StartAsync(callUse: false));

        var invalidOperation = Assert.IsType<InvalidOperationException>(exception is TargetInvocationException { InnerException: { } inner } ? inner : exception);
        Assert.Contains("app.UseErrorBodiesApi()", invalidOperation.Message, StringComparison.Ordinal);
        Assert.Contains("requireUse: false", invalidOperation.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A host that orders its whole pipeline itself says so with <c>requireUse: false</c>: it starts without
    /// <c>Use{Api}Api()</c>, and the endpoints are mapped.
    /// </summary>
    [Fact]
    public async Task MapApi_WithoutUseApi_StartsWhenTheHostOrdersItsOwnPipeline()
    {
        await using var host = await ErrorBodyHost.StartAsync(callUse: false, requireUse: false);
        using var http = new HttpClient { BaseAddress = host.BaseAddress };

        using var response = await http.GetAsync(new Uri("faults/throw", UriKind.Relative), TestContext.Current.CancellationToken);

        // Mapped (not a 404), and with no error handler of the generator's in front of it
        Assert.Equal(500, (int)response.StatusCode);
    }
}