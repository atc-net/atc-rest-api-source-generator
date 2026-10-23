namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// A field of a form an endpoint binds field by field.
/// </summary>
/// <param name="ParamName">The handler parameter it is bound to (empty for a field read as an object part).</param>
/// <param name="PropName">The request's property it fills.</param>
/// <param name="TypeName">The bound type.</param>
/// <param name="IsRequired">Whether the form requires it.</param>
/// <param name="IsValueType">Whether the bound type is a value type (a required one is read as <c>.Value</c>).</param>
/// <param name="WireName">The field's name on the wire.</param>
/// <param name="JsonValue">The expression that reads an object part, or <see langword="null"/>.</param>
internal sealed record FormField(
    string ParamName,
    string PropName,
    string TypeName,
    bool IsRequired,
    bool IsValueType,
    string WireName,
    string? JsonValue);