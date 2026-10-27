namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// A query or a function used as a test: true when the query selects a node, or as the function's logical result.
/// </summary>
internal sealed record TestExpression(JsonPathExpression Operand) : JsonPathExpression;