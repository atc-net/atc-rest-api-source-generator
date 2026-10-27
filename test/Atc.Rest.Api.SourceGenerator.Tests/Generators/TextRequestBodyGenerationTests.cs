namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A request body that is plain text (<c>text/plain</c>, a string): the server and both C# clients compile, and the
/// EndpointPerOperation client sends the text it is given.
/// </summary>
public sealed class TextRequestBodyGenerationTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Notes
          version: 1.0.0
        paths:
          /notes:
            post:
              operationId: createNote
              requestBody:
                required: true
                content:
                  text/plain:
                    schema:
                      type: string
              responses:
                '204':
                  description: Created
        """;

    [Fact]
    public void Server_ATextBody_Compiles()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);

        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true);
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    [Theory]
    [InlineData("TypedClient")]
    [InlineData("EndpointPerOperation")]
    public void Client_ATextBody_Compiles(string generationMode)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", $$"""{ "generationMode": "{{generationMode}}" }""", includeMinimalApi: false, Yaml);

        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources);
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }
}