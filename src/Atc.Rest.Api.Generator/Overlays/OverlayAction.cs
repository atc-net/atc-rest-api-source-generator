namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// An action of an overlay, read and checked (a reusable action resolved into its reference).
/// </summary>
internal sealed record OverlayAction(
    int Index,
    JsonPathQuery Target,
    string? Description,
    bool HasUpdate,
    JsonNode? Update,
    JsonPathQuery? Copy,
    bool Remove);