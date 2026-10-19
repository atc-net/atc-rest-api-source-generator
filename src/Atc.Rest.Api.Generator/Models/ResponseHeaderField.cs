namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// A property of an object a response header carries (OpenAPI's <c>simple</c> style: <c>name,value,…</c>, or
/// <c>name=value,…</c> exploded), as the model declares it.
/// </summary>
/// <param name="WireName">The property's name in the header (the schema's).</param>
/// <param name="PropertyName">The model's property.</param>
/// <param name="Type">The C# type of its value.</param>
/// <param name="Kind">How its value is written and read.</param>
/// <param name="IsRequired">Whether the schema requires it: a header without it is not a value of the model.</param>
public record ResponseHeaderField(
    string WireName,
    string PropertyName,
    string Type,
    ResponseHeaderValueKind Kind,
    bool IsRequired);