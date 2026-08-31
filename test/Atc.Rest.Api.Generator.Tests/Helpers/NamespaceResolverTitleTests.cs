namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// Tests for the <c>info.title</c> qualification gate used as rule 2 of the
/// namespace precedence chain.
/// </summary>
/// <remarks>
/// The gate is deliberately strict: a title is only accepted when it is already a valid
/// dot-separated C# identifier. No normalization, space-stripping or PascalCasing is performed.
/// <para>
/// The rejection tests below are load-bearing for backward compatibility. Loosening the gate so
/// that a prose title such as "My Demo API - Full" becomes "MyDemoApiFull" would silently rename
/// the generated namespaces of existing projects (e.g. Showcase.Generated.*) and is a breaking change.
/// </para>
/// </remarks>
public class NamespaceResolverTitleTests
{
    // ========== Rejection cases (backward-compatibility guards) ==========
    [Theory]
    [InlineData("My Demo API - Full")] // Showcase.yaml + MultipartDemo.yaml
    [InlineData("Swagger Petstore")] // PetStoreSimple.yaml
    [InlineData("Swagger Petstore - OpenAPI 3.0")] // PetStoreFull.yaml
    [InlineData("MONTA Partner API")] // Monta.yaml
    [InlineData("Contoso IoT Nexus API")] // Nexus.spec.api.v1.yaml
    public void TryGetNamespaceFromTitle_SampleProseTitles_AreRejected(
        string title)
    {
        // Act
        var result = NamespaceResolver.TryGetNamespaceFromTitle(title);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123Api")] // starts with a digit
    [InlineData("My..Api")] // empty segment
    [InlineData(".MyApi")] // leading dot
    [InlineData("MyApi.")] // trailing dot
    [InlineData("My-Api")] // hyphen
    [InlineData("My Api")] // whitespace
    [InlineData("Api.class")] // reserved keyword segment
    [InlineData("namespace")] // reserved keyword
    [InlineData("Api.2Fast")] // segment starts with a digit
    public void TryGetNamespaceFromTitle_MalformedTitles_AreRejected(
        string? title)
    {
        // Act
        var result = NamespaceResolver.TryGetNamespaceFromTitle(title);

        // Assert
        Assert.Null(result);
    }

    // ========== Acceptance cases ==========
    [Theory]
    [InlineData("Eloverblik.Api.ThirdPartyApi", "Eloverblik.Api.ThirdPartyApi")]
    [InlineData("Eloverblik.Api.CustomerApi", "Eloverblik.Api.CustomerApi")]
    [InlineData("Monta", "Monta")]
    [InlineData("_Internal.Api", "_Internal.Api")]
    [InlineData("  Eloverblik.Api.ThirdPartyApi  ", "Eloverblik.Api.ThirdPartyApi")] // trimmed
    public void TryGetNamespaceFromTitle_QualifyingTitles_AreAccepted(
        string title,
        string expected)
    {
        // Act
        var result = NamespaceResolver.TryGetNamespaceFromTitle(title);

        // Assert
        Assert.Equal(expected, result);
    }
}