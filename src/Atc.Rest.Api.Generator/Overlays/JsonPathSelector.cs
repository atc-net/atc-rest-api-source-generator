namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// A selector of a segment.
/// </summary>
[SuppressMessage("Sonar", "S2094:Classes should not be empty", Justification = "The base of a closed hierarchy of selector records.")]
internal abstract record JsonPathSelector;