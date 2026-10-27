namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// Finds the component schemas that contain themselves. The extractors inline three things - the members of an
/// <c>allOf</c>, a bare <c>$ref</c> and the <c>items</c> of an array alias - so a cycle through them never ends
/// (a stack overflow no catch can report); a <c>$ref</c> from a property is a type reference, which is a tree and
/// fine. Each cycle is one error naming the chain (<c>ATC_API_SCH023</c>), and the document is not generated from.
/// </summary>
public static class SchemaCycleValidator
{
    /// <summary>
    /// Validates the component schemas for cycles through <c>allOf</c>, <c>$ref</c> and an array alias's <c>items</c>.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="sourceFilePath">The specification's path, for the diagnostics.</param>
    /// <returns>One error per cycle; empty when every schema can be inlined.</returns>
    public static List<DiagnosticMessage> Validate(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();
        var schemas = document?.Components?.Schemas;
        if (schemas is null || schemas.Count == 0)
        {
            return diagnostics;
        }

        var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var schema in schemas)
        {
            var targets = new List<string>();
            CollectInlinedReferences(schema.Value, targets);
            edges[schema.Key] = targets.Where(schemas.ContainsKey).Distinct(StringComparer.Ordinal).ToList();
        }

        var reported = new HashSet<string>(StringComparer.Ordinal);
        var state = new Dictionary<string, bool>(StringComparer.Ordinal); // true: on the current path, false: done
        var path = new List<string>();
        foreach (var name in schemas.Keys)
        {
            Walk(name, edges, state, path, reported, diagnostics, sourceFilePath);
        }

        return diagnostics;
    }

    /// <summary>
    /// The component names this schema inlines: the referenced members of its <c>allOf</c>, a bare reference, the
    /// referenced <c>items</c> of an array - each followed through inline schemas, never through a property.
    /// </summary>
    private static void CollectInlinedReferences(
        IOpenApiSchema? schema,
        List<string> targets)
    {
        switch (schema)
        {
            case OpenApiSchemaReference reference:
                if (reference.Reference?.Id is { } id)
                {
                    targets.Add(id);
                }

                break;

            case OpenApiSchema inline:
                foreach (var member in inline.AllOf ?? [])
                {
                    CollectInlinedReferences(member, targets);
                }

                if (inline.Items is not null && (inline.Properties is null || inline.Properties.Count == 0))
                {
                    CollectInlinedReferences(inline.Items, targets);
                }

                break;
        }
    }

    private static void Walk(
        string name,
        Dictionary<string, List<string>> edges,
        Dictionary<string, bool> state,
        List<string> path,
        HashSet<string> reported,
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath)
    {
        if (state.TryGetValue(name, out var onPath))
        {
            if (onPath)
            {
                Report(path.Skip(path.IndexOf(name)).Append(name).ToList(), reported, diagnostics, sourceFilePath);
            }

            return;
        }

        state[name] = true;
        path.Add(name);
        foreach (var target in edges[name])
        {
            Walk(target, edges, state, path, reported, diagnostics, sourceFilePath);
        }

        path.RemoveAt(path.Count - 1);
        state[name] = false;
    }

    private static void Report(
        List<string> cycle,
        HashSet<string> reported,
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath)
    {
        // One diagnostic per cycle, whichever member it was entered from
        var key = string.Join("\n", cycle.Take(cycle.Count - 1).OrderBy(n => n, StringComparer.Ordinal));
        if (!reported.Add(key))
        {
            return;
        }

        var chain = string.Join(" -> ", cycle);
        diagnostics.Add(new DiagnosticMessage(
            RuleIdentifiers.SchemaCycle,
            $"Schema '{cycle[0]}' contains itself: {chain}. A schema that inherits itself (allOf), aliases itself ($ref) or lists itself (the items of an array) cannot be generated.",
            DiagnosticSeverity.Error,
            sourceFilePath,
            Context: $"Schema: {cycle[0]}",
            Suggestions:
            [
                "Refer to the schema from a property instead (a tree), or break the cycle with a separate schema",
            ]));
    }
}