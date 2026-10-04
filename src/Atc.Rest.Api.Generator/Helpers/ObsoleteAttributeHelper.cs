namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// The <c>[Obsolete]</c> a generated member gets for what the specification marks <c>deprecated: true</c>, so every
/// consumer that uses it is warned (CS0618). The generated files of such a specification suppress the warning
/// themselves (<see cref="GeneratedCodeAttributeHelper.SuppressObsolete"/>).
/// </summary>
public static class ObsoleteAttributeHelper
{
    /// <summary>
    /// The attribute: <c>[Obsolete("Deprecated in the OpenAPI specification.")]</c>.
    /// </summary>
    public static AttributeParameters Attribute { get; } = new("Obsolete", "\"Deprecated in the OpenAPI specification.\"");

    /// <summary>
    /// The attributes of a generated member: <c>[Obsolete]</c> when deprecated, else none.
    /// </summary>
    /// <param name="deprecated">Whether the specification marks it deprecated.</param>
    /// <returns>The attribute list, or <see langword="null"/>.</returns>
    public static List<AttributeParameters>? For(bool deprecated)
        => deprecated ? [Attribute] : null;
}