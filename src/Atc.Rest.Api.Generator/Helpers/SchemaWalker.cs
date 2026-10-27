namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Visits every schema a generator can reach in a document - the components, the parameters, request and response
/// bodies of the paths and the webhooks, and everything nested in them (properties, items, additional properties,
/// allOf/oneOf/anyOf) - each once.
/// </summary>
public static class SchemaWalker
{
    /// <summary>
    /// Whether any reachable schema matches.
    /// </summary>
    /// <param name="openApiDoc">The specification.</param>
    /// <param name="predicate">The test for one schema.</param>
    /// <returns>True when a schema matches.</returns>
    public static bool Any(
        OpenApiDocument openApiDoc,
        Func<IOpenApiSchema, bool> predicate)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        var visited = new HashSet<IOpenApiSchema>();
        var schemas = new Stack<IOpenApiSchema>();

        foreach (var schema in openApiDoc.Components?.Schemas?.Values ?? [])
        {
            Push(schema);
        }

        // A document as the reader leaves it may hold null entries (a webhook that is no object): skipped
        var pathItems = (openApiDoc.Paths?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
            .Concat(openApiDoc.Webhooks?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
            .Where(pathItem => pathItem is not null);
        foreach (var pathItem in pathItems)
        {
            foreach (var parameter in pathItem.Parameters ?? [])
            {
                PushParameter(parameter);
            }

            foreach (var operation in pathItem.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            {
                if (operation is null)
                {
                    continue;
                }

                foreach (var parameter in operation.Parameters ?? [])
                {
                    PushParameter(parameter);
                }

                PushContent(operation.RequestBody?.Content);
                foreach (var response in operation.Responses?.Values ?? Enumerable.Empty<IOpenApiResponse>())
                {
                    PushContent(response?.Content);
                }
            }
        }

        while (schemas.Count > 0)
        {
            var schema = schemas.Pop();
            if (!visited.Add(schema))
            {
                continue;
            }

            if (predicate(schema))
            {
                return true;
            }

            foreach (var property in schema.Properties?.Values ?? [])
            {
                Push(property);
            }

            Push(schema.Items);
            Push(schema.AdditionalProperties);
            foreach (var part in (schema.AllOf ?? []).Concat(schema.OneOf ?? []).Concat(schema.AnyOf ?? []))
            {
                Push(part);
            }
        }

        return false;

        void PushParameter(IOpenApiParameter? parameter)
        {
            Push(parameter?.Schema);
            PushContent(parameter?.Content);
        }

        void PushContent(IDictionary<string, IOpenApiMediaType>? content)
        {
            foreach (var mediaType in content?.Values ?? [])
            {
                Push(mediaType?.Schema);
            }
        }

        void Push(IOpenApiSchema? schema)
        {
            if (schema is not null)
            {
                schemas.Push(schema);
            }
        }
    }
}