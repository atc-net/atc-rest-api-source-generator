namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// The C# type of an <c>allOf</c> response that is a page (<c>PaginatedResult&lt;Item&gt;</c>: a <c>$ref</c> to a
/// pagination base plus an inline member whose <c>items</c> / <c>results</c> array gives the item type), and of an
/// array's items. The typed and the per-operation client resolve them the same way; only the type of an inline item
/// schema is theirs to decide.
/// </summary>
public static class PaginationTypeResolver
{
    /// <summary>
    /// Whether a type name is one of the generic pagination bases (<c>PaginationResult</c>, <c>PaginatedResult</c>,
    /// <c>PagedResult</c>).
    /// </summary>
    /// <param name="typeName">The type name.</param>
    /// <returns><see langword="true"/> for a pagination base.</returns>
    public static bool IsPaginationBaseType(string typeName)
        => typeName is not null &&
           (typeName.StartsWith("PaginationResult", StringComparison.Ordinal) ||
            typeName.StartsWith("PaginatedResult", StringComparison.Ordinal) ||
            typeName.StartsWith("PagedResult", StringComparison.Ordinal));

    /// <summary>
    /// The type of an <c>allOf</c> schema: <c>Base&lt;Item&gt;</c> for the pagination pattern, else the referenced base,
    /// else <c>object</c>.
    /// </summary>
    /// <param name="allOfSchemas">The <c>allOf</c> members.</param>
    /// <param name="openApiDoc">The OpenAPI document (array aliases are resolved through its components).</param>
    /// <param name="registry">The type-conflict registry, or <see langword="null"/>.</param>
    /// <param name="inlineItemTypeName">The type of an array item that is an inline schema.</param>
    /// <returns>The C# type name.</returns>
    public static string GetAllOfSchemaTypeName(
        IList<IOpenApiSchema> allOfSchemas,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        Func<OpenApiSchema, string> inlineItemTypeName)
    {
        if (allOfSchemas is null)
        {
            throw new ArgumentNullException(nameof(allOfSchemas));
        }

        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        // Look for pagination pattern: allOf with $ref to PaginationResult/PaginatedResult and items/results array
        string? baseType = null;
        string? itemType = null;

        foreach (var schemaItem in allOfSchemas)
        {
            if (schemaItem is OpenApiSchemaReference refSchema)
            {
                var refId = refSchema.Reference.Id;
                if (!string.IsNullOrEmpty(refId))
                {
                    baseType = OpenApiSchemaExtensions.ResolveTypeName(refId!, registry);
                }
            }
            else if (schemaItem is OpenApiSchema { Properties: not null } objSchema)
            {
                // Look for "items" or "results" property which contains the array item type
                foreach (var prop in objSchema.Properties)
                {
                    if (!prop.Key.Equals("items", StringComparison.OrdinalIgnoreCase) &&
                        !prop.Key.Equals("results", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var propSchema = prop.Value;
                    if (propSchema is OpenApiSchema arraySchema && arraySchema.IsOfType(JsonSchemaType.Array))
                    {
                        itemType = GetArrayItemTypeName(arraySchema, openApiDoc, registry, inlineItemTypeName);
                    }
                    else if (propSchema is OpenApiSchemaReference propRef)
                    {
                        // A reference to an array alias
                        var propRefId = propRef.Reference.Id;
                        if (!string.IsNullOrEmpty(propRefId) &&
                            openApiDoc.Components?.Schemas?.TryGetValue(propRefId!, out var resolvedSchema) == true &&
                            resolvedSchema is OpenApiSchema resolvedArray && resolvedArray.IsOfType(JsonSchemaType.Array))
                        {
                            itemType = GetArrayItemTypeName(resolvedArray, openApiDoc, registry, inlineItemTypeName);
                        }
                    }

                    break;
                }
            }
        }

        // If we found PaginationResult<T> or PaginatedResult<T> pattern, return it
        if (baseType is not null && IsPaginationBaseType(baseType) && itemType is not null)
        {
            return $"{baseType}<{itemType}>";
        }

        // Return the base type if found
        return baseType ?? "object";
    }

    /// <summary>
    /// The type of an array's items: a referenced model by name (an array alias is unwrapped to its element), an
    /// inline schema by <paramref name="inlineItemTypeName"/>, <c>object</c> when there are no items.
    /// </summary>
    /// <param name="arraySchema">The array schema.</param>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="registry">The type-conflict registry, or <see langword="null"/>.</param>
    /// <param name="inlineItemTypeName">The type of an item that is an inline schema.</param>
    /// <returns>The C# type name.</returns>
    public static string GetArrayItemTypeName(
        OpenApiSchema arraySchema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        Func<OpenApiSchema, string> inlineItemTypeName)
    {
        if (arraySchema is null)
        {
            throw new ArgumentNullException(nameof(arraySchema));
        }

        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (inlineItemTypeName is null)
        {
            throw new ArgumentNullException(nameof(inlineItemTypeName));
        }

        if (arraySchema.Items is null)
        {
            return "object";
        }

        if (arraySchema.Items is OpenApiSchemaReference itemRef)
        {
            var itemRefId = itemRef.Reference.Id;
            if (!string.IsNullOrEmpty(itemRefId))
            {
                // An array type alias (Accounts -> Account[]) is unwrapped to its element
                if (openApiDoc.Components?.Schemas?.TryGetValue(itemRefId!, out var itemSchema) == true &&
                    itemSchema is OpenApiSchema innerArray && innerArray.IsOfType(JsonSchemaType.Array))
                {
                    return GetArrayItemTypeName(innerArray, openApiDoc, registry, inlineItemTypeName);
                }

                return OpenApiSchemaExtensions.ResolveTypeName(itemRefId!, registry);
            }
        }
        else if (arraySchema.Items is OpenApiSchema inlineItem)
        {
            return inlineItemTypeName(inlineItem);
        }

        return "object";
    }
}