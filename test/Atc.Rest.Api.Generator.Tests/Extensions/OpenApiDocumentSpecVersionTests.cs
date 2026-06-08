namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class OpenApiDocumentSpecVersionTests
{
    private static string MinimalYaml(
        string openApiVersion,
        string title)
        => $$"""
            openapi: {{openApiVersion}}
            info:
              title: {{title}}
              version: 1.0.0
            paths: {}
            """;

    [Fact]
    public void GetOpenApiSpecVersion_Parsed32Document_ReturnsOpenApi3_2()
    {
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(
            MinimalYaml("3.2.0", "Spec Version 3.2"),
            "specversion-32.yaml");

        Assert.NotNull(document);
        Assert.Equal(OpenApiSpecVersion.OpenApi3_2, document!.GetOpenApiSpecVersion());
    }

    [Fact]
    public void GetOpenApiSpecVersion_Parsed31Document_ReturnsOpenApi3_1()
    {
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(
            MinimalYaml("3.1.0", "Spec Version 3.1"),
            "specversion-31.yaml");

        Assert.NotNull(document);
        Assert.Equal(OpenApiSpecVersion.OpenApi3_1, document!.GetOpenApiSpecVersion());
    }

    [Fact]
    public void GetOpenApiSpecVersion_Parsed30Document_ReturnsOpenApi3_0()
    {
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(
            MinimalYaml("3.0.0", "Spec Version 3.0"),
            "specversion-30.yaml");

        Assert.NotNull(document);
        Assert.Equal(OpenApiSpecVersion.OpenApi3_0, document!.GetOpenApiSpecVersion());
    }

    [Fact]
    public void GetOpenApiSpecVersion_DocumentWithoutMetadata_ReturnsNull()
    {
        var document = new OpenApiDocument();

        Assert.Null(document.GetOpenApiSpecVersion());
    }
}