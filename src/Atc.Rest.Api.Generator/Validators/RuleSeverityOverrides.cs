namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// The severity a project gives a rule about its specification: <c>dotnet_diagnostic.&lt;id&gt;.severity</c> in an
/// <c>.editorconfig</c> section that matches the specification file (or a global config), and the marker's
/// <c>suppressRules</c>. The compiler applies those settings to C# files only, and a rule about the specification is
/// reported in the specification file, so the generator applies them itself. An error stands for code the generator
/// cannot write and keeps its severity.
/// </summary>
public sealed class RuleSeverityOverrides
{
    private const string Prefix = "dotnet_diagnostic.";
    private const string Suffix = ".severity";

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
    /// Reads the <c>dotnet_diagnostic.&lt;id&gt;.severity</c> of every rule from a file's analyzer options, as one
    /// comparable text (<c>ID=severity;…</c>, in id order) - an incremental generator compares what it caches.
    /// </summary>
    /// <param name="tryGetValue">The file's analyzer options (<c>AnalyzerConfigOptions.TryGetValue</c>).</param>
    /// <returns>The severities as text, empty when no rule has one.</returns>
    public static string ReadSeverities(TryGetOption tryGetValue)
    {
        if (tryGetValue is null)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var id in KnownRules.Value)
        {
            if (tryGetValue(Prefix + id + Suffix, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                parts.Add(id + "=" + value!.Trim().ToLowerInvariant());
            }
        }

        return string.Join(";", parts);
    }

    /// <summary>
    /// The overrides of a specification file's severities (see <see cref="ReadSeverities"/>) and a marker's
    /// <c>suppressRules</c>; a suppressed rule is off whatever its severity says.
    /// </summary>
    /// <param name="severities">The severities as <see cref="ReadSeverities"/> writes them.</param>
    /// <param name="suppressRules">The marker's <c>suppressRules</c>.</param>
    /// <returns>The overrides.</returns>
    public static RuleSeverityOverrides From(
        string? severities,
        IEnumerable<string>? suppressRules)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in (severities ?? string.Empty).Split(';'))
        {
            var separator = part.IndexOf('=');
            if (separator > 0)
            {
                map[part.Substring(0, separator)] = part.Substring(separator + 1);
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

    /// <summary>
    /// Reads one analyzer option (<c>AnalyzerConfigOptions.TryGetValue</c>), so this library does not reference Roslyn.
    /// </summary>
    /// <param name="key">The option's key.</param>
    /// <param name="value">The option's value.</param>
    /// <returns>Whether the option is set.</returns>
    public delegate bool TryGetOption(
        string key,
        out string? value);
}