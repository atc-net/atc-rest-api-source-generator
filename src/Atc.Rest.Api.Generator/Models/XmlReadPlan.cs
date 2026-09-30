namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// How a generated TypedClient method reads an XML response body.
/// </summary>
/// <param name="ReadMethod">The generated read method (<c>PetXml.Read</c>, <c>PetXml.ReadList</c>).</param>
/// <param name="ByContentType">Whether the body is offered as JSON too: read as XML only when the response is XML.</param>
/// <param name="Accept">The <c>Accept</c> header the method sends.</param>
public sealed record XmlReadPlan(
    string ReadMethod,
    bool ByContentType,
    string Accept);