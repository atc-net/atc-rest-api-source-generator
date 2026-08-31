namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Tests that an explicit <c>clientName</c> is honoured verbatim by the client extractor, and that
/// the default derivation still applies when it is absent.
/// </summary>
/// <remarks>
/// The extractor produces the class name while <c>ApiClientGenerator</c> independently produces the
/// file name. Both must apply the same precedence, otherwise a class and its containing file
/// diverge. These tests pin the extractor half of that contract.
/// </remarks>
public class HttpClientExtractorClientNameTests
{
    private const string Yaml = """
                                openapi: 3.0.0
                                info:
                                  title: Test API
                                  version: 1.0.0
                                paths:
                                  /devices:
                                    get:
                                      operationId: getDevices
                                      responses:
                                        '200':
                                          description: OK
                                """;

    /// <summary>
    /// An explicit name is the author stating the full type name, so no suffix is appended.
    /// </summary>
    [Theory]
    [InlineData("MyApiClient", "MyApiClient")]
    [InlineData("ShowcaseGateway", "ShowcaseGateway")]
    [InlineData("  PaddedClient  ", "PaddedClient")]
    public void Extract_WithExplicitClientName_UsesItVerbatim(
        string clientName,
        string expected)
    {
        // Arrange
        var document = ParseYaml(Yaml);
        Assert.NotNull(document);

        // Act
        var (clientClass, _) = HttpClientExtractor.ExtractWithInlineSchemas(
            document,
            projectName: "Eloverblik.ThirdPartyApi.Client",
            pathSegment: null,
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false,
            useServersBasePath: true,
            hasSegmentModels: null,
            hasSharedModels: null,
            namespaceSegment: "",
            clientSuffix: "Client",
            clientName: clientName);

        // Assert
        Assert.NotNull(clientClass);
        Assert.Equal(expected, clientClass.ClassTypeName);
    }

    /// <summary>
    /// A blank clientName must not win over the derived name, otherwise an empty marker entry would
    /// produce an unnamed type.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Extract_WithoutClientName_FallsBackToDerivedName(
        string? clientName)
    {
        // Arrange
        var document = ParseYaml(Yaml);
        Assert.NotNull(document);

        // Act
        var (clientClass, _) = HttpClientExtractor.ExtractWithInlineSchemas(
            document,
            projectName: "Eloverblik.ThirdPartyApi.Client",
            pathSegment: null,
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false,
            useServersBasePath: true,
            hasSegmentModels: null,
            hasSharedModels: null,
            namespaceSegment: "",
            clientSuffix: "Client",
            clientName: clientName);

        // Assert - the §3.2 algorithm drops the trailing "Client" segment.
        Assert.NotNull(clientClass);
        Assert.Equal("ThirdPartyApiClient", clientClass.ClassTypeName);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}