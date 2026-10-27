namespace Atc.Rest.Api.Generator.Tests.Models;

/// <summary>
/// A specification file is parsed once per path and content. A multi-part specification has several files:
/// each keeps its own parsed document, so the generators do not parse every part again on every run.
/// </summary>
public class SpecificationFileParseCacheTests
{
    private const string BaseYaml = """
        openapi: 3.0.0
        info:
          title: Cache
          version: 1.0.0
        paths: {}
        """;

    private const string PetsYaml = """
        openapi: 3.0.0
        paths:
          /pets:
            get:
              operationId: listPets
              responses:
                '200':
                  description: ok
        """;

    private const string UsersYaml = """
        openapi: 3.0.0
        paths:
          /users:
            get:
              operationId: listUsers
              responses:
                '200':
                  description: ok
        """;

    [Fact]
    public void FromContent_SeveralFiles_EachKeepsItsParsedDocument()
    {
        var pets = SpecificationFile.FromContent("ParseCache_Pets.yaml", PetsYaml, "ParseCache");
        var users = SpecificationFile.FromContent("ParseCache_Users.yaml", UsersYaml, "ParseCache");

        var petsAgain = SpecificationFile.FromContent("ParseCache_Pets.yaml", PetsYaml, "ParseCache");
        var usersAgain = SpecificationFile.FromContent("ParseCache_Users.yaml", UsersYaml, "ParseCache");

        Assert.NotNull(pets.Document);
        Assert.Same(pets.Document, petsAgain.Document);
        Assert.Same(users.Document, usersAgain.Document);
        Assert.NotSame(pets.Document, users.Document);
    }

    [Fact]
    public void FromContent_ChangedContent_IsParsedAgain()
    {
        var first = SpecificationFile.FromContent("ParseCacheChanged_Pets.yaml", PetsYaml, "ParseCacheChanged");
        var changed = SpecificationFile.FromContent("ParseCacheChanged_Pets.yaml", UsersYaml, "ParseCacheChanged");

        Assert.NotSame(first.Document, changed.Document);
        Assert.Contains("/users", changed.Document!.Paths.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public void Merge_Twice_GivesTheSameDocument_AndLeavesTheCachedPartsAlone()
    {
        static SpecificationFile[] Read() =>
        [
            SpecificationFile.FromContent("ParseCacheMerge.yaml", BaseYaml, "ParseCacheMerge"),
            SpecificationFile.FromContent("ParseCacheMerge_Pets.yaml", PetsYaml, "ParseCacheMerge"),
            SpecificationFile.FromContent("ParseCacheMerge_Users.yaml", UsersYaml, "ParseCacheMerge"),
        ];

        var first = Read();
        var firstMerge = SpecificationService.MergeSpecifications(first[0], [first[1], first[2]], MultiPartConfiguration.Default);
        var second = Read();
        var secondMerge = SpecificationService.MergeSpecifications(second[0], [second[1], second[2]], MultiPartConfiguration.Default);

        Assert.True(firstMerge.IsSuccess);
        Assert.True(secondMerge.IsSuccess);
        Assert.Equal(["/pets", "/users"], firstMerge.Document!.Paths.Keys.OrderBy(k => k, StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["/pets", "/users"], secondMerge.Document!.Paths.Keys.OrderBy(k => k, StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Empty(second[0].Document!.Paths);
        Assert.Equal(["/pets"], second[1].Document!.Paths.Keys, StringComparer.Ordinal);
        Assert.Equal(["/users"], second[2].Document!.Paths.Keys, StringComparer.Ordinal);
    }
}