namespace Atc.Rest.Api.Generator.Cli.Commands;

/// <summary>
/// Command to create a default ApiGeneratorOptions.json file.
/// </summary>
public sealed class OptionsCreateCommand : Command<OptionsCommandSettings>
{
    /// <summary>
    /// Writes a default ApiGeneratorOptions.json to the output path, refusing to overwrite an existing file unless forced.
    /// </summary>
    /// <param name="context">The Spectre.Console command context.</param>
    /// <param name="settings">The parsed command-line settings.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>0 when the file was written; otherwise 1.</returns>
    public override int Execute(
        CommandContext context,
        OptionsCommandSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader();

        var outputPath = Path.GetFullPath(settings.OutputPath);
        var filePath = Directory.Exists(outputPath)
            ? Path.Combine(outputPath, ApiGeneratorOptions.FileName)
            : outputPath;

        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Output path:[/] {filePath}");
        AnsiConsole.WriteLine();

        // Check if file already exists
        if (File.Exists(filePath) && !settings.Force)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]File already exists:[/] {filePath}");
            AnsiConsole.MarkupLine("[yellow]Use --force to overwrite.[/]");
            return 1;
        }

        // Create the options file
        if (!ApiOptionsHelper.CreateDefaultOptionsFile(outputPath))
        {
            AnsiConsole.MarkupLine("[red]Failed to create options file.[/]");
            return 1;
        }

        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]Created:[/] {filePath}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]You can now customize the options file for your project.[/]");

        return 0;
    }

    private static void WriteHeader()
    {
        AnsiConsole.Write(new FigletText("ATC REST API").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]Options File Creator[/]");
        AnsiConsole.WriteLine();
    }
}