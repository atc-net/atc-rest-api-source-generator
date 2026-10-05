namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// <c>['name']</c> or <c>.name</c>.
/// </summary>
internal sealed record NameSelector(string Name) : JsonPathSelector;