namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// <c>!expression</c>.
/// </summary>
internal sealed record NotExpression(JsonPathExpression Operand) : JsonPathExpression;