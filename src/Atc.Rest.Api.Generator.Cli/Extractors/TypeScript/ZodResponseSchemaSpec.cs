namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Result of <see cref="TypeScriptOperationHelper.TryGetResponseZodSchemaSpec"/>.
/// Carries the inline Zod expression plus the set of named schemas to import.
/// </summary>
public sealed record ZodResponseSchemaSpec(
    string Expression,
    HashSet<string> RefSchemaNames,
    bool NeedsZodImport);