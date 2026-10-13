namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// Applies the <c>--overlay</c> files of a command to a specification, the way the build applies the overlays a
/// marker file lists, and prints what they report.
/// </summary>
public static class OverlayCliHelper
{
    /// <summary>
    /// The full path of an <c>--overlay</c> (relative to the current directory).
    /// </summary>
    /// <param name="path">The path as given.</param>
    /// <returns>The full path.</returns>
    public static string FullPath(string path)
        => Path.GetFullPath(PathHelper.ResolveRelativePath(path));

    /// <summary>
    /// The overlays of a generate command, in the order they are applied: the options file's
    /// (<c>general.overlays</c>), then the <c>--overlay</c> ones. An overlay named by both is applied once, where the
    /// options file has it.
    /// </summary>
    /// <param name="fromOptions">The options file's overlays, as full paths.</param>
    /// <param name="fromCommandLine">The <c>--overlay</c> paths.</param>
    /// <returns>The full paths.</returns>
    public static string[] All(
        IEnumerable<string>? fromOptions,
        IEnumerable<string>? fromCommandLine)
        => [.. (fromOptions ?? []).Concat((fromCommandLine ?? []).Select(FullPath)).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Checks that every overlay file exists.
    /// </summary>
    /// <param name="overlays">The <c>--overlay</c> paths.</param>
    /// <returns>An error naming the first missing file, or <see langword="null"/>.</returns>
    public static string? FindMissing(IReadOnlyList<string> overlays)
    {
        ArgumentNullException.ThrowIfNull(overlays);

        return overlays
            .Where(o => !File.Exists(FullPath(o)))
            .Select(o => $"Overlay file not found: {o}")
            .FirstOrDefault();
    }

    /// <summary>
    /// Applies overlays to a specification's text and prints what they report: an error per problem that stops, a
    /// warning per action that changed nothing.
    /// </summary>
    /// <param name="specificationContent">The specification (YAML or JSON).</param>
    /// <param name="overlays">The <c>--overlay</c> paths, in the order they are applied.</param>
    /// <returns>The effective specification (JSON text), the content as it was without overlays, or <see langword="null"/> after an error.</returns>
    public static string? Apply(
        string specificationContent,
        IReadOnlyList<string> overlays)
    {
        ArgumentNullException.ThrowIfNull(overlays);
        if (overlays.Count == 0)
        {
            return specificationContent;
        }

        var files = overlays
            .Select(FullPath)
            .Select(path => new OverlayFile(path, File.ReadAllText(path)))
            .ToList();

        var (effective, problems) = SpecificationOverlays.Apply(specificationContent, files);
        foreach (var problem in problems)
        {
            var where = Path.GetFileName(problem.OverlayPath) + problem.Where;
            if (problem.Problem.IsError)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] [[{RuleIdentifiers.OverlayInvalid}]] {where}: {problem.Problem.Message}");
            }
            else
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]![/] [[{RuleIdentifiers.OverlayTargetSelectsNothing}]] {where}: {problem.Problem.Message}");
            }
        }

        if (effective is not null)
        {
            var fileNames = string.Join(", ", files.Select(f => Path.GetFileName(f.FilePath)));
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Applied {files.Count} overlay(s): {fileNames}");
        }

        return effective;
    }
}