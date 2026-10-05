namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// A query inside a filter: relative to the current node (<c>@</c>) or to the root (<c>$</c>).
/// </summary>
internal sealed record QueryExpression(
    bool IsRelative,
    IReadOnlyList<JsonPathSegment> Segments) : JsonPathExpression
{
    /// <summary>
    /// Gets a value indicating whether the query selects at most one node: only name and index selectors, one per
    /// child segment.
    /// </summary>
    public bool IsSingular
        => Segments.All(s => !s.IsDescendant &&
                             s.Selectors.Count == 1 &&
                             s.Selectors[0] is NameSelector or IndexSelector);
}