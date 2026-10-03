namespace Atc.CodeGeneration.TypeScript.Content.Factories;

/// <summary>
/// Builds <see cref="TypeScriptTypeAliasParameters"/> for an exported type alias.
/// </summary>
public static class TypeScriptTypeAliasParametersFactory
{
    /// <summary>
    /// Creates the parameters of an <c>export type</c> that is a union of string literals, for example
    /// <c>'a' | 'b'</c>; each value is escaped and quoted.
    /// </summary>
    /// <param name="headerContent">Text written at the top of the file, or null.</param>
    /// <param name="documentationTags">The alias's JSDoc, or null for none.</param>
    /// <param name="typeName">The alias name.</param>
    /// <param name="values">The string values of the union, in order.</param>
    /// <returns>The type alias parameters with <see cref="TypeScriptModifiers.Export"/>.</returns>
    public static TypeScriptTypeAliasParameters CreateStringUnion(
        string? headerContent,
        JsDocComment? documentationTags,
        string typeName,
        IList<string> values)
    {
        var definition = string.Join(
            " | ",
            values.Select(TypeScriptLiteralHelper.ToStringLiteral));

        return new TypeScriptTypeAliasParameters(
            headerContent,
            documentationTags,
            TypeScriptModifiers.Export,
            typeName,
            GenericTypeParameter: null,
            ImportStatements: null,
            definition);
    }
}