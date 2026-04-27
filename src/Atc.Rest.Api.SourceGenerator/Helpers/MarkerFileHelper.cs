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
                // Prefer sibling matching the convention: <X>.Domain pairs with <X>.Contracts.
                // This is critical when multiple Contracts projects exist as siblings (multi-API repos),
                // where the alphabetical fallback would otherwise pick the wrong Contracts namespace.
                var markerDirName = Path.GetFileName(markerDirectory) ?? string.Empty;
                if (markerDirName.EndsWith(".Domain", StringComparison.Ordinal))
                {
                    var preferredSiblingName = markerDirName.Substring(0, markerDirName.Length - ".Domain".Length) + ".Contracts";
                    var preferredSiblingDir = Path.Combine(parentDirectory, preferredSiblingName);

                    if (Directory.Exists(preferredSiblingDir))
                    {
                        var preferredMarkerPath = Path.Combine(preferredSiblingDir, Constants.MarkerFile.Server);
                        var preferredMarkerJsonPath = Path.Combine(preferredSiblingDir, Constants.MarkerFile.ServerJson);

                        if (File.Exists(preferredMarkerPath))
                        {
                            markerPath = preferredMarkerPath;
                        }
                        else if (File.Exists(preferredMarkerJsonPath))
                        {
                            markerPath = preferredMarkerJsonPath;
                        }
                    }
                }

                // Fall back to scanning siblings for a marker file. Succeed only when
                // exactly ONE sibling has a marker — anything else is ambiguous and the
                // caller is expected to require explicit `contractsNamespace` configuration.
                if (markerPath == null)
                {
                    var siblingMarkers = new List<string>();
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
                            siblingMarkers.Add(siblingMarkerPath);
                        }
                        else if (File.Exists(siblingMarkerJsonPath))
                        {
                            siblingMarkers.Add(siblingMarkerJsonPath);
                        }
                    }

                    if (siblingMarkers.Count == 1)
                    {
                        markerPath = siblingMarkers[0];
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

    /// <summary>
    /// Returns true when the parent of <paramref name="markerDirectory"/> contains more than one
    /// sibling directory with an .atc-rest-api-server marker. Used by callers to detect ambiguous
    /// auto-detection scenarios in monorepos with multiple Api.Contracts projects.
    /// </summary>
    public static bool HasMultipleSiblingServerMarkers(string markerDirectory)
    {
        if (string.IsNullOrEmpty(markerDirectory))
        {
            return false;
        }

        var parentDirectory = Path.GetDirectoryName(markerDirectory);
        if (string.IsNullOrEmpty(parentDirectory) || !Directory.Exists(parentDirectory))
        {
            return false;
        }

        var count = 0;
        foreach (var siblingDir in Directory.GetDirectories(parentDirectory))
        {
            if (siblingDir == markerDirectory)
            {
                continue;
            }

            if (File.Exists(Path.Combine(siblingDir, Constants.MarkerFile.Server)) ||
                File.Exists(Path.Combine(siblingDir, Constants.MarkerFile.ServerJson)))
            {
                count++;
                if (count > 1)
                {
                    return true;
                }
            }
        }

        return false;
    }
}