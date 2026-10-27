namespace Atc.Rest.Api.SourceGenerator.Tests.Helpers;

/// <summary>
/// The rules as a reader sees them, pinned: every Roslyn descriptor (its id, severity, category, title and message), and
/// every diagnostic the validator reports for every scenario specification under each strategy. A change to how the
/// rules are organized must leave both exactly as they are; a change to a rule shows here as a reviewed snapshot change.
/// </summary>
public sealed class RuleCatalogCharacterizationTests
{
    [Fact]
    public Task Descriptors_AreAsPublished()
    {
        var lines = typeof(DiagnosticHelpers)
            .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
            .Select(f => (DiagnosticDescriptor)f.GetValue(null)!)
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ThenBy(d => d.Title.ToString(CultureInfo.InvariantCulture), StringComparer.Ordinal)
            .Select(d => string.Join(
                " | ",
                d.Id,
                d.DefaultSeverity,
                d.Category,
                d.Title.ToString(CultureInfo.InvariantCulture),
                d.MessageFormat.ToString(CultureInfo.InvariantCulture)));

        return Verifier.Verify(string.Join("\n", lines), "txt");
    }

    [Fact]
    public async Task Diagnostics_OfEveryScenario_AreAsReported()
    {
        var root = Generators.GeneratorSnapshotHarness.GetScenariosRoot();
        var lines = new List<string>();
        foreach (var scenario in Directory.GetDirectories(root).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal))
        {
            var specPath = Path.Combine(root, scenario!, scenario + ".yaml");
            if (!File.Exists(specPath))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(specPath, TestContext.Current.CancellationToken);
            foreach (var strategy in new[] { ValidateSpecificationStrategy.Standard, ValidateSpecificationStrategy.Strict })
            {
                // The specification as every generator reads it; a path per strategy, as a parsed document is normalized
                var result = Atc.Rest.Api.Generator.Services.SpecificationPipeline.ParseAndValidate($"characterization/{strategy}/{scenario}.yaml", content, strategy);
                if (result.Document is null)
                {
                    lines.Add($"{scenario} | {strategy} | not parsed");
                    continue;
                }

                var diagnostics = result.Diagnostics;
                lines.AddRange(diagnostics.Select(d => $"{scenario} | {strategy} | {d.RuleId} | {d.Severity} | {d.LineNumber?.ToString(CultureInfo.InvariantCulture) ?? "-"} | {d.Message}"));
            }
        }

        await Verifier.Verify(string.Join("\n", lines), "txt");
    }
}