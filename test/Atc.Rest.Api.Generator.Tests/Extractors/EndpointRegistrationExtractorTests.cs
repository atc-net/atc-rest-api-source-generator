namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Tests for the consolidated endpoint-mapping extension of <see cref="EndpointRegistrationExtractor"/>.
/// The other methods of the type are covered by <see cref="PolicyExtractorTests"/>.
/// </summary>
public class EndpointRegistrationExtractorTests
{
    [Fact]
    public void ExtractConsolidatedEndpointMappingExtension_TwoSegments_OneMapMethodPerSegment()
    {
        var segmentDefinitions = new List<(string Segment, List<string> EndpointDefinitionClassNames)>
        {
            ("Pets", ["ListPetsEndpointDefinition", "GetPetEndpointDefinition"]),
            (string.Empty, ["HealthEndpointDefinition"]),
        };

        var result = EndpointRegistrationExtractor.ExtractConsolidatedEndpointMappingExtension("TestApi", segmentDefinitions);

        Assert.NotNull(result);
        Assert.Equal("EndpointDefinitionExtensions", result.ClassTypeName);
        Assert.Equal("TestApi.Generated.Endpoints", result.Namespace);
        Assert.NotNull(result.Methods);
        Assert.Equal(2, result.Methods.Count);

        // A named segment maps to Map{Segment}Endpoints; the root segment falls back to MapApiEndpoints.
        var petsMethod = result.Methods.Single(m => m.Name == "MapPetsEndpoints");
        Assert.Equal("WebApplication", petsMethod.ReturnTypeName);
        Assert.NotNull(petsMethod.Content);

        // Definitions are instantiated in ordinal order, not the order they were given in.
        var getIndex = petsMethod.Content.IndexOf("new GetPetEndpointDefinition().DefineEndpoints(app);", StringComparison.Ordinal);
        var listIndex = petsMethod.Content.IndexOf("new ListPetsEndpointDefinition().DefineEndpoints(app);", StringComparison.Ordinal);
        Assert.True(getIndex >= 0 && listIndex >= 0, "Both definitions must be mapped");
        Assert.True(getIndex < listIndex, "GetPet must be mapped before ListPets");
        Assert.EndsWith("return app;", petsMethod.Content, StringComparison.Ordinal);

        var rootMethod = result.Methods.Single(m => m.Name == "MapApiEndpoints");
        Assert.Contains("new HealthEndpointDefinition().DefineEndpoints(app);", rootMethod.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractConsolidatedEndpointMappingExtension_SegmentWithoutDefinitions_IsSkipped()
    {
        var segmentDefinitions = new List<(string Segment, List<string> EndpointDefinitionClassNames)>
        {
            ("Pets", ["ListPetsEndpointDefinition"]),
            ("Users", []),
        };

        var result = EndpointRegistrationExtractor.ExtractConsolidatedEndpointMappingExtension("TestApi", segmentDefinitions);

        Assert.NotNull(result);
        var method = Assert.Single(result.Methods!);
        Assert.Equal("MapPetsEndpoints", method.Name);
    }

    [Fact]
    public void ExtractConsolidatedEndpointMappingExtension_NoSegments_ReturnsNull()
    {
        var empty = new List<(string Segment, List<string> EndpointDefinitionClassNames)>();
        var onlyEmptySegments = new List<(string Segment, List<string> EndpointDefinitionClassNames)>
        {
            ("Pets", []),
        };

        Assert.Null(EndpointRegistrationExtractor.ExtractConsolidatedEndpointMappingExtension("TestApi", empty));
        Assert.Null(EndpointRegistrationExtractor.ExtractConsolidatedEndpointMappingExtension("TestApi", onlyEmptySegments));
    }
}