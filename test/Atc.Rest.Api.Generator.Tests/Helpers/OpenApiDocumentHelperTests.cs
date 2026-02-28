namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class OpenApiDocumentHelperTests
{
    private const string MinimalValidYaml = """
        openapi: 3.1.0
        info:
          title: Test API
          version: 1.0.0
        paths: {}
        """;

    private const string YamlWithPet = """
        openapi: 3.1.0
        info:
          title: Pet API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: listPets
              responses:
                '200':
                  description: A list of pets
        """;

    // ========== ParseYaml Tests ==========
    [Fact]
    public void ParseYaml_ValidYaml_ReturnsDocument()
    {
        var document = OpenApiDocumentHelper.ParseYaml(MinimalValidYaml);

        Assert.NotNull(document);
        Assert.Equal("Test API", document.Info?.Title);
    }

    [Fact]
    public void ParseYaml_NullContent_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => OpenApiDocumentHelper.ParseYaml(null!));
    }

    // ========== TryParseYaml Tests ==========
    [Fact]
    public void TryParseYaml_ValidYaml_ReturnsTrueAndDocument()
    {
        var success = OpenApiDocumentHelper.TryParseYaml(MinimalValidYaml, "test.yaml", out var document);

        Assert.True(success);
        Assert.NotNull(document);
        Assert.Equal("Test API", document!.Info?.Title);
    }

    [Fact]
    public void TryParseYaml_NullContent_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            OpenApiDocumentHelper.TryParseYaml(null!, "test.yaml", out _));
    }

    [Fact]
    public void TryParseYaml_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            OpenApiDocumentHelper.TryParseYaml(MinimalValidYaml, null!, out _));
    }

    // ========== TryParseYamlWithDiagnostic Tests ==========
    [Fact]
    public void TryParseYamlWithDiagnostic_ValidYaml_ReturnsDocumentAndDiagnostic()
    {
        var (document, diagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(
            MinimalValidYaml,
            "test.yaml");

        Assert.NotNull(document);
        Assert.NotNull(diagnostic);
        Assert.Equal("Test API", document!.Info?.Title);
        Assert.Equal("1.0.0", document.Info?.Version);
    }

    [Fact]
    public void TryParseYamlWithDiagnostic_NullContent_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            OpenApiDocumentHelper.TryParseYamlWithDiagnostic(null!, "test.yaml"));
    }

    [Fact]
    public void TryParseYamlWithDiagnostic_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            OpenApiDocumentHelper.TryParseYamlWithDiagnostic(MinimalValidYaml, null!));
    }

    [Fact]
    public void TryParseYamlWithDiagnostic_YamlWithPaths_ParsesOperations()
    {
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(
            YamlWithPet,
            "pet.yaml");

        Assert.NotNull(document);
        Assert.NotNull(document!.Paths);
        Assert.True(document.Paths.ContainsKey("/pets"));
    }

    [Fact]
    public void TryParseYamlWithDiagnostic_WindowsPath_HandlesBackslashes()
    {
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(
            MinimalValidYaml,
            @"C:\Projects\MyApi\spec.yaml");

        Assert.NotNull(document);
    }

    // ========== Caching Behavior Tests ==========
    [Fact]
    public void TryParseYamlWithDiagnostic_SameContent_ReturnsCachedResult()
    {
        // Use unique content so this test is isolated from other tests sharing the static cache
        var uniqueYaml = """
            openapi: 3.1.0
            info:
              title: Cache Test Same Content
              version: 1.0.0
            paths: {}
            """;

        var (doc1, diag1) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(uniqueYaml, "first.yaml");
        var (doc2, diag2) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(uniqueYaml, "second.yaml");

        // Same content returns the same cached object references
        Assert.Same(doc1, doc2);
        Assert.Same(diag1, diag2);
    }

    [Fact]
    public void TryParseYamlWithDiagnostic_DifferentContent_ReturnsDifferentResults()
    {
        var yaml1 = """
            openapi: 3.1.0
            info:
              title: Cache Test API One
              version: 1.0.0
            paths: {}
            """;

        var yaml2 = """
            openapi: 3.1.0
            info:
              title: Cache Test API Two
              version: 2.0.0
            paths: {}
            """;

        var (doc1, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yaml1, "one.yaml");
        var (doc2, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yaml2, "two.yaml");

        Assert.NotSame(doc1, doc2);
        Assert.Equal("Cache Test API One", doc1!.Info?.Title);
        Assert.Equal("Cache Test API Two", doc2!.Info?.Title);
    }

    [Fact]
    public void TryParseYamlWithDiagnostic_SameContentDifferentPaths_ReturnsCachedResult()
    {
        // The cache is keyed by content, not by path.
        // Different paths with the same content should return the cached result.
        var uniqueYaml = """
            openapi: 3.1.0
            info:
              title: Cache Test Path Invariant
              version: 1.0.0
            paths: {}
            """;

        var (doc1, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(uniqueYaml, "path/a.yaml");
        var (doc2, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(uniqueYaml, "path/b.yaml");

        Assert.Same(doc1, doc2);
    }

    [Fact]
    public void ParseYaml_AlsoUsesCacheFromTryParseYamlWithDiagnostic()
    {
        // ParseYaml delegates to TryParseYamlWithDiagnostic, so the cache applies
        var uniqueYaml = """
            openapi: 3.1.0
            info:
              title: Cache Test ParseYaml Delegation
              version: 1.0.0
            paths: {}
            """;

        var (docFromTry, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(uniqueYaml, "first.yaml");
        var docFromParse = OpenApiDocumentHelper.ParseYaml(uniqueYaml);

        Assert.Same(docFromTry, docFromParse);
    }

    [Fact]
    public void TryParseYaml_AlsoUsesCacheFromTryParseYamlWithDiagnostic()
    {
        // TryParseYaml delegates to TryParseYamlWithDiagnostic, so the cache applies
        var uniqueYaml = """
            openapi: 3.1.0
            info:
              title: Cache Test TryParseYaml Delegation
              version: 1.0.0
            paths: {}
            """;

        var (docFromDiag, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(uniqueYaml, "first.yaml");
        OpenApiDocumentHelper.TryParseYaml(uniqueYaml, "second.yaml", out var docFromTry);

        Assert.Same(docFromDiag, docFromTry);
    }

    // ========== ClearCache Tests ==========
    [Fact]
    public void ClearCache_InvalidatesCachedEntries()
    {
        var uniqueYaml = """
            openapi: 3.1.0
            info:
              title: Cache Test ClearCache
              version: 1.0.0
            paths: {}
            """;

        var (doc1, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(uniqueYaml, "test.yaml");

        OpenApiDocumentHelper.ClearCache();

        var (doc2, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(uniqueYaml, "test.yaml");

        // After clearing, a new parse should produce a different object instance
        Assert.NotSame(doc1, doc2);
    }
}