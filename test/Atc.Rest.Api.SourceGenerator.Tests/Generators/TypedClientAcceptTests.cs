namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The TypedClient reads a JSON answer: when the operation offers its answer as text or XML too (a value it does not
/// read), it asks for JSON, so a server that negotiates does not answer with the first media type it declares.
/// </summary>
public sealed class TypedClientAcceptTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Health
          version: 1.0.0
        paths:
          /alive:
            get:
              operationId: isAlive
              responses:
                '200':
                  description: Whether the service is up
                  content:
                    text/plain:
                      schema: { type: boolean }
                    application/json:
                      schema: { type: boolean }
                    application/xml:
                      schema: { type: boolean }
          /ready:
            get:
              operationId: isReady
              responses:
                '200':
                  description: Whether the service is ready
                  content:
                    application/json:
                      schema: { type: boolean }
        """;

    [Fact]
    public void AJsonAnswerOfferedAsTextOrXmlToo_AsksForJson()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "TypedClient" }""", includeMinimalApi: false, Yaml);
        var client = string.Join("\n", sources.Where(s => s.HintName.EndsWith("Client.g.cs", StringComparison.Ordinal)).Select(s => s.Source));

        var isAlive = client[client.IndexOf("IsAliveAsync(", StringComparison.Ordinal)..client.IndexOf("IsReadyAsync(", StringComparison.Ordinal)];
        Assert.Contains("requestMessage.Headers.Accept.ParseAdd(\"application/json, application/problem+json\");", isAlive, StringComparison.Ordinal);

        // JSON alone: no Accept is needed
        var isReady = client[client.IndexOf("IsReadyAsync(", StringComparison.Ordinal)..];
        Assert.DoesNotContain("Headers.Accept", isReady, StringComparison.Ordinal);
        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources));
    }
}