namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Helpers for reading configuration from marker files (.atc-rest-api-server).
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "OK.")]
[SuppressMessage("", "RS1035:The symbol 'File' is banned for use by analyzers: Do not do file IO in analyzers", Justification = "OK.")]
internal static class MarkerFileHelper
{
    /// <summary>
    /// Whether a file is a server marker (<c>.atc-rest-api-server</c> or <c>.atc-rest-api-server.json</c>).
    /// </summary>
    /// <param name="path">The file's path.</param>
    /// <returns>True for a server marker.</returns>
    public static bool IsServerMarker(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.Equals(Constants.MarkerFile.Server, StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals(Constants.MarkerFile.ServerJson, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tries to read the server namespace from the server marker the domain project pairs with: one of the declared
    /// <paramref name="serverMarkers"/> (the project's own, or a sibling project's that the package's targets add to
    /// the AdditionalFiles) - in the same directory first, then the <c>X.Contracts</c> sibling of an <c>X.Domain</c>
    /// project, then the one sibling that has a marker. Without declared markers (a project that does not import the
    /// package's targets) the sibling directories are searched on disk.
    /// </summary>
    /// <param name="markerDirectory">The directory of the domain project's marker file.</param>
    /// <param name="serverMarkers">The server markers among the project's AdditionalFiles.</param>
    /// <returns>The namespace; null when no marker is found, or it has none.</returns>
    public static string? TryGetServerNamespace(
        string markerDirectory,
        IReadOnlyList<MarkerFileInfo> serverMarkers)
    {
        if (serverMarkers is null || serverMarkers.Count == 0)
        {
            return TryGetServerNamespace(markerDirectory);
        }

        var marker = SelectServerMarker(markerDirectory, serverMarkers);
        return marker is null ? null : NamespaceOf(marker.Value.Content);
    }

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
        if (markerPath is null)
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
                if (markerPath is null)
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

        if (markerPath is null)
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
    /// Returns true when more than one sibling directory of <paramref name="markerDirectory"/> has a server marker among
    /// the declared <paramref name="serverMarkers"/> (on disk, without declared markers).
    /// </summary>
    /// <param name="markerDirectory">The directory of the domain project's marker file.</param>
    /// <param name="serverMarkers">The server markers among the project's AdditionalFiles.</param>
    /// <returns>True when the sibling to pair with is ambiguous.</returns>
    public static bool HasMultipleSiblingServerMarkers(
        string markerDirectory,
        IReadOnlyList<MarkerFileInfo> serverMarkers)
    {
        if (serverMarkers is null || serverMarkers.Count == 0)
        {
            return HasMultipleSiblingServerMarkers(markerDirectory);
        }

        return serverMarkers
            .Select(m => DirectoryOf(m.Path))
            .Where(d => !SameDirectory(d, markerDirectory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() > 1;
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

    // The marker the domain project pairs with, by the rules of the search on disk
    private static MarkerFileInfo? SelectServerMarker(
        string markerDirectory,
        IReadOnlyList<MarkerFileInfo> serverMarkers)
    {
        MarkerFileInfo? In(string directory)
        {
            // .atc-rest-api-server before .atc-rest-api-server.json, as on disk
            MarkerFileInfo? found = null;
            foreach (var marker in serverMarkers.Where(m => SameDirectory(DirectoryOf(m.Path), directory)))
            {
                if (Path.GetFileName(marker.Path).Equals(Constants.MarkerFile.Server, StringComparison.OrdinalIgnoreCase))
                {
                    return marker;
                }

                found ??= marker;
            }

            return found;
        }

        if (In(markerDirectory) is { } own)
        {
            return own;
        }

        var parentDirectory = Path.GetDirectoryName(markerDirectory);
        if (string.IsNullOrEmpty(parentDirectory))
        {
            return null;
        }

        var markerDirName = Path.GetFileName(markerDirectory) ?? string.Empty;
        if (markerDirName.EndsWith(".Domain", StringComparison.Ordinal) &&
            In(Path.Combine(parentDirectory, markerDirName.Substring(0, markerDirName.Length - ".Domain".Length) + ".Contracts")) is { } preferred)
        {
            return preferred;
        }

        var siblings = serverMarkers
            .Select(m => DirectoryOf(m.Path))
            .Where(d => !SameDirectory(d, markerDirectory) && SameDirectory(Path.GetDirectoryName(d) ?? string.Empty, parentDirectory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return siblings.Count == 1 ? In(siblings[0]) : null;
    }

    private static string DirectoryOf(string path)
        => Path.GetDirectoryName(path) ?? string.Empty;

    private static string? NamespaceOf(string markerContent)
    {
        try
        {
            return JsonSerializer.Deserialize<ServerConfig>(markerContent, JsonHelper.ConfigOptions)?.Namespace;
        }
        catch
        {
            return null;
        }
    }

    private static bool SameDirectory(
        string first,
        string second)
        => string.Equals(
            first.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            second.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}