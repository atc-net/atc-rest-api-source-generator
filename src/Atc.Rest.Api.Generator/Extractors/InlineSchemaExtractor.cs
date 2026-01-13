namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts inline object schemas from OpenAPI operations and generates record types for them.
/// Inline schemas are those defined directly in request bodies or responses rather than as $ref to components/schemas.
/// </summary>
public static class InlineSchemaExtractor
{
    /// <summary>
    /// Checks if a schema is an inline object with properties (not a $ref to components/schemas).
    /// </summary>
    /// <param name="schema">The schema to check.</param>
    /// <returns>True if the schema is an inline object with properties.</returns>
    public static bool IsInlineObjectSchema(IOpenApiSchema? schema)
    {
        // Must not be a reference (those are handled by the normal schema extraction)
        if (schema is OpenApiSchemaReference)
        {
            return false;
        }

        // Must be an actual schema with object type and properties
        if (schema is not OpenApiSchema actualSchema)
        {
            return false;
        }

        return actualSchema.Type?.HasFlag(JsonSchemaType.Object) == true &&
               actualSchema.Properties != null &&
               actualSchema.Properties.Count > 0;
    }

    /// <summary>
    /// Generates a unique type name for an inline schema based on operation context.
    /// </summary>
    /// <param name="operationId">The operation ID (e.g., "listReports").</param>
    /// <param name="context">The context: "Response", "Request", or "ResponseItem".</param>
    /// <param name="statusCode">Optional status code for response types (e.g., "200").</param>
    /// <returns>The generated type name (e.g., "ListReportsResponseItem").</returns>
    public static string GenerateInlineTypeName(
        string operationId,
        string context,
        string? statusCode = null)
    {
        var baseName = operationId.ToPascalCaseForDotNet();

        // For non-200 responses, include the status code to avoid conflicts
        if (!string.IsNullOrEmpty(statusCode) && statusCode != "200" && statusCode != "default")
        {
            return $"{baseName}{context}{statusCode}";
        }

        return $"{baseName}{context}";
    }

    /// <summary>
    /// Extracts a RecordParameters from an inline schema.
    /// </summary>
    /// <param name="schema">The inline schema to extract.</param>
    /// <param name="typeName">The generated type name for the record.</param>
    /// <param name="registry">Optional type conflict registry.</param>
    /// <returns>The record parameters for code generation.</returns>
    public static RecordParameters ExtractRecordFromInlineSchema(
        OpenApiSchema schema,
        string typeName,
        TypeConflictRegistry? registry = null)
    {
        var properties = schema.Properties?.ToList() ?? [];
        var required = schema.Required ?? new HashSet<string>(StringComparer.Ordinal);

        var parametersList = new List<ParameterBaseParameters>();
        var seenPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in properties)
        {
            var propName = prop.Key.ToPascalCaseForDotNet();

            // Skip duplicate property names
            if (!seenPropertyNames.Add(propName))
            {
                continue;
            }

            // Rename property if it matches the enclosing type name
            if (propName.Equals(typeName, StringComparison.OrdinalIgnoreCase))
            {
                propName = propName + "Value";
            }

            var isRequired = required.Contains(prop.Key, StringComparer.Ordinal);

            // Get C# type using model type mapping
            var csharpType = prop.Value.HasAdditionalProperties()
                ? prop.Value.GetDictionaryTypeString(isRequired, registry) ?? prop.Value.ToCSharpTypeForModel(isRequired, registry)
                : prop.Value.ToCSharpTypeForModel(isRequired, registry);

            // Extract nullability
            var isNullableType = csharpType.EndsWith("?", StringComparison.Ordinal);
            var cleanTypeName = isNullableType
                ? csharpType.Substring(0, csharpType.Length - 1)
                : csharpType;

            // Get validation attributes
            var validationAttributes = prop.Value.GetValidationAttributes(isRequired);

            var isReferenceType = cleanTypeName.IsReferenceType();

            IList<AttributeParameters>? attributes = null;
            if (validationAttributes.Count > 0)
            {
                attributes = validationAttributes
                    .Select(attr => new AttributeParameters(attr, null))
                    .ToList();
            }

            // Extract default value
            var defaultValue = DefaultValueHelper.ExtractSchemaDefault(prop.Value, cleanTypeName);

            parametersList.Add(new ParameterBaseParameters(
                Attributes: attributes,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: cleanTypeName,
                IsNullableType: isNullableType,
                IsReferenceType: isReferenceType,
                Name: propName,
                DefaultValue: defaultValue));
        }

        // Sort: required without defaults first, then parameters with defaults
        var sortedParameters = parametersList
            .OrderBy(p => p.DefaultValue != null ? 1 : 0)
            .ToList();

        return new RecordParameters(
            DocumentationTags: null,
            DeclarationModifier: DeclarationModifiers.PublicSealedRecord,
            Name: typeName,
            Parameters: sortedParameters);
    }

    /// <summary>
    /// Checks if an array schema has inline object items.
    /// </summary>
    /// <param name="schema">The array schema to check.</param>
    /// <returns>True if the array items are inline objects with properties.</returns>
    public static bool HasInlineArrayItems(OpenApiSchema schema)
    {
        if (schema.Type?.HasFlag(JsonSchemaType.Array) != true)
        {
            return false;
        }

        return IsInlineObjectSchema(schema.Items);
    }

    /// <summary>
    /// Gets the inline schema from an array's items.
    /// </summary>
    /// <param name="schema">The array schema.</param>
    /// <returns>The inline schema if present, null otherwise.</returns>
    public static OpenApiSchema? GetInlineArrayItemSchema(OpenApiSchema schema)
    {
        if (!HasInlineArrayItems(schema))
        {
            return null;
        }

        return schema.Items as OpenApiSchema;
    }
}