namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A project holds one specification (with its parts named <c>{Base}_{Part}.yaml</c>). Two base specifications -
/// files that are no part of each other - are an error (<c>ATC_API_GEN021</c>) naming both, and nothing is generated:
/// the generator used to read whichever it saw first and ignore the other without a word.
/// </summary>
public sealed class MultipleSpecificationsTests
{
    [Theory]
    [InlineData(".atc-rest-api-client")]
    [InlineData(".atc-rest-api-server")]
    [InlineData(".atc-rest-api-server-handlers")]
    public void Generator_TwoBaseSpecifications_ReportsGEN021Error_AndGeneratesNothing(
        string markerFileName)
    {
        var (diagnostics, generated) = Run(
            markerFileName,
            [("Api.yaml", Specification("/things", "listThings")), ("Legacy.yaml", Specification("/legacy", "listLegacy"))]);

        var error = Assert.Single(diagnostics, d => string.Equals(d.Id, Generator.RuleIdentifiers.MultipleSpecifications, StringComparison.Ordinal));
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        var message = error.GetMessage(CultureInfo.InvariantCulture);
        Assert.Contains("Api.yaml", message, StringComparison.Ordinal);
        Assert.Contains("Legacy.yaml", message, StringComparison.Ordinal);
        Assert.Equal(0, generated);
    }

    [Theory]
    [InlineData(".atc-rest-api-client")]
    [InlineData(".atc-rest-api-server")]
    [InlineData(".atc-rest-api-server-handlers")]
    public void Generator_ABaseWithItsParts_IsNoGEN021(
        string markerFileName)
    {
        var (diagnostics, generated) = Run(
            markerFileName,
            [("Api.yaml", Specification("/things", "listThings")), ("Api_Others.yaml", "openapi: 3.0.3\npaths:\n  /others:\n    get:\n      operationId: listOthers\n      responses:\n        '200':\n          description: OK\n")]);

        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, Generator.RuleIdentifiers.MultipleSpecifications, StringComparison.Ordinal));
        Assert.True(generated > 0);
    }

    private static string Specification(
        string path,
        string operationId)
        => $"openapi: 3.0.3\ninfo: {{ title: Contoso, version: 1.0.0 }}\npaths:\n  {path}:\n    get:\n      operationId: {operationId}\n      responses:\n        '200':\n          description: OK\n";

    private static (ImmutableArray<Diagnostic> Diagnostics, int GeneratedSources) Run(
        string markerFileName,
        (string Path, string Content)[] specifications)
    {
        IIncrementalGenerator generator = markerFileName switch
        {
            ".atc-rest-api-client" => new ApiClientGenerator(),
            ".atc-rest-api-server" => new ApiServerGenerator(),
            _ => new ApiServerDomainGenerator(),
        };

        var additionalTexts = specifications
            .Select(s => (AdditionalText)new InMemoryAdditionalText(s.Path, s.Content))
            .Append(new InMemoryAdditionalText(markerFileName, "{}"))
            .ToImmutableArray();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),

                // The server generators only run for a project that references ASP.NET Core
                MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Http.HttpContext).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(additionalTexts)
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out var generatorDiagnostics, CancellationToken.None);

        return (generatorDiagnostics, driver.GetRunResult().GeneratedTrees.Length);
    }
}