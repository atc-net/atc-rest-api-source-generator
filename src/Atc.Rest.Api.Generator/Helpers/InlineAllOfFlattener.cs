namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Turns an inline <c>allOf</c> on a request or response body into the one object it describes.
/// <para>
/// <c>allOf: [$ref Envelope, { properties: { items: ... } }]</c> written directly on an operation has no
/// name, and no single component covers it: typed as <c>Envelope</c> it loses <c>items</c>. As one inline
/// object it gets a model of its own (<c>{OperationId}Response</c>), the same as any other inline object.
/// </para>
/// <para>
/// Left alone: a generic page with its items (<c>PaginatedResult&lt;Account&gt;</c>), a single reference
/// (an alias for the component), and a composition over something that is not a plain object.
/// </para>
/// </summary>
public static class InlineAllOfFlattener
{
    /// <summary>
    /// Flattens the inline <c>allOf</c> bodies of every operation and webhook in place. Running it again
    /// changes nothing.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    public static void Flatten(OpenApiDocument document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (document.Paths is not null)
        {
            foreach (var pathItem in document.Paths.Values)
            {
                FlattenOperations(pathItem, document);
            }
        }

        if (document.Webhooks is not null)
        {
            foreach (var pathItem in document.Webhooks.Values)
            {
                FlattenOperations(pathItem, document);
            }
        }
    }

    private static void FlattenOperations(
        IOpenApiPathItem? pathItem,
        OpenApiDocument document)
    {
        if (pathItem?.Operations is null)
        {
            return;
        }

        foreach (var operation in pathItem.Operations.Values)
        {
            if (operation is null)
            {
                continue;
            }

            UnwrapParameters(pathItem.Parameters);
            UnwrapParameters(operation.Parameters);
            FlattenContent(operation.RequestBody?.Content, document);

            if (operation.Responses is null)
            {
                continue;
            }

            foreach (var response in operation.Responses.Values)
            {
                FlattenContent(response?.Content, document);
            }
        }
    }

    // A parameter whose schema wraps one reference in an allOf - the OpenAPI 3.0 way to give a referenced enum a
    // description or 'nullable' (allOf: [$ref, {}], what springdoc writes) - is the reference: every generator then
    // types, binds and sends it as the referenced type, not as an object
    private static void UnwrapParameters(IList<IOpenApiParameter>? parameters)
    {
        foreach (var parameter in parameters ?? [])
        {
            if (parameter.Resolve().Parameter is OpenApiParameter { Schema: OpenApiSchema schema } resolved &&
                schema.SingleWrappedReference() is { } reference)
            {
                resolved.Schema = reference;
            }
        }
    }

    private static void FlattenContent(
        IDictionary<string, IOpenApiMediaType>? content,
        OpenApiDocument document)
    {
        if (content is null)
        {
            return;
        }

        foreach (var value in content.Values)
        {
            if (value is not OpenApiMediaType { Schema: OpenApiSchema schema } mediaType)
            {
                continue;
            }

            if (TryFlatten(schema, document, out var flattened))
            {
                mediaType.Schema = flattened;
            }
            else if (schema.IsOfType(JsonSchemaType.Array) &&
                     schema.Items is OpenApiSchema items &&
                     TryFlatten(items, document, out var flattenedItems))
            {
                schema.Items = flattenedItems;
            }
        }
    }

    private static bool TryFlatten(
        OpenApiSchema schema,
        OpenApiDocument document,
        out OpenApiSchema flattened)
    {
        flattened = schema;

        if (schema.AllOf is not { Count: > 0 } ||
            schema.OneOf is { Count: > 0 } ||
            schema.AnyOf is { Count: > 0 })
        {
            return false;
        }

        var references = schema.AllOf.Count(part => part is OpenApiSchemaReference);
        var inlineObjects = schema.AllOf.Count(part => part is OpenApiSchema { Properties.Count: > 0 });

        // A single reference and nothing of its own is that component under another description
        if (references + inlineObjects < 2 && !(references == 0 && inlineObjects == 1))
        {
            return false;
        }

        if (IsGenericPageWithItems(schema, document))
        {
            return false;
        }

        var properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        var propertyOrder = new List<string>();
        var required = new HashSet<string>(StringComparer.Ordinal);

        if (!TryCollect(schema, properties, propertyOrder, required, depth: 0) || propertyOrder.Count == 0)
        {
            return false;
        }

        var type = JsonSchemaType.Object;
        if (schema.Type?.HasFlag(JsonSchemaType.Null) == true)
        {
            type |= JsonSchemaType.Null;
        }

        flattened = new OpenApiSchema
        {
            Type = type,
            Description = schema.Description,
            Deprecated = schema.Deprecated,
            Required = required,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal),
        };

        foreach (var name in propertyOrder)
        {
            flattened.Properties.Add(name, properties[name]);
        }

        return true;
    }

    /// <summary>
    /// The generic page pattern: the base is a page with untyped items and the composition gives the items.
    /// </summary>
    private static bool IsGenericPageWithItems(
        OpenApiSchema schema,
        OpenApiDocument document)
        => schema.AllOf!.OfType<OpenApiSchemaReference>().FirstOrDefault() is { Target: OpenApiSchema baseSchema } &&
           SchemaExtractor.IsPaginationBaseSchema(baseSchema) &&
           !string.IsNullOrEmpty(schema.GetAllOfArrayOverrideItemType(document));

    /// <summary>
    /// Collects the properties of a schema and of everything it composes. A later definition of a property
    /// replaces an earlier one, in the earlier one's position. False when a part is not a plain object.
    /// </summary>
    private static bool TryCollect(
        IOpenApiSchema part,
        Dictionary<string, IOpenApiSchema> properties,
        List<string> propertyOrder,
        HashSet<string> required,
        int depth)
    {
        var actual = part is OpenApiSchemaReference reference
            ? reference.Target
            : part;

        if (actual is not OpenApiSchema schema ||
            depth > 16 ||
            schema.OneOf is { Count: > 0 } ||
            schema.AnyOf is { Count: > 0 } ||
            (schema.Type is { } type && !type.HasFlag(JsonSchemaType.Object)))
        {
            return false;
        }

        if (schema.AllOf is not null)
        {
            foreach (var sub in schema.AllOf)
            {
                if (!TryCollect(sub, properties, propertyOrder, required, depth + 1))
                {
                    return false;
                }
            }
        }

        if (schema.Properties is not null)
        {
            foreach (var property in schema.Properties)
            {
                if (!properties.ContainsKey(property.Key))
                {
                    propertyOrder.Add(property.Key);
                }

                properties[property.Key] = property.Value;
            }
        }

        if (schema.Required is not null)
        {
            foreach (var name in schema.Required)
            {
                required.Add(name);
            }
        }

        return true;
    }
}