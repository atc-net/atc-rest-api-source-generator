namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// A function call.
/// </summary>
internal sealed record FunctionExpression(
    string Name,
    IReadOnlyList<JsonPathExpression> Arguments,
    JsonPathType ResultType) : JsonPathExpression;