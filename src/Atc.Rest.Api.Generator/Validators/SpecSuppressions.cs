namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// <c>x-atc-suppress</c>: the rules a specification switches off where they do not fit - on the document (every place),
/// a path item (its operations), an operation or a component schema. A rule about the specification is a warning or
/// information about how it is written; an error stands for code the generator cannot write and is never suppressed.
/// </summary>
public static class SpecSuppressions
{
    /// <summary>
    /// The extension's name.
    /// </summary>
    public const string ExtensionName = "x-atc-suppress";

    /// <summary>
    /// The scope of a diagnostic found at an operation.
    /// </summary>
    /// <param name="httpMethod">The operation's HTTP method.</param>
    /// <param name="path">The operation's path.</param>
    /// <returns>The scope.</returns>
    public static string Operation(
        string httpMethod,
        string path)
        => $"operation:{(httpMethod ?? string.Empty).ToUpperInvariant()} {path}";

    /// <summary>
    /// The scope of a diagnostic found at a path item.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The scope.</returns>
    public static string Path(string path)
        => $"path:{path}";

    /// <summary>
    /// The scope of a diagnostic found at a component schema.
    /// </summary>
    /// <param name="schemaName">The schema's name.</param>
    /// <returns>The scope.</returns>
    public static string Schema(string schemaName)
        => $"schema:{schemaName}";

    /// <summary>
    /// Leaves out the warnings and information the specification suppresses where they were found.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="diagnostics">The diagnostics, each with the scope it was found in (or none).</param>
    /// <returns>The diagnostics that are not suppressed.</returns>
    public static List<DiagnosticMessage> Apply(
        OpenApiDocument document,
        List<DiagnosticMessage> diagnostics)
    {
        if (document is null || diagnostics is null || diagnostics.Count == 0)
        {
            return diagnostics ?? [];
        }

        var everywhere = RulesOf(document.Extensions);
        var byScope = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var path in document.Paths ?? [])
        {
            if (path.Value is not { } pathItem)
            {
                continue;
            }

            var pathRules = RulesOf(pathItem.Extensions);
            Add(byScope, Path(path.Key), pathRules);
            foreach (var operation in pathItem.Operations ?? [])
            {
                var rules = new HashSet<string>(pathRules, StringComparer.Ordinal);
                rules.UnionWith(RulesOf(operation.Value?.Extensions));
                Add(byScope, Operation(operation.Key.ToString(), path.Key), rules);
            }
        }

        foreach (var schema in document.Components?.Schemas ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            var actual = schema.Value is OpenApiSchemaReference { Target: not null } reference ? reference.Target : schema.Value;
            Add(byScope, Schema(schema.Key), RulesOf(actual?.Extensions));
        }

        if (everywhere.Count == 0 && byScope.Count == 0)
        {
            return diagnostics;
        }

        return diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error ||
                        (!everywhere.Contains(d.RuleId) &&
                         !(d.Scope is { } scope && byScope.TryGetValue(scope, out var rules) && rules.Contains(d.RuleId))))
            .ToList();
    }

    private static void Add(
        Dictionary<string, HashSet<string>> byScope,
        string scope,
        HashSet<string> rules)
    {
        if (rules.Count > 0)
        {
            byScope[scope] = rules;
        }
    }

    /// <summary>
    /// The rule ids an <c>x-atc-suppress</c> lists: a list of ids, or one id.
    /// </summary>
    private static HashSet<string> RulesOf(
        IDictionary<string, IOpenApiExtension>? extensions)
    {
        var rules = new HashSet<string>(StringComparer.Ordinal);
        if (extensions is null ||
            !extensions.TryGetValue(ExtensionName, out var extension) ||
            extension is null)
        {
            return rules;
        }

        // The extension's JSON node (Microsoft.OpenApi keeps it in a Node property)
        switch (extension.GetType().GetProperty("Node")?.GetValue(extension))
        {
            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is JsonValue value && value.TryGetValue<string>(out var id) && !string.IsNullOrWhiteSpace(id))
                    {
                        rules.Add(id.Trim());
                    }
                }

                break;
            case JsonValue single when single.TryGetValue<string>(out var id) && !string.IsNullOrWhiteSpace(id):
                rules.Add(id.Trim());
                break;
        }

        return rules;
    }
}