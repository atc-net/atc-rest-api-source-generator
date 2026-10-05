namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// <c>left op right</c> with one of <c>==</c>, <c>!=</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>.
/// </summary>
internal sealed record ComparisonExpression(
    string Operator,
    JsonPathExpression Left,
    JsonPathExpression Right) : JsonPathExpression;