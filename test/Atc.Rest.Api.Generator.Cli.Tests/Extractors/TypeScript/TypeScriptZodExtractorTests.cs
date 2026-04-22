namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptZodExtractorTests
{
    // ============ TypeScriptZodEnumExtractor ============
    [Fact]
    public void ZodEnum_NullDocument_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptZodEnumExtractor.Extract(openApiDoc: null!, new TypeScriptClientConfig()));
    }

    [Fact]
    public void ZodEnum_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptZodEnumExtractor.Extract(new OpenApiDocument(), config: null!));
    }

    [Fact]
    public void ZodEnum_EmptyDocument_ReturnsEmpty()
    {
        var result = TypeScriptZodEnumExtractor.Extract(new OpenApiDocument(), new TypeScriptClientConfig());

        Assert.Empty(result);
    }

    [Fact]
    public void ZodEnum_StringEnum_GeneratesZodEnumSchema()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Status:
                                  type: string
                                  enum: [active, inactive]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptZodEnumExtractor.Extract(doc!, new TypeScriptClientConfig());
        var (name, content) = Assert.Single(result);

        Assert.Equal("Status", name);
        Assert.Contains("z.enum(", content, StringComparison.Ordinal);
        Assert.Contains("'active'", content, StringComparison.Ordinal);
        Assert.Contains("'inactive'", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ZodEnum_NonStringEnum_Skipped()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Level:
                                  type: integer
                                  enum: [1, 2, 3]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptZodEnumExtractor.Extract(doc!, new TypeScriptClientConfig());

        Assert.Empty(result);
    }

    // ============ TypeScriptZodModelExtractor ============
    [Fact]
    public void ZodModel_NullDocument_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptZodModelExtractor.Extract(openApiDoc: null!, new TypeScriptClientConfig()));
    }

    [Fact]
    public void ZodModel_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptZodModelExtractor.Extract(new OpenApiDocument(), config: null!));
    }

    [Fact]
    public void ZodModel_EmptyDocument_ReturnsEmpty()
    {
        var result = TypeScriptZodModelExtractor.Extract(new OpenApiDocument(), new TypeScriptClientConfig());

        Assert.Empty(result);
    }

    [Fact]
    public void ZodModel_ObjectSchema_GeneratesZodObjectSchema()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                                    name:
                                      type: string
                                  required: [id]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptZodModelExtractor.Extract(doc!, new TypeScriptClientConfig());
        var (name, content) = Assert.Single(result);

        Assert.Equal("Pet", name);
        Assert.Contains("z.object", content, StringComparison.Ordinal);
        Assert.Contains("id:", content, StringComparison.Ordinal);
        Assert.Contains("name:", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ZodModel_DeprecatedSchema_RespectsIncludeFlag()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                LegacyAccount:
                                  type: object
                                  deprecated: true
                                  properties:
                                    id: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var defaultResult = TypeScriptZodModelExtractor.Extract(doc!, new TypeScriptClientConfig { IncludeDeprecated = false });
        Assert.Empty(defaultResult);

        var includedResult = TypeScriptZodModelExtractor.Extract(doc!, new TypeScriptClientConfig { IncludeDeprecated = true });
        Assert.Single(includedResult);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}