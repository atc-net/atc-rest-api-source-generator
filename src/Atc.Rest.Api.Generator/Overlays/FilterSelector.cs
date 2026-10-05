namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// <c>[?logical-expression]</c>.
/// </summary>
internal sealed record FilterSelector(JsonPathExpression Expression) : JsonPathSelector;