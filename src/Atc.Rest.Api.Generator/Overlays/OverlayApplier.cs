namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// Applies an OpenAPI Overlay (Overlay Specification 1.0 to 1.2) to a document.
/// <para>
/// The overlay is checked first - its version, <c>info</c>, every action's fields and JSONPath - and an overlay with
/// an error is not applied at all. The actions then run in order, each on the result of the one before: <c>remove</c>
/// deletes the target nodes, <c>copy</c> merges the single node its JSONPath selects into them, and <c>update</c>
/// merges its value into them (an object recursively, an array by concatenation or by appending, a primitive by
/// replacing). An action whose target selects nothing changes nothing, as the specification says - it is reported as a
/// warning, because a fix that silently stopped applying (the vendor renamed the path) is what overlays go wrong with.
/// </para>
/// </summary>
public static class OverlayApplier
{
    private static readonly Regex SupportedVersion = new(@"^1\.(?<minor>[0-2])(\.\d+)?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

    private static readonly HashSet<string> OverlayFields = new(StringComparer.Ordinal) { "overlay", "info", "actions", "extends", "$self", "components" };
    private static readonly HashSet<string> InfoFields = new(StringComparer.Ordinal) { "title", "version", "description" };
    private static readonly HashSet<string> ActionFields = new(StringComparer.Ordinal) { "target", "description", "update", "copy", "remove" };
    private static readonly HashSet<string> ReferenceFields = new(StringComparer.Ordinal) { "$ref", "target", "description" };
    private static readonly HashSet<string> ReusableActionFields = new(StringComparer.Ordinal) { "description", "fields" };

    /// <summary>
    /// Applies an overlay.
    /// </summary>
    /// <param name="document">The document (an OpenAPI description); it is not changed.</param>
    /// <param name="overlay">The overlay.</param>
    /// <returns>The changed document and the problems found; the original document when there is an error.</returns>
    public static OverlayApplication Apply(
        JsonNode? document,
        JsonNode? overlay)
    {
        var problems = new List<OverlayProblem>();
        var actions = ReadOverlay(overlay, problems);
        if (actions is null || problems.Any(p => p.IsError))
        {
            return new OverlayApplication(document, problems);
        }

        var result = document?.DeepClone();
        foreach (var action in actions)
        {
            result = ApplyAction(result, action, problems);
            if (problems.Any(p => p.IsError))
            {
                return new OverlayApplication(document, problems);
            }
        }

        return new OverlayApplication(result, problems);
    }

    private static List<OverlayAction>? ReadOverlay(
        JsonNode? overlay,
        List<OverlayProblem> problems)
    {
        if (overlay is not JsonObject root)
        {
            problems.Add(Error(null, "An overlay is a YAML or JSON object with 'overlay', 'info' and 'actions'."));
            return null;
        }

        RejectUnknownFields(root, OverlayFields, "the overlay", null, problems);

        var version = root["overlay"] is JsonValue versionValue ? versionValue.ToString() : null;
        var match = version is null ? null : SupportedVersion.Match(version);
        if (match is null || !match.Success)
        {
            problems.Add(Error(null, $"Overlay version '{version ?? "(missing)"}' is not supported: the overlay field must be 1.0.x, 1.1.x or 1.2.x."));
            return null;
        }

        var minor = int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture);

        if (root["info"] is not JsonObject info ||
            info["title"] is not JsonValue title || OverlayValueKind(title) != JsonValueKind.String ||
            info["version"] is not JsonValue)
        {
            problems.Add(Error(null, "The overlay needs 'info' with a 'title' and a 'version'."));
        }
        else
        {
            RejectUnknownFields(info, InfoFields, "info", null, problems);
        }

        var reusable = ReadReusableActions(root, minor, problems);

        if (root["actions"] is not JsonArray actionNodes || actionNodes.Count == 0)
        {
            problems.Add(Error(null, "The overlay needs at least one action in 'actions'."));
            return null;
        }

        var actions = new List<OverlayAction>();
        for (var i = 0; i < actionNodes.Count; i++)
        {
            if (ReadAction(actionNodes[i], i, minor, reusable, problems) is { } action)
            {
                actions.Add(action);
            }
        }

        return actions;
    }

    private static Dictionary<string, JsonObject?> ReadReusableActions(
        JsonObject root,
        int minor,
        List<OverlayProblem> problems)
    {
        var reusable = new Dictionary<string, JsonObject?>(StringComparer.Ordinal);
        if (root["components"] is null)
        {
            return reusable;
        }

        if (minor < 2)
        {
            problems.Add(Error(null, "'components' (reusable actions) needs overlay version 1.2 or later."));
            return reusable;
        }

        if (root["components"] is not JsonObject components)
        {
            problems.Add(Error(null, "'components' must be an object."));
            return reusable;
        }

        RejectUnknownFields(components, new HashSet<string>(StringComparer.Ordinal) { "actions" }, "components", null, problems);
        if (components["actions"] is not JsonObject definitions)
        {
            return reusable;
        }

        foreach (var definition in definitions)
        {
            if (definition.Value is not JsonObject body)
            {
                problems.Add(Error(null, $"The reusable action '{definition.Key}' must be an object."));
                continue;
            }

            RejectUnknownFields(body, ReusableActionFields, $"the reusable action '{definition.Key}'", null, problems);
            if (body["fields"] is not JsonObject fields)
            {
                problems.Add(Error(null, $"The reusable action '{definition.Key}' needs 'fields'."));
                continue;
            }

            if (fields.ContainsKey("target"))
            {
                problems.Add(Error(null, $"The reusable action '{definition.Key}' must not have a 'target' in 'fields': the reference gives it."));
                reusable[definition.Key] = null;
                continue;
            }

            reusable[definition.Key] = fields;
        }

        return reusable;
    }

    private static OverlayAction? ReadAction(
        JsonNode? node,
        int index,
        int minor,
        Dictionary<string, JsonObject?> reusable,
        List<OverlayProblem> problems)
    {
        if (node is not JsonObject action)
        {
            problems.Add(Error(index, "An action must be an object."));
            return null;
        }

        var description = action["description"] is JsonValue d ? d.ToString() : null;
        var fields = action;
        if (action["$ref"] is not null)
        {
            if (minor < 2)
            {
                problems.Add(Error(index, "'$ref' (a reusable action) needs overlay version 1.2 or later."));
                return null;
            }

            RejectUnknownFields(action, ReferenceFields, "the action", index, problems);
            var reference = action["$ref"] is JsonValue r ? r.ToString() : string.Empty;
            const string Prefix = "#/components/actions/";
            if (!reference.StartsWith(Prefix, StringComparison.Ordinal) ||
                !reusable.TryGetValue(reference.Substring(Prefix.Length), out var referenced))
            {
                problems.Add(Error(index, $"'$ref: {reference}' names no reusable action (it is missing from components.actions)."));
                return null;
            }

            // An invalid reusable action was reported already
            if (referenced is null)
            {
                return null;
            }

            fields = referenced;
            description ??= referenced["description"] is JsonValue rd ? rd.ToString() : null;
        }
        else
        {
            RejectUnknownFields(action, ActionFields, "the action", index, problems);
        }

        if (action["target"] is not JsonValue targetValue || OverlayValueKind(targetValue) != JsonValueKind.String)
        {
            problems.Add(Error(index, "An action needs a 'target' (a JSONPath expression)."));
            return null;
        }

        var target = ParsePath(targetValue.ToString(), "target", index, problems);
        JsonPathQuery? copy = null;
        if (fields["copy"] is { } copyNode)
        {
            if (minor < 1)
            {
                problems.Add(Error(index, "'copy' needs overlay version 1.1 or later."));
            }
            else if (copyNode is JsonValue copyValue && OverlayValueKind(copyValue) == JsonValueKind.String)
            {
                copy = ParsePath(copyValue.ToString(), "copy", index, problems);
            }
            else
            {
                problems.Add(Error(index, "'copy' must be a JSONPath expression."));
            }
        }

        var remove = false;
        if (fields["remove"] is { } removeNode)
        {
            var kind = OverlayValueKind(removeNode);
            if (kind is not (JsonValueKind.True or JsonValueKind.False))
            {
                problems.Add(Error(index, "'remove' must be true or false."));
            }

            remove = kind == JsonValueKind.True;
        }

        return target is null
            ? null
            : new OverlayAction(index, target, description, fields.ContainsKey("update"), fields["update"], copy, remove);
    }

    private static JsonPathQuery? ParsePath(
        string text,
        string field,
        int index,
        List<OverlayProblem> problems)
    {
        try
        {
            return JsonPathQuery.Parse(text);
        }
        catch (JsonPathException ex)
        {
            problems.Add(Error(index, $"'{field}' is no valid RFC 9535 JSONPath: {ex.Message}"));
            return null;
        }
    }

    private static void RejectUnknownFields(
        JsonObject obj,
        HashSet<string> known,
        string where,
        int? index,
        List<OverlayProblem> problems)
    {
        foreach (var member in obj)
        {
            if (!known.Contains(member.Key) && !member.Key.StartsWith("x-", StringComparison.Ordinal))
            {
                problems.Add(Error(index, $"'{member.Key}' is no field of {where} (an extension starts with 'x-')."));
            }
        }
    }

    private static JsonNode? ApplyAction(
        JsonNode? document,
        OverlayAction action,
        List<OverlayProblem> problems)
    {
        List<JsonPathMatch> targets;
        try
        {
            targets = Distinct(action.Target.Select(document));
        }
        catch (JsonPathException ex)
        {
            problems.Add(Error(action.Index, $"'target' {action.Target.Text} could not be evaluated: {ex.Message}"));
            return document;
        }

        if (targets.Count == 0)
        {
            problems.Add(new OverlayProblem(
                IsError: false,
                action.Index,
                $"The target {action.Target.Text} selects nothing, so the action changes nothing" + (action.Description is null ? "." : $" ({action.Description}).")));
            return document;
        }

        if (action.Remove)
        {
            if (targets.Any(t => t.IsRoot))
            {
                problems.Add(Error(action.Index, $"The target {action.Target.Text} selects the root of the document, which cannot be removed."));
                return document;
            }

            // Array elements from the highest index down, so the indexes of the others stay right
            foreach (var target in targets.OrderByDescending(t => t.ArrayIndex))
            {
                switch (target.Container)
                {
                    case JsonObject obj:
                        obj.Remove(target.MemberName!);
                        break;
                    case JsonArray array:
                        array.RemoveAt(target.ArrayIndex);
                        break;
                }
            }

            return document;
        }

        JsonNode? value;
        string source;
        if (action.Copy is not null)
        {
            IReadOnlyList<JsonPathMatch> sources;
            try
            {
                sources = action.Copy.Select(document);
            }
            catch (JsonPathException ex)
            {
                problems.Add(Error(action.Index, $"'copy' {action.Copy.Text} could not be evaluated: {ex.Message}"));
                return document;
            }

            if (sources.Count != 1)
            {
                problems.Add(Error(action.Index, $"'copy' must select exactly one node, but {action.Copy.Text} selects {sources.Count}."));
                return document;
            }

            // A copy of the node as it is now: the source may lie inside the target it is merged into
            value = sources[0].Value?.DeepClone();
            source = "copy";
        }
        else if (action.HasUpdate)
        {
            value = action.Update;
            source = "update";
        }
        else
        {
            problems.Add(new OverlayProblem(IsError: false, action.Index, "The action has no 'update', 'copy' or 'remove', so it changes nothing."));
            return document;
        }

        var kinds = targets.Select(t => Shape(t.Value)).Distinct(StringComparer.Ordinal).ToList();
        if (kinds.Count > 1)
        {
            problems.Add(Error(action.Index, $"The target {action.Target.Text} selects a mix of objects, arrays and primitive values; an {source} applies to one kind."));
            return document;
        }

        foreach (var target in targets)
        {
            switch (target.Value)
            {
                case JsonObject obj:
                    if (value is not JsonObject objectValue)
                    {
                        problems.Add(Error(action.Index, $"The target {action.Target.Text} selects objects, so the {source} must be an object."));
                        return document;
                    }

                    if (Merge(obj, objectValue, target.NormalizedPath) is { } mergeError)
                    {
                        problems.Add(Error(action.Index, mergeError));
                        return document;
                    }

                    break;
                case JsonArray array:
                    if (value is JsonArray arrayValue)
                    {
                        foreach (var item in arrayValue)
                        {
                            array.Add(item?.DeepClone());
                        }
                    }
                    else
                    {
                        array.Add(value?.DeepClone());
                    }

                    break;
                default:
                    if (value is JsonObject or JsonArray)
                    {
                        problems.Add(Error(action.Index, $"The target {action.Target.Text} selects primitive values, so the {source} must be a primitive value."));
                        return document;
                    }

                    if (target.IsRoot)
                    {
                        return value?.DeepClone();
                    }

                    Replace(target, value?.DeepClone());
                    break;
            }
        }

        return document;
    }

    // Overlay 1.2 section 4.5.3: insert what is new, replace a primitive, concatenate arrays, merge objects
    private static string? Merge(
        JsonObject target,
        JsonObject update,
        string path)
    {
        foreach (var member in update)
        {
            if (!target.TryGetPropertyValue(member.Key, out var existing))
            {
                target[member.Key] = member.Value?.DeepClone();
                continue;
            }

            switch (existing, member.Value)
            {
                case (JsonObject existingObject, JsonObject updateObject):
                    if (Merge(existingObject, updateObject, path + "['" + member.Key + "']") is { } error)
                    {
                        return error;
                    }

                    break;
                case (JsonArray existingArray, JsonArray updateArray):
                    foreach (var item in updateArray)
                    {
                        existingArray.Add(item?.DeepClone());
                    }

                    break;
                case (JsonObject or JsonArray, _):
                case (_, JsonObject or JsonArray):
                    return $"'{member.Key}' in {path} is {Describe(existing)} and the update gives {Describe(member.Value)}: they cannot be merged. Remove it first to replace it.";
                default:
                    target[member.Key] = member.Value?.DeepClone();
                    break;
            }
        }

        return null;
    }

    private static void Replace(
        JsonPathMatch target,
        JsonNode? value)
    {
        switch (target.Container)
        {
            case JsonObject obj:
                obj[target.MemberName!] = value;
                break;
            case JsonArray array:
                array[target.ArrayIndex] = value;
                break;
        }
    }

    // A node selected twice (a[0,0], overlapping wildcards) is changed once
    private static List<JsonPathMatch> Distinct(
        IReadOnlyList<JsonPathMatch> matches)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return matches.Where(m => seen.Add(m.NormalizedPath)).ToList();
    }

    private static string Shape(JsonNode? node)
        => node switch
        {
            JsonObject => "object",
            JsonArray => "array",
            _ => "primitive",
        };

    private static string Describe(JsonNode? node)
        => node switch
        {
            JsonObject => "an object",
            JsonArray => "an array",
            _ => "a primitive value",
        };

    private static JsonValueKind OverlayValueKind(JsonNode? node)
        => JsonPathEvaluator.Kind(node);

    private static OverlayProblem Error(
        int? index,
        string message)
        => new(IsError: true, index, message);
}