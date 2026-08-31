namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// Validates that no two schema keys normalise to the same generated C# type name when
/// <see cref="ClientGranularityType.Single"/> flattens all models into one namespace.
/// Implements rule <c>ATC_API_CLT001</c>.
/// </summary>
/// <remarks>
/// OpenAPI guarantees that <c>components.schemas</c> keys are unique, but the generator normalises
/// those keys into C# identifiers via <c>ToPascalCaseForDotNet</c>, which collapses separators and
/// casing. Distinct keys such as <c>pet-status</c> and <c>pet_status</c> therefore both become
/// <c>PetStatus</c>. Under <see cref="ClientGranularityType.PerArea"/> the per-area namespaces hide
/// the clash; flattening to <c>{root}.Generated.Models</c> exposes it as a CS0101 in generated code
/// the developer cannot edit, so it is reported here as an actionable error instead.
/// </remarks>
public static class SingleClientCollisionValidator
{
    [SuppressMessage("Design", "S1075:Refactor your code not to use hardcoded absolute paths or URIs", Justification = "OK - stable public documentation URL surfaced in the diagnostic.")]
    private const string DocsBaseUrl = "https://github.com/atc-net/atc-rest-api-generator/blob/main/docs/analyzer-rules.md";

    /// <summary>
    /// Detects generated type-name collisions for single-client mode.
    /// </summary>
    /// <param name="document">The OpenAPI document to inspect.</param>
    /// <param name="granularity">The configured client granularity.</param>
    /// <param name="filePath">The specification file path, used for diagnostic location.</param>
    /// <returns>One diagnostic per colliding generated name; empty when there are no collisions or when granularity is not Single.</returns>
    public static IReadOnlyList<DiagnosticMessage> Validate(
        OpenApiDocument? document,
        ClientGranularityType granularity,
        string? filePath)
    {
        // PerArea keeps the historical per-area namespaces, which make collisions harmless.
        // Never evaluate it, so no existing project can newly fail validation.
        if (granularity != ClientGranularityType.Single)
        {
            return [];
        }

        var schemas = document?.Components?.Schemas;
        if (schemas is null || schemas.Count == 0)
        {
            return [];
        }

        // Ordinal grouping: C# is case-sensitive, so 'petStatus' and 'PetStatus' both normalising
        // to 'PetStatus' is a genuine collision rather than a false positive.
        var byGeneratedName = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var schemaKey in schemas.Keys)
        {
            if (string.IsNullOrEmpty(schemaKey))
            {
                continue;
            }

            var generatedName = CasingHelper.ToPascalCase(schemaKey);
            if (string.IsNullOrEmpty(generatedName))
            {
                continue;
            }

            if (!byGeneratedName.TryGetValue(generatedName, out var contributors))
            {
                contributors = [];
                byGeneratedName[generatedName] = contributors;
            }

            contributors.Add(schemaKey);
        }

        var diagnostics = new List<DiagnosticMessage>();

        foreach (var entry in byGeneratedName)
        {
            if (entry.Value.Count < 2)
            {
                continue;
            }

            // Report once per colliding name, not once per contributor, to avoid diagnostic spam.
            diagnostics.Add(CreateCollisionDiagnostic(entry.Key, entry.Value, filePath));
        }

        return diagnostics;
    }

    private static DiagnosticMessage CreateCollisionDiagnostic(
        string generatedName,
        List<string> contributors,
        string? filePath)
    {
        var sortedContributors = new List<string>(contributors);
        sortedContributors.Sort(StringComparer.Ordinal);

        var quoted = string.Join(", ", sortedContributors.ConvertAll(c => $"'{c}'"));

        return new DiagnosticMessage(
            RuleId: RuleIdentifiers.SingleClientTypeNameCollision,
            Message: $"Type name '{generatedName}' is generated from multiple schemas ({quoted}) " +
                     "and cannot be placed in a single flat namespace.",
            Severity: DiagnosticSeverity.Error,
            FilePath: filePath,
            Context: generatedName,
            Suggestions:
            [
                $"Rename one of the schemas so they no longer both normalise to '{generatedName}'",
                "Or use \"clientGranularity\": \"PerArea\" to keep per-area namespaces",
            ],
            DocumentationUrl: $"{DocsBaseUrl}#{RuleIdentifiers.SingleClientTypeNameCollision.ToLowerInvariant()}");
    }
}