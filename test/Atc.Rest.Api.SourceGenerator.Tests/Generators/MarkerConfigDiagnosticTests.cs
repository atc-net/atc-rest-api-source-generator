namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Verifies that the generators surface a diagnostic (instead of silently swallowing) when a
/// marker file cannot be deserialized, so a user with a typo'd marker JSON gets a signal rather
/// than silent default generation.
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

    [Fact]
    public void ClientGenerator_MalformedMarkerJson_ReportsGEN013Warning()
    {
        // Arrange & Act
        var diagnostics = RunGenerator(
            new ApiClientGenerator(),
            ".atc-rest-api-client",
            "{ this is not valid json ");

        // Assert
        var parseWarning = diagnostics.FirstOrDefault(d =>
            string.Equals(d.Id, Generator.RuleIdentifiers.MarkerConfigParseError, StringComparison.Ordinal));

        Assert.NotNull(parseWarning);
        Assert.Equal(DiagnosticSeverity.Warning, parseWarning.Severity);
    }

    [Fact]
    public void ClientGenerator_ValidMarkerJson_NoGEN013Warning()
    {
        // Arrange & Act
        var diagnostics = RunGenerator(
            new ApiClientGenerator(),
            ".atc-rest-api-client",
            "{ \"generate\": true }");

        // Assert
        var parseWarning = diagnostics.FirstOrDefault(d =>
            string.Equals(d.Id, Generator.RuleIdentifiers.MarkerConfigParseError, StringComparison.Ordinal));

        Assert.Null(parseWarning);
    }

    [Fact]
    public void ServerGenerator_MalformedMarkerJson_ReportsGEN013Warning()
    {
        // Arrange & Act
        var diagnostics = RunGenerator(
            new ApiServerGenerator(),
            ".atc-rest-api-server",
            "{ this is not valid json ");

        // Assert
        var parseWarning = diagnostics.FirstOrDefault(d =>
            string.Equals(d.Id, Generator.RuleIdentifiers.MarkerConfigParseError, StringComparison.Ordinal));

        Assert.NotNull(parseWarning);
        Assert.Equal(DiagnosticSeverity.Warning, parseWarning.Severity);
    }

    private static ImmutableArray<Diagnostic> RunGenerator(
        IIncrementalGenerator generator,
        string markerFileName,
        string markerContent)
    {
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText("Test.yaml", MinimalYaml),
            new InMemoryAdditionalText(markerFileName, markerContent));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(additionalTexts)
            .RunGeneratorsAndUpdateCompilation(
                compilation, out _, out var generatorDiagnostics, CancellationToken.None);

        return generatorDiagnostics;
    }
}