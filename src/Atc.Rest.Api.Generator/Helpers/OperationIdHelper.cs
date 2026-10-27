namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// The name every generator derives an operation's types from: the operationId when the specification has one, a
/// name built from the method and the path when it is missing or empty - and in both cases an identifier, so that
/// a digit at the start (<c>1stThing</c>) or a character that is no identifier (<c>repos/get</c>) breaks nothing.
/// One rule for the server, the handler scaffolds, both C# clients and the TypeScript client
/// (<see cref="OpenApiOperationExtensions"/>).
/// </summary>
public static class OperationIdHelper
{
    /// <summary>
    /// The effective operationId of an operation.
    /// </summary>
    /// <param name="operation">The operation; <see langword="null"/> gives the synthesised name.</param>
    /// <param name="httpMethod">Its HTTP method, as the path item keys it.</param>
    /// <param name="path">Its path template.</param>
    /// <returns>The operationId as written, or synthesised from the method and the path, made an identifier.</returns>
    public static string Effective(
        OpenApiOperation? operation,
        string httpMethod,
        string path)
        => (operation ?? new OpenApiOperation()).GetOperationId(path, httpMethod);
}