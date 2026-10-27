namespace Atc.Rest.Api.Generator.Cli.Enums;

/// <summary>
/// What a component the TypeScript client emits as a type alias is.
/// </summary>
public enum TypeScriptAliasKind
{
    /// <summary>
    /// A component that only names a primitive (<c>UserId: string, uuid</c>) or a numeric enum.
    /// </summary>
    Scalar,

    /// <summary>
    /// A dictionary: <c>additionalProperties</c> and no properties of its own.
    /// </summary>
    Record,

    /// <summary>
    /// An array, or a <c>prefixItems</c> tuple.
    /// </summary>
    Array,
}