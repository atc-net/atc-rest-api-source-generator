namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A marker file the generator cannot read is an error that stops generation, not a warning and a build from the
/// defaults: JSON that is no JSON, a value of the wrong kind (named with the setting and the kind expected), two
/// marker files of one kind in a project. A setting the generator does not have, or a value it does not know, is a
/// warning naming it and the value used instead.
/// </summary>
public class MarkerConfigDiagnosticTests
{
    private const string MinimalYaml =
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
        """;

    [Theory]
    [InlineData(".atc-rest-api-client")]
    [InlineData(".atc-rest-api-server")]
    [InlineData(".atc-rest-api-server-handlers")]
    public void Generator_MalformedMarkerJson_ReportsGEN013Error_AndGeneratesNothing(
        string markerFileName)
    {
        var (diagnostics, generated) = RunGenerator(GeneratorFor(markerFileName), markerFileName, "{ this is not valid json ");

        var error = Assert.Single(diagnostics, d => string.Equals(d.Id, Generator.RuleIdentifiers.MarkerConfigParseError, StringComparison.Ordinal));
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Nothing is generated", error.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Equal(0, generated);
    }

    [Fact]
    public void ClientGenerator_ValidMarkerJson_NoGEN013()
    {
        var (diagnostics, _) = RunGenerator(new ApiClientGenerator(), ".atc-rest-api-client", "{ \"generate\": true }");

        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, Generator.RuleIdentifiers.MarkerConfigParseError, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(".atc-rest-api-server", """{ "generate": "yes" }""", "'generate'", "true or false")]
    [InlineData(".atc-rest-api-server", """{ "namespace": 5 }""", "'namespace'", "a string")]
    [InlineData(".atc-rest-api-server", """{ "overlays": "Contoso.overlay.yaml" }""", "'overlays'", "an array")]
    [InlineData(".atc-rest-api-server", """{ "healthChecks": { "enabled": "on" } }""", "'healthChecks.enabled'", "true or false")]
    [InlineData(".atc-rest-api-client", """{ "generate": 1 }""", "'generate'", "true or false")]
    [InlineData(".atc-rest-api-server-handlers", """{ "generate": [] }""", "'generate'", "true or false")]
    public void Generator_AValueOfTheWrongKind_IsAGEN013ErrorNamingTheSetting_AndGeneratesNothing(
        string markerFileName,
        string markerContent,
        string setting,
        string expectedKind)
    {
        var (diagnostics, generated) = RunGenerator(GeneratorFor(markerFileName), markerFileName, markerContent);

        var error = Assert.Single(diagnostics, d => string.Equals(d.Id, Generator.RuleIdentifiers.MarkerConfigParseError, StringComparison.Ordinal));
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        var message = error.GetMessage(CultureInfo.InvariantCulture);
        Assert.Contains(setting, message, StringComparison.Ordinal);
        Assert.Contains(expectedKind, message, StringComparison.Ordinal);
        Assert.DoesNotContain("LineNumber", message, StringComparison.Ordinal);
        Assert.Equal(0, generated);
    }

    [Theory]
    [InlineData(".atc-rest-api-client")]
    [InlineData(".atc-rest-api-server")]
    [InlineData(".atc-rest-api-server-handlers")]
    public void Generator_TwoMarkerFilesOfOneKind_ReportsGEN020Error_AndGeneratesNothing(
        string markerFileName)
    {
        var first = Path.Combine("Api", markerFileName);
        var second = Path.Combine("Api", "Legacy", markerFileName);
        var (diagnostics, generated) = RunGenerator(
            GeneratorFor(markerFileName),
            [(first, "{}"), (second, "{}")]);

        var error = Assert.Single(diagnostics, d => string.Equals(d.Id, Generator.RuleIdentifiers.MultipleMarkerFiles, StringComparison.Ordinal));
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        var message = error.GetMessage(CultureInfo.InvariantCulture);
        Assert.Contains(first, message, StringComparison.Ordinal);
        Assert.Contains(second, message, StringComparison.Ordinal);
        Assert.Equal(0, generated);
    }

    [Theory]
    [InlineData(".atc-rest-api-client", """{ "generationMode": "EndpointPerOperation", "clientSufix": "Gateway" }""", "'clientSufix' is not a setting of this marker file and is ignored")]
    [InlineData(".atc-rest-api-server", """{ "validateSpecificationStrategy": "Minimal" }""", "'validateSpecificationStrategy' has the unknown value \"Minimal\" (expected none, standard or strict), so strict is used")]
    [InlineData(".atc-rest-api-server-handlers.json", """{ "healthChecks": { "enabled": true } }""", "'healthChecks' is not a setting of this marker file and is ignored")]
    public void Generator_UnknownMarkerSetting_ReportsGEN014Warning(
        string markerFileName,
        string markerContent,
        string expectedProblem)
    {
        var (diagnostics, _) = RunGenerator(GeneratorFor(markerFileName), markerFileName, markerContent);

        var warning = Assert.Single(diagnostics, d => string.Equals(d.Id, Generator.RuleIdentifiers.MarkerConfigUnknownSetting, StringComparison.Ordinal));
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Equal($"The marker file '{markerFileName}': {expectedProblem}", warning.GetMessage(CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(".atc-rest-api-client", """{ "$schema": "https://example.com/atc-rest-api-client.schema.json", "generationMode": "endpoint-per-operation" }""")]
    [InlineData(".atc-rest-api-server", "{ this is not valid json ")]
    public void Generator_KnownSettingsOrMalformedJson_NoGEN014Warning(
        string markerFileName,
        string markerContent)
    {
        var (diagnostics, _) = RunGenerator(GeneratorFor(markerFileName), markerFileName, markerContent);

        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, Generator.RuleIdentifiers.MarkerConfigUnknownSetting, StringComparison.Ordinal));
    }

    private static IIncrementalGenerator GeneratorFor(string markerFileName)
        => markerFileName switch
        {
            _ when markerFileName.StartsWith(".atc-rest-api-client", StringComparison.Ordinal) => new ApiClientGenerator(),
            _ when markerFileName.StartsWith(".atc-rest-api-server-handlers", StringComparison.Ordinal) => new ApiServerDomainGenerator(),
            _ => new ApiServerGenerator(),
        };

    private static (ImmutableArray<Diagnostic> Diagnostics, int GeneratedSources) RunGenerator(
        IIncrementalGenerator generator,
        string markerFileName,
        string markerContent)
        => RunGenerator(generator, [(markerFileName, markerContent)]);

    private static (ImmutableArray<Diagnostic> Diagnostics, int GeneratedSources) RunGenerator(
        IIncrementalGenerator generator,
        (string Path, string Content)[] markers)
    {
        var additionalTexts = ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText("Test.yaml", MinimalYaml))
            .AddRange(markers.Select(m => (AdditionalText)new InMemoryAdditionalText(m.Path, m.Content)));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(additionalTexts)
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out var generatorDiagnostics, CancellationToken.None);

        return (generatorDiagnostics, driver.GetRunResult().GeneratedTrees.Length);
    }
}