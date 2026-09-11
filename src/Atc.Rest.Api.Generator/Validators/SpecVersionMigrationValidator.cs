namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// Validates constructs whose meaning depends on the declared OpenAPI version — the ones a
/// specification author has to revisit when moving a document from 3.0 to 3.1 or later, and the
/// ones that never meant what they look like they mean.
/// </summary>
/// <remarks>
/// These rules read the raw specification text rather than the parsed document. That is not a
/// shortcut: in an OpenAPI 3.0 document the parser recognises <c>nullable</c>, consumes it, and then
/// discards it when there is no <c>type</c> in the same Schema Object for it to modify. The keyword
/// reaches neither the type flags nor <c>UnrecognizedKeywords</c>, so by the time validation sees an
/// <see cref="OpenApiDocument"/> there is nothing left to report on. Reading the text is the only
/// way to see it — and it has the side benefit of giving every diagnostic a real line number.
/// <para>
/// The rules are arranged so that a correct document is silent at every version. A 3.0 document is
/// only told about declarations that are inert <em>today</em>; the migration advice fires once the
/// author has actually raised <c>openapi:</c>, which is the moment it becomes actionable.
/// </para>
/// </remarks>
public static class SpecVersionMigrationValidator
{
    /// <summary>
    /// Runs the version-migration rules.
    /// </summary>
    /// <param name="diagnostics">The list to add diagnostics to.</param>
    /// <param name="sourceFilePath">Path to the source OpenAPI file for error reporting.</param>
    /// <param name="document">The parsed document, used for its declared spec version.</param>
    /// <param name="sourceText">The raw specification text, or <see langword="null"/> when the caller does not have it.</param>
    public static void Validate(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document,
        string? sourceText)
    {
        if (diagnostics is null ||
            document is null ||
            string.IsNullOrEmpty(sourceText))
        {
            return;
        }

        var lines = SplitLines(sourceText!);
        var specVersion = document.GetOpenApiSpecVersion();

        if (specVersion == OpenApiSpecVersion.OpenApi3_0)
        {
            ValidateNullableWithoutSiblingType(diagnostics, sourceFilePath, lines);
            ValidateTypeArrayNotSupported(diagnostics, sourceFilePath, lines);
        }
        else if (specVersion >= OpenApiSpecVersion.OpenApi3_1)
        {
            ValidateNullableKeywordRemoved(diagnostics, sourceFilePath, lines);
            ValidateExclusiveBoundBooleanForm(diagnostics, sourceFilePath, lines);
        }
    }

    /// <summary>
    /// Emits ATC_API_VER003 for a boolean <c>exclusiveMinimum</c> or <c>exclusiveMaximum</c> in an
    /// OpenAPI 3.1 or later document.
    /// </summary>
    /// <remarks>
    /// In 3.0 the bound and its exclusivity were two keywords: <c>maximum: 10</c> said where, and
    /// <c>exclusiveMaximum: true</c> said whether the endpoint was included. JSON Schema — and so
    /// 3.1 — folds them into one: <c>exclusiveMaximum: 10</c> is the bound. A boolean left behind at
    /// 3.1 is not a bound, so the exclusivity is dropped and the range quietly widens by one
    /// endpoint. Unlike <c>nullable</c> this is rare enough to report per declaration.
    /// </remarks>
    private static void ValidateExclusiveBoundBooleanForm(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        IReadOnlyList<YamlLine> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!line.IsKey ||
                !string.Equals(line.Value, "true", StringComparison.Ordinal))
            {
                continue;
            }

            string companionKey;
            if (string.Equals(line.Key, "exclusiveMaximum", StringComparison.Ordinal))
            {
                companionKey = "maximum";
            }
            else if (string.Equals(line.Key, "exclusiveMinimum", StringComparison.Ordinal))
            {
                companionKey = "minimum";
            }
            else
            {
                continue;
            }

            var bound = FindSibling(lines, i, companionKey)?.Value;
            var suggestions = new List<string>();

            if (!string.IsNullOrEmpty(bound))
            {
                suggestions.Add($"{line.Key}: {bound}");
                suggestions.Add($"Remove the '{companionKey}: {bound}' line - the bound now lives on '{line.Key}'.");
            }
            else
            {
                suggestions.Add($"Give '{line.Key}' the bound itself, as a number, and remove the separate '{companionKey}'.");
            }

            diagnostics.Add(new DiagnosticMessage(
                RuleId: RuleIdentifiers.ExclusiveBoundBooleanForm,
                Message: $"'{line.Key}' is declared as a boolean, which is the OpenAPI 3.0 spelling. From 3.1 the " +
                         $"keyword carries the bound itself, so a boolean is not a bound and the exclusivity is " +
                         $"lost - the accepted range widens to include the '{companionKey}' endpoint.",
                Severity: DiagnosticSeverity.Warning,
                FilePath: sourceFilePath,
                LineNumber: line.Number,
                Context: line.Raw.Trim(),
                Suggestions: suggestions,
                DocumentationUrl: GetRuleUrl(RuleIdentifiers.ExclusiveBoundBooleanForm)));
        }
    }

    /// <summary>
    /// Emits ATC_API_VER002 once when an OpenAPI 3.1 or later document still spells nullability with
    /// the <c>nullable</c> keyword.
    /// </summary>
    /// <remarks>
    /// OpenAPI 3.1 removed the keyword outright rather than deprecating it, because its Schema Object
    /// became a superset of JSON Schema 2020-12 and JSON Schema expresses the same thing with a type
    /// array. This generator still honours <c>nullable</c> at 3.1 and later so that raising the
    /// version header does not silently change anyone's models — but that is this generator's
    /// leniency, not the specification's, and no other tool is obliged to match it.
    /// <para>
    /// One diagnostic per document, not per declaration. A specification of any size carries these
    /// by the hundred, the change is mechanical, and three hundred identical warnings is a wall an
    /// author scrolls past rather than a list they work through.
    /// </para>
    /// </remarks>
    private static void ValidateNullableKeywordRemoved(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        IReadOnlyList<YamlLine> lines)
    {
        var count = 0;
        var firstLine = 0;

        foreach (var line in lines)
        {
            if (!line.IsKey ||
                !string.Equals(line.Key, "nullable", StringComparison.Ordinal))
            {
                continue;
            }

            count++;
            if (firstLine == 0)
            {
                firstLine = line.Number;
            }
        }

        if (count == 0)
        {
            return;
        }

        diagnostics.Add(new DiagnosticMessage(
            RuleId: RuleIdentifiers.NullableKeywordRemovedInSpecVersion,
            Message: $"The document declares 'nullable' {count} time(s), but OpenAPI 3.1 removed the keyword in " +
                     "favour of the JSON Schema type array. This generator still honours it so that raising the " +
                     "version does not change your models, and other tooling is under no such obligation - it will " +
                     "read these properties as non-nullable. The first declaration is on line " +
                     $"{firstLine.ToString(GlobalizationConstants.EnglishCultureInfo)}.",
            Severity: DiagnosticSeverity.Warning,
            FilePath: sourceFilePath,
            LineNumber: firstLine,
            Suggestions:
            [
                "Replace 'type: string' plus 'nullable: true' with 'type: [string, \"null\"]'.",
                "For a nullable '$ref', add a null branch: 'oneOf: [ { $ref: ... }, { type: \"null\" } ]'.",
                "An enum needs null in both places: 'type: [string, \"null\"]' and 'null' among the enum values.",
                "To keep the legacy spelling deliberately, suppress this rule with " +
                "'<NoWarn>$(NoWarn);ATC_API_VER002</NoWarn>'.",
            ],
            DocumentationUrl: GetRuleUrl(RuleIdentifiers.NullableKeywordRemovedInSpecVersion)));
    }

    /// <summary>
    /// Emits ATC_API_VER001 for every <c>nullable</c> declared on a Schema Object that has no
    /// <c>type</c> of its own.
    /// </summary>
    /// <remarks>
    /// The OpenAPI Initiative's clarification of the keyword is explicit: <c>nullable</c> "adds
    /// <c>null</c> to the allowed type specified by the <c>type</c> keyword, only if <c>type</c> is
    /// explicitly defined within the same Schema Object", and it "does not 'override' or otherwise
    /// compete with" a schema pulled in by <c>allOf</c>, <c>oneOf</c> or <c>$ref</c>. A declaration
    /// without a sibling <c>type</c> therefore does nothing at all — it has never done anything —
    /// and the property it decorates is not generated as nullable.
    /// <para>
    /// This is worth a warning rather than a shrug precisely because nothing fails. The property is
    /// still optional, so the code compiles either way; under <c>Nullable</c> the compiler simply
    /// stops warning about unguarded access at the one place a null can still arrive.
    /// </para>
    /// </remarks>
    private static void ValidateNullableWithoutSiblingType(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        IReadOnlyList<YamlLine> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!line.IsKey ||
                !string.Equals(line.Key, "nullable", StringComparison.Ordinal) ||
                FindSibling(lines, i, "type") is not null)
            {
                continue;
            }

            diagnostics.Add(new DiagnosticMessage(
                RuleId: RuleIdentifiers.NullableWithoutSiblingType,
                Message: "'nullable' is declared on a schema that has no 'type' of its own, so it has no effect. " +
                         "The OpenAPI specification applies 'nullable' only alongside a 'type' in the same schema " +
                         "object, and it does not reach through 'allOf', 'oneOf' or '$ref'. The generated property " +
                         "is not nullable.",
                Severity: DiagnosticSeverity.Warning,
                FilePath: sourceFilePath,
                LineNumber: line.Number,
                Context: line.Raw.Trim(),
                Suggestions:
                [
                    "If the value really can be null, move the nullability into the composition: " +
                    "'oneOf: [ { $ref: ... }, { type: object, nullable: true } ]'.",
                    "On OpenAPI 3.1 or later the cleaner spelling is a null branch: " +
                    "'oneOf: [ { $ref: ... }, { type: \"null\" } ]'.",
                    "If the value never is null, delete the 'nullable' line - it has never had any effect.",
                ],
                DocumentationUrl: GetRuleUrl(RuleIdentifiers.NullableWithoutSiblingType)));
        }
    }

    /// <summary>
    /// Emits ATC_API_VER004 when an OpenAPI 3.0 document declares <c>type</c> as an array.
    /// </summary>
    /// <remarks>
    /// This is the migration performed in the wrong order — the properties rewritten in the 3.1
    /// spelling while the version header still reads 3.0, where <c>type</c> must be a single string.
    /// The document is not valid 3.0, and what a reader makes of the array is its own business, so
    /// the resulting model is whatever the parser happened to salvage.
    /// </remarks>
    private static void ValidateTypeArrayNotSupported(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        IReadOnlyList<YamlLine> lines)
    {
        foreach (var line in lines)
        {
            if (!line.IsKey ||
                !string.Equals(line.Key, "type", StringComparison.Ordinal) ||
                line.Value is not { Length: > 0 } value ||
                value[0] != '[')
            {
                continue;
            }

            diagnostics.Add(new DiagnosticMessage(
                RuleId: RuleIdentifiers.TypeArrayNotSupportedInSpecVersion,
                Message: "'type' is declared as an array, which OpenAPI 3.1 introduced by adopting JSON Schema. " +
                         "This document declares OpenAPI 3.0, where 'type' must be a single string, so the " +
                         "declaration is not valid here and the generated model depends on whatever the reader " +
                         "salvages from it.",
                Severity: DiagnosticSeverity.Warning,
                FilePath: sourceFilePath,
                LineNumber: line.Number,
                Context: line.Raw.Trim(),
                Suggestions:
                [
                    "Raise the document to 'openapi: 3.1.0' (or later), where the type array is the correct spelling.",
                    "Or stay on 3.0 and use the 3.0 spelling: a single 'type' plus 'nullable: true' beside it.",
                ],
                DocumentationUrl: GetRuleUrl(RuleIdentifiers.TypeArrayNotSupportedInSpecVersion)));
        }
    }

    /// <summary>
    /// Finds <paramref name="key"/> among the siblings of the entry on line <paramref name="index"/>,
    /// or returns <see langword="null"/> when the mapping does not declare it.
    /// </summary>
    /// <remarks>
    /// Siblings are the entries sharing a content indent within one mapping. The search walks
    /// outward in both directions and stops at a dedent, because that leaves the mapping, and at a
    /// sequence-item marker, because that starts or ends a different mapping. Blank and comment
    /// lines do not break a block and are skipped.
    /// </remarks>
    private static YamlLine? FindSibling(
        IReadOnlyList<YamlLine> lines,
        int index,
        string key)
    {
        var indent = lines[index].ContentIndent;

        for (var i = index - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (line.IsIgnorable || line.ContentIndent > indent)
            {
                continue;
            }

            if (line.ContentIndent < indent)
            {
                break;
            }

            if (line.IsKey && string.Equals(line.Key, key, StringComparison.Ordinal))
            {
                return line;
            }

            if (line.IsSequenceItem)
            {
                // The first entry of this mapping - there is nothing above it that is still a sibling.
                break;
            }
        }

        for (var i = index + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.IsIgnorable || line.ContentIndent > indent)
            {
                continue;
            }

            if (line.ContentIndent < indent || line.IsSequenceItem)
            {
                break;
            }

            if (line.IsKey && string.Equals(line.Key, key, StringComparison.Ordinal))
            {
                return line;
            }
        }

        return null;
    }

    private static string GetRuleUrl(string ruleId)
        => Constants.Documentation.GetRuleUrl(ruleId);

    private static List<YamlLine> SplitLines(string text)
    {
        var raw = text.Split('\n');
        var result = new List<YamlLine>(raw.Length);

        for (var i = 0; i < raw.Length; i++)
        {
            result.Add(YamlLine.Parse(raw[i].TrimEnd('\r'), i + 1));
        }

        return result;
    }

    /// <summary>
    /// One line of a specification, reduced to what the rules need: where its content starts, and
    /// the mapping key it declares, if any.
    /// </summary>
    /// <remarks>
    /// This is deliberately not a YAML parser. It recognises indentation, sequence markers, comments
    /// and <c>key: value</c> entries, which is the whole vocabulary these rules reason about. A
    /// construct it does not model - a flow mapping spread over one line, say - simply does not match
    /// as a key, so the rules stay silent rather than guess.
    /// </remarks>
    private sealed class YamlLine
    {
        private YamlLine(
            string raw,
            int number,
            int contentIndent,
            bool isSequenceItem,
            bool isIgnorable,
            string? key,
            string? value)
        {
            Raw = raw;
            Number = number;
            ContentIndent = contentIndent;
            IsSequenceItem = isSequenceItem;
            IsIgnorable = isIgnorable;
            Key = key;
            Value = value;
        }

        public string Raw { get; }

        public int Number { get; }

        /// <summary>Gets the column at which the entry's content starts, past any sequence marker.</summary>
        public int ContentIndent { get; }

        public bool IsSequenceItem { get; }

        /// <summary>Gets a value indicating whether the line is blank or a comment, and so does not end a mapping block.</summary>
        public bool IsIgnorable { get; }

        public string? Key { get; }

        public string? Value { get; }

        public bool IsKey => Key is not null;

        public static YamlLine Parse(
            string raw,
            int number)
        {
            var position = 0;
            while (position < raw.Length && raw[position] == ' ')
            {
                position++;
            }

            if (position >= raw.Length || raw[position] == '#')
            {
                return new YamlLine(raw, number, int.MaxValue, isSequenceItem: false, isIgnorable: true, key: null, value: null);
            }

            var isSequenceItem = false;
            if (raw[position] == '-' &&
                (position + 1 >= raw.Length || raw[position + 1] == ' '))
            {
                isSequenceItem = true;
                position++;
                while (position < raw.Length && raw[position] == ' ')
                {
                    position++;
                }

                if (position >= raw.Length || raw[position] == '#')
                {
                    // A bare '-' opening a mapping on the following lines.
                    return new YamlLine(raw, number, position, isSequenceItem: true, isIgnorable: false, key: null, value: null);
                }
            }

            var contentIndent = position;
            var separator = raw.IndexOf(':', position);
            if (separator < 0 ||
                (separator + 1 < raw.Length && raw[separator + 1] != ' '))
            {
                return new YamlLine(raw, number, contentIndent, isSequenceItem, isIgnorable: false, key: null, value: null);
            }

            var key = raw.Substring(position, separator - position).Trim();
            var value = StripComment(raw.Substring(separator + 1)).Trim();

            return new YamlLine(raw, number, contentIndent, isSequenceItem, isIgnorable: false, key, value);
        }

        private static string StripComment(string value)
        {
            var hash = value.IndexOf(" #", StringComparison.Ordinal);

            return hash < 0
                ? value
                : value.Substring(0, hash);
        }
    }
}