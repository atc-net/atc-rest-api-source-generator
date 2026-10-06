namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Makes every media type of the document canonical, in the document itself, before any generator looks a body
/// up by it: the parameters are dropped (<c>application/json; charset=utf-8</c> is <c>application/json</c>), the
/// name is lower case (<c>Application/JSON</c>), and a wildcard that carries a model - <c>*/*</c> or
/// <c>application/*</c>, springdoc's default - is read as JSON, which is what such a server writes. A wildcard with
/// a binary or text schema stays what it is. Two declarations that become one media type keep the first.
/// </summary>
public static class MediaTypeNormalizer
{
    /// <summary>
    /// Rewrites the media types of every request body and response (the operations', the webhooks' and the
    /// components') to their canonical form.
    /// </summary>
    /// <param name="document">The parsed document; it is changed in place.</param>
    /// <returns>The same document.</returns>
    public static OpenApiDocument WithCanonicalMediaTypes(
        OpenApiDocument document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var pathItems = (document.Paths?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
            .Concat(document.Webhooks?.Values ?? Enumerable.Empty<IOpenApiPathItem>());
        foreach (var pathItem in pathItems)
        {
            foreach (var operation in pathItem?.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            {
                if (operation.RequestBody is OpenApiRequestBody requestBody)
                {
                    Rewrite(requestBody.Content);
                }

                foreach (var response in operation.Responses?.Values ?? Enumerable.Empty<IOpenApiResponse>())
                {
                    if (response is OpenApiResponse own)
                    {
                        Rewrite(own.Content);
                    }
                }
            }
        }

        foreach (var requestBody in document.Components?.RequestBodies?.Values ?? Enumerable.Empty<IOpenApiRequestBody>())
        {
            if (requestBody is OpenApiRequestBody own)
            {
                Rewrite(own.Content);
            }
        }

        foreach (var response in document.Components?.Responses?.Values ?? Enumerable.Empty<IOpenApiResponse>())
        {
            if (response is OpenApiResponse own)
            {
                Rewrite(own.Content);
            }
        }

        return document;
    }

    /// <summary>
    /// The canonical form of one media type: without parameters, lower case, and <c>application/json</c> for a
    /// wildcard that carries a model.
    /// </summary>
    /// <param name="mediaType">The media type as declared.</param>
    /// <param name="schema">The schema declared under it, if any.</param>
    /// <returns>The canonical media type.</returns>
    public static string Canonical(
        string mediaType,
        IOpenApiSchema? schema)
    {
        var type = (mediaType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        return (type is "*/*" or "application/*") && IsModel(schema)
            ? "application/json"
            : type;
    }

    private static bool IsModel(IOpenApiSchema? schema)
        => schema is OpenApiSchemaReference ||
           (schema is OpenApiSchema own &&
            (own.Properties is { Count: > 0 } ||
             own.AllOf is { Count: > 0 } ||
             own.OneOf is { Count: > 0 } ||
             own.AnyOf is { Count: > 0 } ||
             own.IsOfType(JsonSchemaType.Object) ||
             own.IsOfType(JsonSchemaType.Array)));

    private static void Rewrite(IDictionary<string, IOpenApiMediaType>? content)
    {
        if (content is null || content.Count == 0)
        {
            return;
        }

        var declared = content.ToList();
        var canonical = new List<KeyValuePair<string, IOpenApiMediaType>>(declared.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in declared)
        {
            var key = Canonical(entry.Key, entry.Value?.Schema);
            if (entry.Value is not null && seen.Add(key))
            {
                canonical.Add(new KeyValuePair<string, IOpenApiMediaType>(key, entry.Value));
            }
        }

        if (canonical.Select(e => e.Key).SequenceEqual(declared.Select(e => e.Key), StringComparer.Ordinal))
        {
            return;
        }

        content.Clear();
        foreach (var entry in canonical)
        {
            content[entry.Key] = entry.Value;
        }
    }
}