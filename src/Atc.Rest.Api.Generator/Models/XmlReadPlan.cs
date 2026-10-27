namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// How a generated TypedClient method sends and reads bodies other than JSON: XML bodies, and a model or a file.
/// </summary>
/// <param name="ReadMethod">The generated read method of an XML response (<c>PetXml.Read</c>, <c>PetXml.ReadList</c>), or <see langword="null"/>.</param>
/// <param name="ByContentType">Whether the response is offered as JSON too: read as XML only when the response is XML.</param>
/// <param name="Accept">The <c>Accept</c> header the method sends, or <see langword="null"/>.</param>
/// <param name="RequestContent">The <c>HttpContent</c> expression of an XML request body, or <see langword="null"/>.</param>
/// <param name="ModelOrFileType">A model or a file: the model's type (the method returns a <c>ContentOrFile&lt;T&gt;</c>), else <see langword="null"/>.</param>
public sealed record XmlReadPlan(
    string? ReadMethod,
    bool ByContentType,
    string? Accept,
    string? RequestContent = null,
    string? ModelOrFileType = null);