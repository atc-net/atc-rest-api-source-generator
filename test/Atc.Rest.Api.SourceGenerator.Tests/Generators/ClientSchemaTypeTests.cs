namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The typed and the per-operation client read a body schema as the same C# type (one resolver, their collection type
/// apart). A <c>prefixItems</c> tuple behind a <c>$ref</c> is its own type in both - the per-operation client read it as
/// a collection of objects. An error body's inline array items get their own record (<c>{Status}ErrorItem</c>), not the
/// success body's item record of the same operation.
/// </summary>
public sealed class ClientSchemaTypeTests
{
    private const string Yaml = """
        openapi: 3.1.0
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /position:
            get:
              operationId: getPosition
              responses:
                '200':
                  description: A coordinate
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Coordinate'
          /readings:
            get:
              operationId: listReadings
              responses:
                '200':
                  description: The readings
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          type: object
                          properties:
                            value:
                              type: number
                '400':
                  description: The rejected filters
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          type: object
                          properties:
                            filter:
                              type: string
                            reason:
                              type: string
        components:
          schemas:
            Coordinate:
              type: array
              prefixItems:
                - type: number
                - type: number
        """;

    [Theory]
    [InlineData("EndpointPerOperation")]
    [InlineData("TypedClient")]
    public void ATuple_IsItsOwnType_AndAnErrorItem_IsItsOwnRecord(string mode)
    {
        var marker = mode == "EndpointPerOperation"
            ? ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails")
            : """{ "generationMode": "TypedClient", "typedClientResultStyle": "Result" }""";

        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        var all = string.Join("\n", sources.Select(s => s.Source));

        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: false));
        Assert.DoesNotContain("IEnumerable<object>", all, StringComparison.Ordinal);
        Assert.DoesNotContain("List<object>", all, StringComparison.Ordinal);
        Assert.Contains("Coordinate", all, StringComparison.Ordinal);
        Assert.Contains("record ListReadingsResponseItem(", all, StringComparison.Ordinal);
        if (mode == "EndpointPerOperation")
        {
            Assert.Contains("ListReadingsBadRequestErrorItem", all, StringComparison.Ordinal);
        }
    }
}