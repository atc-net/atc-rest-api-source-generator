namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Tests for unified extractors that produce method names by combining the
/// last project-name segment with an "Api"/"ApiVersioning" suffix.
/// Verifies that names ending in "Api" do not produce duplicated suffixes
/// such as <c>MapApiApi</c>, <c>UseApiApi</c>, <c>AddApiApi</c>, or
/// <c>AddApiApiVersioning</c>.
/// </summary>
public class UnifiedExtractorsNamingTests
{
    private const string MinimalYaml = """
                                       openapi: 3.0.0
                                       info:
                                         title: Test
                                         version: 1.0.0
                                       paths:
                                         /health:
                                           get:
                                             operationId: getHealth
                                             responses:
                                               '200':
                                                 description: OK
                                       """;

    // ========== WebApplicationExtensionsExtractor.ExtractUnified ==========
    [Theory]
    [InlineData("KL.IoT.Provider.Cpms.Monta.Api", "MapApi")]
    [InlineData("MyCompany.Product.Api", "MapApi")]
    [InlineData("Showcase", "MapShowcaseApi")]
    [InlineData("MyCompany.Product.WebApi", "MapWebApi")]
    [InlineData("PetStoreFull", "MapPetStoreFullApi")]
    public void ExtractUnified_GeneratesExpectedMapMethodName(
        string projectName,
        string expectedMethodName)
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(MinimalYaml);
        var config = new ServerConfig();

        // Act
        var result = WebApplicationExtensionsExtractor.ExtractUnified(document, projectName, config);

        // Assert
        Assert.Contains(expectedMethodName + "(", result, StringComparison.Ordinal);
        Assert.DoesNotContain("MapApiApi", result, StringComparison.Ordinal);
    }

    // ========== WebApplicationExtensionsExtractor.Extract (UseXApi) ==========
    [Theory]
    [InlineData("KL.IoT.Provider.Cpms.Monta.Api", "UseApi")]
    [InlineData("MyCompany.Product.Api", "UseApi")]
    [InlineData("Showcase", "UseShowcaseApi")]
    [InlineData("MyCompany.Product.WebApi", "UseWebApi")]
    public void Extract_GeneratesExpectedUseMethodName(
        string projectName,
        string expectedMethodName)
    {
        // Act
        var result = WebApplicationExtensionsExtractor.Extract(projectName, useGlobalErrorHandler: true);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result!.Methods);
        Assert.Contains(result.Methods!, m => string.Equals(m.Name, expectedMethodName, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Methods!, m => string.Equals(m.Name, "UseApiApi", StringComparison.Ordinal));
    }

    // ========== VersioningDependencyInjectionExtractor.Extract ==========
    [Theory]
    [InlineData("KL.IoT.Provider.Cpms.Monta.Api", "AddApiVersioning")]
    [InlineData("MyCompany.Product.Api", "AddApiVersioning")]
    [InlineData("Showcase", "AddShowcaseApiVersioning")]
    [InlineData("MyCompany.Product.WebApi", "AddWebApiVersioning")]
    public void Extract_GeneratesExpectedAddApiVersioningMethodName(
        string projectName,
        string expectedMethodName)
    {
        // Arrange
        var config = new ServerConfig
        {
            VersioningStrategy = VersioningStrategyType.QueryString,
        };

        // Act
        var result = VersioningDependencyInjectionExtractor.Extract(projectName, config);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result!.Methods);
        Assert.Contains(result.Methods!, m => string.Equals(m.Name, expectedMethodName, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Methods!, m => string.Equals(m.Name, "AddApiApiVersioning", StringComparison.Ordinal));
    }

    // ========== UnifiedServiceCollectionExtractor.Extract ==========
    [Theory]
    [InlineData("KL.IoT.Provider.Cpms.Monta.Api", "AddApi")]
    [InlineData("MyCompany.Product.Api", "AddApi")]
    [InlineData("Showcase", "AddShowcaseApi")]
    [InlineData("MyCompany.Product.WebApi", "AddWebApi")]
    public void Extract_GeneratesExpectedAddApiMethodName(
        string projectName,
        string expectedMethodName)
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(MinimalYaml);
        var config = new ServerConfig();

        // Act
        var result = UnifiedServiceCollectionExtractor.Extract(document, projectName, config);

        // Assert
        Assert.Contains(expectedMethodName + "(", result, StringComparison.Ordinal);
        Assert.DoesNotContain("AddApiApi", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_WithVersioning_GeneratesExpectedAddApiVersioningCall_NoDoubleSuffix()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(MinimalYaml);
        var config = new ServerConfig
        {
            VersioningStrategy = VersioningStrategyType.QueryString,
        };

        // Act
        var result = UnifiedServiceCollectionExtractor.Extract(document, "KL.IoT.Provider.Cpms.Monta.Api", config);

        // Assert
        Assert.Contains("services.AddApiVersioning();", result, StringComparison.Ordinal);
        Assert.DoesNotContain("AddApiApiVersioning", result, StringComparison.Ordinal);
    }
}