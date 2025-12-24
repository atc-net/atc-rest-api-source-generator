namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI enum schema definitions and converts them to EnumParameters for code generation.
/// </summary>
public static class EnumExtractor
{
    /// <summary>
    /// Extracts all enum definitions from OpenAPI document components for a specific path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Users").</param>
    /// <returns>List of EnumParameters for each enum schema, or null if no enums exist.</returns>
    public static List<EnumParameters>? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment)
    {
        if (openApiDoc.Components?.Schemas == null ||
            openApiDoc.Components.Schemas.Count == 0)
        {
            return null;
        }

        // Get schemas used by operations in this path segment
        var usedSchemas = string.IsNullOrEmpty(pathSegment)
            ? null
            : PathSegmentHelper.GetSchemasUsedBySegment(openApiDoc, pathSegment);

        return ExtractCore(openApiDoc, projectName, usedSchemas, pathSegment);
    }

    /// <summary>
    /// Extracts enum definitions from OpenAPI document components for specific schemas.
    /// Used for generating shared enums (used by multiple segments) or segment-specific enums.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="schemaNames">The specific schema names to extract.</param>
    /// <param name="pathSegment">The path segment for namespace (null = shared namespace without segment).</param>
    /// <returns>List of EnumParameters for each enum schema, or null if no enums exist.</returns>
    public static List<EnumParameters>? ExtractForSchemas(
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemaNames,
        string? pathSegment)
    {
        if (openApiDoc.Components?.Schemas == null ||
            openApiDoc.Components.Schemas.Count == 0 ||
            schemaNames == null ||
            schemaNames.Count == 0)
        {
            return null;
        }

        return ExtractCore(openApiDoc, projectName, schemaNames, pathSegment);
    }

    /// <summary>
    /// Core extraction logic for enum definitions.
    /// </summary>
    private static List<EnumParameters>? ExtractCore(
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string>? schemaFilter,
        string? pathSegment)
    {
        var enumParametersList = new List<EnumParameters>();

        // Use shared namespace if no pathSegment, otherwise segment-specific namespace
        var ns = NamespaceBuilder.ForModels(projectName, pathSegment);

        foreach (var schema in openApiDoc.Components!.Schemas!)
        {
            var originalSchemaName = schema.Key;
            var schemaValue = schema.Value;

            // Apply schema filter if provided (uses original name from OpenAPI spec)
            if (schemaFilter != null && !schemaFilter.Contains(originalSchemaName))
            {
                continue;
            }

            // Skip schema references
            if (schemaValue is OpenApiSchemaReference)
            {
                continue;
            }

            // Check if this is a string enum schema
            if (schemaValue is OpenApiSchema actualSchema && IsEnumSchema(actualSchema))
            {
                // Sanitize schema name - replace dots with underscores for valid C# identifiers
                var schemaName = OpenApiSchemaExtensions.SanitizeSchemaName(originalSchemaName);
                var enumParams = ExtractEnumFromSchema(schemaName, actualSchema, ns);
                if (enumParams != null)
                {
                    enumParametersList.Add(enumParams);
                }
            }
        }

        return enumParametersList.Count > 0 ? enumParametersList : null;
    }

    /// <summary>
    /// Checks if a schema represents an enum (string type with enum values).
    /// </summary>
    private static bool IsEnumSchema(OpenApiSchema schema)
    {
        // Check if it's a string type with enum values
        if (schema.Type != JsonSchemaType.String)
        {
            return false;
        }

        // Must have enum values defined
        return schema.Enum is { Count: > 0 };
    }

    /// <summary>
    /// Extracts enum parameters from an OpenAPI schema.
    /// </summary>
    private static EnumParameters? ExtractEnumFromSchema(
        string schemaName,
        OpenApiSchema schema,
        string ns)
    {
        if (schema.Enum == null ||
            schema.Enum.Count == 0)
        {
            return null;
        }

        var enumValues = new List<EnumValueParameters>();

        foreach (var enumValue in schema.Enum)
        {
            // OpenAPI enum values are stored as JsonNode
            var valueStr = enumValue?.ToString();
            if (string.IsNullOrEmpty(valueStr))
            {
                continue;
            }

            // Clean up the value - remove quotes if present
            var originalValue = valueStr!.Trim('"');

            // Check if transformation is needed
            var needsTransformation = NeedsEnumMemberAttribute(originalValue);

            string name;
            string? enumMemberValue;

            if (needsTransformation)
            {
                // Transform to valid C# identifier
                name = originalValue.ToPascalCase(separators: [' ', '-', '_', ':'], removeSeparators: true);
                enumMemberValue = originalValue;
            }
            else
            {
                // Use original value as-is
                name = originalValue;
                enumMemberValue = null;
            }

            enumValues.Add(new EnumValueParameters(
                DocumentationTags: null,
                DescriptionAttribute: null,
                Name: name,
                EnumMemberValue: enumMemberValue,
                Value: null));
        }

        if (enumValues.Count == 0)
        {
            return null;
        }

        // Check if any value needs EnumMember attribute
        var hasEnumMemberValues = enumValues.Any(v => v.EnumMemberValue != null);

        // Build header content with appropriate usings
        var headerContent = BuildEnumHeaderContent(hasEnumMemberValues);

        // Get description from schema if available
        CodeDocumentationTags? docTags = null;
        if (!string.IsNullOrEmpty(schema.Description))
        {
            docTags = new CodeDocumentationTags(schema.Description!);
        }

        // Build attributes list - JsonConverter is added by GenerateContentForEnum when EnumMember values are present
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
            EnumTypeName: schemaName,
            UseFlags: false,
            Values: enumValues);
    }

    /// <summary>
    /// Determines if an enum value needs an EnumMember attribute.
    /// This is required when the value contains characters that are not valid in C# identifiers
    /// or when the casing doesn't match PascalCase.
    /// </summary>
    private static bool NeedsEnumMemberAttribute(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // Check for special characters that require transformation
        if (value.Contains("-") ||
            value.Contains(":") ||
            value.Contains("_") ||
            value.Contains(" ") ||
            value.Equals("*", StringComparison.Ordinal))
        {
            return true;
        }

        // Check if first character is lowercase (camelCase)
        return char.IsLower(value[0]);
    }

    /// <summary>
    /// Builds the header content for generated enum files.
    /// </summary>
    /// <param name="hasEnumMemberValues">Whether the enum has values requiring EnumMember attributes.</param>
    private static string BuildEnumHeaderContent(bool hasEnumMemberValues)
        => hasEnumMemberValues
            ? HeaderBuilder.WithUsings(
                "System.CodeDom.Compiler",
                "System.Runtime.Serialization",
                "System.Text.Json.Serialization")
            : HeaderBuilder.WithUsings("System.CodeDom.Compiler");

    /// <summary>
    /// Generates the content for a single enum using GenerateContentForEnum.
    /// </summary>
    public static string GenerateEnumContent(EnumParameters parameters)
    {
        var generator = new GenerateContentForEnum(
            new CodeDocumentationTagsGenerator(),
            parameters);

        return generator.Generate();
    }
}