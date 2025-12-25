namespace Atc.Rest.Api.CliGenerator.Commands;

/// <summary>
/// Command to validate an ApiGeneratorOptions.json file.
/// </summary>
public sealed class OptionsValidateCommand : Command<OptionsCommandSettings>
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

        var filePath = Path.GetFullPath(settings.OutputPath);

        // If it's a directory, append the default file name
        if (Directory.Exists(filePath))
        {
            filePath = Path.Combine(filePath, ApiGeneratorOptions.FileName);
        }

        AnsiConsole.MarkupLine($"[blue]Validating:[/] {filePath}");
        AnsiConsole.WriteLine();

        return AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .Start("Validating options file...", ctx =>
            {
                var result = Helpers.ApiOptionsHelper.ValidateOptionsFile(settings.OutputPath);

                if (result.Errors.Count > 0)
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Validation failed with {result.Errors.Count} error(s):");
                    foreach (var error in result.Errors)
                    {
                        AnsiConsole.MarkupLine($"  [red]{Markup.Escape(error)}[/]");
                    }

                    return 1;
                }

                if (result.Warnings.Count > 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]![/] Validation passed with {result.Warnings.Count} warning(s):");
                    foreach (var warning in result.Warnings)
                    {
                        AnsiConsole.MarkupLine($"  [yellow]{Markup.Escape(warning)}[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[green]✓[/] Validation passed - options file is valid");
                }

                AnsiConsole.WriteLine();

                // Display loaded configuration summary
                if (result.Options is not null)
                {
                    DisplayConfigurationSummary(result.Options);
                }

                return 0;
            });
    }

    private static void WriteHeader()
    {
        AnsiConsole.Write(new FigletText("ATC REST API").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]Options File Validator[/]");
        AnsiConsole.WriteLine();
    }

    private static void DisplayConfigurationSummary(ApiGeneratorOptions options)
    {
        var table = new Table();
        table.AddColumn("Section");
        table.AddColumn("Setting");
        table.AddColumn("Value");

        // General settings
        table.AddRow("general", "validateSpecificationStrategy", options.General.ValidateSpecificationStrategy.ToString());
        table.AddRow("general", "includeDeprecated", options.General.IncludeDeprecated.ToString());

        // Server settings
        table.AddRow("server", "namespace", options.Server.Namespace ?? "(auto-detect)");
        table.AddRow("server", "subFolderStrategy", options.Server.SubFolderStrategy.ToString());
        table.AddRow("server", "versioningStrategy", options.Server.VersioningStrategy.ToString());
        table.AddRow("server", "defaultApiVersion", options.Server.DefaultApiVersion);

        // Server domain settings
        table.AddRow("server.domain", "generateHandlersOutput", options.Server.Domain.GenerateHandlersOutput);
        table.AddRow("server.domain", "handlerSuffix", options.Server.Domain.HandlerSuffix);
        table.AddRow("server.domain", "stubImplementation", options.Server.Domain.StubImplementation);

        // Client settings
        table.AddRow("client", "namespace", options.Client.Namespace ?? "(auto-detect)");
        table.AddRow("client", "generationMode", options.Client.GenerationMode.ToString());
        table.AddRow("client", "clientSuffix", options.Client.ClientSuffix);
        table.AddRow("client", "generateOAuthTokenManagement", options.Client.GenerateOAuthTokenManagement.ToString());

        AnsiConsole.MarkupLine("[blue]Configuration summary:[/]");
        AnsiConsole.Write(table);
    }
}