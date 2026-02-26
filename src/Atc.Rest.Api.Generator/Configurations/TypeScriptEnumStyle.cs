namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Defines how TypeScript enums are generated.
/// </summary>
public enum TypeScriptEnumStyle
{
    /// <summary>
    /// Generate enums as union types (e.g., type Color = 'Red' | 'Green' | 'Blue').
    /// This is the default and preferred style for TypeScript.
    /// </summary>
    Union,

    /// <summary>
    /// Generate enums as TypeScript enum declarations (e.g., enum Color { Red = 'Red', Green = 'Green' }).
    /// </summary>
    Enum,
}