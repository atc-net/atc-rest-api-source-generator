namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// A header the responses of an operation declare, as a client's result reads it back.
/// </summary>
/// <param name="Name">The header's name.</param>
/// <param name="PropertyName">The property the result reads it through.</param>
/// <param name="Type">The C# type of one value.</param>
/// <param name="IsArray">Whether the header is an array of such values (joined by commas).</param>
/// <param name="Kind">How a value is read.</param>
public record DeclaredResponseHeader(
    string Name,
    string PropertyName,
    string Type,
    bool IsArray,
    ResponseHeaderValueKind Kind);