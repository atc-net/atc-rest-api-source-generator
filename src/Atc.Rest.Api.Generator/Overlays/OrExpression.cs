namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// <c>left || right</c>.
/// </summary>
internal sealed record OrExpression(
    JsonPathExpression Left,
    JsonPathExpression Right) : JsonPathExpression;