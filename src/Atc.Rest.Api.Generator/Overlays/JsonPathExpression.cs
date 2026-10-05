namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// An expression of a filter.
/// </summary>
[SuppressMessage("Sonar", "S2094:Classes should not be empty", Justification = "The base of a closed hierarchy of expression records.")]
internal abstract record JsonPathExpression;