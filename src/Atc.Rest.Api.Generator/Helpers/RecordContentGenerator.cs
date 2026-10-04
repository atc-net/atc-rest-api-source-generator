namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Renders records like <see cref="GenerateContentForRecords"/>, except that a deprecated positional property (one
/// with <c>[Obsolete]</c>) is declared explicitly in the record's body: the compiler does not warn on
/// <c>[property: Obsolete]</c> of a positional parameter used in the same compilation - and generated code always is.
/// The parameter keeps its place (and its constructor argument); the property takes its attributes and documentation
/// and is initialized from it.
/// </summary>
public static class RecordContentGenerator
{
    /// <summary>
    /// Renders the records.
    /// </summary>
    /// <param name="codeDocumentationTagsGenerator">The documentation generator.</param>
    /// <param name="parameters">The records.</param>
    /// <returns>The rendered content.</returns>
    public static string Generate(
        ICodeDocumentationTagsGenerator codeDocumentationTagsGenerator,
        RecordsParameters parameters)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        var bodies = new List<(string Name, string Body)>();
        var records = new List<RecordParameters>(parameters.Parameters.Count);
        foreach (var record in parameters.Parameters)
        {
            var deprecated = record.Parameters?
                .Where(p => p.Attributes?.Any(IsObsolete) == true)
                .ToList() ?? [];
            if (deprecated.Count == 0)
            {
                records.Add(record);
                continue;
            }

            bodies.Add((record.Name, Body(record, deprecated)));
            records.Add(record with
            {
                Parameters = record.Parameters!
                    .Select(p => deprecated.Contains(p) ? p with { Attributes = null } : p)
                    .ToList(),
            });
        }

        var content = new GenerateContentForRecords(
                codeDocumentationTagsGenerator,
                parameters with { Parameters = records })
            .Generate();

        foreach (var (name, body) in bodies)
        {
            content = AppendBody(content, name, body);
        }

        return content;
    }

    private static bool IsObsolete(AttributeParameters attribute)
        => string.Equals(attribute.Name, ObsoleteAttributeHelper.Attribute.Name, StringComparison.Ordinal);

    private static string Body(
        RecordParameters record,
        List<ParameterBaseParameters> deprecated)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.Append('{');
        for (var i = 0; i < deprecated.Count; i++)
        {
            var property = deprecated[i];
            if (i > 0)
            {
                sb.AppendLine();
            }

            if (record.DocumentationTags?.Parameters is { } docs &&
                docs.TryGetValue(property.Name, out var doc))
            {
                sb.AppendLine();
                sb.Append("    /// <summary>");
                foreach (var line in doc.Split('\n'))
                {
                    sb.AppendLine();
                    sb.Append("    /// ").Append(line.TrimEnd('\r'));
                }

                sb.AppendLine();
                sb.Append("    /// </summary>");
            }

            foreach (var attribute in property.Attributes!)
            {
                sb.AppendLine();
                sb.AppendAttribute(4, usePropertyPrefix: false, attribute);
            }

            sb.AppendLine();
            sb.Append("    public ");
            sb.AppendTypeAndName(property.GenericTypeName, property.TypeName, property.IsNullableType, property.Name);
            sb.Append(" { get; init; } = ").Append(property.Name).Append(';');
        }

        sb.AppendLine();
        sb.Append('}');
        return sb.ToString();
    }

    // The record's declaration ends at the first ';' outside parentheses and string literals after "{name}(".
    private static string AppendBody(
        string content,
        string recordName,
        string body)
    {
        var start = content.IndexOf(" " + recordName + "(", StringComparison.Ordinal);
        if (start < 0)
        {
            return content;
        }

        var depth = 0;
        var i = start + recordName.Length + 1;
        while (i < content.Length)
        {
            switch (content[i])
            {
                case '"':
                    i = EndOfString(content, i);
                    break;
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ';' when depth == 0:
                    return content.Substring(0, i) + body + content.Substring(i + 1);
            }

            i++;
        }

        return content;
    }

    // The index of the quote that closes the string literal opened at 'quote' (regular or verbatim).
    private static int EndOfString(
        string content,
        int quote)
    {
        var verbatim = quote > 0 && content[quote - 1] == '@';
        var i = quote + 1;
        while (i < content.Length)
        {
            if (content[i] == '"' && !(verbatim && i + 1 < content.Length && content[i + 1] == '"'))
            {
                return i;
            }

            // An escape (or a verbatim string's doubled quote) takes the next character with it
            i += (!verbatim && content[i] == '\\') || content[i] == '"' ? 2 : 1;
        }

        return content.Length;
    }
}