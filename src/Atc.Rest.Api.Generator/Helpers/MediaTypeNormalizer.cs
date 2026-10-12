namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Makes every media type of the document canonical, in the document itself, before any generator looks a body
/// up by it: the parameters are dropped (<c>application/json; charset=utf-8</c> is <c>application/json</c>), the
/// name is lower case (<c>Application/JSON</c>), and a wildcard that carries a model - <c>*/*</c> or
/// <c>application/*</c>, springdoc's default - is read as JSON, which is what such a server writes. A wildcard with
/// a binary or text schema stays what it is. Two declarations that become one media type keep the first; when the
/// second has another schema the validator warns about it (<c>ATC_API_OPR040</c>).
/// </summary>
public static class MediaTypeNormalizer
{
    // The declarations that were dropped, kept on the document: the parsed document is cached and rewritten in place,
    // so a later validation of the same specification sees one declaration - and must still warn
    private const string DroppedKey = "atc:media-types-dropped";

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

        var dropped = DroppedDeclarations(document);
        if (dropped.Count > 0)
        {
            document.Metadata ??= new Dictionary<string, object>(StringComparer.Ordinal);
            document.Metadata[DroppedKey] = dropped.ToList();
        }

        foreach (var (_, content) in ContentOf(document))
        {
            Rewrite(content);
        }

        return document;
    }

    /// <summary>
    /// The declarations that become a media type their request body or response has already declared, with another
    /// schema - what the rewrite drops, or has dropped, while saying something the kept declaration does not.
    /// </summary>
    /// <param name="document">The parsed document, before or after the rewrite.</param>
    /// <returns>Where (<c>the 200 response of operation 'getPet'</c>), the media type kept and the one dropped, both as declared.</returns>
    public static IReadOnlyList<(string Where, string Kept, string Dropped)> DroppedDeclarations(
        OpenApiDocument document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var dropped = document.Metadata is { } metadata && metadata.TryGetValue(DroppedKey, out var recorded) && recorded is List<(string Where, string Kept, string Dropped)> earlier
            ? earlier.ToList()
            : [];
        foreach (var (where, content) in ContentOf(document))
        {
            var first = new Dictionary<string, KeyValuePair<string, IOpenApiMediaType>>(StringComparer.Ordinal);
            foreach (var entry in content.Where(e => e.Value is not null))
            {
                var key = Canonical(entry.Key, entry.Value.Schema);
                if (!first.TryGetValue(key, out var kept))
                {
                    first[key] = entry;
                }
                else if (!IsSameSchema(kept.Value.Schema, entry.Value.Schema))
                {
                    dropped.Add((where, kept.Key, entry.Key));
                }
            }
        }

        return dropped;
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

    // Nothing or the same component twice; two inline schemas are not compared - they are told apart by being two
    private static bool IsSameSchema(
        IOpenApiSchema? first,
        IOpenApiSchema? second)
        => (first is null && second is null) ||
           (first is OpenApiSchemaReference one &&
            second is OpenApiSchemaReference other &&
            string.Equals(one.Reference.Id, other.Reference.Id, StringComparison.Ordinal));

    // Every content map of the document that is its owner's (a reference's is its component's), with what it belongs to
    private static IEnumerable<(string Where, IDictionary<string, IOpenApiMediaType> Content)> ContentOf(
        OpenApiDocument document)
    {
        var pathItems = (document.Paths ?? []).Select(p => (p.Key, p.Value))
            .Concat((document.Webhooks ?? new Dictionary<string, IOpenApiPathItem>(StringComparer.Ordinal)).Select(w => (w.Key, w.Value)));
        foreach (var (pathKey, pathItem) in pathItems)
        {
            foreach (var entry in pathItem?.Operations ?? new Dictionary<HttpMethod, OpenApiOperation>())
            {
                var operation = entry.Value;
                var operationId = operation?.OperationId ?? $"{entry.Key.ToString().ToUpperInvariant()} {pathKey}";
                if (operation?.RequestBody is OpenApiRequestBody { Content: { Count: > 0 } bodyContent })
                {
                    yield return ($"the request body of operation '{operationId}'", bodyContent);
                }

                foreach (var response in operation?.Responses ?? [])
                {
                    if (response.Value is OpenApiResponse { Content: { Count: > 0 } responseContent })
                    {
                        yield return ($"the {response.Key} response of operation '{operationId}'", responseContent);
                    }
                }
            }
        }

        foreach (var requestBody in document.Components?.RequestBodies ?? new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal))
        {
            if (requestBody.Value is OpenApiRequestBody { Content: { Count: > 0 } content })
            {
                yield return ($"the request body '{requestBody.Key}' (components)", content);
            }
        }

        foreach (var response in document.Components?.Responses ?? new Dictionary<string, IOpenApiResponse>(StringComparer.Ordinal))
        {
            if (response.Value is OpenApiResponse { Content: { Count: > 0 } content })
            {
                yield return ($"the response '{response.Key}' (components)", content);
            }
        }
    }

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