namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Regression tests for inline (anonymous) schema models in <c>EndpointPerOperation</c> mode.
/// <para>
/// A response or request body may declare an anonymous object schema instead of a
/// <c>$ref</c> into <c>components.schemas</c>. The generator materializes a record for it - for
/// example <c>GetAnalyticsSummaryResponse</c> - into the segment's Models namespace.
/// </para>
/// <para>
/// Previously <c>hasSegmentModels</c> was derived purely from the count of named component schemas,
/// so a segment whose only models were inline evaluated to <see langword="false"/> and the consuming
/// endpoint/result files omitted the Models <c>using</c>. The types were emitted but unreachable,
/// producing CS0246. Snapshot tests compare text and never caught it.
/// </para>
/// </summary>
public class InlineSchemaModelUsingTests
{
    private const string ScenarioName = "InlineSchemas";
    private const string YamlFileName = "InlineSchemas.yaml";

    [Fact]
    public void InlineSchemaScenario_CompilesWithoutErrors()
    {
        // Arrange & Act
        var generatedSources = RunGenerator();

        var errors = CompilationVerificationHarness.CompileGeneratedSources(generatedSources);

        // Assert - the whole point: inline-model types must be reachable from their consumers.
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("InlineSchemas.Generated.Analytics.Models.GetAnalyticsSummaryResponse.g.cs")]
    [InlineData("InlineSchemas.Generated.Reports.Models.GetReportResponse.g.cs")]
    [InlineData("InlineSchemas.Generated.Reports.Models.ListReportsResponseItem.g.cs")]
    public void InlineSchemaModels_AreGenerated(string expectedHintNameSuffix)
    {
        // Arrange & Act
        var generatedSources = RunGenerator();

        // Assert
        Assert.Contains(
            generatedSources,
            s => s.HintName.EndsWith(expectedHintNameSuffix, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Analytics.Endpoints.Results.GetAnalyticsSummaryEndpointResult.g.cs", "InlineSchemas.Generated.Analytics.Models")]
    [InlineData("Reports.Endpoints.Results.GetReportEndpointResult.g.cs", "InlineSchemas.Generated.Reports.Models")]
    public void ConsumersOfInlineModels_ImportTheModelsNamespace(
        string hintNameSuffix,
        string expectedUsing)
    {
        // Arrange
        var generatedSources = RunGenerator();

        // Act
        var source = generatedSources
            .Single(s => s.HintName.EndsWith(hintNameSuffix, StringComparison.Ordinal))
            .Source;

        // Assert - the segment has no named component schemas, only inline ones, so this using
        // is present only when inline models are counted towards 'hasSegmentModels'.
        Assert.Contains($"using {expectedUsing};", source, StringComparison.Ordinal);
    }

    private static List<(string HintName, string Source)> RunGenerator()
        => CompilationVerificationHarness.RunGenerator(
            new ApiClientGenerator(),
            ScenarioName,
            YamlFileName,
            ".atc-rest-api-client",
            "Client-Operation",
            useFullReferences: true).GeneratedSources;
}