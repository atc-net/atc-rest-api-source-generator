namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The server's JSON writes go through the <c>JsonTypeInfo</c> the app's JSON options resolve, never the
/// reflection-based overloads: an error model, a JSON string error, a body under another JSON media type and the
/// sequential streams (NDJSON, JSON Text Sequences, multipart/mixed) build without trim or AOT warnings in a
/// Native AOT project.
/// </summary>
public sealed class ServerJsonAotTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /things:
            get:
              operationId: getThing
              responses:
                '200':
                  description: OK
                  content:
                    application/vnd.contoso.thing+json:
                      schema:
                        $ref: '#/components/schemas/Thing'
                '404':
                  description: Not found
                  content:
                    application/json:
                      schema:
                        type: string
                default:
                  description: Unexpected error
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Error'
          /lines:
            get:
              operationId: streamLines
              x-return-async-enumerable: true
              responses:
                '200':
                  description: OK
                  content:
                    application/x-ndjson:
                      schema:
                        $ref: '#/components/schemas/Thing'
          /sequence:
            get:
              operationId: streamSequence
              x-return-async-enumerable: true
              responses:
                '200':
                  description: OK
                  content:
                    application/json-seq:
                      schema:
                        $ref: '#/components/schemas/Thing'
          /parts:
            get:
              operationId: streamParts
              x-return-async-enumerable: true
              responses:
                '200':
                  description: OK
                  content:
                    multipart/mixed:
                      schema:
                        $ref: '#/components/schemas/Thing'
        components:
          schemas:
            Thing:
              type: object
              properties:
                n: { type: integer }
            Error:
              type: object
              properties:
                message: { type: string }
        """;

    [Fact]
    public async Task ServerJsonWrites_HaveNoTrimOrAotWarnings()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);

        var problems = await AotAnalyzerHarness.AnalyzeAsync(sources, includeMinimalApi: true);

        var ours = problems.Where(p => p.Contains("ApiServerGenerator", StringComparison.Ordinal)).ToList();
        Assert.True(ours.Count == 0, string.Join(Environment.NewLine, ours));
    }
}