namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Removes the content of every response of a HEAD operation, in the document itself, before any generator reads
/// the responses: a HEAD answer has no body (RFC 9110 §9.3.2), whatever the operation declares - a specification
/// often gives its HEAD the responses of its GET. Without this a client would type a value that is never there. The
/// description and the headers of a response stay; a response that is a reference is replaced by its own copy, so the
/// component other operations share keeps its content. The validator warns about every such operation
/// (<c>ATC_API_OPR039</c>).
/// </summary>
public static class HeadResponseNormalizer
{
    // What was removed, kept on the operation: the parsed document is cached and rewritten in place, so a later
    // validation of the same specification sees no content - and must still warn
    private const string RemovedKey = "atc:head-response-content-removed";

    /// <summary>
    /// Removes the response content of every HEAD operation (the paths' and the webhooks').
    /// </summary>
    /// <param name="document">The parsed document; it is changed in place.</param>
    /// <returns>The same document.</returns>
    public static OpenApiDocument WithoutResponseBodies(
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
            foreach (var operation in pathItem?.Operations ?? new Dictionary<HttpMethod, OpenApiOperation>())
            {
                if (operation.Key == HttpMethod.Head && operation.Value is { } head)
                {
                    Rewrite(head);
                }
            }
        }

        return document;
    }

    /// <summary>
    /// The response keys of a HEAD operation that declare content, in the order declared; none for any other method.
    /// </summary>
    /// <param name="method">The operation's method.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The keys (<c>200</c>, <c>404</c>, <c>default</c>) whose content is not generated.</returns>
    public static IReadOnlyList<string> ResponsesWithContent(
        HttpMethod method,
        OpenApiOperation? operation)
    {
        if (method != HttpMethod.Head || operation?.Responses is not { } responses)
        {
            return [];
        }

        var removed = operation.Metadata is { } metadata && metadata.TryGetValue(RemovedKey, out var recorded) && recorded is List<string> keys
            ? keys
            : [];
        return [.. removed, .. responses.Where(r => r.Value?.Content is { Count: > 0 } && !removed.Contains(r.Key, StringComparer.Ordinal)).Select(r => r.Key)];
    }

    private static void Rewrite(OpenApiOperation operation)
    {
        var withContent = ResponsesWithContent(HttpMethod.Head, operation);
        if (operation.Responses is not { } responses || withContent.Count == 0)
        {
            return;
        }

        operation.Metadata ??= new Dictionary<string, object>(StringComparer.Ordinal);
        operation.Metadata[RemovedKey] = withContent.ToList();

        foreach (var entry in responses.ToList())
        {
            if (entry.Value?.Content is not { Count: > 0 })
            {
                continue;
            }

            if (entry.Value is OpenApiResponse own)
            {
                own.Content = null;
                continue;
            }

            // A reference: the component is another operation's too
            responses[entry.Key] = new OpenApiResponse
            {
                Description = entry.Value.Description,
                Headers = entry.Value.Headers,
                Links = entry.Value.Links,
                Extensions = entry.Value.Extensions,
            };
        }
    }
}