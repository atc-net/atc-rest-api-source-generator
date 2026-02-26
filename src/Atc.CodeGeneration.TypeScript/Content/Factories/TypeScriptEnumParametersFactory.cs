namespace Atc.CodeGeneration.TypeScript.Content.Factories;

public static class TypeScriptEnumParametersFactory
{
    public static TypeScriptEnumParameters Create(
        string? headerContent,
        JsDocComment? documentationTags,
        string typeName,
        IList<TypeScriptEnumValueParameters> values,
        bool isConstEnum = false)
        => new(
            headerContent,
            documentationTags,
            TypeScriptModifiers.Export,
            typeName,
            isConstEnum,
            values);

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
