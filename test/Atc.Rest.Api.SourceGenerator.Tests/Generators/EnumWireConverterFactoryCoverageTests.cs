namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The <c>EnumWireConverterFactory</c> of a generated project lists every enum the project generates: an enum it
/// missed would be left to whatever enum converter the host registers - or to none, and go on the wire as a number.
/// Checked against every scenario's real output (its snapshots).
/// </summary>
public sealed class EnumWireConverterFactoryCoverageTests
{
    public static TheoryData<string> FoldersWithAFactory()
        => [.. Directory
            .EnumerateFiles(GeneratorSnapshotHarness.GetScenariosRoot(), "*.EnumWireConverterFactory.verified.cs", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .Select(d => Path.GetRelativePath(GeneratorSnapshotHarness.GetScenariosRoot(), d!))
            .Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(FoldersWithAFactory))]
    public void TheFactory_ListsEveryEnumTheFolderDeclares(string folder)
    {
        var directory = Path.Combine(GeneratorSnapshotHarness.GetScenariosRoot(), folder);
        var factory = File.ReadAllText(Directory.GetFiles(directory, "*.EnumWireConverterFactory.verified.cs").Single());

        var missing = Directory
            .GetFiles(directory, "*.verified.cs")
            .SelectMany(file => GeneratedEnumDeclaration.FindIn(File.ReadAllText(file)))
            .Where(e => !factory.Contains($"typeToConvert == typeof({e.FullName})", StringComparison.Ordinal))
            .Select(e => e.FullName)
            .ToList();

        Assert.True(missing.Count == 0, $"{folder}: the factory does not list {string.Join(", ", missing)}");
    }

    private const string StrictEnumsYaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /notes:
            get:
              operationId: listNotes
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Note'
        components:
          schemas:
            Note:
              type: object
              properties:
                plain: { $ref: '#/components/schemas/Plain' }
                kebab: { $ref: '#/components/schemas/Kebab' }
                level: { $ref: '#/components/schemas/Level' }
            Plain:
              type: string
              enum: [Red, Green]
            Kebab:
              type: string
              enum: [dark-blue, light-blue]
            Level:
              type: integer
              enum: [1, 2]
        """;

    // Both client modes with C# enums for strings (extensibleEnums off): a PascalCase string enum carries no
    // converter of its own, so the factory is all that keeps it a string on the wire
    [Theory]
    [InlineData("TypedClient")]
    [InlineData("EndpointPerOperation")]
    public void TheFactory_ListsEveryEnum_OfAClientWithCSharpEnumsForStrings(
        string generationMode)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(
            new ApiClientGenerator(),
            ".atc-rest-api-client",
            $$"""{ "generationMode": "{{generationMode}}", "extensibleEnums": false }""",
            includeMinimalApi: false,
            StrictEnumsYaml);

        var factory = sources.Single(s => s.HintName.EndsWith(".EnumWireConverterFactory.g.cs", StringComparison.Ordinal)).Source;
        var enums = sources.SelectMany(s => GeneratedEnumDeclaration.FindIn(s.Source)).ToList();

        Assert.Equal(3, enums.Count);
        Assert.All(enums, e => Assert.Contains($"typeToConvert == typeof({e.FullName})", factory, StringComparison.Ordinal));
    }
}