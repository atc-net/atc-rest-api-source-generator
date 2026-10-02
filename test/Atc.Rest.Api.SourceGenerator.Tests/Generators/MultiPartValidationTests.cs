namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A specification split into a base file and part files is validated like a single file:
/// the configured <c>validateSpecificationStrategy</c> applies to the merged document.
/// </summary>
public sealed class MultiPartValidationTests : IDisposable
{
    private const string Header =
        """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        """;

    // An operationId that is not camelCase and an operation without a description/summary:
    // the strict rules report it for a single file.
    private const string Paths =
        """
        paths:
          /pets:
            get:
              operationId: Get_All_Pets
              responses:
                '200':
                  description: ok
        """;

    private readonly string markerDirectory = Path.Combine(Path.GetTempPath(), "atc-multipart-validation-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<string, string> Generators => new()
    {
        { nameof(ApiServerGenerator), ".atc-rest-api-server" },
        { nameof(ApiClientGenerator), ".atc-rest-api-client" },
        { nameof(ApiServerDomainGenerator), ".atc-rest-api-server-handlers" },
    };

    [Theory]
    [MemberData(nameof(Generators))]
    public void MultiPartSpec_ReportsTheSameValidationDiagnosticsAsTheSingleFile(
        string generatorName,
        string markerFileName)
    {
        const string marker = """{ "validateSpecificationStrategy": "Strict" }""";

        var single = ValidationIds(
            Run(generatorName, markerFileName, marker, ("Test.yaml", Header + "\n" + Paths)));

        var multiPart = ValidationIds(
            Run(
                generatorName,
                markerFileName,
                marker,
                ("Test.yaml", Header + "\npaths: {}"),
                ("Test_Pets.yaml", "openapi: 3.0.0\n" + Paths)));

        Assert.NotEmpty(single);
        Assert.Equal(single, multiPart);
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public void MultiPartSpec_WithStrategyNone_ReportsNoValidationDiagnostics(
        string generatorName,
        string markerFileName)
    {
        var multiPart = ValidationIds(
            Run(
                generatorName,
                markerFileName,
                """{ "validateSpecificationStrategy": "None" }""",
                ("Test.yaml", Header + "\npaths: {}"),
                ("Test_Pets.yaml", "openapi: 3.0.0\n" + Paths)));

        Assert.Empty(multiPart);
    }

    /// <summary>
    /// The rule ids the document validator owns (naming, operation, schema, security, ... rules);
    /// generator, dependency and multi-part merge diagnostics are not validation results.
    /// </summary>
    private static string[] ValidationIds(
        ImmutableArray<Diagnostic> diagnostics)
        => diagnostics
            .Select(d => d.Id)
            .Where(id => id.StartsWith("ATC_API_", StringComparison.Ordinal) &&
                         !id.StartsWith("ATC_API_GEN", StringComparison.Ordinal) &&
                         !id.StartsWith("ATC_API_DEP", StringComparison.Ordinal) &&
                         !id.StartsWith("ATC_API_MPT", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

    public void Dispose()
    {
        if (Directory.Exists(markerDirectory))
        {
            Directory.Delete(markerDirectory, recursive: true);
        }
    }

    private ImmutableArray<Diagnostic> Run(
        string generatorName,
        string markerFileName,
        string markerContent,
        params (string Path, string Content)[] yamlFiles)
    {
        IIncrementalGenerator generator = generatorName switch
        {
            nameof(ApiServerGenerator) => new ApiServerGenerator(),
            nameof(ApiClientGenerator) => new ApiClientGenerator(),
            _ => new ApiServerDomainGenerator(),
        };

        // The handlers generator scaffolds files next to its marker, so the marker lives in a temp folder.
        Directory.CreateDirectory(markerDirectory);

        var additionalTexts = yamlFiles
            .Select(f => (AdditionalText)new InMemoryAdditionalText(f.Path, f.Content))
            .Append(new InMemoryAdditionalText(Path.Combine(markerDirectory, markerFileName), markerContent))
            .ToImmutableArray();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),

                // The server generator only runs for a project that references ASP.NET Core.
                MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Http.HttpContext).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(additionalTexts)
            .RunGeneratorsAndUpdateCompilation(
                compilation, out _, out var generatorDiagnostics, CancellationToken.None);

        return generatorDiagnostics;
    }
}