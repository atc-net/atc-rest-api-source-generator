namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// Finds the constraints the generated code could not carry (<c>ATC_API_SCH025</c>): bounds the wrong way round
/// (<c>minLength</c> past <c>maxLength</c>, <c>minimum</c> past <c>maximum</c>, <c>minItems</c> past
/// <c>maxItems</c>), a bound that is no finite number, a <c>pattern</c> that is no regular expression, an integer
/// enum with a member that is no integer, an enum with no member but <c>null</c>. The generated attribute would throw
/// at run time (<c>RangeAttribute</c>, <c>RegularExpressionAttribute</c>), or a type be named that is never
/// generated. Every component schema is walked with its properties and items.
/// </summary>
internal static class SchemaConstraintValidator
{
    public static List<DiagnosticMessage> Validate(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();
        var visited = new HashSet<OpenApiSchema>(ByReference.Instance);
        foreach (var schema in document.Components?.Schemas ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            Walk(schema.Value, schema.Key, $"Schema '{schema.Key}'", diagnostics, sourceFilePath, visited);
        }

        return diagnostics;
    }

    private static void Walk(
        IOpenApiSchema? schema,
        string schemaName,
        string where,
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        HashSet<OpenApiSchema> visited)
    {
        if (schema is not OpenApiSchema actual || !visited.Add(actual))
        {
            return;
        }

        foreach (var problem in Problems(actual))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.SchemaConstraintNotGeneratable,
                $"{where}: {problem}.",
                DiagnosticSeverity.Error,
                sourceFilePath,
                Context: $"Schema: {schemaName}",
                Suggestions: ["Fix the constraint; the generated validation attribute could not carry it"]));
        }

        foreach (var duplicate in DuplicateEnumValues(actual))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.EnumValueListedTwice,
                $"{where}: enum lists the value {duplicate} more than once; it is generated once.",
                DiagnosticSeverity.Warning,
                sourceFilePath,
                Context: $"Schema: {schemaName}",
                Suggestions: ["List each enum value once"]));
        }

        foreach (var property in actual.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            Walk(property.Value, schemaName, $"Schema '{schemaName}' property '{property.Key}'", diagnostics, sourceFilePath, visited);
        }

        Walk(actual.Items, schemaName, $"{where} items", diagnostics, sourceFilePath, visited);
        foreach (var member in (actual.AllOf ?? []).Concat(actual.OneOf ?? []).Concat(actual.AnyOf ?? []))
        {
            Walk(member, schemaName, where, diagnostics, sourceFilePath, visited);
        }
    }

    private static IEnumerable<string> Problems(OpenApiSchema schema)
    {
        if (schema.MinLength is { } minLength && schema.MaxLength is { } maxLength && minLength > maxLength)
        {
            yield return $"minLength {minLength} is past maxLength {maxLength}";
        }

        if (schema.MinItems is { } minItems && schema.MaxItems is { } maxItems && minItems > maxItems)
        {
            yield return $"minItems {minItems} is past maxItems {maxItems}";
        }

        double? minimum = null;
        double? maximum = null;
        (string Keyword, string? Text, bool IsMinimum)[] bounds =
        [
            ("minimum", schema.Minimum, true),
            ("exclusiveMinimum", schema.ExclusiveMinimum, true),
            ("maximum", schema.Maximum, false),
            ("exclusiveMaximum", schema.ExclusiveMaximum, false),
        ];
        foreach (var (keyword, text, isMinimum) in bounds)
        {
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || double.IsInfinity(value) || double.IsNaN(value))
            {
                yield return $"{keyword} {text} is no finite number";
                continue;
            }

            if (isMinimum)
            {
                minimum = value;
            }
            else
            {
                maximum = value;
            }
        }

        if (minimum is { } min && maximum is { } max && min > max)
        {
            yield return $"minimum {min.ToString(CultureInfo.InvariantCulture)} is past maximum {max.ToString(CultureInfo.InvariantCulture)}";
        }

        if (!string.IsNullOrEmpty(schema.Pattern) && !IsRegularExpression(schema.Pattern!))
        {
            yield return $"pattern '{schema.Pattern}' is no regular expression";
        }

        if (schema.Enum is { Count: > 0 } members)
        {
            // The reader keeps a null member as a sentinel string
            var values = members
                .Where(m => m is JsonValue value && value.GetValueKind() != JsonValueKind.Null && !JsonNullSentinel.IsJsonNullSentinel(value))
                .Cast<JsonValue>()
                .ToList();
            if (values.Count == 0)
            {
                yield return "enum has no member but null";
            }
            else if (schema.Type is { } type && (type & ~JsonSchemaType.Null) == JsonSchemaType.Integer)
            {
                foreach (var value in values.Where(v => v.GetValueKind() != JsonValueKind.Number || !long.TryParse(v.ToJsonString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
                {
                    yield return $"enum member {value.ToJsonString()} is no integer";
                }
            }
        }
    }

    /// <summary>
    /// The enum values listed more than once, as JSON (<c>"archived"</c>, <c>2</c>); values that differ in case only
    /// are two values.
    /// </summary>
    private static IEnumerable<string> DuplicateEnumValues(OpenApiSchema schema)
        => (schema.Enum ?? [])
            .Where(m => m is JsonValue value && value.GetValueKind() != JsonValueKind.Null && !JsonNullSentinel.IsJsonNullSentinel(value))
            .Select(m => m!.ToJsonString())
            .GroupBy(json => json, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

    private static bool IsRegularExpression(string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private sealed class ByReference : IEqualityComparer<OpenApiSchema>
    {
        public static ByReference Instance { get; } = new();

        public bool Equals(
            OpenApiSchema x,
            OpenApiSchema y)
            => ReferenceEquals(x, y);

        public int GetHashCode(OpenApiSchema obj)
            => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}