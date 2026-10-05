namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// <c>[start:end:step]</c>.
/// </summary>
internal sealed record SliceSelector(
    long? Start,
    long? End,
    long? Step) : JsonPathSelector;