namespace Atc.CodeGeneration.TypeScript.Content.Factories;

/// <summary>
/// Builds <see cref="TypeScriptEnumParameters"/> for an exported enum from member names alone or from
/// name/value pairs.
/// </summary>
public static class TypeScriptEnumParametersFactory
{
    /// <summary>
    /// Creates the parameters of an <c>export enum</c> whose members have no explicit values.
    /// </summary>
    /// <param name="headerContent">Text written at the top of the file, or null.</param>
    /// <param name="documentationTags">The enum's JSDoc, or null for none.</param>
    /// <param name="typeName">The enum name.</param>
    /// <param name="names">The member names, in order.</param>
    /// <param name="isConstEnum">True for a <c>const enum</c>.</param>
    /// <returns>The enum parameters with <see cref="TypeScriptModifiers.Export"/>.</returns>
    public static TypeScriptEnumParameters CreateFromNames(
        string? headerContent,
        JsDocComment? documentationTags,
        string typeName,
        IList<string> names,
        bool isConstEnum = false)
    {
        var values = names
            .Select(name => new TypeScriptEnumValueParameters(
                DocumentationTags: null,
                Name: name,
                Value: null))
            .ToList();

        return new TypeScriptEnumParameters(
            headerContent,
            documentationTags,
            TypeScriptModifiers.Export,
            typeName,
            isConstEnum,
            values);
    }

    /// <summary>
    /// Creates the parameters of an <c>export enum</c> whose members each have an explicit value.
    /// </summary>
    /// <param name="headerContent">Text written at the top of the file, or null.</param>
    /// <param name="documentationTags">The enum's JSDoc, or null for none.</param>
    /// <param name="typeName">The enum name.</param>
    /// <param name="nameValuePairs">The member names and their values as TypeScript source text (a string value already quoted), in order.</param>
    /// <param name="isConstEnum">True for a <c>const enum</c>.</param>
    /// <returns>The enum parameters with <see cref="TypeScriptModifiers.Export"/>.</returns>
    public static TypeScriptEnumParameters CreateFromNameValuePairs(
        string? headerContent,
        JsDocComment? documentationTags,
        string typeName,
        IList<KeyValuePair<string, string>> nameValuePairs,
        bool isConstEnum = false)
    {
        var values = nameValuePairs
            .Select(pair => new TypeScriptEnumValueParameters(
                DocumentationTags: null,
                Name: pair.Key,
                Value: pair.Value))
            .ToList();

        return new TypeScriptEnumParameters(
            headerContent,
            documentationTags,
            TypeScriptModifiers.Export,
            typeName,
            isConstEnum,
            values);
    }
}