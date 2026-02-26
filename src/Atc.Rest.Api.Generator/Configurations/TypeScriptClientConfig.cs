namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Configuration for TypeScript client code generation.
/// </summary>
public class TypeScriptClientConfig
{
    /// <summary>
    /// OpenAPI specification validation strategy. Default: Strict.
    /// </summary>
    public ValidateSpecificationStrategy ValidateSpecificationStrategy { get; set; } = ValidateSpecificationStrategy.Strict;

    /// <summary>
    /// Include deprecated operations and schemas in generated code. Default: false.
    /// </summary>
    public bool IncludeDeprecated { get; set; }

    /// <summary>
    /// How to generate TypeScript enums. Default: Union (string union types).
    /// </summary>
    public TypeScriptEnumStyle EnumStyle { get; set; } = TypeScriptEnumStyle.Union;

    /// <summary>
    /// Whether to generate auto-generated file headers. Default: true.
    /// </summary>
    public bool GenerateFileHeaders { get; set; } = true;
}