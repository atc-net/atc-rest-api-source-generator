namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// Guards the behaviour of real sample specifications against the namespace precedence chain.
/// </summary>
/// <remarks>
/// These tests encode the measured backward-compatibility matrix. They exist so that a change to
/// the title qualification gate immediately shows which shipped projects it would rename.
/// </remarks>
public class NamespaceResolverSampleSpecTests
{
    /// <summary>
    /// The samples that currently resolve their namespace from the specification file name.
    /// They must keep doing so, because their GlobalUsings reference the generated namespaces.
    /// </summary>
    [Theory]
    [InlineData("My Demo API - Full", "sample/Showcase/Showcase.yaml", "Showcase")]
    [InlineData("Swagger Petstore", "sample/PetStoreSimple/PetStoreSimple.yaml", "PetStoreSimple")]
    [InlineData("Swagger Petstore - OpenAPI 3.0", "sample/PetStoreFull/PetStoreFull.yaml", "PetStoreFull")]
    [InlineData("My Demo API - Full", "sample/MultipartDemo/MultipartDemo.yaml", "MultipartDemo")]
    public void Resolve_SamplesWithoutMarkerNamespace_KeepFileNameBasedNamespace(
        string title,
        string yamlPath,
        string expected)
    {
        // Act
        var result = NamespaceResolver.Resolve(
            configNamespace: null,
            documentTitle: title,
            yamlPath: yamlPath);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// The Eloverblik specifications are named api-1.yaml, which is not a usable namespace.
    /// Their info.title is a valid dotted identifier, so the file name is never reached even
    /// when the marker file does not pin a namespace.
    /// </summary>
    [Theory]
    [InlineData("Eloverblik.Api.ThirdPartyApi", "Eloverblik.Api.ThirdPartyApi")]
    [InlineData("Eloverblik.Api.CustomerApi", "Eloverblik.Api.CustomerApi")]
    public void Resolve_EloverblikSpec_DoesNotFallBackToApi1FileName(
        string title,
        string expected)
    {
        // Act
        var result = NamespaceResolver.Resolve(
            configNamespace: null,
            documentTitle: title,
            yamlPath: "sample/ThirdParty-EPO-Clients/api-1.yaml");

        // Assert
        Assert.Equal(expected, result);
        Assert.NotEqual("api-1", result, StringComparer.Ordinal);
    }
}