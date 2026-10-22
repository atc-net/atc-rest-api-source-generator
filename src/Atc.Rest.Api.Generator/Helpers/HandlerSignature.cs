namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// The <c>ExecuteAsync</c> of a handler - one rule for the handler interface, the scaffolded stub and the signature a
/// written handler is updated to: it takes the operation's <c>{Operation}Parameters</c> when the operation has a
/// parameter (its own or its path's) or a request body, and a cancellation token.
/// </summary>
public static class HandlerSignature
{
    /// <summary>
    /// Whether the handler takes the operation's parameters record.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="pathParameters">The path item's parameters, which the operation inherits.</param>
    /// <returns><see langword="true"/> when the operation has a parameter or a request body.</returns>
    public static bool TakesParameters(
        OpenApiOperation operation,
        IList<IOpenApiParameter>? pathParameters)
        => operation.HasParameters() ||
           pathParameters is { Count: > 0 } ||
           operation.HasRequestBody();

    /// <summary>
    /// The <c>ExecuteAsync</c> declaration of a handler, its parameters on lines of their own (ATC201/ATC202).
    /// </summary>
    /// <param name="operationId">The operation id.</param>
    /// <param name="takesParameters">Whether the handler takes the parameters record (<see cref="TakesParameters"/>).</param>
    /// <returns>The declaration, up to and including its closing parenthesis.</returns>
    public static string ExecuteAsync(
        string operationId,
        bool takesParameters)
    {
        var name = (operationId ?? string.Empty).ToPascalCaseForDotNet();
        return takesParameters
            ? $"public Task<{name}Result> ExecuteAsync(\n" +
              $"        {name}Parameters parameters,\n" +
              "        CancellationToken cancellationToken = default)"
            : $"public Task<{name}Result> ExecuteAsync(\n" +
              "        CancellationToken cancellationToken = default)";
    }
}