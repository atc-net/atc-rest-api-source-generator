namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Controls how an inline <c>enum</c> declared directly on a path/query/header parameter
/// (i.e. <c>type: string, enum: [...]</c> with no <c>$ref</c>) is generated.
/// </summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Members mirror the user-facing marker values 'Enum' and 'String'.")]
public enum InlineParameterEnumMode
{
    /// <summary>
    /// Generate a named C# enum type for the parameter (default; type-safe contract).
    /// </summary>
    Enum,

    /// <summary>
    /// Generate the parameter as a plain <c>string</c> (pre-1.0.252 behavior). Use this to
    /// keep existing handlers/services written against the old string shape compiling.
    /// </summary>
    String,
}