namespace Atc.OpenApi.Extensions;

/// <summary>
/// Maps OpenAPI types to TypeScript type names.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
public static class TypeScriptTypeMapper
{
    /// <summary>
    /// Maps a JsonSchemaType and format to a TypeScript type name.
    /// Handles combined type flags (e.g., String | Null in OpenAPI 3.1).
    /// </summary>
    /// <param name="schemaType">The JSON schema type (can be combined flags like String | Null).</param>
    /// <param name="format">Optional format string (e.g., "int64", "uuid", "date-time").</param>
    /// <param name="convertDates">When true, maps date/date-time formats to Date instead of string.</param>
    /// <returns>The TypeScript type name, or "unknown" if type cannot be determined.</returns>
    public static string ToTypeScriptTypeName(
        this JsonSchemaType? schemaType,
        string? format = null,
        bool convertDates = false)
    {
        if (schemaType == null)
        {
            return "unknown";
        }

        var typeValue = schemaType.Value;

        // Strip the Null flag for matching (JsonSchemaType is a flags enum in OpenAPI 3.1.0)
        if (typeValue.HasFlag(JsonSchemaType.Null))
        {
            typeValue &= ~JsonSchemaType.Null;
        }

        if (typeValue.HasFlag(JsonSchemaType.Integer))
        {
            return "number";
        }

        if (typeValue.HasFlag(JsonSchemaType.Number))
        {
            return "number";
        }

        if (typeValue.HasFlag(JsonSchemaType.String))
        {
            return GetStringTypeName(format, convertDates);
        }

        if (typeValue.HasFlag(JsonSchemaType.Boolean))
        {
            return "boolean";
        }

        if (typeValue.HasFlag(JsonSchemaType.Array))
        {
            return "unknown[]";
        }

        return "unknown";
    }

    /// <param name="schema">The OpenAPI schema interface.</param>
    extension(IOpenApiSchema schema)
    {
        /// <summary>
        /// Maps an OpenAPI schema to a TypeScript type string for model properties.
        /// Handles $ref, allOf, arrays, nullable (T | null), Record&lt;string, T&gt;, and primitives.
        /// </summary>
        /// <param name="isRequired">Whether the property is in the required array.</param>
        /// <param name="convertDates">When true, maps date/date-time formats to Date instead of string.</param>
        /// <returns>A TypeScript type string representation.</returns>
        public string ToTypeScriptTypeForModel(
            bool isRequired,
            bool convertDates = false)
        {
            // Handle schema references
            if (schema is OpenApiSchemaReference schemaRef)
            {
                var refName = schemaRef.Reference.Id ?? schemaRef.Id ?? "unknown";
                return refName;
            }

            // Handle actual schemas
            if (schema is not OpenApiSchema actualSchema)
            {
                return "unknown";
            }

            // Check if schema has nullable: true
            var isNullable = actualSchema.IsNullable();

            // Handle allOf compositions (commonly used for nullable $ref in OpenAPI 3.0)
            if (actualSchema.AllOf is { Count: > 0 })
            {
                foreach (var subSchema in actualSchema.AllOf)
                {
                    if (subSchema is OpenApiSchemaReference allOfRef)
                    {
                        var refName = allOfRef.Reference.Id ?? allOfRef.Id ?? "unknown";
                        return isNullable ? $"{refName} | null" : refName;
                    }
                }
            }

            // Handle oneOf with single reference
            if (actualSchema.OneOf is { Count: 1 } && actualSchema.OneOf[0] is OpenApiSchemaReference oneOfRef)
            {
                var refName = oneOfRef.Reference.Id ?? oneOfRef.Id ?? "unknown";
                return isNullable ? $"{refName} | null" : refName;
            }

            // Handle additionalProperties (Dictionary/Record types)
            if (actualSchema.AdditionalProperties != null)
            {
                var valueType = actualSchema.AdditionalProperties.ToTypeScriptTypeForModel(isRequired: true, convertDates);
                return isNullable ? $"Record<string, {valueType}> | null" : $"Record<string, {valueType}>";
            }

            // Handle array types
            if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true)
            {
                var itemType = GetArrayItemTypeScript(actualSchema);
                return isNullable ? $"{itemType}[] | null" : $"{itemType}[]";
            }

            // Handle primitive types
            var baseType = actualSchema.Type.ToTypeScriptTypeName(actualSchema.Format, convertDates);

            return isNullable ? $"{baseType} | null" : baseType;
        }

        /// <summary>
        /// Maps an OpenAPI response schema to a TypeScript return type for client methods.
        /// Handles $ref, arrays, binary (Blob), allOf pagination patterns, and primitives.
        /// </summary>
        /// <returns>A TypeScript type string representation for method return types.</returns>
        public string ToTypeScriptReturnType()
        {
            // Handle schema references
            if (schema is OpenApiSchemaReference schemaRef)
            {
                return schemaRef.Reference.Id ?? schemaRef.Id ?? "unknown";
            }

            if (schema is not OpenApiSchema actualSchema)
            {
                return "unknown";
            }

            // Handle binary response
            if (actualSchema.Type?.HasFlag(JsonSchemaType.String) == true &&
                string.Equals(actualSchema.Format, "binary", StringComparison.OrdinalIgnoreCase))
            {
                return "Blob";
            }

            // Handle allOf (pagination pattern: allOf with $ref to PaginatedResult + inline results)
            if (actualSchema.AllOf is { Count: > 0 })
            {
                foreach (var subSchema in actualSchema.AllOf)
                {
                    if (subSchema is OpenApiSchemaReference allOfRef)
                    {
                        return allOfRef.Reference.Id ?? allOfRef.Id ?? "unknown";
                    }
                }
            }

            // Handle array types
            if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true)
            {
                var itemType = GetArrayItemTypeScript(actualSchema);
                return $"{itemType}[]";
            }

            // Handle primitive types
            return actualSchema.Type.ToTypeScriptTypeName(actualSchema.Format);
        }
    }

    /// <summary>
    /// Gets the TypeScript type name for String JsonSchemaType with format.
    /// </summary>
    private static string GetStringTypeName(
        string? format,
        bool convertDates = false)
        => format?.ToLowerInvariant() switch
        {
            "binary" => "Blob | File",
            "byte" => "string",
            "uuid" => "string",
            "guid" => "string",
            "date-time" => convertDates ? "Date" : "string",
            "date" => convertDates ? "Date" : "string",
            "uri" => "string",
            _ => "string",
        };

    /// <summary>
    /// Gets the TypeScript item type for an array schema.
    /// </summary>
    private static string GetArrayItemTypeScript(OpenApiSchema arraySchema)
    {
        if (arraySchema.Items == null)
        {
            return "unknown";
        }

        if (arraySchema.Items is OpenApiSchemaReference itemRef)
        {
            return itemRef.Reference.Id ?? itemRef.Id ?? "unknown";
        }

        if (arraySchema.Items is OpenApiSchema itemSchema)
        {
            return itemSchema.Type.ToTypeScriptTypeName(itemSchema.Format);
        }

        return "unknown";
    }
}