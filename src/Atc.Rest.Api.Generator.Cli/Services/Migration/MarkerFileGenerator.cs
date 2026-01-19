namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Generates marker files for source generator configuration.
/// </summary>
internal static class MarkerFileGenerator
{
    /// <summary>
    /// Generates the server marker file (.atc-rest-api-server).
    /// </summary>
    /// <param name="projectDirectory">The directory of the Api.Contracts project.</param>
    /// <param name="options">The generator options from the old configuration.</param>
    /// <param name="projectName">The base project name.</param>
    /// <param name="dryRun">If true, only returns what would be created.</param>
    /// <returns>The path to the created marker file.</returns>
    public static string GenerateServerMarker(
        string projectDirectory,
        GeneratorOptionsResult options,
        string projectName,
        bool dryRun = false)
    {
        var markerPath = Path.Combine(projectDirectory, ".atc-rest-api-server");

        var markerContent = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["namespace"] = $"{projectName}.Api.Contracts",
            ["validateSpecificationStrategy"] = options.StrictMode == true ? "Strict" : "Default",
            ["includeDeprecated"] = false,
        };

        if (!dryRun)
        {
            WriteMarkerFile(markerPath, markerContent);
        }

        return markerPath;
    }

    /// <summary>
    /// Generates the client marker file (.atc-rest-api-client).
    /// </summary>
    /// <param name="projectDirectory">The directory of the ApiClient project.</param>
    /// <param name="options">The generator options from the old configuration.</param>
    /// <param name="projectName">The base project name.</param>
    /// <param name="clientSuffix">The client suffix to use (e.g., "ApiClient" or "Api.Client"). Default: "ApiClient".</param>
    /// <param name="httpClientName">Optional HttpClient name for HttpClientFactory registration.</param>
    /// <param name="dryRun">If true, only returns what would be created.</param>
    /// <returns>The path to the created marker file.</returns>
    public static string GenerateClientMarker(
        string projectDirectory,
        GeneratorOptionsResult options,
        string projectName,
        string? clientSuffix = null,
        string? httpClientName = null,
        bool dryRun = false)
    {
        var markerPath = Path.Combine(projectDirectory, ".atc-rest-api-client");

        var suffix = string.IsNullOrEmpty(clientSuffix) ? "ApiClient" : clientSuffix;
        var markerContent = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["namespace"] = $"{projectName}.{suffix}",
            ["validateSpecificationStrategy"] = options.StrictMode == true ? "Strict" : "Default",
            ["includeDeprecated"] = false,
            ["generationMode"] = "EndpointPerOperation",
        };

        if (!string.IsNullOrEmpty(httpClientName))
        {
            markerContent["httpClientName"] = httpClientName;
        }

        if (!dryRun)
        {
            WriteMarkerFile(markerPath, markerContent);
        }

        return markerPath;
    }

    /// <summary>
    /// Generates the handler marker file (.atc-rest-api-server-handlers).
    /// </summary>
    /// <param name="projectDirectory">The directory of the Domain project.</param>
    /// <param name="options">The generator options from the old configuration.</param>
    /// <param name="projectName">The base project name.</param>
    /// <param name="dryRun">If true, only returns what would be created.</param>
    /// <returns>The path to the created marker file.</returns>
    public static string GenerateHandlerMarker(
        string projectDirectory,
        GeneratorOptionsResult options,
        string projectName,
        bool dryRun = false)
    {
        var markerPath = Path.Combine(projectDirectory, ".atc-rest-api-server-handlers");

        var markerContent = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["namespace"] = $"{projectName}.Api.Domain",
            ["validateSpecificationStrategy"] = options.StrictMode == true ? "Strict" : "Default",
            ["includeDeprecated"] = false,
        };

        if (!dryRun)
        {
            WriteMarkerFile(markerPath, markerContent);
        }

        return markerPath;
    }

    private static void WriteMarkerFile(
        string path,
        Dictionary<string, object> content)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        var json = JsonSerializer.Serialize(content, options);
        File.WriteAllText(path, json);
    }
}