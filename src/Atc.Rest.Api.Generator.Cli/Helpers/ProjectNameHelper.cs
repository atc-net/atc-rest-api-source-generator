namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// Helper methods for extracting and normalizing project names.
/// </summary>
internal static class ProjectNameHelper
{
    private static readonly string[] CommonSuffixes =
    [
        ".Api.Contracts",
        ".Api.Domain",
        ".Api",
        ".Contracts",
        ".Domain",
    ];

    /// <summary>
    /// Extracts the base solution name from a project name by removing common suffixes.
    /// For example, "PizzaPlanet.Api.Contracts" becomes "PizzaPlanet".
    /// </summary>
    /// <param name="projectName">The full project name.</param>
    /// <returns>The base solution name with suffixes removed.</returns>
    public static string ExtractSolutionName(string projectName)
    {
        var name = projectName;

        foreach (var suffix in CommonSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length];
                break;
            }
        }

        return name;
    }
}