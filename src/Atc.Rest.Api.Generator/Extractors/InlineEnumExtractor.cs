namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts inline enum definitions from OpenAPI property schemas.
/// Inline enums are defined directly on properties rather than as standalone schemas in components/schemas.
/// </summary>
public static class InlineEnumExtractor
{
    /// <summary>
    /// Checks if a schema represents an inline enum (string type with enum values, not a reference).
    /// </summary>
    /// <param name="schema">The schema to check.</param>
    /// <returns>True if the schema is an inline enum definition.</returns>
    public static bool IsInlineEnumSchema(IOpenApiSchema? schema)
    {
        // Must not be a reference (references point to components/schemas)
        if (schema is OpenApiSchemaReference)
        {
            return false;
        }

        // Must be an actual schema
        if (schema is not OpenApiSchema actualSchema)
        {
            return false;
        }

        // Must be a string type with enum values
        return actualSchema.Type?.HasFlag(JsonSchemaType.String) == true
            && actualSchema.Enum is { Count: > 0 };
    }

    /// <summary>
    /// Generates a unique type name for an inline enum based on parent schema and property name.
    /// </summary>
    /// <param name="parentSchemaName">The name of the parent schema (e.g., "ResendEventsRequest").</param>
    /// <param name="propertyName">The property name (e.g., "resourceType").</param>
    /// <returns>The generated type name (e.g., "ResendEventsRequestResourceType").</returns>
    public static string GenerateInlineEnumTypeName(
        string parentSchemaName,
        string propertyName)
        => $"{parentSchemaName}{propertyName.EnsureFirstCharacterToUpper()}";

    /// <summary>
    /// Generates a key based on sorted enum values for deduplication.
    /// If two inline enums have the same values, they can share the same type.
    /// </summary>
    /// <param name="schema">The enum schema.</param>
    /// <returns>A key string representing the sorted enum values.</returns>
    public static string GetEnumValuesKey(OpenApiSchema schema)
    {
        if (schema.Enum == null || schema.Enum.Count == 0)
        {
            return string.Empty;
        }

        var values = schema.Enum
            .Select(e => e?.ToString()?.Trim('"') ?? string.Empty)
            .Where(v => !string.IsNullOrEmpty(v))
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase);

        return string.Join("|", values);
    }

    /// <summary>
    /// Extracts EnumParameters from an inline enum schema.
    /// </summary>
    /// <param name="schema">The inline enum schema.</param>
    /// <param name="typeName">The generated type name for the enum.</param>
    /// <param name="ns">The namespace for the enum.</param>
    /// <returns>The EnumParameters for code generation, or null if extraction fails.</returns>
    public static EnumParameters? ExtractEnumFromInlineSchema(
        OpenApiSchema schema,
        string typeName,
        string ns)
    {
        var enumValues = EnumExtractor.ExtractEnumValues(schema.Enum);
        if (enumValues == null)
        {
            return null;
        }

        // Check if any value needs EnumMember attribute
        var hasEnumMemberValues = enumValues.Any(v => v.EnumMemberValue != null);

        // Build header content with appropriate usings
        var headerContent = EnumExtractor.BuildEnumHeaderContent(hasEnumMemberValues);

        // Get description from schema if available
        CodeDocumentationTags? docTags = null;
        if (!string.IsNullOrEmpty(schema.Description))
        {
            docTags = new CodeDocumentationTags(schema.Description!);
        }

        // Build attributes list
        var attributes = new List<AttributeParameters>
        {
            new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
        };

        return new EnumParameters(
            HeaderContent: headerContent,
            Namespace: ns,
            DocumentationTags: docTags,
            Attributes: attributes,
            DeclarationModifier: DeclarationModifiers.Public,
            EnumTypeName: typeName,
            UseFlags: false,
            Values: enumValues);
    }

    /// <summary>
    /// Generates the content for a single inline enum using GenerateContentForEnum.
    /// Delegates to <see cref="EnumExtractor.GenerateEnumContent"/>.
    /// </summary>
    public static string GenerateEnumContent(EnumParameters parameters)
        => EnumExtractor.GenerateEnumContent(parameters);
}