namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Helper class for extracting and formatting default values from OpenAPI schemas.
/// </summary>
public static class DefaultValueHelper
{
    /// <summary>
    /// The C# default of a record constructor parameter: the schema's default, else <c>null</c> for an optional
    /// (nullable) property - it may be absent, so neither System.Text.Json (<c>RespectRequiredConstructorParameters</c>)
    /// nor code creating the record must supply it. A required property has no default.
    /// </summary>
    /// <param name="schemaDefault">The schema's default as a C# literal, or <see langword="null"/>.</param>
    /// <param name="isRequired">Whether the property is in the schema's <c>required</c> list.</param>
    /// <param name="isNullableType">Whether the parameter's type is nullable.</param>
    /// <returns>The C# default value, or <see langword="null"/> for none.</returns>
    public static string? ForRecordParameter(
        string? schemaDefault,
        bool isRequired,
        bool isNullableType)
        => schemaDefault ?? (!isRequired && isNullableType ? "null" : null);

    /// <summary>
    /// Extracts the default value from an OpenAPI schema and formats it as a C# literal.
    /// </summary>
    /// <param name="schemaInterface">The OpenAPI schema interface.</param>
    /// <param name="csharpTypeName">The C# type name for the property/parameter.</param>
    /// <param name="registry">The registry of the generation: a client's extensible string enum has no constant member to be a default.</param>
    /// <returns>The formatted default value, or null if no default is specified.</returns>
    public static string? ExtractSchemaDefault(
        IOpenApiSchema? schemaInterface,
        string csharpTypeName,
        TypeConflictRegistry? registry = null)
    {
        // OpenAPI 3.1: a default next to a $ref is the referencing property's own
        if (schemaInterface is OpenApiSchemaReference { Target: OpenApiSchema target } reference)
        {
            return ExtractSiblingDefault(reference, target, csharpTypeName, registry);
        }

        if (schemaInterface is not OpenApiSchema schema)
        {
            return null;
        }

        var defaultValue = schema.Default;
        if (defaultValue is null)
        {
            return null;
        }

        var rawValue = defaultValue.ToString();
        if (string.IsNullOrEmpty(rawValue))
        {
            return null;
        }

        // An enum's default is its member: `Color Color = Color.Red`, `Priority Priority = Priority.Value2`
        var enumSchema = schema.Enum is { Count: > 0 } ? schema : schema.WrappedEnum();
        if (enumSchema is not null)
        {
            return schema.HasConstantDefault(registry)
                ? EnumMemberFor(enumSchema, rawValue, csharpTypeName)
                : null;
        }

        return FormatDefaultValue(rawValue, csharpTypeName);
    }

    /// <summary>
    /// Formats a raw default value string as a C# literal based on the type.
    /// </summary>
    /// <param name="rawValue">The raw value string from the OpenAPI schema.</param>
    /// <param name="csharpTypeName">The C# type name.</param>
    /// <returns>The formatted C# literal value, or null if not supported.</returns>
    public static string? FormatDefaultValue(
        string rawValue,
        string csharpTypeName)
    {
        if (string.IsNullOrEmpty(rawValue))
        {
            return null;
        }

        // String types - the code generation library adds the quotes, so return the escaped content of the literal
        if (csharpTypeName == "string")
        {
            return CSharpLiteralHelper.EscapeString(StripQuotes(rawValue));
        }

        // Boolean values need lowercase in C#
        if (csharpTypeName == "bool")
        {
            return rawValue.ToLowerInvariant();
        }

        // A float or decimal literal needs its suffix: "float X = 1.5" and "decimal X = 1.5" do not compile
        if (csharpTypeName is "float" or "decimal" &&
            decimal.TryParse(rawValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            return rawValue + (csharpTypeName == "float" ? "f" : "m");
        }

        // Numeric types can use the raw value directly
        if (CSharpTypeHelper.IsNumericType(csharpTypeName) ||
            CSharpTypeHelper.IsBasicValueType(csharpTypeName))
        {
            return rawValue;
        }

        return null;
    }

    /// <summary>
    /// Formats the default value for use in a [DefaultValue] attribute.
    /// </summary>
    /// <param name="defaultValue">The default value to format.</param>
    /// <param name="csharpTypeName">The C# type name.</param>
    /// <returns>The formatted value suitable for use in a DefaultValue attribute.</returns>
    public static string FormatForAttribute(
        string defaultValue,
        string csharpTypeName)
    {
        // String types need quotes in attribute
        if (csharpTypeName == "string")
        {
            return $"\"{defaultValue}\"";
        }

        // Boolean values need lowercase
        if (csharpTypeName == "bool")
        {
            return defaultValue.ToLowerInvariant();
        }

        // A decimal is no valid attribute argument: the attribute takes the number as a double
        if (csharpTypeName == "decimal" && defaultValue.EndsWith("m", StringComparison.Ordinal))
        {
            return defaultValue.Substring(0, defaultValue.Length - 1);
        }

        // Numeric types can use the raw value directly
        return defaultValue;
    }

    /// <summary>
    /// Strips surrounding quotes from a string value if present.
    /// </summary>
    // The default a $ref carries as a sibling (not the component's own, which the reference also gives): a member of
    // the referenced enum, or the literal of a referenced primitive
    private static string? ExtractSiblingDefault(
        OpenApiSchemaReference reference,
        OpenApiSchema target,
        string csharpTypeName,
        TypeConflictRegistry? registry)
    {
        if (reference.Default is not System.Text.Json.Nodes.JsonValue value ||
            ReferenceEquals(value, target.Default) ||
            value.GetValueKind() is not (System.Text.Json.JsonValueKind.String or System.Text.Json.JsonValueKind.Number or System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False))
        {
            return null;
        }

        var rawValue = value.ToString();
        if (string.IsNullOrEmpty(rawValue))
        {
            return null;
        }

        var enumSchema = target.Enum is { Count: > 0 } ? target : target.WrappedEnum();
        if (enumSchema is null)
        {
            return FormatDefaultValue(rawValue, csharpTypeName.TrimEnd('?'));
        }

        // A client's extensible enum is a struct: its members are no compile-time constants
        return registry?.ExtensibleEnums == true && enumSchema.IsOfType(JsonSchemaType.String)
            ? null
            : EnumMemberFor(enumSchema, rawValue, csharpTypeName);
    }

    private static string? EnumMemberFor(
        OpenApiSchema enumSchema,
        string rawValue,
        string csharpTypeName)
    {
        var members = Extractors.EnumExtractor.ExtractEnumValues(enumSchema.Enum, Extractors.EnumExtractor.MemberNamesOf(enumSchema));
        var wire = rawValue.Trim('"');
        var member = members?.FirstOrDefault(m =>
            string.Equals(m.EnumMemberValue ?? m.Name, wire, StringComparison.Ordinal) ||
            (m.Value is { } number && long.TryParse(wire, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var asNumber) && number == asNumber));

        return member is null ? null : $"{csharpTypeName.TrimEnd('?')}.{member.Name}";
    }

    private static string StripQuotes(string value)
    {
        if (value.StartsWith("\"", StringComparison.Ordinal) &&
            value.EndsWith("\"", StringComparison.Ordinal) &&
            value.Length >= 2)
        {
            return value.Substring(1, value.Length - 2);
        }

        return value;
    }
}