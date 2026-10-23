namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The TypedClient needs no Atc.Rest.Client: a project with the generator and Microsoft.Extensions.Http only builds,
/// for every scenario - or the generator says which package it needs (a Result-style client, a file upload), never a
/// compile error in generated code. The other gates compile with Atc.Rest.Client referenced, so they cannot tell.
/// </summary>
[RuleTest("ATC_API_DEP014")]
public sealed class TypedClientWithoutAtcRestClientTests
{
    public static TheoryData<string> Scenarios()
    {
        var data = new TheoryData<string>();
        foreach (var directory in new DirectoryInfo(GeneratorSnapshotHarness.GetScenariosRoot()).GetDirectories().OrderBy(d => d.Name, StringComparer.Ordinal))
        {
            if (File.Exists(Path.Combine(directory.FullName, directory.Name + ".yaml")) &&
                File.Exists(Path.Combine(directory.FullName, "Client-Typed", ".atc-rest-api-client")))
            {
                data.Add(directory.Name);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void TypedClient_WithoutAtcRestClient_CompilesOrNamesThePackage(
        string scenarioName)
    {
        var references = CompilationVerificationHarness.GetFullFrameworkReferencesWithout("Atc.Rest.Client");

        var (diagnostics, sources) = CompilationVerificationHarness.RunGenerator(
            new ApiClientGenerator(),
            scenarioName,
            scenarioName + ".yaml",
            ".atc-rest-api-client",
            "Client-Typed",
            references: references);

        // The package is needed exactly when the client uses it: its snapshots (made with the package) carry a file
        // (IFileContent), or the marker asks for the Result style. Then the generator names the package and stops
        var scenarioFolder = Path.Combine(GeneratorSnapshotHarness.GetScenariosRoot(), scenarioName, "Client-Typed");
        var needsPackage =
            Directory.GetFiles(scenarioFolder, "*.verified.cs").Any(f => File.ReadAllText(f).Contains("IFileContent", StringComparison.Ordinal)) ||
            File.ReadAllText(Path.Combine(scenarioFolder, ".atc-rest-api-client")).Contains("\"Result\"", StringComparison.Ordinal);
        var reported = diagnostics.Where(d => d.Id == "ATC_API_DEP014").ToList();
        Assert.Equal(needsPackage, reported.Count > 0);
        if (needsPackage)
        {
            Assert.Contains("Atc.Rest.Client", reported[0].GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            return;
        }

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources, references: references);
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors.Take(10)));
    }
}