namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// How one value of a declared response header is written and read.
/// </summary>
public enum ResponseHeaderValueKind
{
    /// <summary>
    /// A text, number, boolean, date, date-time or uuid.
    /// </summary>
    Scalar,

    /// <summary>
    /// A generated string enum: written and read as its wire value.
    /// </summary>
    StringEnum,

    /// <summary>
    /// A generated integer enum: written and read as its number.
    /// </summary>
    IntegerEnum,

    /// <summary>
    /// A generated model whose properties are values of the kinds above: written and read in OpenAPI's
    /// <c>simple</c> style.
    /// </summary>
    Model,
}