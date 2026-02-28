namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Helpers for reading configuration from marker files (.atc-rest-api-server).
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "OK.")]
[SuppressMessage("", "RS1035:The symbol 'File' is banned for use by analyzers: Do not do file IO in analyzers", Justification = "OK.")]
internal static class MarkerFileHelper
{
    /// <summary>
    /// Tries to read the server namespace from the .atc-rest-api-server marker file.
    /// Searches in the same directory first, then in sibling directories.
    /// Returns null if no marker file is found or doesn't have a namespace configured.
    /// </summary>
    public static string? TryGetServerNamespace(string markerDirectory)
    {
        if (string.IsNullOrEmpty(markerDirectory))
        {
            return null;
        }

        // First, check same directory (existing behavior)
        var serverMarkerPath = Path.Combine(markerDirectory, Constants.MarkerFile.Server);
        var serverMarkerJsonPath = Path.Combine(markerDirectory, Constants.MarkerFile.ServerJson);

        var markerPath = File.Exists(serverMarkerPath) ? serverMarkerPath :
                         File.Exists(serverMarkerJsonPath) ? serverMarkerJsonPath : null;

        // If not in same directory, search sibling directories
        if (markerPath == null)
        {
            var parentDirectory = Path.GetDirectoryName(markerDirectory);
            if (!string.IsNullOrEmpty(parentDirectory) && Directory.Exists(parentDirectory))
            {
                foreach (var siblingDir in Directory.GetDirectories(parentDirectory))
                {
                    if (siblingDir == markerDirectory)
                    {
                        continue;
                    }

                    var siblingMarkerPath = Path.Combine(siblingDir, Constants.MarkerFile.Server);
                    var siblingMarkerJsonPath = Path.Combine(siblingDir, Constants.MarkerFile.ServerJson);

                    if (File.Exists(siblingMarkerPath))
                    {
                        markerPath = siblingMarkerPath;
                        break;
                    }

                    if (File.Exists(siblingMarkerJsonPath))
                    {
                        markerPath = siblingMarkerJsonPath;
                        break;
                    }
                }
            }
        }

        if (markerPath == null)
        {
            return null;
        }

        try
        {
            var content = File.ReadAllText(markerPath);
            var serverConfig = JsonSerializer.Deserialize<ServerConfig>(content, JsonHelper.ConfigOptions);
            return serverConfig?.Namespace;
        }
        catch
        {
            return null;
        }
    }
}