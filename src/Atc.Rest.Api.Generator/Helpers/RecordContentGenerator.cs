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

    /// <summary>
    /// Renders the server's parameters records (bound with <c>[AsParameters]</c>) so ASP.NET Core's Request Delegate
    /// Generator - what a Native AOT build binds the endpoints with - can build them. RDG looks the record's
    /// constructor up with <c>typeof</c> of each parameter's declared type, and <c>typeof(string?)</c> does not
    /// compile (CS8639). A nullable reference-type parameter is therefore declared nullable-oblivious
    /// (<c>#nullable disable annotations</c>) and keeps its place, its default and its constructor argument; its
    /// property is declared in the record's body as the nullable type, with the attributes, so binding (both RDG and
    /// <c>RequestDelegateFactory</c> read optionality from the property) and the handler see <c>string?</c>. A default
    /// is also the property's fallback: a binder passes null for an absent oblivious parameter.
    /// </summary>
    /// <param name="codeDocumentationTagsGenerator">The documentation generator.</param>
    /// <param name="parameters">The parameters records.</param>
    /// <returns>The rendered content.</returns>
    public static string GenerateParameterRecords(
        ICodeDocumentationTagsGenerator codeDocumentationTagsGenerator,
        RecordsParameters parameters)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        var bodies = new List<(string Name, string Body, List<ParameterBaseParameters> Oblivious)>();
        var records = new List<RecordParameters>(parameters.Parameters.Count);
        foreach (var record in parameters.Parameters)
        {
            var oblivious = record.Parameters?
                .Where(p => p.IsNullableType && IsReferenceTypeName(p.GenericTypeName, p.TypeName))
                .ToList() ?? [];
            if (oblivious.Count == 0)
            {
                records.Add(record);
                continue;
            }

            bodies.Add((record.Name, ObliviousBody(record, oblivious), oblivious));
            records.Add(record with
            {
                Parameters = record.Parameters!
                    .Select(p => oblivious.Contains(p) ? p with { Attributes = null, IsNullableType = false } : p)
                    .ToList(),
            });
        }

        var content = new GenerateContentForRecords(
                codeDocumentationTagsGenerator,
                parameters with { Parameters = records })
            .Generate();

        foreach (var (name, body, oblivious) in bodies)
        {
            content = AppendBody(content, name, body);
            content = WithObliviousParameters(content, name, oblivious);
        }

        return content;
    }

    // A reference type a parameters record holds: a string, a list, an array, a file - not an enum or an extensible
    // enum (generated value types)
    private static bool IsReferenceTypeName(
        string? genericTypeName,
        string typeName)
        => genericTypeName is not null ||
           typeName is "string" or "object" or "IFormFile" or "IFormFileCollection" or "Stream" or "Uri" or "System.Uri" ||
           typeName.EndsWith("[]", StringComparison.Ordinal) ||
           typeName.StartsWith("ParsableList<", StringComparison.Ordinal) ||
           typeName.StartsWith("List<", StringComparison.Ordinal) ||
           typeName.StartsWith("IEnumerable<", StringComparison.Ordinal);

    private static string ObliviousBody(
        RecordParameters record,
        List<ParameterBaseParameters> oblivious)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.Append('{');
        for (var i = 0; i < oblivious.Count; i++)
        {
            var property = oblivious[i];
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

            foreach (var attribute in property.Attributes ?? [])
            {
                sb.AppendLine();
                sb.AppendAttribute(4, usePropertyPrefix: false, attribute);
            }

            sb.AppendLine();
            sb.Append("    public ");
            sb.AppendTypeAndName(property.GenericTypeName, property.TypeName, isNullableType: true, property.Name);
            sb.Append(" { get; init; } = ").Append(property.Name);
            if (property.DefaultValue is { } defaultValue && !string.Equals(defaultValue, "null", StringComparison.Ordinal))
            {
                // The default as the constructor writes it (a string default is quoted there)
                var declaration = new StringBuilder();
                declaration.AppendTypeAndName(0, property.GenericTypeName, property.TypeName, isNullableType: false, property.Name, defaultValue);
                var text = declaration.ToString();
                sb.Append(" ?? ").Append(text.Substring(text.IndexOf(" = ", StringComparison.Ordinal) + 3));
            }

            sb.Append(';');
        }

        sb.AppendLine();
        sb.Append('}');
        return sb.ToString();
    }

    // Wraps the declaration line of each oblivious parameter of the record in #nullable disable/restore annotations
    private static string WithObliviousParameters(
        string content,
        string recordName,
        List<ParameterBaseParameters> oblivious)
    {
        var start = content.IndexOf(" " + recordName + "(", StringComparison.Ordinal);
        if (start < 0)
        {
            return content;
        }

        var lineStart = content.IndexOf('\n', start) + 1;
        var names = new HashSet<string>(oblivious.Select(p => p.Name), StringComparer.Ordinal);
        var sb = new StringBuilder(content.Substring(0, lineStart));
        var inHeader = true;
        foreach (var line in content.Substring(lineStart).Split('\n'))
        {
            var trimmed = line.TrimEnd('\r').Trim();
            if (inHeader && ParameterName(trimmed) is { } name && names.Contains(name))
            {
                // Adjacent parameters share one region
                const string restore = "#nullable restore annotations\n";
                if (sb.Length >= restore.Length && sb.ToString(sb.Length - restore.Length, restore.Length) == restore)
                {
                    sb.Length -= restore.Length;
                }
                else
                {
                    sb.Append("#nullable disable annotations\n");
                }

                sb.Append(line).Append('\n').Append(restore);
            }
            else
            {
                sb.Append(line).Append('\n');
            }

            if (inHeader && trimmed.EndsWith(")", StringComparison.Ordinal))
            {
                inHeader = false;
            }
        }

        return sb.ToString(0, sb.Length - 1);
    }

    // "string Search," / "string Sort = "name")" - the identifier before the default, the comma or the parenthesis
    private static string? ParameterName(string line)
    {
        var end = line.IndexOf(" = ", StringComparison.Ordinal);
        var declaration = (end >= 0 ? line.Substring(0, end) : line).TrimEnd(',', ')').Trim();
        var space = declaration.LastIndexOf(' ');
        return space < 0 ? null : declaration.Substring(space + 1);
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