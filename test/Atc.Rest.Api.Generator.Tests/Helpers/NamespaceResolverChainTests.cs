namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// Tests for the three-rule namespace precedence chain:
/// 1. marker <c>namespace</c>, 2. qualifying <c>info.title</c>, 3. specification file name.
/// </summary>
public class NamespaceResolverChainTests
{
    private const string YamlPath = "C:/repo/specs/api-1.yaml";

    [Fact]
    public void Resolve_MarkerNamespace_WinsOverEverything()
    {
        // Act
        var result = NamespaceResolver.Resolve(
            configNamespace: "Marker.Wins",
            documentTitle: "Title.Loses",
            yamlPath: YamlPath);

        // Assert
        Assert.Equal("Marker.Wins", result);
    }

    [Fact]
    public void Resolve_QualifyingTitle_WinsOverFileName()
    {
        // Act
        var result = NamespaceResolver.Resolve(
            configNamespace: null,
            documentTitle: "Eloverblik.Api.ThirdPartyApi",
            yamlPath: YamlPath);

        // Assert
        Assert.Equal("Eloverblik.Api.ThirdPartyApi", result);
    }

    [Fact]
    public void Resolve_NonQualifyingTitle_FallsThroughToFileName()
    {
        // Act
        var result = NamespaceResolver.Resolve(
            configNamespace: null,
            documentTitle: "Swagger Petstore",
            yamlPath: YamlPath);

        // Assert
        Assert.Equal("api-1", result);
    }

    [Fact]
    public void Resolve_NoTitle_FallsThroughToFileName()
    {
        // Act
        var result = NamespaceResolver.Resolve(
            configNamespace: null,
            documentTitle: null,
            yamlPath: YamlPath);

        // Assert
        Assert.Equal("api-1", result);
    }

    /// <summary>
    /// Backward-compatibility guard: mirrors <c>Showcase.ClientApp</c>, which has no marker
    /// namespace and a prose title. It must keep resolving to "Showcase" so that
    /// Showcase.Generated.* is unchanged.
    /// </summary>
    /// <remarks>
    /// This is also the reason the MSBuild <c>RootNamespace</c> is not part of the chain. MSBuild
    /// defaults it to the project file name, so for this project it evaluates to
    /// "Showcase.ClientApp". Had it been placed above the file name rule it would always win here,
    /// renaming the generated namespace and breaking the project's GlobalUsings.
    /// </remarks>
    [Fact]
    public void Resolve_ShowcaseScenario_IsUnchanged()
    {
        // Act
        var result = NamespaceResolver.Resolve(
            configNamespace: null,
            documentTitle: "My Demo API - Full",
            yamlPath: "C:/repo/sample/Showcase/Showcase.yaml");

        // Assert
        Assert.Equal("Showcase", result);
        Assert.NotEqual("Showcase.ClientApp", result, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_BlankConfigNamespace_IsIgnored(string configNamespace)
    {
        // Act
        var result = NamespaceResolver.Resolve(
            configNamespace: configNamespace,
            documentTitle: "Eloverblik.Api.ThirdPartyApi",
            yamlPath: YamlPath);

        // Assert
        Assert.Equal("Eloverblik.Api.ThirdPartyApi", result);
    }

    [Fact]
    public void Resolve_ConfigNamespace_IsTrimmed()
    {
        // Act
        var result = NamespaceResolver.Resolve(
            configNamespace: "  Marker.Namespace  ",
            documentTitle: null,
            yamlPath: YamlPath);

        // Assert
        Assert.Equal("Marker.Namespace", result);
    }
}