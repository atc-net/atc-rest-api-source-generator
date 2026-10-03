namespace Atc.CodeGeneration.TypeScript.Content.Factories;

/// <summary>
/// Builds <see cref="TypeScriptClassParameters"/> for the usual cases - an exported class and an exported
/// abstract class.
/// </summary>
public static class TypeScriptClassParametersFactory
{
    /// <summary>
    /// Creates the parameters of an <c>export class</c>.
    /// </summary>
    /// <param name="headerContent">Text written at the top of the file, or null.</param>
    /// <param name="documentationTags">The class's JSDoc, or null for none.</param>
    /// <param name="typeName">The class name.</param>
    /// <param name="genericTypeParameter">The type parameter written as <c>&lt;T&gt;</c>, or null.</param>
    /// <param name="extendsTypeName">The base class, or null.</param>
    /// <param name="implementsTypeNames">The implemented interfaces, or null for none.</param>
    /// <param name="importStatements">The import lines written before the class, or null for none.</param>
    /// <param name="constructors">The constructors, or null for none.</param>
    /// <param name="properties">The properties, or null for none.</param>
    /// <param name="methods">The methods, or null for none.</param>
    /// <returns>The class parameters with <see cref="TypeScriptModifiers.Export"/>.</returns>
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

    /// <summary>
    /// Creates the parameters of an <c>export abstract class</c> with no base class or interfaces.
    /// </summary>
    /// <param name="headerContent">Text written at the top of the file, or null.</param>
    /// <param name="documentationTags">The class's JSDoc, or null for none.</param>
    /// <param name="typeName">The class name.</param>
    /// <param name="genericTypeParameter">The type parameter written as <c>&lt;T&gt;</c>, or null.</param>
    /// <param name="importStatements">The import lines written before the class, or null for none.</param>
    /// <param name="constructors">The constructors, or null for none.</param>
    /// <param name="properties">The properties, or null for none.</param>
    /// <param name="methods">The methods, or null for none.</param>
    /// <returns>The class parameters with <see cref="TypeScriptModifiers.Export"/> and <see cref="TypeScriptModifiers.Abstract"/>.</returns>
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