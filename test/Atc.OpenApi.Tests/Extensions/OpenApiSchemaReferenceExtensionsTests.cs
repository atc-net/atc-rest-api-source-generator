namespace Atc.OpenApi.Tests.Extensions;

public sealed class OpenApiSchemaReferenceExtensionsTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Refs
          version: 1.0.0
        paths: {}
        components:
          schemas:
            Pet:
              type: object
              properties:
                name: { type: string }
        """;

    [Fact]
    public void ComponentName_And_Dereferenced_OfAReference()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var reference = new OpenApiSchemaReference("Pet", document);
        IOpenApiSchema inline = new OpenApiSchema { Type = JsonSchemaType.String };

        Assert.Equal("Pet", reference.ComponentName);
        Assert.Same(document.Components!.Schemas!["Pet"], ((IOpenApiSchema)reference).Dereferenced);
        Assert.Same(inline, inline.Dereferenced);
        Assert.Null(((IOpenApiSchema?)null).Dereferenced);
    }
}