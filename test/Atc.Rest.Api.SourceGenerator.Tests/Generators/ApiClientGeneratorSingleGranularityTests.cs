namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Pins the namespace layout produced by the Roslyn <see cref="ApiClientGenerator"/> when
/// <c>clientGranularity</c> is <c>Single</c>.
/// </summary>
/// <remarks>
/// The Roslyn generator computes namespaces independently of <c>CodeGenerationService</c>, so the
/// scenario snapshot tests in Atc.Rest.Api.Generator.IntegrationTests do not cover this path.
/// Single mode must flatten the client and its parameter records into <c>{root}.Generated</c>
/// instead of the per-area <c>{root}.Generated.Client</c>.
/// </remarks>
public class ApiClientGeneratorSingleGranularityTests
{
    private const string ScenarioName = "SingleTypedClient";
    private const string YamlFileName = "SingleTypedClient.yaml";

    [Fact]
    public void Single_EmitsClientIntoFlatGeneratedNamespace()
    {
        var sources = CompilationVerificationHarness.RunClient(ScenarioName, YamlFileName);

        var client = sources.Single(x => x.HintName.Contains("SingleTypedApiClient", StringComparison.Ordinal));

        Assert.Contains("namespace SingleTypedClient.Generated;", client.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace SingleTypedClient.Generated.Client;", client.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Single_EmitsParametersIntoFlatGeneratedNamespace()
    {
        var sources = CompilationVerificationHarness.RunClient(ScenarioName, YamlFileName);

        var parameters = sources
            .Where(x => x.HintName.Contains("Parameters", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(parameters);

        foreach (var parameter in parameters)
        {
            Assert.Contains("namespace SingleTypedClient.Generated;", parameter.Source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Single_EmitsModelsIntoFlatModelsNamespace()
    {
        var sources = CompilationVerificationHarness.RunClient(ScenarioName, YamlFileName);

        var models = sources
            .Where(x => x.HintName.Contains(".Generated.Models.", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(models);

        foreach (var model in models)
        {
            Assert.Contains("namespace SingleTypedClient.Generated.Models;", model.Source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Single_HonoursClientNameVerbatimWithoutAppendingSuffix()
    {
        var sources = CompilationVerificationHarness.RunClient(ScenarioName, YamlFileName);

        var client = sources.Single(x => x.HintName.Contains("SingleTypedApiClient", StringComparison.Ordinal));

        Assert.Contains("public sealed class SingleTypedApiClient", client.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("SingleTypedApiClientClient", client.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Single_GeneratedSourcesCompileCleanly()
    {
        var sources = CompilationVerificationHarness.RunClient(ScenarioName, YamlFileName);

        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources);

        Assert.Empty(errors);
    }
}