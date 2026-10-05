namespace Atc.Rest.Api.Generator.Cli.Commands.Settings;

/// <summary>
/// Settings for the generate client-typescript command.
/// Does not extend BaseGenerateCommandSettings because TypeScript
/// generation does not require --name/--namespace options.
/// </summary>
public sealed class GenerateClientTypeScriptCommandSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the path to the OpenAPI specification file (YAML or JSON); resolved to an absolute path by <see cref="Validate"/>.
    /// </summary>
    [CommandOption("-s|--specification <PATH>")]
    [Description("Path to the OpenAPI specification file (YAML or JSON).")]
    public string SpecificationPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets the OpenAPI Overlay files applied to the specification first, in the order given.
    /// </summary>
    [CommandOption("--overlay <PATH>")]
    [Description("An OpenAPI Overlay file (1.0 to 1.2) applied to the specification first. Repeat the option to apply several, in order.")]
    public string[] Overlays { get; init; } = [];

    /// <summary>
    /// Gets or sets the output directory for the generated TypeScript files; resolved to an absolute path by <see cref="Validate"/>.
    /// </summary>
    [CommandOption("-o|--output <PATH>")]
    [Description("Path to the output directory for the generated TypeScript files.")]
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the specification is validated with the standard strategy instead of the strict one.
    /// </summary>
    [CommandOption("--no-strict")]
    [Description("Validate with the Standard strategy: only the OpenAPI parser's errors and structural checks, not the ATC rules.")]
    [DefaultValue(false)]
    public bool DisableStrictMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether deprecated operations and schemas are generated instead of skipped.
    /// </summary>
    [CommandOption("--include-deprecated")]
    [Description("Include deprecated operations and schemas in generated code.")]
    [DefaultValue(false)]
    public bool IncludeDeprecated { get; init; }

    /// <summary>
    /// Gets how string enums are emitted: <c>Union</c> (string literal unions, default) or <c>Enum</c> (TypeScript enums).
    /// </summary>
    [CommandOption("--enum-style <STYLE>")]
    [Description("Enum generation style: Union (string union types, default) or Enum (TypeScript enum declarations).")]
    public string? EnumStyle { get; init; }

    /// <summary>
    /// Gets which data-fetching hooks are generated: <c>None</c> (default), <c>ReactQuery</c> or <c>Swr</c>.
    /// </summary>
    [CommandOption("--hooks <STYLE>")]
    [Description("Hook generation style: None (default), ReactQuery (TanStack Query hooks), or Swr (SWR hooks).")]
    public string? HooksStyle { get; init; }

    /// <summary>
    /// Gets the React Query hook variant: <c>Standard</c> (useQuery, default), <c>Suspense</c> (useSuspenseQuery) or <c>Both</c>.
    /// </summary>
    [CommandOption("--hooks-mode <MODE>")]
    [Description("React Query hooks variant: Standard (useQuery, default), Suspense (useSuspenseQuery), or Both.")]
    public string? HooksMode { get; init; }

    /// <summary>
    /// Gets the HTTP library the generated ApiClient is built on: <c>Fetch</c> (default) or <c>Axios</c>.
    /// </summary>
    [CommandOption("--client-type <TYPE>")]
    [Description("HTTP client library: Fetch (native fetch API, default) or Axios.")]
    public string? ClientType { get; init; }

    /// <summary>
    /// Gets how property and parameter names are cased in the models: <c>CamelCase</c> (default), <c>Original</c> or <c>PascalCase</c>.
    /// </summary>
    [CommandOption("--naming-strategy <STRATEGY>")]
    [Description("Naming strategy for properties and parameters: CamelCase (default), Original, or PascalCase.")]
    public string? NamingStrategy { get; init; }

    /// <summary>
    /// Gets a value indicating whether date and date-time properties are typed as <c>Date</c> and converted by a JSON reviver/replacer.
    /// </summary>
    [CommandOption("--convert-dates")]
    [Description("Convert date/date-time properties to Date objects with automatic JSON reviver/replacer.")]
    [DefaultValue(false)]
    public bool ConvertDates { get; init; }

    /// <summary>
    /// Gets a value indicating whether model properties are emitted without the <c>readonly</c> modifier.
    /// </summary>
    [CommandOption("--no-readonly")]
    [Description("Generate mutable model properties (omit readonly modifier).")]
    [DefaultValue(false)]
    public bool NoReadonly { get; init; }

    /// <summary>
    /// Gets a value indicating whether Zod schemas are generated next to the models and enums.
    /// </summary>
    [CommandOption("--zod")]
    [Description("Generate Zod runtime validation schemas alongside model and enum files.")]
    [DefaultValue(false)]
    public bool GenerateZodSchemas { get; init; }

    /// <summary>
    /// Gets a value indicating whether Mock Service Worker handlers are generated for every operation.
    /// </summary>
    [CommandOption("--msw")]
    [Description("Generate Mock Service Worker (MSW) handlers for frontend testing.")]
    [DefaultValue(false)]
    public bool GenerateMswHandlers { get; init; }

    /// <summary>
    /// Gets a value indicating whether the files to be generated are listed without being written.
    /// </summary>
    [CommandOption("--dry-run")]
    [Description("Preview what would be generated without writing any files.")]
    [DefaultValue(false)]
    public bool DryRun { get; init; }

    /// <summary>
    /// Gets a value indicating whether the command keeps running and regenerates whenever the specification changes.
    /// </summary>
    [CommandOption("--watch")]
    [Description("Watch the specification file for changes and re-generate automatically.")]
    [DefaultValue(false)]
    public bool Watch { get; init; }

    /// <summary>
    /// Gets a value indicating whether package.json and tsconfig.json are generated so the output is an npm package.
    /// </summary>
    [CommandOption("--scaffold")]
    [Description("Generate package.json and tsconfig.json to make the output a ready-to-use npm package.")]
    [DefaultValue(false)]
    public bool Scaffold { get; init; }

    /// <summary>
    /// Gets the <c>name</c> written to the scaffolded package.json; derived from the specification's <c>info.title</c> when not set.
    /// </summary>
    [CommandOption("--package-name <NAME>")]
    [Description("Package name for generated package.json (default: derived from OpenAPI info.title).")]
    public string? PackageName { get; init; }

    /// <summary>
    /// Gets the <c>version</c> written to the scaffolded package.json (default <c>0.1.0</c>).
    /// </summary>
    [CommandOption("--package-version <VERSION>")]
    [Description("Package version for generated package.json (default: 0.1.0).")]
    public string? PackageVersion { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>uuid</c>-formatted properties named <c>*Id</c> get branded types, so a <c>UserId</c> cannot be passed where a <c>PetId</c> is expected.
    /// </summary>
    [CommandOption("--branded-ids")]
    [Description("Emit branded ID types for `string + format: uuid` properties whose name ends in 'Id'. Catches caller mistakes like getPet(userId) at compile time.")]
    [DefaultValue(false)]
    public bool BrandedIds { get; init; }

    /// <summary>
    /// Gets a value indicating whether responses are validated against the Zod schemas at runtime, surfacing a <c>schemaMismatch</c> arm on failure; implies <see cref="GenerateZodSchemas"/>.
    /// </summary>
    [CommandOption("--zod-runtime-validate")]
    [Description("Validate response payloads at runtime against the generated Zod schemas. On mismatch the ApiResult surfaces a 'schemaMismatch' arm with the Zod issues. Implies --zod.")]
    [DefaultValue(false)]
    public bool ZodRuntimeValidate { get; init; }

    /// <summary>
    /// Gets a value indicating whether Union-style enums also get a <c>{EnumName}Values</c> const array for iterating the members at runtime.
    /// </summary>
    [CommandOption("--enum-runtime-values")]
    [Description("For Union-style enums, also emit a runtime '{EnumName}Values' const array (as const) for iterating/populating dropdowns without a TS enum or Zod. No effect with --enum-style Enum.")]
    [DefaultValue(false)]
    public bool EnumRuntimeValues { get; init; }

    /// <summary>
    /// Gets a value indicating whether string enums are closed to values the specification does not list.
    /// </summary>
    [CommandOption("--no-extensible-enums")]
    [Description("Close the string enums: a Union-style enum lists only the specification's values, and a Zod enum schema refuses any other value. By default they stay open, so a value the server adds later still type-checks and parses. The same as \"extensibleEnums\": false in the C# client marker.")]
    [DefaultValue(false)]
    public bool NoExtensibleEnums { get; init; }

    /// <summary>
    /// Checks the specification and output paths, every enum-valued option and the options file, resolving the paths to absolute ones.
    /// </summary>
    /// <returns>A success result, or an error naming the first option that is missing or invalid.</returns>
    public override ValidationResult Validate()
    {
        if (OverlayCliHelper.FindMissing(Overlays) is { } missingOverlay)
        {
            return ValidationResult.Error(missingOverlay);
        }

        if (string.IsNullOrWhiteSpace(SpecificationPath))
        {
            return ValidationResult.Error("Specification path is required. Use -s or --specification.");
        }

        SpecificationPath = PathHelper.ResolveRelativePath(SpecificationPath);

        if (!File.Exists(SpecificationPath))
        {
            return ValidationResult.Error($"Specification file not found: {SpecificationPath}");
        }

        var extension = Path.GetExtension(SpecificationPath).ToLowerInvariant();
        if (extension is not ".yaml" and not ".yml" and not ".json")
        {
            return ValidationResult.Error("Specification file must be a YAML (.yaml, .yml) or JSON (.json) file.");
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            return ValidationResult.Error("Output path is required. Use -o or --output.");
        }

        OutputPath = PathHelper.ResolveRelativePath(OutputPath);

        // Validate enum style if provided
        if (!string.IsNullOrWhiteSpace(EnumStyle) &&
            !Enum.TryParse<TypeScriptEnumStyle>(EnumStyle, ignoreCase: true, out _))
        {
            return ValidationResult.Error($"Invalid enum style: '{EnumStyle}'. Valid values: Union, Enum.");
        }

        // Validate hooks style if provided
        if (!string.IsNullOrWhiteSpace(HooksStyle) &&
            !Enum.TryParse<TypeScriptHooksStyle>(HooksStyle, ignoreCase: true, out _))
        {
            return ValidationResult.Error($"Invalid hooks style: '{HooksStyle}'. Valid values: None, ReactQuery, Swr.");
        }

        // Validate hooks mode if provided
        if (!string.IsNullOrWhiteSpace(HooksMode) &&
            !Enum.TryParse<TypeScriptHooksMode>(HooksMode, ignoreCase: true, out _))
        {
            return ValidationResult.Error($"Invalid hooks mode: '{HooksMode}'. Valid values: Standard, Suspense, Both.");
        }

        // Validate client type if provided
        if (!string.IsNullOrWhiteSpace(ClientType) &&
            !Enum.TryParse<TypeScriptHttpClient>(ClientType, ignoreCase: true, out _))
        {
            return ValidationResult.Error($"Invalid client type: '{ClientType}'. Valid values: Fetch, Axios.");
        }

        // Validate naming strategy if provided
        if (!string.IsNullOrWhiteSpace(NamingStrategy) &&
            !Enum.TryParse<TypeScriptNamingStrategy>(NamingStrategy, ignoreCase: true, out _))
        {
            return ValidationResult.Error($"Invalid naming strategy: '{NamingStrategy}'. Valid values: CamelCase, Original, PascalCase.");
        }

        return ValidationResult.Success();
    }
}