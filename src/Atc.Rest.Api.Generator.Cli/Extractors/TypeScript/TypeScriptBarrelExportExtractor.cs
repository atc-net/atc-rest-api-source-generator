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
    /// Creates barrel export parameters that interleave type and .zod re-exports for each type name.
    /// </summary>
    /// <param name="headerContent">Optional auto-generated header content.</param>
    /// <param name="typeNames">The type names to re-export.</param>
    /// <param name="isTypeOnly">Whether to use type-only exports for the main type file.</param>
    /// <returns>Barrel export parameters with interleaved .zod re-exports.</returns>
    public static TypeScriptBarrelExportParameters CreateWithZod(
        string? headerContent,
        IEnumerable<string> typeNames,
        bool isTypeOnly = true)
    {
        var exports = new List<TypeScriptReExportParameters>();

        foreach (var name in typeNames.OrderBy(n => n, StringComparer.Ordinal))
        {
            // Re-export the main type file
            exports.Add(new TypeScriptReExportParameters(
                ModulePath: $"./{name}",
                NamedExports: null,
                IsTypeOnly: isTypeOnly));

            // Re-export the .zod schema file (schemas are const values, never type-only)
            exports.Add(new TypeScriptReExportParameters(
                ModulePath: $"./{name}.zod",
                NamedExports: null,
                IsTypeOnly: false));
        }

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