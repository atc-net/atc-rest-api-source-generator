namespace Atc.Rest.Api.Generator.IntegrationTests.Services;

/// <summary>
/// Utility methods for file path operations in test scenarios.
/// </summary>
public static class FileComparer
{
    /// <summary>
    /// Gets the relative path for a generated type based on its SubFolder and TypeName.
    /// </summary>
    public static string GetRelativePath(GeneratedType type)
    {
        var subFolder = type.SubFolder ?? string.Empty;

        // Sanitize type name for Windows filename compatibility
        // Replace <T> with [T] and : with " -" to avoid invalid filename characters
        var safeTypeName = type.TypeName
            .Replace("<", "[", StringComparison.Ordinal)
            .Replace(">", "]", StringComparison.Ordinal)
            .Replace(":", " -", StringComparison.Ordinal);

        var fileName = $"{safeTypeName}.verified.cs";

        if (string.IsNullOrEmpty(subFolder))
        {
            return fileName;
        }

        // Normalize path separators
        subFolder = subFolder.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(subFolder, fileName);
    }
}