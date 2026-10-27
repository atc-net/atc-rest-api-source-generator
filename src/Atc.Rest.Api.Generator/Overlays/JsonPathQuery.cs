namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// An RFC 9535 JSONPath query - the language an OpenAPI Overlay's <c>target</c> and <c>copy</c> are written in.
/// <para>
/// The whole RFC is implemented: name, wildcard, index, slice and filter selectors, child and descendant segments,
/// comparisons, the logical operators, and the functions <c>length</c>, <c>count</c>, <c>match</c>, <c>search</c>
/// and <c>value</c> (with the well-typedness checks done when the query is parsed). The official compliance test
/// suite runs in the test project.
/// </para>
/// </summary>
public sealed class JsonPathQuery
{
    private readonly JsonPathSegment[] segments;

    private JsonPathQuery(
        string text,
        JsonPathSegment[] segments)
    {
        Text = text;
        this.segments = segments;
    }

    /// <summary>
    /// Gets the query as it was written.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Parses a query.
    /// </summary>
    /// <param name="query">The query, for example <c>$.paths['/pets'].get</c>.</param>
    /// <returns>The parsed query.</returns>
    /// <exception cref="JsonPathException">The query is not valid RFC 9535 JSONPath.</exception>
    public static JsonPathQuery Parse(string query)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        return new JsonPathQuery(query, new JsonPathParser(query).ParseRootQuery());
    }

    /// <summary>
    /// Selects the nodes of a document, in the order RFC 9535 gives them (duplicates included).
    /// </summary>
    /// <param name="root">The document; <see langword="null"/> for the JSON value <c>null</c>.</param>
    /// <returns>The selected nodes with their locations.</returns>
    public IReadOnlyList<JsonPathMatch> Select(JsonNode? root)
        => JsonPathEvaluator.Select(segments, new JsonPathMatch(root, parent: null, memberName: null, arrayIndex: -1), root);

    /// <inheritdoc />
    public override string ToString()
        => Text;
}