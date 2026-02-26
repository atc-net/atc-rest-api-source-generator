namespace Atc.CodeGeneration.TypeScript.Content.Factories;

public static class TypeScriptTypeAliasParametersFactory
{
    public static TypeScriptTypeAliasParameters Create(
        string? headerContent,
        JsDocComment? documentationTags,
        string typeName,
        string definition,
        string? genericTypeParameter = null,
        IList<string>? importStatements = null)
        => new(
            headerContent,
            documentationTags,
            TypeScriptModifiers.Export,
            typeName,
            genericTypeParameter,
            importStatements,
            definition);

    public static TypeScriptTypeAliasParameters CreateStringUnion(
        string? headerContent,
        JsDocComment? documentationTags,
        string typeName,
        IList<string> values)
    {
        var definition = string.Join(
            " | ",
            values.Select(v => $"'{v}'"));

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
