namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// How a generated TypedClient method sends and reads XML bodies.
/// </summary>
/// <param name="ReadMethod">The generated read method of an XML response (<c>PetXml.Read</c>, <c>PetXml.ReadList</c>), or <see langword="null"/>.</param>
/// <param name="ByContentType">Whether the response is offered as JSON too: read as XML only when the response is XML.</param>
/// <param name="Accept">The <c>Accept</c> header the method sends, or <see langword="null"/>.</param>
/// <param name="RequestContent">The <c>HttpContent</c> expression of an XML request body, or <see langword="null"/>.</param>
public sealed record XmlReadPlan(
    string? ReadMethod,
    bool ByContentType,
    string? Accept,
    string? RequestContent = null);