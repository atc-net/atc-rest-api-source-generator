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

    /// <summary>
    /// Style of React hooks to generate alongside the TypeScript client. Default: None.
    /// </summary>
    public TypeScriptHooksStyle HooksStyle { get; set; } = TypeScriptHooksStyle.None;

    /// <summary>
    /// HTTP client library to use in the generated TypeScript client. Default: Fetch.
    /// </summary>
    public TypeScriptHttpClient HttpClient { get; set; } = TypeScriptHttpClient.Fetch;

    /// <summary>
    /// Naming strategy for generated TypeScript property and parameter names. Default: CamelCase.
    /// </summary>
    public TypeScriptNamingStrategy NamingStrategy { get; set; } = TypeScriptNamingStrategy.CamelCase;

    /// <summary>
    /// Convert date/date-time properties to Date objects with automatic JSON reviver/replacer. Default: false.
    /// </summary>
    public bool ConvertDates { get; set; }

    /// <summary>
    /// Generate mutable model properties (omit readonly modifier). Default: false.
    /// </summary>
    public bool MutableModels { get; set; }

    /// <summary>
    /// Generate Zod runtime validation schemas alongside model and enum files. Default: false.
    /// </summary>
    public bool GenerateZodSchemas { get; set; }

    /// <summary>
    /// Preview what would be generated without writing any files. Default: false.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Generate package.json and tsconfig.json to make the output a ready-to-use npm package. Default: false.
    /// </summary>
    public bool Scaffold { get; set; }

    /// <summary>
    /// Package name for the generated package.json. If null, derived from OpenAPI info.title.
    /// </summary>
    public string? PackageName { get; set; }

    /// <summary>
    /// Package version for the generated package.json. Default: "0.1.0".
    /// </summary>
    public string PackageVersion { get; set; } = "0.1.0";
}