namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A body declared under a media type with parameters (<c>application/json; charset=utf-8</c>), in another case
/// (<c>Application/JSON</c>) or under a wildcard that carries a model (<c>*/*</c>, <c>application/*</c>) is the
/// same body as one declared under <c>application/json</c>: every generator types it, none drops it. A wildcard with
/// a binary schema stays a file.
/// </summary>
public class MediaTypeParametersTests
{
    private const string Scenario = "MediaTypeParameters";

    [Fact]
    public void Server_TypesEveryBody()
    {
        var sources = GeneratorSnapshotHarness.RunRaw(Scenario, GeneratorSnapshotHarness.ServerMasterFolder);

        var endpoints = Source(sources, "PetsEndpointDefinition");
        Assert.Contains(".Produces<List<Pet>>()", endpoints, StringComparison.Ordinal); // charset parameter
        Assert.Contains(".Produces<Pet>(StatusCodes.Status201Created)", endpoints, StringComparison.Ordinal); // */* with a model
        Assert.Contains(".Produces<Pet>()", endpoints, StringComparison.Ordinal); // application/*
        Assert.Contains("[property: FromBody, Required] Pet Request", Source(sources, "Generated.Parameters"), StringComparison.Ordinal); // Application/JSON

        var results = Source(sources, "Generated.Results");
        Assert.Contains("ListPetsResult Ok(List<Pet>", results, StringComparison.Ordinal);
        Assert.Contains("CreatePetResult Created(Pet", results, StringComparison.Ordinal);
        Assert.Contains("GetPetResult Ok(Pet", results, StringComparison.Ordinal);
        Assert.Matches(@"GetPhotoResult Ok\(\s+byte\[\] bytes", results); // */* with a binary schema stays a file
    }

    [Theory]
    [InlineData(GeneratorSnapshotHarness.ClientTypedMasterFolder)]
    [InlineData(GeneratorSnapshotHarness.ClientOperationMasterFolder)]
    public void Clients_TypeEveryBody(string masterFolder)
    {
        var sources = GeneratorSnapshotHarness.RunRaw(Scenario, masterFolder);
        var all = string.Join("\n", sources.Select(s => s.Source));

        Assert.Matches("List<Pet>|IEnumerable<Pet>", all);
        Assert.DoesNotContain("Task<byte[]> GetPetAsync", all, StringComparison.Ordinal);
        Assert.Matches("GetPetAsync|GetPetEndpoint", all);
        Assert.Contains("Pet", Source(sources, "CreatePet"), StringComparison.Ordinal);
    }

    private static string Source(
        IReadOnlyList<(string HintName, string Source)> sources,
        string hintNamePart)
    {
        var matches = sources.Where(s => s.HintName.Contains(hintNamePart, StringComparison.Ordinal)).ToList();
        Assert.True(matches.Count > 0, $"No generated source named like '{hintNamePart}'. Sources: {string.Join(", ", sources.Select(s => s.HintName))}");
        return string.Join("\n", matches.Select(m => m.Source));
    }
}