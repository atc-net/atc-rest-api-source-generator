namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// What the server's models need to know about the bodies that carry them.
/// </summary>
/// <param name="XmlModels">The models an XML body can carry (they get an XML mapping).</param>
/// <param name="BoundRequestModels">The models bound by Content-Type (JSON and/or XML request bodies).</param>
/// <param name="XmlRequestModels">The models of an XML request body (they name their XML names).</param>
/// <param name="XmlOnlyRequestModels">The models only an XML request carries (validated as XML only).</param>
/// <param name="RequireJsonProperties">Whether a required property is required in the JSON too (without the validation filter).</param>
internal sealed record ServerBodyModels(
    ISet<string> XmlModels,
    Dictionary<string, ServerBoundModel> BoundRequestModels,
    ISet<string> XmlRequestModels,
    ISet<string> XmlOnlyRequestModels,
    bool RequireJsonProperties);