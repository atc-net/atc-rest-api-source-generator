namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// How a TypeScript client types the headers of its results.
/// </summary>
/// <param name="ConvertDates">Whether a date or a date-time is a Date (otherwise its text).</param>
/// <param name="NamingStrategy">How the models name their properties (an object header's fields).</param>
/// <param name="BrandedIds">Whether a model's id is a branded type.</param>
public sealed record TypeScriptResponseHeaderOptions(
    bool ConvertDates,
    TypeScriptNamingStrategy NamingStrategy,
    bool BrandedIds);