namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// A literal: a string, a number, <c>true</c>, <c>false</c> or <c>null</c>.
/// </summary>
internal sealed record LiteralExpression(JsonNode? Value) : JsonPathExpression;