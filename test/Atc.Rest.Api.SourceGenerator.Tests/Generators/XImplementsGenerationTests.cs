namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// <c>x-implements</c> adds interfaces to a generated record - also a generic one, such as
/// <c>IEquatable&lt;Address&gt;</c>, and also on a model with no properties: the interfaces follow the record's
/// parameter list, the model is generated into its own file, and the server and both C# clients compile.
/// </summary>
public sealed class XImplementsGenerationTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Implements
          version: 1.0.0
        paths:
          /addresses:
            get:
              operationId: listAddresses
              responses:
                '200':
                  description: The addresses
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Address'
        components:
          schemas:
            Address:
              type: object
              x-implements:
                - IEquatable<Address>
              properties:
                street:
                  type: string
                marker:
                  $ref: '#/components/schemas/Marker'
            Marker:
              type: object
              x-implements: IEquatable<Marker>
        """;

    [Fact]
    public void Server_AGenericInterface_IsImplementedByTheRecord()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);

        AssertImplemented(sources);
        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true);
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    [Theory]
    [InlineData("TypedClient")]
    [InlineData("EndpointPerOperation")]
    public void Client_AGenericInterface_IsImplementedByTheRecord(
        string generationMode)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", $$"""{ "generationMode": "{{generationMode}}" }""", includeMinimalApi: false, Yaml);

        AssertImplemented(sources);
        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources);
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    private static void AssertImplemented(
        List<(string HintName, string Source)> sources)
    {
        var address = sources.Single(s => s.HintName.EndsWith(".Models.Address.g.cs", StringComparison.Ordinal)).Source;
        Assert.Contains("record Address(", address, StringComparison.Ordinal);
        Assert.Contains(") : IEquatable<Address>;", address, StringComparison.Ordinal);

        var marker = sources.Single(s => s.HintName.EndsWith(".Models.Marker.g.cs", StringComparison.Ordinal)).Source;
        Assert.Contains("record Marker() : IEquatable<Marker>;", marker, StringComparison.Ordinal);
    }
}