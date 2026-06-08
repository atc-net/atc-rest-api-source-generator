namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class EndpointMapHelperTests
{
    [Theory]
    [InlineData("GET")]
    [InlineData("get")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public void IsStandardMappableMethod_StandardVerbs_ReturnsTrue(
        string httpMethod)
        => Assert.True(EndpointMapHelper.IsStandardMappableMethod(httpMethod));

    [Theory]
    [InlineData("QUERY")]
    [InlineData("query")]
    [InlineData("LINK")]
    [InlineData("UNLINK")]
    [InlineData("PURGE")]
    public void IsStandardMappableMethod_NonStandardVerbs_ReturnsFalse(
        string httpMethod)
        => Assert.False(EndpointMapHelper.IsStandardMappableMethod(httpMethod));

    [Fact]
    public void BuildSingleLineMapCall_StandardGet_UsesMapGet()
        => Assert.Equal(
            "MapGet(\"/pets\", ListPets)",
            EndpointMapHelper.BuildSingleLineMapCall("GET", "/pets", "ListPets"));

    [Fact]
    public void BuildSingleLineMapCall_StandardPatch_UsesMapPatch()
        => Assert.Equal(
            "MapPatch(\"/pets/{id}\", PatchPet)",
            EndpointMapHelper.BuildSingleLineMapCall("PATCH", "/pets/{id}", "PatchPet"));

    [Fact]
    public void BuildSingleLineMapCall_QueryMethod_UsesMapMethodsWithUppercaseVerb()
        => Assert.Equal(
            "MapMethods(\"/pets\", new[] { \"QUERY\" }, QueryPets)",
            EndpointMapHelper.BuildSingleLineMapCall("query", "/pets", "QueryPets"));

    [Fact]
    public void BuildSingleLineMapCall_CustomVerb_UsesMapMethodsWithUppercaseVerb()
        => Assert.Equal(
            "MapMethods(\"/pets/{id}\", new[] { \"LINK\" }, LinkPet)",
            EndpointMapHelper.BuildSingleLineMapCall("LINK", "/pets/{id}", "LinkPet"));
}