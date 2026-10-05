namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// A segment of a query: its selectors, applied to each input node (child) or to each input node and all its
/// descendants (descendant, <c>..</c>).
/// </summary>
internal sealed record JsonPathSegment(
    bool IsDescendant,
    IReadOnlyList<JsonPathSelector> Selectors);