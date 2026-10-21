namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// How a generated server binds a model of a request body by its <c>Content-Type</c>.
/// </summary>
/// <param name="AcceptsJson">Whether an operation accepts the model (alone) as JSON too.</param>
/// <param name="BoundAlone">Whether an operation's body is the model alone (a <c>BindAsync</c> on the record).</param>
/// <param name="BoundInList">Whether an operation's body is a list of the model (a <c>RequestBodyList&lt;T&gt;</c> of it).</param>
/// <param name="AcceptsXml">Whether an operation accepts the model as XML (it has an XML mapping to read with).</param>
public sealed record ServerBoundModel(
    bool AcceptsJson,
    bool BoundAlone,
    bool BoundInList,
    bool AcceptsXml = true);