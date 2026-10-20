namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Finds the component schemas an XML body can carry: every schema a request or response body of an XML media type
/// (<c>application/xml</c>, <c>text/xml</c>, <c>*+xml</c> with a model schema) - or an XML part of a multipart form -
/// references, directly or through properties, array items, dictionary values and compositions. Only these models get a
/// generated XML mapping.
/// </summary>
public static class XmlBodyReachability
{
    /// <summary>
    /// Collects the names (as in <c>components.schemas</c>) of the schemas reachable from an XML body or an XML part.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>The schema names; empty when the specification has no XML body.</returns>
    public static HashSet<string> Collect(OpenApiDocument openApiDoc)
        => Collect(openApiDoc, includeResponses: true);

    /// <summary>
    /// Collects the names of the schemas reachable from an XML <b>request</b> body - the models whose validation errors
    /// an XML client reads by their XML names.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>The schema names; empty when no request body is XML.</returns>
    public static HashSet<string> CollectRequestBodies(
        OpenApiDocument openApiDoc)
        => Collect(openApiDoc, includeResponses: false);

    /// <summary>
    /// Collects the names of the schemas reachable from a request that is <b>not</b> an XML body - a request body of
    /// another media type (JSON, a form) or a parameter. A model an XML request carries that none of these reaches is
    /// only ever validated as XML, so its validation messages can name its XML names.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>The schema names.</returns>
    public static HashSet<string> CollectOtherRequests(
        OpenApiDocument openApiDoc)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        var reachable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pathItem in openApiDoc.Paths?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
        {
            foreach (var parameter in pathItem.Parameters ?? Enumerable.Empty<IOpenApiParameter>())
            {
                Walk(parameter.Schema, reachable);
            }

            foreach (var operation in pathItem.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            {
                foreach (var parameter in operation.Parameters ?? Enumerable.Empty<IOpenApiParameter>())
                {
                    Walk(parameter.Schema, reachable);
                }

                foreach (var entry in operation.RequestBody?.Content ?? new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal))
                {
                    if (!IsXmlModelBody(entry.Key, entry.Value.Schema))
                    {
                        Walk(entry.Value.Schema, reachable);
                    }
                }
            }
        }

        return reachable;
    }

    /// <summary>
    /// Whether a body of this media type and schema is an XML model body.
    /// </summary>
    /// <param name="mediaType">The media type.</param>
    /// <param name="schema">The body schema.</param>
    /// <returns><see langword="true"/> for an XML model body.</returns>
    public static bool IsXmlModelBody(
        string mediaType,
        IOpenApiSchema? schema)
        => ResponseBodyClassifier.Classify(mediaType, schema) == ResponseBodyKind.Xml;

    private static HashSet<string> Collect(
        OpenApiDocument openApiDoc,
        bool includeResponses)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        var reachable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pathItem in openApiDoc.Paths?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
        {
            foreach (var operation in pathItem.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            {
                AddXmlContent(operation.RequestBody?.Content, reachable);
                if (!includeResponses)
                {
                    continue;
                }

                // An XML part is read and written as an XML body of its model; its validation errors name the form's
                // field, so it is no request body of the XML names
                AddXmlParts(operation, reachable);

                foreach (var response in operation.Responses?.Values ?? Enumerable.Empty<IOpenApiResponse>())
                {
                    AddXmlContent(response.Content, reachable);
                }
            }
        }

        return reachable;
    }

    private static void AddXmlContent(
        IDictionary<string, IOpenApiMediaType>? content,
        HashSet<string> reachable)
    {
        if (content is null)
        {
            return;
        }

        foreach (var entry in content)
        {
            if (IsXmlModelBody(entry.Key, entry.Value.Schema))
            {
                Walk(entry.Value.Schema, reachable);
            }
        }
    }

    private static void AddXmlParts(
        OpenApiOperation operation,
        HashSet<string> reachable)
    {
        if (FormBodyHelper.FindMultipartFormData(operation) is not { } multipart)
        {
            return;
        }

        var (_, properties, _) = operation.GetFormBodySchemaInfo();
        foreach (var property in properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            if (FormBodyHelper.PartOf(property.Value, property.Key, multipart).Encoding == MultipartPartEncoding.Xml)
            {
                Walk(property.Value, reachable);
            }
        }
    }

    private static void Walk(
        IOpenApiSchema? schema,
        HashSet<string> reachable)
    {
        if (schema is null)
        {
            return;
        }

        if (schema is OpenApiSchemaReference reference)
        {
            var name = reference.Reference?.Id ?? reference.Id;
            if (name is null || !reachable.Add(name))
            {
                return;
            }

            schema = reference.Target;
            if (schema is null)
            {
                return;
            }
        }

        foreach (var property in schema.Properties?.Values ?? Enumerable.Empty<IOpenApiSchema>())
        {
            Walk(property, reachable);
        }

        Walk(schema.Items, reachable);
        Walk(schema.AdditionalProperties, reachable);

        foreach (var part in (schema.AllOf ?? Enumerable.Empty<IOpenApiSchema>())
                     .Concat(schema.OneOf ?? Enumerable.Empty<IOpenApiSchema>())
                     .Concat(schema.AnyOf ?? Enumerable.Empty<IOpenApiSchema>()))
        {
            Walk(part, reachable);
        }
    }
}