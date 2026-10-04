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

    [Theory]
    [InlineData(".atc-rest-api-client", """{ "generationMode": "EndpointPerOperation", "clientSufix": "Gateway" }""", "'clientSufix' is not a setting of this marker file and is ignored")]
    [InlineData(".atc-rest-api-server", """{ "validateSpecificationStrategy": "Minimal" }""", "'validateSpecificationStrategy' has the unknown value \"Minimal\" (expected none, standard or strict), so standard is used")]
    [InlineData(".atc-rest-api-server-handlers.json", """{ "healthChecks": { "enabled": true } }""", "'healthChecks' is not a setting of this marker file and is ignored")]
    public void Generator_UnknownMarkerSetting_ReportsGEN014Warning(
        string markerFileName,
        string markerContent,
        string expectedProblem)
    {
        // Arrange & Act
        var diagnostics = RunGenerator(GeneratorFor(markerFileName), markerFileName, markerContent);

        // Assert
        var warning = Assert.Single(diagnostics, d =>
            string.Equals(d.Id, Generator.RuleIdentifiers.MarkerConfigUnknownSetting, StringComparison.Ordinal));
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
        // Arrange & Act
        var diagnostics = RunGenerator(GeneratorFor(markerFileName), markerFileName, markerContent);

        // Assert
        Assert.DoesNotContain(diagnostics, d =>
            string.Equals(d.Id, Generator.RuleIdentifiers.MarkerConfigUnknownSetting, StringComparison.Ordinal));
    }

    private static IIncrementalGenerator GeneratorFor(string markerFileName)
        => markerFileName switch
        {
            _ when markerFileName.StartsWith(".atc-rest-api-client", StringComparison.Ordinal) => new ApiClientGenerator(),
            _ when markerFileName.StartsWith(".atc-rest-api-server-handlers", StringComparison.Ordinal) => new ApiServerDomainGenerator(),
            _ => new ApiServerGenerator(),
        };

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