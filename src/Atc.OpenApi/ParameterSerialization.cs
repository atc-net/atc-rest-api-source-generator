namespace Atc.OpenApi;

/// <summary>The shape of a parameter's value, which (with style/explode) determines serialization.</summary>
[SuppressMessage("", "CA1720:Identifier contains type name", Justification = "OK - 'Object' is the OpenAPI term")]
public enum ParameterValueKind
{
    /// <summary>A scalar (string, number, boolean, enum).</summary>
    Primitive,

    /// <summary>An array of items.</summary>
    Array,

    /// <summary>An object with properties.</summary>
    Object,
}

/// <summary>
/// The effective serialization of an OpenAPI parameter (OpenAPI style + explode), plus whether
/// this generator serializes it correctly today. <see cref="Style"/> reuses
/// <see cref="Microsoft.OpenApi.ParameterStyle"/>.
/// </summary>
public readonly record struct ParameterSerialization(
    ParameterStyle Style,
    bool Explode,
    bool AllowReserved,
    ParameterValueKind ValueKind,
    bool IsSupported);
