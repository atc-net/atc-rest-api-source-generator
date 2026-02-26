namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Creates barrel export (index.ts) parameters from a list of type names.
/// </summary>
public static class TypeScriptBarrelExportExtractor
{
    /// <summary>
    /// Creates barrel export parameters that re-export all types from the given type names.
    /// </summary>
    /// <param name="headerContent">Optional auto-generated header content.</param>
    /// <param name="typeNames">The type names to re-export.</param>
    /// <param name="isTypeOnly">Whether to use type-only exports.</param>
    /// <returns>Barrel export parameters for generating an index.ts file.</returns>
    public static TypeScriptBarrelExportParameters Create(
        string? headerContent,
        IEnumerable<string> typeNames,
        bool isTypeOnly = true)
    {
        var exports = typeNames
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new TypeScriptReExportParameters(
                ModulePath: $"./{name}",
                NamedExports: null,
                IsTypeOnly: isTypeOnly))
            .ToList();

        return new TypeScriptBarrelExportParameters(
            HeaderContent: headerContent,
            Exports: exports);
    }

    /// <summary>
    /// Creates a root barrel export that re-exports from subdirectories.
    /// </summary>
    /// <param name="headerContent">Optional auto-generated header content.</param>
    /// <param name="subdirectories">The subdirectory names to re-export from.</param>
    /// <returns>Barrel export parameters for generating a root index.ts file.</returns>
    public static TypeScriptBarrelExportParameters CreateForSubdirectories(
        string? headerContent,
        IEnumerable<string> subdirectories)
    {
        var exports = subdirectories
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new TypeScriptReExportParameters(
                ModulePath: $"./{name}",
                NamedExports: null,
                IsTypeOnly: false))
            .ToList();

        return new TypeScriptBarrelExportParameters(
            HeaderContent: headerContent,
            Exports: exports);
    }
}