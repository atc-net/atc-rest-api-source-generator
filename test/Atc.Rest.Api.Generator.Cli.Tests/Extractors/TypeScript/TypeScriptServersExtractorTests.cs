namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptServersExtractorTests
{
    [Fact]
    public void Generate_MultipleServers_EmitsConstWithDescriptionDerivedKeys()
    {
        // Server descriptions camelCase into TS-safe keys. The full URL (with variables
        // resolved) lands as the string value. A `ServerName` type alias surfaces the
        // keys so consumers can constrain config to a known server.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: T, version: '1' }
                            servers:
                              - url: https://api.example.com/v1
                                description: Production
                              - url: https://staging.example.com/v1
                                description: Staging
                              - url: http://localhost:3000/v1
                                description: Local development
                            paths: {}
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var content = TypeScriptServersExtractor.Generate(doc!, headerContent: null);

        Assert.NotNull(content);
        Assert.Contains("export const Servers = {", content, StringComparison.Ordinal);
        Assert.Contains("production: 'https://api.example.com/v1'", content, StringComparison.Ordinal);
        Assert.Contains("staging: 'https://staging.example.com/v1'", content, StringComparison.Ordinal);
        Assert.Contains("localDevelopment: 'http://localhost:3000/v1'", content, StringComparison.Ordinal);
        Assert.Contains("} as const;", content, StringComparison.Ordinal);
        Assert.Contains("export type ServerName = keyof typeof Servers;", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_SingleServer_ReturnsNull()
    {
        // Single-server specs keep the existing single-baseUrl ctor-arg pattern; emitting
        // a Servers const with one entry would add noise without giving a real choice.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: T, version: '1' }
                            servers:
                              - url: https://api.example.com/v1
                                description: Production
                            paths: {}
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var content = TypeScriptServersExtractor.Generate(doc!, headerContent: null);

        Assert.Null(content);
    }

    [Fact]
    public void Generate_NoServers_ReturnsNull()
    {
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: T, version: '1' }
                            paths: {}
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var content = TypeScriptServersExtractor.Generate(doc!, headerContent: null);

        Assert.Null(content);
    }

    [Fact]
    public void Generate_DescriptionsMissing_FallBackToIndexedKeys()
    {
        // Servers with no description must still get a valid TS identifier as their key.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: T, version: '1' }
                            servers:
                              - url: https://api1.example.com
                              - url: https://api2.example.com
                            paths: {}
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var content = TypeScriptServersExtractor.Generate(doc!, headerContent: null);

        Assert.NotNull(content);
        Assert.Contains("server1: 'https://api1.example.com'", content, StringComparison.Ordinal);
        Assert.Contains("server2: 'https://api2.example.com'", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_DuplicateDescriptions_DisambiguateKeysWithNumericSuffix()
    {
        // Two servers with the same description must not collide on the resulting key —
        // the second occurrence picks up a numeric suffix.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: T, version: '1' }
                            servers:
                              - url: https://eu.example.com
                                description: Production
                              - url: https://us.example.com
                                description: Production
                            paths: {}
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var content = TypeScriptServersExtractor.Generate(doc!, headerContent: null);

        Assert.NotNull(content);
        Assert.Contains("production: 'https://eu.example.com'", content, StringComparison.Ordinal);
        Assert.Contains("production2: 'https://us.example.com'", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ServerWithVariables_ResolvesToDefaultValues()
    {
        // Server URLs may contain {variable} placeholders; the resolved default is what
        // the consumer cares about.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: T, version: '1' }
                            servers:
                              - url: https://{region}.example.com/{version}
                                description: Primary
                                variables:
                                  region:
                                    default: eu
                                  version:
                                    default: v1
                              - url: https://staging.example.com/v1
                                description: Staging
                            paths: {}
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var content = TypeScriptServersExtractor.Generate(doc!, headerContent: null);

        Assert.NotNull(content);
        Assert.Contains("primary: 'https://eu.example.com/v1'", content, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}