namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// Puts a diagnostic found at an operation, a path item or a component schema (its <see cref="DiagnosticMessage.Scope"/>,
/// see <see cref="SpecSuppressions"/>) at that place in the specification: the line of the operation's method key
/// (<c>get:</c>), of the path's key (<c>/pets:</c>) or of the schema's name (<c>Pet:</c>), so an IDE takes you there
/// instead of to the first line. Microsoft.OpenApi keeps no positions; the keys are found in the text - YAML by its
/// indentation, JSON in the order the keys appear. In a specification of several files the file that declares the place
/// is the diagnostic's. A place that is not found (an operation an overlay adds, say) keeps the diagnostic where it was.
/// </summary>
public static class SpecLocations
{
    /// <summary>
    /// Locates the diagnostics that have a scope and no line yet.
    /// </summary>
    /// <param name="diagnostics">The diagnostics; located ones are replaced in place.</param>
    /// <param name="sources">The specification's files and their text: the base file first, then its parts.</param>
    public static void Apply(
        IList<DiagnosticMessage> diagnostics,
        IReadOnlyList<(string FilePath, string Text)> sources)
    {
        if (diagnostics is null || sources is null || sources.Count == 0)
        {
            return;
        }

        var indexes = sources
            .Where(s => !string.IsNullOrEmpty(s.Text))
            .Select(s => (s.FilePath, Index: new KeyIndex(s.Text)))
            .ToList();
        var found = new Dictionary<string, (string FilePath, int Line, int Column)?>(StringComparer.Ordinal);
        for (var i = 0; i < diagnostics.Count; i++)
        {
            if (diagnostics[i] is not { LineNumber: null, Scope: { } scope })
            {
                continue;
            }

            if (!found.TryGetValue(scope, out var place))
            {
                place = Locate(scope, indexes);
                found[scope] = place;
            }

            if (place is { } at)
            {
                diagnostics[i] = diagnostics[i] with { FilePath = at.FilePath, LineNumber = at.Line, ColumnNumber = at.Column };
            }
        }
    }

    private static (string FilePath, int Line, int Column)? Locate(
        string scope,
        List<(string FilePath, KeyIndex Index)> indexes)
    {
        string[]? keys = null;
        if (scope.StartsWith("operation:", StringComparison.Ordinal))
        {
            // operation:GET /pets
            var rest = scope.Substring("operation:".Length);
            var space = rest.IndexOf(' ');
            if (space > 0)
            {
                keys = ["paths", rest.Substring(space + 1), rest.Substring(0, space).ToLowerInvariant()];
            }
        }
        else if (scope.StartsWith("path:", StringComparison.Ordinal))
        {
            keys = ["paths", scope.Substring("path:".Length)];
        }
        else if (scope.StartsWith("schema:", StringComparison.Ordinal))
        {
            keys = ["components", "schemas", scope.Substring("schema:".Length)];
        }

        if (keys is null)
        {
            return null;
        }

        foreach (var (filePath, index) in indexes)
        {
            if (index.Find(keys) is { } at)
            {
                return (filePath, at.Line, at.Column);
            }
        }

        return null;
    }

    // The keys of a YAML or JSON text, with their lines (1-based) and columns
    private sealed class KeyIndex
    {
        private readonly string[] lines;
        private readonly bool isJson;

        public KeyIndex(string text)
        {
            lines = text.Replace("\r\n", "\n").Split('\n');
            isJson = text.TrimStart().StartsWith("{", StringComparison.Ordinal);
        }

        public (int Line, int Column)? Find(string[] keys)
            => isJson ? FindJson(keys) : FindYaml(keys);

        // YAML: each key is a child of the one before it - more indented, before the parent's block ends
        private (int Line, int Column)? FindYaml(string[] keys)
        {
            var start = 0;
            var end = lines.Length;
            var parentIndent = -1;
            (int Line, int Column)? at = null;
            foreach (var key in keys)
            {
                if (FindChild(key, start, end, parentIndent) is not { } line)
                {
                    return null;
                }

                var indent = lines[line].Length - lines[line].TrimStart(' ').Length;
                at = (line + 1, indent + 1);
                parentIndent = indent;
                start = line + 1;
                end = BlockEnd(line, indent);
            }

            return at;
        }

        // The line of a key in a block: the first key more indented than its parent, before the block ends
        private int? FindChild(
            string key,
            int start,
            int end,
            int parentIndent)
        {
            for (var i = start; i < end; i++)
            {
                var content = lines[i].TrimStart(' ');
                if (content.Length == 0 || content.StartsWith("#", StringComparison.Ordinal) || content.StartsWith("-", StringComparison.Ordinal))
                {
                    continue;
                }

                if (lines[i].Length - content.Length <= parentIndent)
                {
                    return null;
                }

                if (KeyOf(content) is { } name && string.Equals(name, key, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return null;
        }

        // The line after the last one of the block a key at this indent opens
        private int BlockEnd(
            int keyLine,
            int indent)
        {
            for (var i = keyLine + 1; i < lines.Length; i++)
            {
                var content = lines[i].TrimStart(' ');
                if (content.Length == 0 || content.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (lines[i].Length - content.Length <= indent)
                {
                    return i;
                }
            }

            return lines.Length;
        }

        // A mapping key: plain, or in single or double quotes
        private static string? KeyOf(string content)
        {
            if (content[0] is '"' or '\'')
            {
                var close = content.IndexOf(content[0], 1);
                return close > 0 && content.Length > close + 1 && content[close + 1] == ':'
                    ? content.Substring(1, close - 1)
                    : null;
            }

            var colon = content.IndexOf(": ", StringComparison.Ordinal);
            if (colon < 0 && content.EndsWith(":", StringComparison.Ordinal))
            {
                colon = content.Length - 1;
            }

            return colon > 0 ? content.Substring(0, colon) : null;
        }

        // JSON: each key is the next "key": after the one before it
        private (int Line, int Column)? FindJson(string[] keys)
        {
            var line = 0;
            var column = 0;
            foreach (var key in keys)
            {
                var needle = "\"" + key + "\"";
                var found = false;
                for (; line < lines.Length; line++, column = 0)
                {
                    var at = lines[line].IndexOf(needle, column, StringComparison.Ordinal);
                    if (at >= 0 && lines[line].Substring(at + needle.Length).TrimStart().StartsWith(":", StringComparison.Ordinal))
                    {
                        column = at + needle.Length;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return null;
                }
            }

            var keyColumn = lines[line].LastIndexOf('"', column - 2);
            return (line + 1, keyColumn + 1);
        }
    }
}