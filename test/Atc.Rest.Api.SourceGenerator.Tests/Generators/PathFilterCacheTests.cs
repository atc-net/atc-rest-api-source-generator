namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The parsed specification is cached per file (one compiler process serves many builds and
/// projects). A marker's <c>includePaths</c> / <c>excludePaths</c> must not leak into that cache:
/// removing the filter, or another project reading the same file, gets every path again.
/// </summary>
public class PathFilterCacheTests
{
    private const string Yaml =
        """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: ok
          /users:
            get:
              operationId: getUsers
              responses:
                '200':
                  description: ok
        """;

    public static TheoryData<string, string> Generators => new()
    {
        { nameof(ApiServerGenerator), ".atc-rest-api-server" },
        { nameof(ApiClientGenerator), ".atc-rest-api-client" },
    };

    [Theory]
    [MemberData(nameof(Generators))]
    public void RemovingAPathFilter_BringsTheFilteredPathsBack(
        string generatorName,
        string markerFileName)
    {
        // Each case uses its own file path: the cache is keyed by path.
        var yamlPath = $"PathFilterCache_{generatorName}.yaml";

        var filtered = Run(generatorName, markerFileName, """{ "validateSpecificationStrategy": "None", "excludePaths": ["/users"] }""", yamlPath);
        var unfiltered = Run(generatorName, markerFileName, """{ "validateSpecificationStrategy": "None" }""", yamlPath);

        Assert.Contains("GetPets", filtered, StringComparison.Ordinal);
        Assert.DoesNotContain("GetUsers", filtered, StringComparison.Ordinal);
        Assert.Contains("GetPets", unfiltered, StringComparison.Ordinal);
        Assert.Contains("GetUsers", unfiltered, StringComparison.Ordinal);
    }

    private static string Run(
        string generatorName,
        string markerFileName,
        string markerContent,
        string yamlPath)
    {
        IIncrementalGenerator generator = generatorName == nameof(ApiServerGenerator)
            ? new ApiServerGenerator()
            : new ApiClientGenerator();

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(yamlPath, Yaml),
            new InMemoryAdditionalText(markerFileName, markerContent));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: CompilationVerificationHarness.GetFullFrameworkReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(additionalTexts)
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out _, CancellationToken.None);

        return string.Join(
            "\n",
            driver.GetRunResult().GeneratedTrees.Select(t => t.GetText().ToString()));
    }
}