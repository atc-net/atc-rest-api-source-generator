namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Rewrites every response declared for a status range (<c>2XX</c>, <c>4XX</c>, <c>5XX</c>) to its first status
/// (<c>200</c>, <c>400</c>, <c>500</c>), in the document itself, before any generator reads the responses: a range
/// names no status a server could answer or a client could read with. An operation that declares the first status
/// itself keeps it, and the range is dropped. The validator warns about every range (<c>ATC_API_OPR034</c>).
/// </summary>
public static class ResponseRangeNormalizer
{
    /// <summary>
    /// Rewrites the status ranges of every operation's responses (the paths' and the webhooks').
    /// </summary>
    /// <param name="document">The parsed document; it is changed in place.</param>
    /// <returns>The same document.</returns>
    public static OpenApiDocument WithExplicitStatuses(OpenApiDocument document)
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
                Rewrite(operation.Responses);
            }
        }

        return document;
    }

    /// <summary>
    /// The first status of a range (<c>2XX</c> is 200), or <see langword="null"/> for a key that is no range.
    /// </summary>
    /// <param name="responseKey">The response key as declared.</param>
    /// <returns>The status the range is generated as.</returns>
    public static string? FirstStatusOf(string responseKey)
        => responseKey is { Length: 3 } &&
           responseKey[0] is >= '1' and <= '5' &&
           responseKey[1] is 'X' or 'x' &&
           responseKey[2] is 'X' or 'x'
            ? responseKey[0] + "00"
            : null;

    private static void Rewrite(OpenApiResponses? responses)
    {
        if (responses is null || !responses.Keys.Any(k => FirstStatusOf(k) is not null))
        {
            return;
        }

        // A range never replaces a status the operation declares; the order is kept
        var declared = responses.ToList();
        var statuses = new HashSet<string>(declared.Select(e => e.Key).Where(k => FirstStatusOf(k) is null), StringComparer.Ordinal);
        responses.Clear();
        foreach (var entry in declared)
        {
            var status = FirstStatusOf(entry.Key);
            if (status is null)
            {
                responses[entry.Key] = entry.Value;
            }
            else if (statuses.Add(status))
            {
                responses[status] = entry.Value;
            }
        }
    }
}