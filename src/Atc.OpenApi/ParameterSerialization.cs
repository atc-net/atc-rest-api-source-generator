namespace Atc.OpenApi;

/// <summary>The shape of a parameter's value, which (with style/explode) determines serialization.</summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "OK - 'Object' is the OpenAPI term")]
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
/// <param name="Style">Effective ParameterStyle (explicit or defaulted by location).</param>
/// <param name="Explode">Effective explode flag.</param>
/// <param name="AllowReserved">Whether reserved characters are left unencoded.</param>
/// <param name="ValueKind">Classified shape of the parameter's schema.</param>
/// <param name="IsSupported">True if this generator emits correct wire output for this style/explode/value-kind; false means callers should warn and fall back to default form serialization.</param>
public readonly record struct ParameterSerialization(
    ParameterStyle Style,
    bool Explode,
    bool AllowReserved,
    ParameterValueKind ValueKind,
    bool IsSupported);
