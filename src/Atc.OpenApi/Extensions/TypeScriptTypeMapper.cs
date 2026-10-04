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
        if (schemaType is null)
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

            // Handle oneOf/anyOf references. An explicit `type: "null"` branch is OpenAPI 3.1's
            // spelling of a nullable reference (3.1 removed the `nullable` keyword), so it is a
            // nullability marker rather than a union member — counting it as one would turn
            // `Address | null` into a two-member union including a useless null type.
            var oneOfRefs = actualSchema.GetOneOfReferenceIds();
            var nullableViaBranch = isNullable || actualSchema.HasNullBranchInOneOf();
            var hasUnnameableBranch = actualSchema.HasNonReferenceBranchInOneOf();

            if (oneOfRefs.Count == 1 && !hasUnnameableBranch)
            {
                return nullableViaBranch ? $"{oneOfRefs[0]} | null" : oneOfRefs[0];
            }

            // Several references become a TypeScript union. The Zod emitter already produces
            // z.union([...]) for this shape, so leaving the type as `unknown` had the compile-time
            // type and the runtime validator disagreeing about the same property.
            var unionType = BuildRefUnionTypeForTypeScript(actualSchema);
            if (unionType is not null)
            {
                return nullableViaBranch ? $"{unionType} | null" : unionType;
            }

            // Handle additionalProperties (Dictionary/Record types)
            if (actualSchema.AdditionalProperties is not null)
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
                if (tupleType is not null)
                {
                    return isNullable ? $"{tupleType} | null" : tupleType;
                }
            }

            // Handle array types
            if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true)
            {
                var itemType = GetArrayItemTypeScript(actualSchema, convertDates);
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
                return BuildAllOfTypeScriptType(actualSchema.AllOf);
            }

            // Handle prefixItems (OpenAPI 3.1 / JSON Schema 2020-12 tuple types).
            // Must come before the regular array branch (same reason as ToTypeScriptTypeForModel).
            if (actualSchema.HasPrefixItems())
            {
                var tupleType = BuildTupleTypeForTypeScript(actualSchema, convertDates: false);
                if (tupleType is not null)
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
    /// Recognizes the generic pagination form of an <c>allOf</c>: exactly one <c>$ref</c>, to a generic pagination
    /// base (emitted as <c>Base&lt;T&gt;</c>), and an inline member whose array property supplies the item type.
    /// </summary>
    /// <remarks>
    /// The array property may be an array (also a nullable one, <c>type: [array, 'null']</c>) or a <c>$ref</c> to
    /// an array alias. An item that is itself an array alias (<c>items: $ref Accounts</c>, Accounts = Account[]) is
    /// unwrapped to its element, as the C# generator does: the server writes the page's items as one flat array.
    /// </remarks>
    /// <param name="allOf">The <c>allOf</c> members.</param>
    /// <param name="baseName">The pagination base's schema name.</param>
    /// <param name="itemType">The TypeScript item type (a schema name or a primitive type).</param>
    /// <param name="itemSchemaName">The item's schema name to import, or <see langword="null"/> for a primitive.</param>
    /// <returns><see langword="true"/> for the generic pagination form.</returns>
    public static bool TryGetGenericPagination(
        IList<IOpenApiSchema> allOf,
        out string baseName,
        out string itemType,
        out string? itemSchemaName)
    {
        if (allOf is null)
        {
            throw new ArgumentNullException(nameof(allOf));
        }

        baseName = string.Empty;
        itemType = string.Empty;
        itemSchemaName = null;

        var refs = allOf.OfType<OpenApiSchemaReference>().ToList();
        if (refs.Count != 1 ||
            !IsGenericPaginationBase(refs[0].Target) ||
            (refs[0].Reference.Id ?? refs[0].Id) is not { } name)
        {
            return false;
        }

        var items = allOf
            .OfType<OpenApiSchema>()
            .Where(s => s.Properties is { Count: > 0 })
            .SelectMany(s => s.Properties!.Values)
            .Select(p => p switch
            {
                OpenApiSchemaReference { Target: { Type: { } aliasType } alias } when aliasType.HasFlag(JsonSchemaType.Array) => alias.Items,
                OpenApiSchema { Type: { } arrayType } array when arrayType.HasFlag(JsonSchemaType.Array) => array.Items,
                _ => null,
            })
            .FirstOrDefault(i => i is not null);

        switch (items)
        {
            case OpenApiSchemaReference itemRef:
                var element = itemRef.Target is OpenApiSchema { Type: { } itemTargetType, Items: OpenApiSchemaReference elementRef } &&
                              itemTargetType.HasFlag(JsonSchemaType.Array)
                    ? elementRef
                    : itemRef;
                itemSchemaName = element.Reference.Id ?? element.Id;
                if (itemSchemaName is null)
                {
                    return false;
                }

                itemType = itemSchemaName;
                break;
            case OpenApiSchema { Type: not null } itemSchema:
                itemType = itemSchema.ToTypeScriptReturnType();
                break;
            default:
                return false;
        }

        baseName = name;
        return true;
    }

    /// <summary>
    /// Locates the <c>prefixItems</c> JSON array on a schema. OpenAPI 3.1 specs land it in
    /// <see cref="Microsoft.OpenApi.OpenApiSchema.UnrecognizedKeywords"/>; older specs that
    /// jam it through the extension layer land it in <see cref="Microsoft.OpenApi.OpenApiSchema.Extensions"/>.
    /// </summary>
    internal static JsonArray? GetPrefixItemsArray(OpenApiSchema schema)
    {
        if (schema.UnrecognizedKeywords is not null &&
            schema.UnrecognizedKeywords.TryGetValue("prefixItems", out var unrecognizedNode) &&
            unrecognizedNode is JsonArray unrecognizedArray)
        {
            return unrecognizedArray;
        }

        if (schema.Extensions is not null &&
            schema.Extensions.TryGetValue("prefixItems", out var extension) &&
            extension is JsonNodeExtension { Node: JsonArray extensionArray })
        {
            return extensionArray;
        }

        return null;
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
    /// The TypeScript type of an inline <c>allOf</c>: <c>Base&lt;Item&gt;</c> when the referenced base is a
    /// generic pagination base (an array property with untyped items - the TypeScript model is emitted as
    /// <c>Base&lt;T&gt;</c>) and the inline member supplies the item type; otherwise the intersection of the
    /// referenced types and the inline members (<c>(PaginationResult &amp; { items?: Item[] })</c>).
    /// </summary>
    private static string BuildAllOfTypeScriptType(IList<IOpenApiSchema> allOf)
    {
        var refs = allOf.OfType<OpenApiSchemaReference>().ToList();
        var inlineMembers = allOf.OfType<OpenApiSchema>().Where(s => s.Properties is { Count: > 0 }).ToList();

        if (TryGetGenericPagination(allOf, out var baseName, out var itemType, out _))
        {
            return $"{baseName}<{itemType}>";
        }

        var parts = refs
            .Select(r => r.Reference.Id ?? r.Id)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .ToList();

        foreach (var member in inlineMembers)
        {
            var required = member.Required ?? new HashSet<string>(StringComparer.Ordinal);
            var members = member.Properties!.Select(p =>
                $"{(IsTypeScriptIdentifier(p.Key) ? p.Key : $"'{p.Key}'")}{(required.Contains(p.Key) ? string.Empty : "?")}: {p.Value.ToTypeScriptReturnType()}");
            parts.Add("{ " + string.Join("; ", members) + " }");
        }

        return parts.Count switch
        {
            0 => "unknown",
            1 => parts[0],

            // Parenthesized so it stays one type where callers embed it (`readonly X[]`, `X | null`).
            _ => "(" + string.Join(" & ", parts) + ")",
        };
    }

    /// <summary>
    /// A pagination base: an object with an array property whose items are untyped (<c>items: {}</c>) -
    /// the TypeScript model extractor emits it as <c>Name&lt;T&gt;</c>.
    /// </summary>
    private static bool IsGenericPaginationBase(IOpenApiSchema? schema)
        => schema?.Properties?.Values.Any(p =>
               p is OpenApiSchema arraySchema && arraySchema.IsOfType(JsonSchemaType.Array) &&
               (arraySchema.Items is null ||
                (arraySchema.Items is OpenApiSchema { Type: null } itemSchema &&
                 itemSchema.Properties?.Count is null or 0))) == true;

    private static bool IsTypeScriptIdentifier(string name)
        => name.Length > 0 &&
           (char.IsLetter(name[0]) || name[0] is '_' or '$') &&
           name.All(c => char.IsLetterOrDigit(c) || c is '_' or '$');

    /// <summary>
    /// Builds a TypeScript union from a schema's <c>oneOf</c> or <c>anyOf</c> branches, e.g.
    /// <c>SystemNotification | UserActivityEvent</c>.
    /// </summary>
    /// <remarks>
    /// Returns <c>null</c> — leaving the caller on its <c>unknown</c> path — unless there are at
    /// least two branches and <b>every</b> one of them is a <c>$ref</c>. A partial union is worse
    /// than none: TypeScript collapses <c>A | unknown</c> back to <c>unknown</c>, so naming some
    /// branches would suggest a precision the type does not actually have. The Zod emitter can
    /// afford a per-branch <c>z.unknown()</c> because Zod unions do not collapse.
    /// </remarks>
    private static string? BuildRefUnionTypeForTypeScript(
        OpenApiSchema schema)
    {
        var branches = schema.OneOf is { Count: > 0 }
            ? schema.OneOf
            : schema.AnyOf;

        if (branches is not { Count: > 1 })
        {
            return null;
        }

        var names = new List<string>(branches.Count);
        foreach (var branch in branches)
        {
            // A null-only branch marks the union nullable rather than adding a member; the caller
            // appends `| null`. Skipping it here keeps `oneOf: [A, B, null]` as `A | B`, and covers
            // the 3.0 spelling of the same thing, `{type: object, nullable: true}`.
            if (OpenApiSchemaExtensions.IsNullOnlyBranch(branch))
            {
                continue;
            }

            if (branch is not OpenApiSchemaReference branchRef)
            {
                return null;
            }

            var refName = branchRef.Reference.Id ?? branchRef.Id;
            if (string.IsNullOrEmpty(refName))
            {
                return null;
            }

            if (!names.Contains(refName!, StringComparer.Ordinal))
            {
                names.Add(refName!);
            }
        }

        return string.Join(" | ", names);
    }

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
        if (jsonArray is null)
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
            schema.Items is null ||
            IsEffectivelyEmptySchema(schema.Items))
        {
            return "[" + string.Join(", ", prefixTypes) + "]";
        }

        // Open tuple: the regular Items schema describes the rest element type.
        var restType = GetArrayItemTypeScript(schema, convertDates);
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

        return actualItems.Type is null &&
               (actualItems.Properties is null || actualItems.Properties.Count == 0) &&
               actualItems.Items is null &&
               (actualItems.AllOf is null || actualItems.AllOf.Count == 0) &&
               (actualItems.OneOf is null || actualItems.OneOf.Count == 0) &&
               (actualItems.AnyOf is null || actualItems.AnyOf.Count == 0);
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
        if (refStr is not null)
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
    private static string GetArrayItemTypeScript(
        OpenApiSchema arraySchema,
        bool convertDates = false)
    {
        if (arraySchema.Items is null)
        {
            return "unknown";
        }

        if (arraySchema.Items is OpenApiSchemaReference itemRef)
        {
            return itemRef.Reference.Id ?? itemRef.Id ?? "unknown";
        }

        if (arraySchema.Items is OpenApiSchema itemSchema)
        {
            return itemSchema.Type.ToTypeScriptTypeName(itemSchema.Format, convertDates);
        }

        return "unknown";
    }
}