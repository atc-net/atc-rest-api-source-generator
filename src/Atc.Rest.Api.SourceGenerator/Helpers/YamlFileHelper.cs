namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Shared helpers for identifying base and part files in multi-part OpenAPI specifications.
/// </summary>
internal static class YamlFileHelper
{
    /// <summary>
    /// Identifies the base file from the collection of YAML files.
    /// The base file is the one that doesn't follow the part file naming convention.
    /// Part files follow the naming convention: {BaseName}_{PartName}.yaml
    /// </summary>
    public static YamlFileInfo? IdentifyBaseFile(
        ImmutableArray<YamlFileInfo> yamlFiles)
    {
        var files = yamlFiles
            .Select(f => (File: f, Name: Path.GetFileNameWithoutExtension(f.Path)))
            .ToList();

        // Find files that are not part files (don't contain underscore that indicates part)
        // A base file either:
        // 1. Has no underscore in the name
        // 2. The part before the underscore doesn't match any other file name
        foreach (var file in files)
        {
            var underscoreIndex = file.Name.LastIndexOf('_');
            if (underscoreIndex <= 0)
            {
                return file.File;
            }

            var potentialBase = file.Name.Substring(0, underscoreIndex);
            var hasMatchingBase = files.Any(f =>
                f.Name.Equals(potentialBase, StringComparison.OrdinalIgnoreCase));

            if (!hasMatchingBase)
            {
                return file.File;
            }
        }

        // If all files look like parts, take the shortest name as base
        return files
            .OrderBy(f => f.Name.Length)
            .Select(f => (YamlFileInfo?)f.File)
            .FirstOrDefault();
    }

    /// <summary>
    /// Checks if a file is a part file for the given base name.
    /// Part files follow the naming convention: {BaseName}_{PartName}.yaml
    /// </summary>
    public static bool IsPartFile(
        string filePath,
        string baseName)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return fileName.StartsWith($"{baseName}_", StringComparison.OrdinalIgnoreCase);
    }
}