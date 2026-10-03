namespace Atc.CodeGeneration.TypeScript.Content;

/// <summary>
/// What every generated TypeScript type file starts with.
/// </summary>
/// <param name="HeaderContent">Text written at the top of the file (an auto-generated banner), or null.</param>
/// <param name="DocumentationTags">The type's JSDoc, or null for none.</param>
/// <param name="Modifiers">The keywords in front of the declaration, normally <see cref="TypeScriptModifiers.Export"/>.</param>
/// <param name="TypeName">The name of the type.</param>
public record TypeScriptBaseParameters(
    string? HeaderContent,
    JsDocComment? DocumentationTags,
    TypeScriptModifiers Modifiers,
    string TypeName);

/// <summary>
/// Everything <see cref="Generators.GenerateContentForInterface"/> needs to write an interface file.
/// </summary>
/// <param name="HeaderContent">Text written at the top of the file, or null.</param>
/// <param name="DocumentationTags">The interface's JSDoc, or null for none.</param>
/// <param name="Modifiers">The keywords in front of the declaration.</param>
/// <param name="TypeName">The interface name.</param>
/// <param name="ExtendsTypeName">The interface it extends, or null.</param>
/// <param name="ImportStatements">The import lines written before the interface, or null for none.</param>
/// <param name="Properties">The properties, or null for none.</param>
/// <param name="Methods">The method signatures, or null for none.</param>
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

/// <summary>
/// Everything <see cref="Generators.GenerateContentForClass"/> needs to write a class file.
/// </summary>
/// <param name="HeaderContent">Text written at the top of the file, or null.</param>
/// <param name="DocumentationTags">The class's JSDoc, or null for none.</param>
/// <param name="Modifiers">The keywords in front of the declaration, for example <c>export abstract</c>.</param>
/// <param name="TypeName">The class name.</param>
/// <param name="GenericTypeParameter">The type parameter written as <c>&lt;T&gt;</c> after the name, or null.</param>
/// <param name="ExtendsTypeName">The base class, or null.</param>
/// <param name="ImplementsTypeNames">The implemented interfaces, or null for none.</param>
/// <param name="ImportStatements">The import lines written before the class, or null for none.</param>
/// <param name="Constructors">The constructors, or null for none.</param>
/// <param name="Properties">The properties, or null for none.</param>
/// <param name="Methods">The methods, or null for none.</param>
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

/// <summary>
/// Everything <see cref="Generators.GenerateContentForEnum"/> needs to write an enum file.
/// </summary>
/// <param name="HeaderContent">Text written at the top of the file, or null.</param>
/// <param name="DocumentationTags">The enum's JSDoc, or null for none.</param>
/// <param name="Modifiers">The keywords in front of the declaration.</param>
/// <param name="TypeName">The enum name.</param>
/// <param name="IsConstEnum">True to write <c>const enum</c>.</param>
/// <param name="Values">The members of the enum, in order.</param>
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

/// <summary>
/// Everything <see cref="Generators.GenerateContentForTypeAlias"/> needs to write a type alias file.
/// </summary>
/// <param name="HeaderContent">Text written at the top of the file, or null.</param>
/// <param name="DocumentationTags">The alias's JSDoc, or null for none.</param>
/// <param name="Modifiers">The keywords in front of the declaration.</param>
/// <param name="TypeName">The alias name.</param>
/// <param name="GenericTypeParameter">The type parameter written as <c>&lt;T&gt;</c> after the name, or null.</param>
/// <param name="ImportStatements">The import lines written before the alias, or null for none.</param>
/// <param name="Definition">The right-hand side of the alias as TypeScript source text.</param>
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

/// <summary>
/// Everything <see cref="Generators.GenerateContentForBarrelExport"/> needs to write an <c>index.ts</c>.
/// </summary>
/// <param name="HeaderContent">Text written at the top of the file, or null.</param>
/// <param name="Exports">The modules to re-export, in order.</param>
public record TypeScriptBarrelExportParameters(
    string? HeaderContent,
    IList<TypeScriptReExportParameters> Exports);

/// <summary>
/// One re-export line of a barrel file.
/// </summary>
/// <param name="ModulePath">The module specifier, for example <c>./models/pet</c>.</param>
/// <param name="NamedExports">The names to re-export as <c>export { a, b }</c>, or null or empty for <c>export *</c>.</param>
/// <param name="IsTypeOnly">True to write <c>export type</c> so the line is erased at compile time.</param>
/// <param name="DocumentationTags">A JSDoc written above the line, or null for none.</param>
public record TypeScriptReExportParameters(
    string ModulePath,
    IList<string>? NamedExports,
    bool IsTypeOnly,
    JsDocComment? DocumentationTags = null);

/// <summary>
/// A property of a generated class or interface.
/// </summary>
/// <param name="DocumentationTags">The property's JSDoc, or null for none.</param>
/// <param name="IsReadonly">True to write the <c>readonly</c> keyword.</param>
/// <param name="TypeAnnotation">The type after the colon, as TypeScript source text.</param>
/// <param name="IsOptional">True to write <c>?</c> after the name.</param>
/// <param name="Name">The property name.</param>
/// <param name="DefaultValue">The initializer as TypeScript source text, or null for none.</param>
public record TypeScriptPropertyParameters(
    JsDocComment? DocumentationTags,
    bool IsReadonly,
    string TypeAnnotation,
    bool IsOptional,
    string Name,
    string? DefaultValue);

/// <summary>
/// A method of a generated class.
/// </summary>
/// <param name="DocumentationTags">The method's JSDoc, or null for none.</param>
/// <param name="Modifiers">The keywords in front of the method; only <see cref="TypeScriptModifiers.Async"/> is written.</param>
/// <param name="Name">The method name.</param>
/// <param name="GenericTypeParameter">The type parameter written as <c>&lt;T&gt;</c> after the name, or null.</param>
/// <param name="ReturnType">The return type after the colon, or null to omit it.</param>
/// <param name="Parameters">The parameters, or null for none.</param>
/// <param name="Content">The body as TypeScript source text, or null for an empty body.</param>
public record TypeScriptMethodParameters(
    JsDocComment? DocumentationTags,
    TypeScriptModifiers Modifiers,
    string Name,
    string? GenericTypeParameter,
    string? ReturnType,
    IList<TypeScriptParameterParameters>? Parameters,
    string? Content);

/// <summary>
/// A method signature of a generated interface.
/// </summary>
/// <param name="DocumentationTags">The method's JSDoc, or null for none.</param>
/// <param name="Name">The method name.</param>
/// <param name="GenericTypeParameter">The type parameter written as <c>&lt;T&gt;</c> after the name, or null.</param>
/// <param name="ReturnType">The return type after the colon, or null to omit it.</param>
/// <param name="Parameters">The parameters, or null for none.</param>
public record TypeScriptMethodSignatureParameters(
    JsDocComment? DocumentationTags,
    string Name,
    string? GenericTypeParameter,
    string? ReturnType,
    IList<TypeScriptParameterParameters>? Parameters);

/// <summary>
/// One parameter of a generated method, written as <c>name[?]: type[ = default]</c>.
/// </summary>
/// <param name="Name">The parameter name.</param>
/// <param name="TypeAnnotation">The type after the colon, as TypeScript source text.</param>
/// <param name="IsOptional">True to write <c>?</c> after the name; omitted when a default value is given.</param>
/// <param name="DefaultValue">The default value as TypeScript source text, or null for none.</param>
public record TypeScriptParameterParameters(
    string Name,
    string TypeAnnotation,
    bool IsOptional,
    string? DefaultValue);

/// <summary>
/// A constructor of a generated class.
/// </summary>
/// <param name="DocumentationTags">The constructor's JSDoc, or null for none.</param>
/// <param name="Parameters">The parameters, or null for a parameterless constructor.</param>
/// <param name="Content">The body as TypeScript source text, or null for <c>{}</c>.</param>
public record TypeScriptConstructorParameters(
    JsDocComment? DocumentationTags,
    IList<TypeScriptConstructorParameterParameters>? Parameters,
    string? Content);

/// <summary>
/// One parameter of a generated constructor; with an access modifier or <c>readonly</c> it is a parameter
/// property that declares and assigns a field.
/// </summary>
/// <param name="AccessModifier">The keyword before the name, or <see cref="TypeScriptModifiers.None"/> for a plain parameter.</param>
/// <param name="IsReadonly">True to write the <c>readonly</c> keyword.</param>
/// <param name="Name">The parameter name.</param>
/// <param name="TypeAnnotation">The type after the colon, as TypeScript source text.</param>
/// <param name="IsOptional">True to write <c>?</c> after the name.</param>
/// <param name="DefaultValue">The default value as TypeScript source text, or null for none.</param>
public record TypeScriptConstructorParameterParameters(
    TypeScriptModifiers AccessModifier,
    bool IsReadonly,
    string Name,
    string TypeAnnotation,
    bool IsOptional,
    string? DefaultValue);

/// <summary>
/// One member of a generated enum.
/// </summary>
/// <param name="DocumentationTags">The member's JSDoc, or null for none.</param>
/// <param name="Name">The member name.</param>
/// <param name="Value">The value as TypeScript source text (a string already quoted), or null to let the compiler assign one.</param>
public record TypeScriptEnumValueParameters(
    JsDocComment? DocumentationTags,
    string Name,
    string? Value);