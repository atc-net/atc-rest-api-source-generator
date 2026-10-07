namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Separates the overlay files from the specification files among a project's AdditionalFiles, and finds the
/// overlays a marker file lists.
/// </summary>
internal static class OverlayFileHelper
{
    private static readonly JsonNodeOptions MarkerNodeOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Whether a collected file can be a specification or an overlay: YAML, or JSON that is an overlay (a JSON file
    /// is otherwise a marker or something else entirely).
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns><see langword="true"/> to keep it.</returns>
    public static bool IsCandidate(YamlFileInfo file)
        => !string.IsNullOrEmpty(file.Content) &&
           (!file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            OverlayDocumentReader.IsOverlay(file.Content));

    /// <summary>
    /// Splits the collected files into specification files and overlay files - by content, not by name: an overlay
    /// named <c>Api.overlay.yaml</c> must never become the base file or a part.
    /// </summary>
    /// <param name="files">The collected files.</param>
    /// <returns>The specification files and the overlay files.</returns>
    public static (ImmutableArray<YamlFileInfo> Specifications, ImmutableArray<YamlFileInfo> Overlays) Split(
        ImmutableArray<YamlFileInfo> files)
    {
        var specifications = ImmutableArray.CreateBuilder<YamlFileInfo>();
        var overlays = ImmutableArray.CreateBuilder<YamlFileInfo>();
        foreach (var file in files)
        {
            if (OverlayDocumentReader.IsOverlay(file.Content))
            {
                overlays.Add(file);
            }
            else if (!file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                specifications.Add(file);
            }
        }

        return (specifications.ToImmutable(), overlays.ToImmutable());
    }

    /// <summary>
    /// The overlays a marker file lists, in its order. A listed overlay that is not an AdditionalFiles item is an
    /// error (<see cref="DiagnosticHelpers.OverlayNotFound"/>); an overlay file the marker does not list is a note.
    /// </summary>
    /// <param name="context">The source-production context the diagnostics are reported to.</param>
    /// <param name="markerPath">The marker file's path.</param>
    /// <param name="listed">The marker's <c>overlays</c> setting.</param>
    /// <param name="overlayFiles">The overlay files among the AdditionalFiles.</param>
    /// <returns>The overlays to apply, or <see langword="null"/> when one is missing (nothing is generated).</returns>
    public static List<OverlayFile>? Resolve(
        SourceProductionContext context,
        string markerPath,
        IReadOnlyList<string>? listed,
        ImmutableArray<YamlFileInfo> overlayFiles)
    {
        var markerDirectory = Path.GetDirectoryName(markerPath) ?? string.Empty;
        var resolved = new List<OverlayFile>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missing = false;

        foreach (var entry in listed ?? [])
        {
            var wanted = FullPath(Path.Combine(markerDirectory, entry));
            var match = overlayFiles.FirstOrDefault(f => string.Equals(FullPath(f.Path), wanted, StringComparison.OrdinalIgnoreCase));
            if (match.Path is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticHelpers.OverlayNotFound, Location.None, markerPath, entry));
                missing = true;
                continue;
            }

            used.Add(FullPath(match.Path));
            resolved.Add(new OverlayFile(match.Path, match.Content));
        }

        foreach (var file in overlayFiles.Where(f => !used.Contains(FullPath(f.Path))))
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticHelpers.OverlayNotListed, Location.None, file.Path, markerPath));
        }

        return missing ? null : resolved;
    }

    /// <summary>
    /// The overlays the project's marker files list, found among the overlay files without reporting anything - for a
    /// generator that reads the specification after the marker's own generator reported the problems.
    /// </summary>
    /// <param name="markers">The project's marker files.</param>
    /// <param name="overlayFiles">The overlay files among the AdditionalFiles.</param>
    /// <returns>The overlays to apply, in the order of the first marker that lists any.</returns>
    public static List<OverlayFile> ResolveListed(
        EquatableArray<MarkerFileInfo> markers,
        ImmutableArray<YamlFileInfo> overlayFiles)
    {
        foreach (var marker in markers.Values)
        {
            List<string>? listed = null;
            try
            {
                // Read as the generators read a marker: the setting's name in any casing
                if (JsonNode.Parse(marker.Content, MarkerNodeOptions) is JsonObject root && root["overlays"] is JsonArray array)
                {
                    listed = array.Select(n => n?.ToString()).OfType<string>().ToList();
                }
            }
            catch (JsonException)
            {
                // The marker's own generator reports an unreadable marker
            }

            if (listed is not { Count: > 0 })
            {
                continue;
            }

            var markerDirectory = Path.GetDirectoryName(marker.Path) ?? string.Empty;
            return listed
                .Select(entry => FullPath(Path.Combine(markerDirectory, entry)))
                .Select(wanted => overlayFiles.FirstOrDefault(f => string.Equals(FullPath(f.Path), wanted, StringComparison.OrdinalIgnoreCase)))
                .Where(f => f.Path is not null)
                .Select(f => new OverlayFile(f.Path, f.Content))
                .ToList();
        }

        return [];
    }

    /// <summary>
    /// Reports what applying the overlays found: an error per problem that stops generation, a warning per action
    /// that changed nothing.
    /// </summary>
    /// <param name="context">The source-production context the diagnostics are reported to.</param>
    /// <param name="problems">The problems.</param>
    public static void Report(
        SourceProductionContext context,
        IReadOnlyList<OverlayFileProblem> problems)
    {
        foreach (var problem in problems)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                problem.Problem.IsError ? DiagnosticHelpers.OverlayInvalid : DiagnosticHelpers.OverlayTargetSelectsNothing,
                Location.None,
                problem.OverlayPath,
                problem.Where,
                problem.Problem.Message));
        }
    }

    private static string FullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }
}