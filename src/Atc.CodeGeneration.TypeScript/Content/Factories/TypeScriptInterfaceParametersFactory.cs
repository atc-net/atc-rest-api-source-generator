namespace Atc.CodeGeneration.TypeScript.Content.Factories;

/// <summary>
/// Builds <see cref="TypeScriptInterfaceParameters"/> for an exported interface.
/// </summary>
public static class TypeScriptInterfaceParametersFactory
{
    /// <summary>
    /// Creates the parameters of an <c>export interface</c>.
    /// </summary>
    /// <param name="headerContent">Text written at the top of the file, or null.</param>
    /// <param name="documentationTags">The interface's JSDoc, or null for none.</param>
    /// <param name="typeName">The interface name.</param>
    /// <param name="extendsTypeName">The interface it extends, or null.</param>
    /// <param name="importStatements">The import lines written before the interface, or null for none.</param>
    /// <param name="properties">The properties, or null for none.</param>
    /// <param name="methods">The method signatures, or null for none.</param>
    /// <returns>The interface parameters with <see cref="TypeScriptModifiers.Export"/>.</returns>
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
}