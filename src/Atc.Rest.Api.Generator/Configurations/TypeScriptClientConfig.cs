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
    /// Which variant of React Query useQuery hooks to emit. Only meaningful when
    /// <see cref="HooksStyle"/> is <see cref="TypeScriptHooksStyle.ReactQuery"/>. Default: Standard.
    /// </summary>
    public TypeScriptHooksMode HooksMode { get; set; } = TypeScriptHooksMode.Standard;

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
    /// Generate Mock Service Worker (MSW) handlers for frontend testing. Default: false.
    /// </summary>
    public bool GenerateMswHandlers { get; set; }

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

    /// <summary>
    /// Emit branded ID types for schema properties and path parameters that look like
    /// entity identifiers (string + format: uuid, name ending in "Id"). Catches caller
    /// mistakes like <c>getPet(userId)</c> at compile time. Default: false.
    /// </summary>
    public bool BrandedIds { get; set; }

    /// <summary>
    /// Validate response payloads at runtime against the generated Zod schemas. When
    /// enabled, each generated client method imports its response Zod schema and
    /// passes it to <c>ApiClient.request</c>; on parse mismatch the result surfaces
    /// a <c>schemaMismatch</c> arm with the Zod issues for diagnostics. Implies
    /// <see cref="GenerateZodSchemas"/>. Default: false.
    /// </summary>
    public bool ZodRuntimeValidate { get; set; }

    /// <summary>
    /// For Union-style enums (<see cref="EnumStyle"/> is <see cref="TypeScriptEnumStyle.Union"/>),
    /// also emit a runtime <c>{EnumName}Values</c> const array (e.g. <c>['A', 'B'] as const</c>)
    /// beside the type alias. This gives consumers an iterable, <c>.map()</c>-able list for
    /// populating dropdowns without forcing a TypeScript enum or requiring Zod. Has no effect
    /// when <see cref="EnumStyle"/> is <see cref="TypeScriptEnumStyle.Enum"/> (which already
    /// supports <c>Object.values</c>). Default: false.
    /// </summary>
    public bool EnumRuntimeValues { get; set; }
}