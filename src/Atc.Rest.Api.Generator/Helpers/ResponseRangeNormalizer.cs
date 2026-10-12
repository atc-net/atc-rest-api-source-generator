namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Rewrites every response declared for a status range (<c>2XX</c>, <c>4XX</c>, <c>5XX</c>) to its first status
/// (<c>200</c>, <c>400</c>, <c>500</c>), in the document itself, before any generator reads the responses: a range
/// names no status a server could answer or a client could read with. An operation that declares the first status
/// itself keeps it, and the range is dropped. The validator warns about every range (<c>ATC_API_OPR034</c>).
/// </summary>
public static class ResponseRangeNormalizer
{
    // The ranges that were rewritten, kept on the operation: the parsed document is cached and rewritten in place, so
    // a later validation of the same specification sees no range - and must still warn
    private const string RewrittenKey = "atc:response-ranges-rewritten";

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
                Rewrite(operation);
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

    /// <summary>
    /// The status ranges an operation declares - or declared, before the document was rewritten - each with its first
    /// status and whether the operation declares that status itself (the range is then not generated).
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The ranges, in the order declared.</returns>
    public static IReadOnlyList<(string Range, string Status, bool StatusDeclared)> RangesOf(
        OpenApiOperation? operation)
    {
        if (operation?.Responses is not { } responses)
        {
            return [];
        }

        var rewritten = operation.Metadata is { } metadata && metadata.TryGetValue(RewrittenKey, out var recorded) && recorded is List<(string Range, string Status, bool StatusDeclared)> ranges
            ? ranges
            : [];
        var statuses = new HashSet<string>(responses.Keys.Where(k => FirstStatusOf(k) is null), StringComparer.Ordinal);
        return [.. rewritten, .. responses.Keys.Where(k => FirstStatusOf(k) is not null).Select(k => (k, FirstStatusOf(k)!, statuses.Contains(FirstStatusOf(k)!)))];
    }

    private static void Rewrite(OpenApiOperation? operation)
    {
        var responses = operation?.Responses;
        if (operation is null || responses is null || !responses.Keys.Any(k => FirstStatusOf(k) is not null))
        {
            return;
        }

        operation.Metadata ??= new Dictionary<string, object>(StringComparer.Ordinal);
        operation.Metadata[RewrittenKey] = RangesOf(operation).ToList();

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