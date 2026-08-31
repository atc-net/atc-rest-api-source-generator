namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// Guards the rule that the specification file name never acts as a version source.
/// </summary>
/// <remarks>
/// Versioning must come from the OpenAPI <c>info.version</c> field. The file name is only used
/// for two things: multi-part specification discovery (<c>{BaseName}_{PartName}.yaml</c>) and, as
/// a last resort, namespace resolution.
/// </remarks>
public class VersionSourceTests
{
    /// <summary>
    /// Statistics must report the version from the document, never from the file name, even when
    /// the file name looks like it carries a version (for example "Nexus.spec.api.v1.yaml").
    /// </summary>
    [Theory]
    [InlineData("Nexus.spec.api.v1", "v1")]
    [InlineData("api-1", "V1.0")]
    [InlineData("Monta", "2024-01-18")]
    public void CollectStatistics_UsesDocumentInfoVersion_NotFileName(
        string specificationName,
        string documentVersion)
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = "Any Title",
                Version = documentVersion,
            },
            Paths = [],
        };

        // Act
        var statistics = StatisticsCollector.CollectFromGeneratedTypes(
            types: [],
            document,
            specificationName,
            generatorType: "Client",
            diagnostics: [],
            duration: TimeSpan.Zero);

        // Assert
        Assert.Equal(documentVersion, statistics.ApiVersion);
        Assert.DoesNotContain(specificationName, statistics.ApiVersion, StringComparison.Ordinal);
    }

    /// <summary>
    /// A missing info.version must not silently fall back to any file-name derived value.
    /// </summary>
    [Fact]
    public void CollectStatistics_MissingVersion_DoesNotFallBackToFileName()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = "Any Title",
            },
            Paths = [],
        };

        // Act
        var statistics = StatisticsCollector.CollectFromGeneratedTypes(
            types: [],
            document,
            specificationName: "Nexus.spec.api.v1",
            generatorType: "Client",
            diagnostics: [],
            duration: TimeSpan.Zero);

        // Assert
        Assert.Empty(statistics.ApiVersion);
    }

    /// <summary>
    /// The namespace resolver must never treat a version-looking file name as a version, it is
    /// only ever a namespace of last resort.
    /// </summary>
    [Fact]
    public void Resolve_VersionLookingFileName_IsUsedOnlyAsNamespace()
    {
        // Act
        var result = NamespaceResolver.Resolve(
            configNamespace: null,
            documentTitle: "Contoso IoT Nexus API",
            yamlPath: "sample/NexusSample/Nexus.spec.api.v1.yaml");

        // Assert
        Assert.Equal("Nexus.spec.api.v1", result);
    }
}