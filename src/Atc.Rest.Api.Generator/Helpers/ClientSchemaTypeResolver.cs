namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// The C# type a generated client reads a body schema as - one rule for the typed and the per-operation client, which
/// differ only in their collection type and in where an inline object schema's record goes: a <c>$ref</c> is its
/// model, an array alias (a component that is an array of items, not a <c>prefixItems</c> tuple) and an inline array
/// are a collection of their items, an <c>allOf</c> page is <c>PaginatedResult&lt;Item&gt;</c>, and a primitive is its C#
/// type.
/// </summary>
public static class ClientSchemaTypeResolver
{
    /// <summary>
    /// The C# type of a schema.
    /// </summary>
    /// <param name="schema">The schema.</param>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="registry">The type-conflict registry, or <see langword="null"/>.</param>
    /// <param name="collection">The generic collection type of an array, e.g. <c>List</c>.</param>
    /// <param name="topCollection">The collection type of the schema itself when it is an array (e.g. <c>IAsyncEnumerable</c>), or <see langword="null"/> for <paramref name="collection"/>.</param>
    /// <param name="context">The context of an inline object schema (<c>Response</c>), or <see langword="null"/>.</param>
    /// <param name="inlineTypeName">The type of an inline object schema in a context (its record registered by the caller), or <see langword="null"/> when the caller has none.</param>
    /// <returns>The C# type name.</returns>
    public static string Resolve(
        IOpenApiSchema? schema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        string collection,
        string? topCollection = null,
        string? context = null,
        Func<OpenApiSchema, string, string?>? inlineTypeName = null)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refId = schemaRef.Reference.Id;
            if (string.IsNullOrEmpty(refId))
            {
                return "object";
            }

            // An array alias ("Pets": an array of Pet) is the collection; a tuple (prefixItems) keeps its own type
            if (openApiDoc.Components?.Schemas is not null &&
                openApiDoc.Components.Schemas.TryGetValue(refId!, out var resolvedSchema) &&
                resolvedSchema is OpenApiSchema arraySchema &&
                arraySchema.IsOfType(JsonSchemaType.Array) &&
                !arraySchema.HasPrefixItems())
            {
                return CollectionOf(arraySchema, openApiDoc, registry, topCollection ?? collection, collection, context, inlineTypeName);
            }

            return OpenApiSchemaExtensions.ResolveTypeName(refId!, registry);
        }

        if (schema is not OpenApiSchema actualSchema)
        {
            return "object";
        }

        if (actualSchema.AllOf is { Count: > 0 })
        {
            return PaginationTypeResolver.GetAllOfSchemaTypeName(
                actualSchema.AllOf,
                openApiDoc,
                registry,
                item => Resolve(item, openApiDoc, registry, collection));
        }

        if (actualSchema.IsOfType(JsonSchemaType.Array))
        {
            // A component's own schema (a tuple reached through its target) keeps its name
            var schemaName = SchemaLookup.FindSchemaNameByReference(openApiDoc, actualSchema);
            if (!string.IsNullOrEmpty(schemaName) && actualSchema.HasPrefixItems())
            {
                return OpenApiSchemaExtensions.ResolveTypeName(schemaName!, registry);
            }

            return CollectionOf(actualSchema, openApiDoc, registry, topCollection ?? collection, collection, context, inlineTypeName);
        }

        if (context is not null &&
            inlineTypeName is not null &&
            InlineSchemaExtractor.IsInlineObjectSchema(actualSchema) &&
            inlineTypeName(actualSchema, context) is { } inlineType)
        {
            return inlineType;
        }

        return actualSchema.Type.ToPrimitiveCSharpTypeName(actualSchema.Format) ?? "object";
    }

    private static string CollectionOf(
        OpenApiSchema arraySchema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        string thisCollection,
        string collection,
        string? context,
        Func<OpenApiSchema, string, string?>? inlineTypeName)
    {
        // An item has the context of its array: ResponseItem for a success body, {Status}ErrorItem for an error body,
        // so an error's item never takes the success item's type
        var itemContext = context is null or "Response" ? "ResponseItem" : context + "Item";
        var itemType = arraySchema.Items is null
            ? "object"
            : Resolve(arraySchema.Items, openApiDoc, registry, collection, topCollection: null, itemContext, inlineTypeName);
        return $"{thisCollection}<{itemType}>";
    }
}