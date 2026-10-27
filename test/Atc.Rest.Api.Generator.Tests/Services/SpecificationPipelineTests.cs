namespace Atc.Rest.Api.Generator.Tests.Services;

/// <summary>
/// The steps a specification goes through before code is generated from it, the same for the build and every CLI
/// command: parse, canonical media types, validation (a server's unbindable parameter an error), and - only for a
/// document without an error - status ranges and HEAD responses normalized.
/// </summary>
public sealed class SpecificationPipelineTests
{
    private const string Valid = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        paths:
          /pets:
            get:
              operationId: listPets
              parameters:
                - name: filter
                  in: query
                  schema: { type: object, properties: { name: { type: string } } }
              responses:
                '2XX':
                  description: OK
                  content:
                    Application/JSON:
                      schema: { type: string }
        """;

    [Fact]
    public void ParseAndValidate_NoOpenApi_HasNoDocumentAndTheParsersErrors()
    {
        var result = SpecificationPipeline.ParseAndValidate("pipeline-broken.yaml", "openapi: [", ValidateSpecificationStrategy.Standard);

        Assert.Null(result.Document);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Diagnostics);
        Assert.All(result.Diagnostics, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
    }

    [Fact]
    public void ParseAndValidate_AValidDocument_IsNormalized()
    {
        var result = SpecificationPipeline.ParseAndValidate("pipeline-valid.yaml", Valid, ValidateSpecificationStrategy.Standard);

        Assert.True(result.IsValid);
        var responses = result.Document!.Paths["/pets"].Operations![HttpMethod.Get].Responses!;
        var (status, response) = Assert.Single(responses);
        Assert.Equal("200", status);
        Assert.Equal("application/json", Assert.Single(response.Content!).Key);
    }

    [Fact]
    public void ParseAndValidate_AnUnbindableParameter_IsAWarningToAClientAndAnErrorToAServer()
    {
        var client = SpecificationPipeline.ParseAndValidate("pipeline-client.yaml", Valid, ValidateSpecificationStrategy.Standard);
        var server = SpecificationPipeline.ParseAndValidate("pipeline-server.yaml", Valid, ValidateSpecificationStrategy.Standard, parametersMustBind: true);

        Assert.Equal(DiagnosticSeverity.Warning, Assert.Single(client.Diagnostics, d => d.RuleId == RuleIdentifiers.ParameterValueNotBindable).Severity);
        Assert.Equal(DiagnosticSeverity.Error, Assert.Single(server.Diagnostics, d => d.RuleId == RuleIdentifiers.ParameterValueNotBindable).Severity);
        Assert.True(client.IsValid);
        Assert.False(server.IsValid);

        // A document with an error is not normalized: nothing is generated from it
        Assert.Contains("2XX", server.Document!.Paths["/pets"].Operations![HttpMethod.Get].Responses!.Keys, StringComparer.Ordinal);
    }
}