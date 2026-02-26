namespace Atc.CodeGeneration.TypeScript.Content.Factories;

public static class TypeScriptClassParametersFactory
{
    public static TypeScriptClassParameters Create(
        string? headerContent,
        JsDocComment? documentationTags,
        string typeName,
        string? genericTypeParameter = null,
        string? extendsTypeName = null,
        IList<string>? implementsTypeNames = null,
        IList<string>? importStatements = null,
        IList<TypeScriptConstructorParameters>? constructors = null,
        IList<TypeScriptPropertyParameters>? properties = null,
        IList<TypeScriptMethodParameters>? methods = null)
        => new(
            headerContent,
            documentationTags,
            TypeScriptModifiers.Export,
            typeName,
            genericTypeParameter,
            extendsTypeName,
            implementsTypeNames,
            importStatements,
            constructors,
            properties,
            methods);

    public static TypeScriptClassParameters Create(
        string? headerContent,
        JsDocComment? documentationTags,
        TypeScriptModifiers modifiers,
        string typeName,
        string? genericTypeParameter = null,
        string? extendsTypeName = null,
        IList<string>? implementsTypeNames = null,
        IList<string>? importStatements = null,
        IList<TypeScriptConstructorParameters>? constructors = null,
        IList<TypeScriptPropertyParameters>? properties = null,
        IList<TypeScriptMethodParameters>? methods = null)
        => new(
            headerContent,
            documentationTags,
            modifiers,
            typeName,
            genericTypeParameter,
            extendsTypeName,
            implementsTypeNames,
            importStatements,
            constructors,
            properties,
            methods);

    public static TypeScriptClassParameters CreateAbstract(
        string? headerContent,
        JsDocComment? documentationTags,
        string typeName,
        string? genericTypeParameter = null,
        IList<string>? importStatements = null,
        IList<TypeScriptConstructorParameters>? constructors = null,
        IList<TypeScriptPropertyParameters>? properties = null,
        IList<TypeScriptMethodParameters>? methods = null)
        => new(
            headerContent,
            documentationTags,
            TypeScriptModifiers.Export | TypeScriptModifiers.Abstract,
            typeName,
            genericTypeParameter,
            ExtendsTypeName: null,
            ImplementsTypeNames: null,
            importStatements,
            constructors,
            properties,
            methods);
}
