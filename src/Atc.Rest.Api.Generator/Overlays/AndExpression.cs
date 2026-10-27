namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// <c>left &amp;&amp; right</c>.
/// </summary>
internal sealed record AndExpression(
    JsonPathExpression Left,
    JsonPathExpression Right) : JsonPathExpression;