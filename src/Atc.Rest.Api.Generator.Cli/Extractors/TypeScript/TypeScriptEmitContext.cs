namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// The client-wide settings every request method is written with: the naming strategy, the date conversion, the models with a writable variant, branded ids and the wire names.
/// </summary>
/// <param name="NamingStrategy">How property names are written.</param>
/// <param name="ConvertDates">Whether date and date-time values are converted to Date.</param>
/// <param name="WritableSchemas">The models with a writable request variant.</param>
/// <param name="BrandedIds">Whether ids are branded types.</param>
/// <param name="WireNames">The wire names of the models' properties, or <see langword="null"/>.</param>
internal sealed record TypeScriptEmitContext(
    TypeScriptNamingStrategy NamingStrategy,
    bool ConvertDates,
    HashSet<string> WritableSchemas,
    bool BrandedIds,
    TypeScriptWireNames? WireNames);