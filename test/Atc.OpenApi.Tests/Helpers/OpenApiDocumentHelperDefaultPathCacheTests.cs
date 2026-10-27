namespace Atc.OpenApi.Tests.Helpers;

/// <summary>
/// <see cref="OpenApiDocumentHelper.ParseYaml"/> parses under the default path <c>test.yaml</c>, which the parse cache
/// keeps one slot for - and many tests parse other YAML under it. Run beside them, one of them could take the slot
/// between this test's two calls, so it runs alone.
/// </summary>
[Collection(nameof(OpenApiDocumentHelperDefaultPathCacheTests))]
[CollectionDefinition(nameof(OpenApiDocumentHelperDefaultPathCacheTests), DisableParallelization = true)]
public class OpenApiDocumentHelperDefaultPathCacheTests
{
    [Fact]
    public void ParseYaml_AlsoUsesCacheFromTryParseYamlWithDiagnostic()
    {
        // Arrange
        // ParseYaml delegates to TryParseYamlWithDiagnostic, so the cache applies
        const string uniqueYaml = """
            openapi: 3.1.0
            info:
              title: Cache Test ParseYaml Delegation
              version: 1.0.0
            paths: {}
            """;

        // Act
        // ParseYaml uses "test.yaml" as the default path
        var (docFromTry, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(uniqueYaml, "test.yaml");
        var docFromParse = OpenApiDocumentHelper.ParseYaml(uniqueYaml);

        // Assert
        Assert.Same(docFromTry, docFromParse);
    }
}