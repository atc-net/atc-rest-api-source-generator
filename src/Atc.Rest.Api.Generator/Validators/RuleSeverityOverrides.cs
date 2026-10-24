namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// The severity a project gives a rule about its specification: the marker's <c>ruleSeverities</c> and
/// <c>suppressRules</c>, which the generator applies itself. A rule about the specification is reported in the
/// specification file, which is no C# file: an <c>.editorconfig</c> section's <c>dotnet_diagnostic.&lt;id&gt;.severity</c>
/// never reaches it (Roslyn gives a severity to the syntax trees it matches, never to a file's analyzer options); a
/// global config's does, applied by the compiler. An error stands for code the generator cannot write and keeps its
/// severity.
/// </summary>
public sealed class RuleSeverityOverrides
{
    private static readonly Lazy<IReadOnlyList<string>> KnownRules = new(() => typeof(RuleIdentifiers)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .Where(id => id.StartsWith("ATC_API_", StringComparison.Ordinal))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToList());

    private readonly Dictionary<string, string> severities;

    private RuleSeverityOverrides(Dictionary<string, string> severities)
        => this.severities = severities;

    /// <summary>
    /// Gets the overrides that change nothing.
    /// </summary>
    public static RuleSeverityOverrides None { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Whether the id is a rule of the generator (<c>ATC_API_*</c>).
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <returns>True for a rule the generator reports.</returns>
    public static bool IsRule(string id)
        => id is not null && KnownRules.Value.Contains(id, StringComparer.Ordinal);

    /// <summary>
    /// The overrides of a marker's <c>ruleSeverities</c> and <c>suppressRules</c>; a suppressed rule is off whatever
    /// its severity says.
    /// </summary>
    /// <param name="ruleSeverities">The marker's <c>ruleSeverities</c>: a rule id and its severity.</param>
    /// <param name="suppressRules">The marker's <c>suppressRules</c>.</param>
    /// <returns>The overrides.</returns>
    public static RuleSeverityOverrides From(
        IReadOnlyDictionary<string, string>? ruleSeverities,
        IEnumerable<string>? suppressRules)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in ruleSeverities ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(entry.Key) && !string.IsNullOrWhiteSpace(entry.Value))
            {
                map[entry.Key.Trim()] = entry.Value.Trim().ToLowerInvariant();
            }
        }

        foreach (var id in suppressRules ?? [])
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                map[id.Trim()] = "none";
            }
        }

        return map.Count == 0 ? None : new RuleSeverityOverrides(map);
    }

    /// <summary>
    /// The diagnostic with the severity the project gives its rule: null when the rule is off (<c>none</c>,
    /// <c>silent</c>), the severity of <c>suggestion</c> (information), <c>warning</c> or <c>error</c>, unchanged for
    /// <c>default</c>, an unknown value or an error.
    /// </summary>
    /// <param name="message">The diagnostic.</param>
    /// <returns>The diagnostic as reported, or null when it is not.</returns>
    public DiagnosticMessage? Apply(DiagnosticMessage message)
    {
        if (message is null ||
            message.Severity == DiagnosticSeverity.Error ||
            !severities.TryGetValue(message.RuleId, out var severity))
        {
            return message;
        }

        return severity switch
        {
            "none" or "silent" => null,
            "suggestion" => message with { Severity = DiagnosticSeverity.Info },
            "warning" => message with { Severity = DiagnosticSeverity.Warning },
            "error" => message with { Severity = DiagnosticSeverity.Error },
            _ => message,
        };
    }
}