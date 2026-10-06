namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// The root namespace is always a valid C# namespace, whatever the specification's file name or the marker says:
/// <c>my-api.yaml</c> is <c>MyApi</c>, <c>1api.yaml</c> is <c>_1api</c>, a keyword segment is capitalised. A name
/// that already is a namespace is kept as it is, so existing projects keep theirs.
/// </summary>
public class NamespaceResolverSanitizeTests
{
    [Theory]
    [InlineData("PetStoreSimple", "PetStoreSimple")]
    [InlineData("Contoso.Api.Contracts", "Contoso.Api.Contracts")]
    [InlineData("my-api", "MyApi")]
    [InlineData("my api v2", "MyApiV2")]
    [InlineData("1api", "_1api")]
    [InlineData("Contoso.class.Api", "Contoso.Class.Api")]
    [InlineData("Contoso..Api", "Contoso.Api")]
    [InlineData("", "Api")]
    public void ToValidNamespace_ReturnsExpected(
        string name,
        string expected)
        => Assert.Equal(expected, NamespaceResolver.ToValidNamespace(name));

    [Theory]
    [InlineData("C:/repo/specs/my-api.yaml", "MyApi")]
    [InlineData("C:/repo/specs/1api.yaml", "_1api")]
    [InlineData("C:/repo/specs/PetStoreSimple.yaml", "PetStoreSimple")]
    public void Resolve_TheFileNameFallback_IsAValidNamespace(
        string yamlPath,
        string expected)
        => Assert.Equal(expected, NamespaceResolver.Resolve(configNamespace: null, documentTitle: "Not A Title", yamlPath));

    [Theory]
    [InlineData("My-Api", "MyApi")]
    [InlineData("1Api", "_1Api")]
    [InlineData("class", "Class")]
    public void Resolve_AMarkerNamespaceThatIsNoNamespace_IsMadeOne(
        string configNamespace,
        string expected)
        => Assert.Equal(expected, NamespaceResolver.Resolve(configNamespace, documentTitle: null, "C:/repo/specs/api.yaml"));
}