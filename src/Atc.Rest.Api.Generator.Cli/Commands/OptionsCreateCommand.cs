namespace Atc.Rest.Api.Generator.Cli.Commands;

/// <summary>
/// Command to create a default ApiGeneratorOptions.json file.
/// </summary>
public sealed class OptionsCreateCommand : Command<OptionsCommandSettings>
{
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

        AnsiConsole.MarkupLine($"[blue]Output path:[/] {filePath}");
        AnsiConsole.WriteLine();

        // Check if file already exists
        if (File.Exists(filePath) && !settings.Force)
        {
            AnsiConsole.MarkupLine($"[yellow]File already exists:[/] {filePath}");
            AnsiConsole.MarkupLine("[yellow]Use --force to overwrite.[/]");
            return 1;
        }

        // Create the options file
        if (!ApiOptionsHelper.CreateDefaultOptionsFile(outputPath))
        {
            AnsiConsole.MarkupLine("[red]Failed to create options file.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Created:[/] {filePath}");
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