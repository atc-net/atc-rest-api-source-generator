namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class PathFilterHelperTests
{
    private const string PathsYaml = """
        openapi: 3.0.0
        info: { title: T, version: 1.0.0 }
        paths:
          /pets:
            get:
              operationId: listPets
              responses:
                '200': { description: OK }
          /pets/{id}:
            get:
              operationId: getPet
              parameters:
                - { name: id, in: path, required: true, schema: { type: string } }
              responses:
                '200': { description: OK }
          /pets/internal:
            get:
              operationId: getInternal
              responses:
                '200': { description: OK }
          /users:
            get:
              operationId: listUsers
              responses:
                '200': { description: OK }
        components:
          schemas:
            Pet:
              type: object
              properties:
                name: { type: string }
        """;

    [Theory]
    [InlineData("/pets", "/pets", true)]
    [InlineData("/pets", "/users", false)]
    [InlineData("/pets/{id}", "/pets/*", true)]
    [InlineData("/pets/{id}/photos", "/pets/**", true)]
    [InlineData("/pets/{id}/photos/{photoId}", "/pets/**", true)]
    [InlineData("/users/{id}", "/pets/**", false)]
    [InlineData("/api/v1/pets", "/api/*/pets", true)]
    [InlineData("/api/v1/pets/{id}", "/api/**/pets/**", true)]
    [InlineData("/PETS", "/pets", true)] // case-insensitive
    public void MatchesPattern_ReturnsExpectedResult(
        string path,
        string pattern,
        bool expected)
    {
        var result = PathFilterHelper.MatchesPattern(path, pattern);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FilterPaths_NullIncludeAndExclude_ReturnsAllPaths()
    {
        var paths = new[] { "/pets", "/users", "/orders" };

        var result = PathFilterHelper.FilterPaths(paths, null, null);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void FilterPaths_IncludeOnly_ReturnsMatchingPaths()
    {
        var paths = new[] { "/pets", "/pets/{id}", "/users", "/orders" };
        var include = new List<string> { "/pets/**" };

        var result = PathFilterHelper.FilterPaths(paths, include, null);

        Assert.Equal(2, result.Count);
        Assert.Contains("/pets", result);
        Assert.Contains("/pets/{id}", result);
    }

    [Fact]
    public void FilterPaths_ExcludeOnly_RemovesMatchingPaths()
    {
        var paths = new[] { "/pets", "/users", "/internal/health", "/internal/metrics" };
        var exclude = new List<string> { "/internal/**" };

        var result = PathFilterHelper.FilterPaths(paths, null, exclude);

        Assert.Equal(2, result.Count);
        Assert.Contains("/pets", result);
        Assert.Contains("/users", result);
    }

    [Fact]
    public void FilterPaths_IncludeAndExclude_AppliesBothFilters()
    {
        var paths = new[] { "/pets", "/pets/{id}", "/pets/internal", "/users" };
        var include = new List<string> { "/pets/**" };
        var exclude = new List<string> { "/pets/internal" };

        var result = PathFilterHelper.FilterPaths(paths, include, exclude);

        Assert.Equal(2, result.Count);
        Assert.Contains("/pets", result);
        Assert.Contains("/pets/{id}", result);
        Assert.DoesNotContain("/pets/internal", result);
    }

    [Fact]
    public void FilterPaths_EmptyInclude_ReturnsAllPaths()
    {
        var paths = new[] { "/pets", "/users" };

        var result = PathFilterHelper.FilterPaths(paths, new List<string>(), null);

        Assert.Equal(2, result.Count);
    }

    [Theory]
    [InlineData("/pets", "/**", true)]         // ** matches everything
    [InlineData("/a/b/c/d", "/**", true)]       // deep path matches **
    [InlineData("/pets", "/*", true)]           // * matches single segment
    [InlineData("/pets/{id}", "/*", false)]     // * does NOT match two segments
    [InlineData("/", "/**", true)]              // ** matches the root (zero trailing segments)
    [InlineData("/a/b/c", "/**/c", true)]       // trailing literal after **
    [InlineData("/a/b", "/**/c", false)]        // trailing literal requires the last segment
    [InlineData("/a/b/c", "/**/**/c", true)]    // adjacent ** collapses
    [InlineData("/x/a/y/b/z/c", "/**/a/**/b/**/c", true)]
    [InlineData("/x/a/y/b/z", "/**/a/**/b/**/c", false)]
    public void MatchesPattern_WildcardEdgeCases(
        string path,
        string pattern,
        bool expected)
    {
        var result = PathFilterHelper.MatchesPattern(path, pattern);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MatchesPattern_PathologicalDoubleStars_TerminatesQuickly()
    {
        // Regression guard for Copilot review item: the recursive backtracking matcher
        // was worst-case exponential on patterns with many ** against paths missing the
        // literal anchors. The DP matcher should evaluate each (i, j) state once.
        const string pattern = "/**/a/**/b/**/c/**/d/**/e";
        var path = "/" + string.Join("/", Enumerable.Repeat("x", 30));

        var sw = Stopwatch.StartNew();
        var result = PathFilterHelper.MatchesPattern(path, pattern);
        sw.Stop();

        Assert.False(result);
        Assert.True(
            sw.ElapsedMilliseconds < 500,
            $"Matcher took {sw.ElapsedMilliseconds}ms — suggests backtracking regression.");
    }

    [Theory]
    [InlineData("/pets/{id}", true)] // second pattern
    [InlineData("/users/1/orders", true)] // first pattern
    [InlineData("/orders", false)]
    public void MatchesAnyPattern_ReturnsTrueWhenAnyPatternMatches(
        string path,
        bool expected)
    {
        var patterns = new List<string> { "/users/**", "/pets/*" };

        Assert.Equal(expected, PathFilterHelper.MatchesAnyPattern(path, patterns));
    }

    [Fact]
    public void MatchesAnyPattern_NoPatterns_ReturnsFalse()
    {
        Assert.False(PathFilterHelper.MatchesAnyPattern("/pets", new List<string>()));
    }

    [Fact]
    public void WithFilteredPaths_IncludeAndExclude_ReturnsACopyWithOnlyTheAllowedPaths()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PathsYaml);

        var filtered = PathFilterHelper.WithFilteredPaths(document, ["/pets/**"], ["/pets/internal"]);

        Assert.NotSame(document, filtered);
        Assert.Equal(["/pets", "/pets/{id}"], filtered.Paths.Keys.OrderBy(k => k, StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Same(document.Paths["/pets"], filtered.Paths["/pets"]);
        Assert.True(filtered.Components!.Schemas!.ContainsKey("Pet"));

        // The given (possibly cached) document is untouched
        Assert.Equal(4, document.Paths.Count);
    }

    [Fact]
    public void WithFilteredPaths_ExcludeOnly_RemovesTheMatchingPaths()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PathsYaml);

        var filtered = PathFilterHelper.WithFilteredPaths(document, null, ["/users"]);

        Assert.Equal(["/pets", "/pets/internal", "/pets/{id}"], filtered.Paths.Keys.OrderBy(k => k, StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public void WithFilteredPaths_NoFilter_ReturnsTheSameDocument()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PathsYaml);

        Assert.Same(document, PathFilterHelper.WithFilteredPaths(document, null, null));
        Assert.Same(document, PathFilterHelper.WithFilteredPaths(document, [], []));
    }

    [Fact]
    public void WithFilteredPaths_NullDocument_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PathFilterHelper.WithFilteredPaths(null!, ["/pets"], null));
    }
}