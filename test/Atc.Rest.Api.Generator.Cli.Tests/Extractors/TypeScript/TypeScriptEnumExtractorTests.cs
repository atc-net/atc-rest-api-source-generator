namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptEnumExtractorTests
{
    [Fact]
    public void Extract_NullDocument_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptEnumExtractor.Extract(openApiDoc: null, new TypeScriptClientConfig()));
    }

    [Fact]
    public void Extract_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptEnumExtractor.Extract(new OpenApiDocument(), config: null));
    }

    [Fact]
    public void Extract_NoComponents_ReturnsEmpty()
    {
        var result = TypeScriptEnumExtractor.Extract(new OpenApiDocument(), new TypeScriptClientConfig());

        Assert.Empty(result);
    }

    [Fact]
    public void Extract_StringEnum_UnionStyle_GeneratesStringUnion()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Status:
                                  type: string
                                  enum: [active, inactive, pending]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var config = new TypeScriptClientConfig { EnumStyle = TypeScriptEnumStyle.Union };

        var result = TypeScriptEnumExtractor.Extract(doc, config);

        var (name, content) = Assert.Single(result);
        Assert.Equal("Status", name);
        Assert.Contains("type Status", content, StringComparison.Ordinal);
        Assert.Contains("'active'", content, StringComparison.Ordinal);
        Assert.Contains("'inactive'", content, StringComparison.Ordinal);
        Assert.Contains("'pending'", content, StringComparison.Ordinal);
        Assert.DoesNotContain("enum Status", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// A value listed twice is one member; two values that differ in case only are two members, each with its value.
    /// </summary>
    [Theory]
    [InlineData(TypeScriptEnumStyle.Enum)]
    [InlineData(TypeScriptEnumStyle.Union)]
    public void Extract_StringEnum_DuplicateValueIsOneMember_CaseVariantsKeepTheirValues(
        TypeScriptEnumStyle style)
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                State:
                                  type: string
                                  enum: [active, Active, archived, archived]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptEnumExtractor.Extract(doc, new TypeScriptClientConfig { EnumStyle = style }));

        Assert.Equal(1, CountOf(content, "'archived'"));
        Assert.Equal(1, CountOf(content, "'active'"));
        Assert.Equal(1, CountOf(content, "'Active'"));
        Assert.DoesNotContain("Archived2", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_StringEnum_UnionStyle_IsOpen_SoAValueTheServerAddsLaterStillTypeChecks()
    {
        var doc = ParseYaml(StatusYaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptEnumExtractor.Extract(doc, new TypeScriptClientConfig()));

        Assert.Contains("export type Status = 'active' | 'inactive' | (string & {});", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_StringEnum_UnionStyle_ExtensibleEnumsFalse_IsClosed()
    {
        var doc = ParseYaml(StatusYaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptEnumExtractor.Extract(doc, new TypeScriptClientConfig { ExtensibleEnums = false }));

        Assert.Contains("export type Status = 'active' | 'inactive';", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_StringEnum_EnumStyle_GeneratesEnumDeclaration()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Color:
                                  type: string
                                  enum: [red, green, blue]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var config = new TypeScriptClientConfig { EnumStyle = TypeScriptEnumStyle.Enum };

        var result = TypeScriptEnumExtractor.Extract(doc, config);

        var (name, content) = Assert.Single(result);
        Assert.Equal("Color", name);
        Assert.Contains("enum Color", content, StringComparison.Ordinal);

        // Pascal-cased keys mapping to original lowercase string literals.
        Assert.Contains("Red", content, StringComparison.Ordinal);
        Assert.Contains("'red'", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_StringEnum_UnionStyle_WithRuntimeValues_AppendsConstArray()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Status:
                                  type: string
                                  enum: [active, inactive, pending]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var config = new TypeScriptClientConfig
        {
            EnumStyle = TypeScriptEnumStyle.Union,
            EnumRuntimeValues = true,
        };

        var result = TypeScriptEnumExtractor.Extract(doc, config);

        var (name, content) = Assert.Single(result);
        Assert.Equal("Status", name);

        // The type alias is still emitted unchanged ...
        Assert.Contains("export type Status = 'active' | 'inactive' | 'pending' | (string & {});", content, StringComparison.Ordinal);

        // ... plus the runtime const array beside it.
        Assert.Contains("export const StatusValues = ['active', 'inactive', 'pending'] as const;", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_StringEnum_UnionStyle_WithoutRuntimeValues_NoConstArray()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Status:
                                  type: string
                                  enum: [active, inactive]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var config = new TypeScriptClientConfig
        {
            EnumStyle = TypeScriptEnumStyle.Union,
            EnumRuntimeValues = false,
        };

        var result = TypeScriptEnumExtractor.Extract(doc, config);

        var (_, content) = Assert.Single(result);
        Assert.DoesNotContain("StatusValues", content, StringComparison.Ordinal);
        Assert.DoesNotContain("as const", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_EnumStyle_WithRuntimeValues_DoesNotAppendConstArray()
    {
        // EnumRuntimeValues only affects Union style; the Enum style already exposes
        // Object.values, so no const array is appended.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Color:
                                  type: string
                                  enum: [red, green, blue]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var config = new TypeScriptClientConfig
        {
            EnumStyle = TypeScriptEnumStyle.Enum,
            EnumRuntimeValues = true,
        };

        var result = TypeScriptEnumExtractor.Extract(doc, config);

        var (_, content) = Assert.Single(result);
        Assert.Contains("enum Color", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ColorValues", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_NonStringSchema_Skipped()
    {
        // Only string-typed enums are surfaced. Object / number schemas are ignored.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    id: { type: string }
                                NumericLevel:
                                  type: integer
                                  enum: [1, 2, 3]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptEnumExtractor.Extract(doc, new TypeScriptClientConfig());

        Assert.Empty(result);
    }

    [Fact]
    public void Extract_DeprecatedEnum_RespectsIncludeDeprecatedFlag()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                LegacyStatus:
                                  type: string
                                  deprecated: true
                                  enum: [old, older]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var defaultResult = TypeScriptEnumExtractor.Extract(doc, new TypeScriptClientConfig { IncludeDeprecated = false });
        Assert.Empty(defaultResult);

        var includedResult = TypeScriptEnumExtractor.Extract(doc, new TypeScriptClientConfig { IncludeDeprecated = true });
        Assert.Single(includedResult);
    }

    private const string StatusYaml = """
                                      openapi: 3.0.0
                                      info: { title: T, version: 1.0.0 }
                                      paths: {}
                                      components:
                                        schemas:
                                          Status:
                                            type: string
                                            enum: [active, inactive]
                                      """;

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;

    private static int CountOf(
        string text,
        string value)
        => text.Split(value).Length - 1;
}