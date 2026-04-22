namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class StringBuilderExtensionsTests
{
    // ========== AppendSegmentUsings — flag filtering ==========
    [Fact]
    public void StringBuilderExtensions_AppendSegmentUsings_EmitsAllFourUsings_WhenAllFlagsAreTrue()
    {
        // Arrange
        var sb = new StringBuilder();
        var namespaces = new PathSegmentNamespaces(
            HasHandlers: true,
            HasResults: true,
            HasParameters: true,
            HasModels: true);

        // Act
        sb.AppendSegmentUsings("MyApi", "Pets", namespaces);
        var result = sb.ToString();

        // Assert
        Assert.Contains("using MyApi.Generated.Pets.Handlers;", result, StringComparison.Ordinal);
        Assert.Contains("using MyApi.Generated.Pets.Models;", result, StringComparison.Ordinal);
        Assert.Contains("using MyApi.Generated.Pets.Parameters;", result, StringComparison.Ordinal);
        Assert.Contains("using MyApi.Generated.Pets.Results;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void StringBuilderExtensions_AppendSegmentUsings_EmitsNothing_WhenAllFlagsAreFalse()
    {
        // Arrange
        var sb = new StringBuilder();
        var namespaces = new PathSegmentNamespaces(
            HasHandlers: false,
            HasResults: false,
            HasParameters: false,
            HasModels: false);

        // Act
        sb.AppendSegmentUsings("MyApi", "Pets", namespaces);

        // Assert
        Assert.Equal(string.Empty, sb.ToString());
    }

    [Theory]
    [InlineData(true, false, false, false, "Handlers")]
    [InlineData(false, true, false, false, "Results")]
    [InlineData(false, false, true, false, "Parameters")]
    [InlineData(false, false, false, true, "Models")]
    public void StringBuilderExtensions_AppendSegmentUsings_EmitsOnlyMatchingFlag(
        bool hasHandlers,
        bool hasResults,
        bool hasParameters,
        bool hasModels,
        string expectedSuffix)
    {
        // Arrange
        var sb = new StringBuilder();
        var namespaces = new PathSegmentNamespaces(hasHandlers, hasResults, hasParameters, hasModels);

        // Act
        sb.AppendSegmentUsings("MyApi", "Pets", namespaces);
        var result = sb.ToString();

        // Assert
        Assert.Contains($"using MyApi.Generated.Pets.{expectedSuffix};", result, StringComparison.Ordinal);
        Assert.Equal(1, CountNewlines(result));
    }

    // ========== AppendSegmentUsings — path segment handling ==========
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void StringBuilderExtensions_AppendSegmentUsings_OmitsSegmentSuffix_WhenPathSegmentIsNullOrEmpty(
        string? pathSegment)
    {
        // Arrange
        var sb = new StringBuilder();
        var namespaces = new PathSegmentNamespaces(
            HasHandlers: true,
            HasResults: true,
            HasParameters: true,
            HasModels: true);

        // Act
        sb.AppendSegmentUsings("MyApi", pathSegment, namespaces);
        var result = sb.ToString();

        // Assert — no ".Pets" style segment injected between "Generated" and the leaf namespace.
        Assert.Contains("using MyApi.Generated.Handlers;", result, StringComparison.Ordinal);
        Assert.Contains("using MyApi.Generated.Models;", result, StringComparison.Ordinal);
        Assert.Contains("using MyApi.Generated.Parameters;", result, StringComparison.Ordinal);
        Assert.Contains("using MyApi.Generated.Results;", result, StringComparison.Ordinal);
    }

    // ========== AppendSegmentUsings — include switches ==========
    [Fact]
    public void StringBuilderExtensions_AppendSegmentUsings_OmitsHandlers_WhenIncludeHandlersIsFalse()
    {
        // Arrange
        var sb = new StringBuilder();
        var namespaces = new PathSegmentNamespaces(
            HasHandlers: true,
            HasResults: false,
            HasParameters: false,
            HasModels: true);

        // Act
        sb.AppendSegmentUsings("MyApi", "Pets", namespaces, includeHandlers: false);
        var result = sb.ToString();

        // Assert
        Assert.DoesNotContain("Handlers", result, StringComparison.Ordinal);
        Assert.Contains("using MyApi.Generated.Pets.Models;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void StringBuilderExtensions_AppendSegmentUsings_OmitsModels_WhenIncludeModelsIsFalse()
    {
        // Arrange
        var sb = new StringBuilder();
        var namespaces = new PathSegmentNamespaces(
            HasHandlers: true,
            HasResults: false,
            HasParameters: false,
            HasModels: true);

        // Act
        sb.AppendSegmentUsings("MyApi", "Pets", namespaces, includeModels: false);
        var result = sb.ToString();

        // Assert
        Assert.DoesNotContain("Models", result, StringComparison.Ordinal);
        Assert.Contains("using MyApi.Generated.Pets.Handlers;", result, StringComparison.Ordinal);
    }

    // ========== AppendSegmentUsings — global using syntax ==========
    [Fact]
    public void StringBuilderExtensions_AppendSegmentUsings_EmitsGlobalUsingPrefix_WhenIsGlobalUsingIsTrue()
    {
        // Arrange
        var sb = new StringBuilder();
        var namespaces = new PathSegmentNamespaces(
            HasHandlers: true,
            HasResults: false,
            HasParameters: false,
            HasModels: false);

        // Act
        sb.AppendSegmentUsings("MyApi", "Pets", namespaces, isGlobalUsing: true);
        var result = sb.ToString();

        // Assert
        Assert.Contains("global using MyApi.Generated.Pets.Handlers;", result, StringComparison.Ordinal);
        Assert.DoesNotContain("\nusing ", result, StringComparison.Ordinal);
    }

    private static int CountNewlines(string value)
    {
        var count = 0;
        foreach (var ch in value)
        {
            if (ch == '\n')
            {
                count++;
            }
        }

        return count;
    }
}