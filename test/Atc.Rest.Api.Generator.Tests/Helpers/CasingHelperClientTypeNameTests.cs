namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// Tests for <see cref="CasingHelper.BuildClientTypeName"/>, which derives the generated HTTP
/// client class name from a namespace or path segment plus a configurable suffix.
/// </summary>
public class CasingHelperClientTypeNameTests
{
    // ========== Trailing suffix segment is dropped ==========
    [Theory]
    [InlineData("Eloverblik.ThirdPartyApi.Client", "ThirdPartyApiClient")]
    [InlineData("Eloverblik.ThirdPartyApi.client", "ThirdPartyApiClient")] // case-insensitive
    [InlineData("My.Api.Client", "ApiClient")]
    public void BuildClientTypeName_TrailingSuffixSegment_IsDropped(
        string name,
        string expected)
    {
        // Act
        var result = CasingHelper.BuildClientTypeName(name, "Client");

        // Assert
        Assert.Equal(expected, result);
    }

    // ========== Suffix is not duplicated ==========
    [Theory]
    [InlineData("EloverblikThirdPartyApiClient", "EloverblikThirdPartyApiClient")]
    [InlineData("My.Product.WeatherClient", "WeatherClient")]
    public void BuildClientTypeName_NameAlreadyEndsWithSuffix_IsNotDuplicated(
        string name,
        string expected)
    {
        // Act
        var result = CasingHelper.BuildClientTypeName(name, "Client");

        // Assert
        Assert.Equal(expected, result);
        Assert.DoesNotContain("ClientClient", result, StringComparison.Ordinal);
    }

    // ========== Existing behaviour must not change ==========
    [Theory]
    [InlineData("Eloverblik.Api.ThirdPartyApi", "ThirdPartyApiClient")]
    [InlineData("PetStoreSimple", "PetStoreSimpleClient")]
    [InlineData("Showcase", "ShowcaseClient")]
    [InlineData("MyCompany.PowerController.HostAgent", "HostAgentClient")]
    public void BuildClientTypeName_RegularNames_AppendSuffix(
        string name,
        string expected)
    {
        // Act
        var result = CasingHelper.BuildClientTypeName(name, "Client");

        // Assert
        Assert.Equal(expected, result);
    }

    // ========== Custom suffix ==========
    [Theory]
    [InlineData("PetStoreSimple", "Api", "PetStoreSimpleApi")]
    [InlineData("My.Product.Api", "Api", "ProductApi")] // trailing "Api" segment dropped
    [InlineData("My.Product.WeatherApi", "Api", "WeatherApi")] // already ends with suffix
    [InlineData("Showcase", "Gateway", "ShowcaseGateway")]
    public void BuildClientTypeName_CustomSuffix_IsHonored(
        string name,
        string suffix,
        string expected)
    {
        // Act
        var result = CasingHelper.BuildClientTypeName(name, suffix);

        // Assert
        Assert.Equal(expected, result);
    }

    // ========== Path segments (no dots) ==========
    [Theory]
    [InlineData("Accounts", "AccountsClient")]
    [InlineData("Files", "FilesClient")]
    [InlineData("Client", "Client")] // segment literally named "Client" must not become ClientClient
    public void BuildClientTypeName_PathSegment_AppendsSuffixOnce(
        string segment,
        string expected)
    {
        // Act
        var result = CasingHelper.BuildClientTypeName(segment, "Client");

        // Assert
        Assert.Equal(expected, result);
    }

    // ========== Edge cases ==========
    [Theory]
    [InlineData(null, "AssemblyClient")]
    [InlineData("", "AssemblyClient")]
    public void BuildClientTypeName_NullOrEmptyName_FallsBackToAssembly(
        string? name,
        string expected)
    {
        // Act
        var result = CasingHelper.BuildClientTypeName(name, "Client");

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildClientTypeName_NullOrEmptySuffix_FallsBackToClient(
        string? suffix)
    {
        // Act
        var result = CasingHelper.BuildClientTypeName("PetStoreSimple", suffix);

        // Assert
        Assert.Equal("PetStoreSimpleClient", result);
    }

    /// <summary>
    /// A namespace consisting only of the suffix must still produce a usable type name rather than
    /// an empty identifier.
    /// </summary>
    [Fact]
    public void BuildClientTypeName_NameIsOnlyTheSuffix_ReturnsSuffix()
    {
        // Act
        var result = CasingHelper.BuildClientTypeName("Client", "Client");

        // Assert
        Assert.Equal("Client", result);
    }
}