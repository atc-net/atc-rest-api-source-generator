namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A body the generated reader cannot read - malformed XML, a value the specification does not list, malformed JSON
/// next to XML - is a 400 problem on every host. On the <c>MapEndpoints()</c> host the reader's
/// <c>BadHttpRequestException</c> used to reach <c>UseExceptionHandler()</c>, which answered 500; the endpoint's
/// <c>WithRequestBodyProblems()</c> answers it first.
/// </summary>
[Collection(nameof(RequestBodyProblemsTests))]
[CollectionDefinition(nameof(RequestBodyProblemsTests), DisableParallelization = true)]
public sealed class RequestBodyProblemsTests
{
    [Theory]
    [InlineData(true, "/xml", "application/xml", "<Echo><camel>bogus</camel></Echo>")]
    [InlineData(true, "/xml", "application/xml", "<Echo><camel>")]
    [InlineData(true, "/xml", "application/xml", "<Echo><twin>ACTIVE</twin></Echo>")]
    [InlineData(false, "/xml", "application/xml", "<Echo><camel>bogus</camel></Echo>")]
    [InlineData(false, "/xml", "application/xml", "<Echo><camel>")]
    public async Task AnUnreadableBody_IsA400Problem_OnEveryHost(
        bool plainHost,
        string path,
        string contentType,
        string body)
    {
        await using var host = await XmlTestHost.StartAsync(
            ErrorResponseBodyRuntimeTests.EnumCasingYaml(),
            ErrorResponseBodyRuntimeTests.EnumCasingHandlers(),
            "ErrorBodies.RuntimeTest.EnumCasingRegistration",
            plainHost: plainHost);

        var (status, mediaType, response) = await host.SendRawAsync(HttpMethod.Post, path, accept: "application/json", contentType: contentType, body: body);

        Assert.True(status == 400, $"{status} {mediaType}: {response}");
        Assert.StartsWith("application/problem+json", mediaType, StringComparison.Ordinal);
        Assert.Contains("The XML request body is invalid", response, StringComparison.Ordinal);
    }
}