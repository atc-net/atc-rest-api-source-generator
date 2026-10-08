namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The endpoint-per-operation client builds without trim or AOT warnings in a Native AOT project: an error model
/// read leniently from the raw body (the typed accessor of a body Atc.Rest.Client could not read as it) goes through
/// the reflection-based serializer only where reflection-based serialization is enabled.
/// </summary>
public sealed class ClientOperationAotTests
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
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Thing'
                '404':
                  description: Not found
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Error'
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
    public async Task EndpointPerOperationClient_HasNoTrimOrAotWarnings()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, Yaml);

        var problems = await AotAnalyzerHarness.AnalyzeAsync(sources, includeMinimalApi: false);

        var ours = problems.Where(p => p.Contains("ApiClientGenerator", StringComparison.Ordinal)).ToList();
        Assert.True(ours.Count == 0, string.Join(Environment.NewLine, ours));
    }
}