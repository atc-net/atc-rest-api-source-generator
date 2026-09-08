namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Describes what an operation reads off the response body, so the terminating statement(s) of a
/// generated method body can be emitted from a single place for both typed-client result styles.
/// </summary>
internal enum ResponsePayloadKind
{
    /// <summary>
    /// No response payload - the operation returns <c>Task</c> (Throw) or <c>Task&lt;EndpointResponse&gt;</c> (Result).
    /// </summary>
    None,

    /// <summary>
    /// A JSON payload deserialized into the operation's return type.
    /// </summary>
    Json,

    /// <summary>
    /// A binary payload read as <c>byte[]</c>.
    /// </summary>
    Binary,

    /// <summary>
    /// A text payload read as <c>string</c>.
    /// </summary>
    Text,
}