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

            // Handle prefixItems (OpenAPI 3.1 / JSON Schema 2020-12 tuple types).
            // Must come before the regular array branch — a prefixItems schema also has
            // type: array, but the tuple shape is the more specific signal.
            if (actualSchema.HasPrefixItems())
            {
                var tupleType = BuildTupleTypeForTypeScript(actualSchema, convertDates);
                if (tupleType != null)
                {
                    return isNullable ? $"{tupleType} | null" : tupleType;
                }
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
                string? refTypeName = null;
                string? arrayItemType = null;

                foreach (var subSchema in actualSchema.AllOf)
                {
                    if (subSchema is OpenApiSchemaReference allOfRef)
                    {
                        refTypeName = allOfRef.Reference.Id ?? allOfRef.Id;
                    }
                    else if (subSchema is OpenApiSchema inlineSchema && inlineSchema.Properties is { Count: > 0 })
                    {
                        // Look for an array property with $ref items (e.g., results: Account[])
                        foreach (var prop in inlineSchema.Properties.Values)
                        {
                            if (prop is OpenApiSchema { Type: JsonSchemaType.Array } arrayProp &&
                                arrayProp.Items is OpenApiSchemaReference itemRef)
                            {
                                arrayItemType = itemRef.Reference.Id ?? itemRef.Id;
                            }
                        }
                    }
                }

                if (refTypeName != null && arrayItemType != null)
                {
                    return $"{refTypeName}<{arrayItemType}>";
                }

                return refTypeName ?? "unknown";
            }

            // Handle prefixItems (OpenAPI 3.1 / JSON Schema 2020-12 tuple types).
            // Must come before the regular array branch (same reason as ToTypeScriptTypeForModel).
            if (actualSchema.HasPrefixItems())
            {
                var tupleType = BuildTupleTypeForTypeScript(actualSchema, convertDates: false);
                if (tupleType != null)
                {
                    return tupleType;
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
    /// Builds a TypeScript tuple type from a schema's <c>prefixItems</c> (OpenAPI 3.1 /
    /// JSON Schema 2020-12). Returns <c>[t1, t2, ...]</c> for strict tuples, or
    /// <c>[t1, t2, ...rest[]]</c> when <c>items</c> is set and not <c>false</c>.
    /// Returns <c>null</c> if the prefixItems extension cannot be parsed — callers
    /// fall through to the regular array path so output stays defined.
    /// </summary>
    private static string? BuildTupleTypeForTypeScript(
        OpenApiSchema schema,
        bool convertDates)
    {
        var jsonArray = GetPrefixItemsArray(schema);
        if (jsonArray == null)
        {
            return null;
        }

        var prefixTypes = new List<string>(jsonArray.Count);
        foreach (var item in jsonArray)
        {
            if (item is not JsonObject schemaObj)
            {
                return null;
            }

            prefixTypes.Add(MapPrefixItemToTypeScript(schemaObj, convertDates));
        }

        if (prefixTypes.Count == 0)
        {
            return null;
        }

        // Strict tuple: items is explicitly false, items is absent, or items is the
        // empty schema the parser materializes when the YAML writer typed `items: false`
        // (no Type, no $ref, no Items, no Properties — nothing meaningful to map).
        if (schema.IsStrictTuple() ||
            schema.Items == null ||
            IsEffectivelyEmptySchema(schema.Items))
        {
            return "[" + string.Join(", ", prefixTypes) + "]";
        }

        // Open tuple: the regular Items schema describes the rest element type.
        var restType = GetArrayItemTypeScript(schema);
        return "[" + string.Join(", ", prefixTypes) + ", ..." + restType + "[]]";
    }

    /// <summary>
    /// Microsoft.OpenApi turns <c>items: false</c> into an empty <see cref="OpenApiSchema"/>
    /// rather than null. Treat such schemas as "no rest element" — anything else would emit
    /// a meaningless <c>...unknown[]</c> rest type on every strict tuple.
    /// </summary>
    private static bool IsEffectivelyEmptySchema(IOpenApiSchema items)
    {
        if (items is OpenApiSchemaReference)
        {
            return false;
        }

        if (items is not OpenApiSchema actualItems)
        {
            return true;
        }

        return actualItems.Type == null &&
               (actualItems.Properties == null || actualItems.Properties.Count == 0) &&
               actualItems.Items == null &&
               (actualItems.AllOf == null || actualItems.AllOf.Count == 0) &&
               (actualItems.OneOf == null || actualItems.OneOf.Count == 0) &&
               (actualItems.AnyOf == null || actualItems.AnyOf.Count == 0);
    }

    /// <summary>
    /// Maps a single prefixItems JSON object to a TypeScript type. Supports <c>$ref</c>,
    /// nested <c>prefixItems</c> (recursive tuple), and the primitive type+format pair
    /// the existing mapper already understands.
    /// </summary>
    private static string MapPrefixItemToTypeScript(
        JsonObject schemaObj,
        bool convertDates)
    {
        var refStr = schemaObj["$ref"]?.GetValue<string>();
        if (refStr != null)
        {
            return ExtractRefName(refStr);
        }

        var typeStr = schemaObj["type"]?.GetValue<string>();
        var format = schemaObj["format"]?.GetValue<string>();

        return typeStr switch
        {
            "string" => GetStringTypeName(format, convertDates),
            "integer" => "number",
            "number" => "number",
            "boolean" => "boolean",
            "array" => "unknown[]",
            _ => "unknown",
        };
    }

    /// <summary>
    /// Locates the <c>prefixItems</c> JSON array on a schema. OpenAPI 3.1 specs land it in
    /// <see cref="Microsoft.OpenApi.OpenApiSchema.UnrecognizedKeywords"/>; older specs that
    /// jam it through the extension layer land it in <see cref="Microsoft.OpenApi.OpenApiSchema.Extensions"/>.
    /// </summary>
    private static JsonArray? GetPrefixItemsArray(OpenApiSchema schema)
    {
        if (schema.UnrecognizedKeywords != null &&
            schema.UnrecognizedKeywords.TryGetValue("prefixItems", out var unrecognizedNode) &&
            unrecognizedNode is JsonArray unrecognizedArray)
        {
            return unrecognizedArray;
        }

        if (schema.Extensions != null &&
            schema.Extensions.TryGetValue("prefixItems", out var extension) &&
            extension is JsonNodeExtension jsonNodeExt &&
            jsonNodeExt.Node is JsonArray extensionArray)
        {
            return extensionArray;
        }

        return null;
    }

    private static string ExtractRefName(string refStr)
    {
        // "#/components/schemas/Account" -> "Account"
        var lastSlash = refStr.LastIndexOf('/');
        return lastSlash >= 0 && lastSlash + 1 < refStr.Length
            ? refStr.Substring(lastSlash + 1)
            : refStr;
    }

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