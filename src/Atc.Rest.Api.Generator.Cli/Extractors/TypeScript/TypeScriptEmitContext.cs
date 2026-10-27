namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// The client-wide settings every client class and request method is written with: the specification, the naming
/// strategy, the date conversion, the HTTP client, the models with a writable variant, branded ids, runtime Zod
/// validation, the wire names and whether XML is preferred.
/// </summary>
/// <param name="OpenApiDoc">The specification.</param>
/// <param name="NamingStrategy">How property names are written.</param>
/// <param name="ConvertDates">Whether date and date-time values are converted to Date.</param>
/// <param name="HttpClient">The HTTP client the ApiClient is written for.</param>
/// <param name="WritableSchemas">The models with a writable request variant.</param>
/// <param name="BrandedIds">Whether ids are branded types.</param>
/// <param name="ZodRuntimeValidate">Whether answers are parsed with their Zod schema at run time.</param>
/// <param name="WireNames">The wire names of the models' properties.</param>
/// <param name="PreferXml">Whether a body offered as JSON and XML is sent and asked for as XML.</param>
internal sealed record TypeScriptEmitContext(
    OpenApiDocument OpenApiDoc,
    TypeScriptNamingStrategy NamingStrategy,
    bool ConvertDates,
    TypeScriptHttpClient HttpClient,
    HashSet<string> WritableSchemas,
    bool BrandedIds,
    bool ZodRuntimeValidate,
    TypeScriptWireNames WireNames,
    bool PreferXml);