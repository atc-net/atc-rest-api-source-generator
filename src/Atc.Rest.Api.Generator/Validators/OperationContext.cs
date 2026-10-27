namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// The operation an operation rule checks, and where it reports: one record for the rules that took the same seven to
/// nine values each.
/// </summary>
/// <param name="Diagnostics">The diagnostics the rule adds to.</param>
/// <param name="SourceFilePath">The specification's path, for the diagnostics.</param>
/// <param name="Document">The document.</param>
/// <param name="PathKey">The operation's path.</param>
/// <param name="PathItem">The operation's path item.</param>
/// <param name="HttpMethod">The operation's HTTP method, in lower case.</param>
/// <param name="Operation">The operation.</param>
/// <param name="OperationId">The operation's id.</param>
internal sealed record OperationContext(
    List<DiagnosticMessage> Diagnostics,
    string SourceFilePath,
    OpenApiDocument Document,
    string PathKey,
    IOpenApiPathItem PathItem,
    string HttpMethod,
    OpenApiOperation Operation,
    string OperationId)
{
    /// <summary>
    /// Gets the operation's HTTP method in upper case, as the messages name it.
    /// </summary>
    public string HttpMethodUpper
        => HttpMethod.ToUpperInvariant();
}