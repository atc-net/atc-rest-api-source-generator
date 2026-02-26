namespace Atc.CodeGeneration.TypeScript.Content;

public record TypeScriptBaseParameters(
    string? HeaderContent,
    JsDocComment? DocumentationTags,
    TypeScriptModifiers Modifiers,
    string TypeName);

public record TypeScriptInterfaceParameters(
    string? HeaderContent,
    JsDocComment? DocumentationTags,
    TypeScriptModifiers Modifiers,
    string TypeName,
    string? ExtendsTypeName,
    IList<string>? ImportStatements,
    IList<TypeScriptPropertyParameters>? Properties,
    IList<TypeScriptMethodSignatureParameters>? Methods)
    : TypeScriptBaseParameters(
        HeaderContent,
        DocumentationTags,
        Modifiers,
        TypeName);

public record TypeScriptClassParameters(
    string? HeaderContent,
    JsDocComment? DocumentationTags,
    TypeScriptModifiers Modifiers,
    string TypeName,
    string? GenericTypeParameter,
    string? ExtendsTypeName,
    IList<string>? ImplementsTypeNames,
    IList<string>? ImportStatements,
    IList<TypeScriptConstructorParameters>? Constructors,
    IList<TypeScriptPropertyParameters>? Properties,
    IList<TypeScriptMethodParameters>? Methods)
    : TypeScriptBaseParameters(
        HeaderContent,
        DocumentationTags,
        Modifiers,
        TypeName);

public record TypeScriptEnumParameters(
    string? HeaderContent,
    JsDocComment? DocumentationTags,
    TypeScriptModifiers Modifiers,
    string TypeName,
    bool IsConstEnum,
    IList<TypeScriptEnumValueParameters> Values)
    : TypeScriptBaseParameters(
        HeaderContent,
        DocumentationTags,
        Modifiers,
        TypeName);

public record TypeScriptTypeAliasParameters(
    string? HeaderContent,
    JsDocComment? DocumentationTags,
    TypeScriptModifiers Modifiers,
    string TypeName,
    string? GenericTypeParameter,
    IList<string>? ImportStatements,
    string Definition)
    : TypeScriptBaseParameters(
        HeaderContent,
        DocumentationTags,
        Modifiers,
        TypeName);

public record TypeScriptBarrelExportParameters(
    string? HeaderContent,
    IList<TypeScriptReExportParameters> Exports);

public record TypeScriptReExportParameters(
    string ModulePath,
    IList<string>? NamedExports,
    bool IsTypeOnly);

public record TypeScriptPropertyParameters(
    JsDocComment? DocumentationTags,
    bool IsReadonly,
    string TypeAnnotation,
    bool IsOptional,
    string Name,
    string? DefaultValue);

public record TypeScriptMethodParameters(
    JsDocComment? DocumentationTags,
    TypeScriptModifiers Modifiers,
    string Name,
    string? GenericTypeParameter,
    string? ReturnType,
    IList<TypeScriptParameterParameters>? Parameters,
    string? Content);

public record TypeScriptMethodSignatureParameters(
    JsDocComment? DocumentationTags,
    string Name,
    string? GenericTypeParameter,
    string? ReturnType,
    IList<TypeScriptParameterParameters>? Parameters);

public record TypeScriptParameterParameters(
    string Name,
    string TypeAnnotation,
    bool IsOptional,
    string? DefaultValue);

public record TypeScriptConstructorParameters(
    JsDocComment? DocumentationTags,
    IList<TypeScriptConstructorParameterParameters>? Parameters,
    string? Content);

public record TypeScriptConstructorParameterParameters(
    TypeScriptModifiers AccessModifier,
    bool IsReadonly,
    string Name,
    string TypeAnnotation,
    bool IsOptional,
    string? DefaultValue);

public record TypeScriptEnumValueParameters(
    JsDocComment? DocumentationTags,
    string Name,
    string? Value);
