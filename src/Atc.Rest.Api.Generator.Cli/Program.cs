namespace Atc.Rest.Api.Generator.Cli;

[ExcludeFromCodeCoverage]
public static class Program
{
    public static Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        args = SetHelpArgumentIfNeeded(args);

        var consoleLoggerConfiguration = new ConsoleLoggerConfiguration();
        ProgramCsHelper.SetMinimumLogLevelIfNeeded(args, consoleLoggerConfiguration);

        var serviceCollection = ServiceCollectionFactory.Create(consoleLoggerConfiguration);
        ConfigureServices(serviceCollection);

        var app = CommandAppFactory.Create(serviceCollection);
        app.ConfigureCommands();

        CheckForUpdates();

        return app.RunAsync(args);
    }

    private static void CheckForUpdates()
    {
        if (!NetworkInformationHelper.HasHttpConnection())
        {
            AnsiConsole.MarkupLine("[yellow]⚠[/] [dim] No internet connection. Version check skipped.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        if (CliVersionHelper.IsLatestVersion())
        {
            return;
        }

        var latestVersion = CliVersionHelper.GetLatestVersion();
        if (latestVersion is null)
        {
            return;
        }

        var currentVersion = CliHelper.GetCurrentVersion();

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(
            new Markup($"[yellow]A new version is available![/]\n\n" +
                       $"  Current: [dim]{currentVersion}[/]\n" +
                       $"  Latest:  [green]{latestVersion}[/]\n\n" +
                       $"[blue]dotnet tool update --global atc-rest-api-gen[/]"))
            .Header("[yellow] ⬆ Update Available [/]")
            .BorderColor(Color.Yellow)
            .Padding(1, 0, 1, 0));
        AnsiConsole.WriteLine();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Add services here if needed
    }

    private static string[] SetHelpArgumentIfNeeded(string[] args)
    {
        if (args.Length == 0)
        {
            return [CommandConstants.ArgumentShortHelp];
        }

        return args;
    }
}