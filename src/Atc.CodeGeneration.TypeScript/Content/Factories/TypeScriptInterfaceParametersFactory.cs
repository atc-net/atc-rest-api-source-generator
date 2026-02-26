namespace Atc.CodeGeneration.TypeScript.Content.Factories;

public static class TypeScriptInterfaceParametersFactory
{
    public static TypeScriptInterfaceParameters Create(
        string? headerContent,
        JsDocComment? documentationTags,
        string typeName,
        string? extendsTypeName = null,
        IList<string>? importStatements = null,
        IList<TypeScriptPropertyParameters>? properties = null,
        IList<TypeScriptMethodSignatureParameters>? methods = null)
        => new(
            headerContent,
            documentationTags,
            TypeScriptModifiers.Export,
            typeName,
            extendsTypeName,
            importStatements,
            properties,
            methods);

    public static TypeScriptInterfaceParameters Create(
        string? headerContent,
        JsDocComment? documentationTags,
        TypeScriptModifiers modifiers,
        string typeName,
        string? extendsTypeName = null,
        IList<string>? importStatements = null,
        IList<TypeScriptPropertyParameters>? properties = null,
        IList<TypeScriptMethodSignatureParameters>? methods = null)
        => new(
            headerContent,
            documentationTags,
            modifiers,
            typeName,
            extendsTypeName,
            importStatements,
            properties,
            methods);
}
