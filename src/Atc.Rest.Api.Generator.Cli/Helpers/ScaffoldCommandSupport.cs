namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// What the commands that scaffold a project (<c>generate server</c>, <c>generate client</c>) share: the check that the
/// output folder holds no other project, and the generation report.
/// </summary>
internal static class ScaffoldCommandSupport
{
    /// <summary>
    /// Whether a project may be written into the output folder: it does not exist yet, or the only project file in it
    /// is this project's. Otherwise says why on the console.
    /// </summary>
    /// <param name="outputPath">The output folder.</param>
    /// <param name="projectName">The project's name.</param>
    /// <returns><see langword="true"/> when the folder may be used.</returns>
    public static bool ValidateOutputDirectory(
        string outputPath,
        string projectName)
    {
        if (!Directory.Exists(outputPath))
        {
            return true;
        }

        var expectedCsprojName = $"{projectName}.csproj";
        foreach (var fileName in Directory.GetFiles(outputPath, "*.csproj").Select(Path.GetFileName))
        {
            if (!string.Equals(fileName, expectedCsprojName, StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Output directory contains a different project file: {fileName}");
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [red]Expected:[/] {expectedCsprojName}");
                AnsiConsole.MarkupLine("  [yellow]Please use a different output directory or project name.[/]");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Writes the generation report to the console, and to <c>.generation-report.md</c> in the output folder when
    /// <c>--report</c> asks for it.
    /// </summary>
    /// <param name="statistics">The statistics of the run.</param>
    /// <param name="writeFile">Whether <c>--report</c> was passed.</param>
    /// <param name="outputPath">The output folder.</param>
    public static void WriteGenerationReport(
        GenerationStatistics statistics,
        bool writeFile,
        string outputPath)
    {
        ReportService.WriteToConsole(statistics);
        if (!writeFile)
        {
            return;
        }

        var reportPath = Path.Combine(outputPath, ".generation-report.md");
        ReportService.WriteToFile(statistics, reportPath);
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[dim]Report written to: {reportPath}[/]");
    }
}