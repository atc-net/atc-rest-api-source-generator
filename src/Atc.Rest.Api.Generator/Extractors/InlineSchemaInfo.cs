namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// An inline object schema an extractor found (a response, a request body or an item written in place rather than as a
/// <c>$ref</c>), and the record generated for it - one record for the server's results and both clients.
/// </summary>
/// <param name="TypeName">The generated type name for the inline schema.</param>
/// <param name="PathSegment">The path segment this schema belongs to.</param>
/// <param name="RecordParameters">The record parameters for code generation.</param>
public sealed record InlineSchemaInfo(
    string TypeName,
    string PathSegment,
    RecordParameters RecordParameters);