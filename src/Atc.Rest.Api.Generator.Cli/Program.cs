namespace Atc.Rest.Api.Generator.Cli;

/// <summary>
/// Entry point of the atc-rest-api-gen tool.
/// </summary>
[ExcludeFromCodeCoverage]
public static class Program
{
    /// <summary>
    /// The time the update check may take before the command runs. The check is best effort:
    /// when the budget runs out, the command starts and nothing is printed.
    /// </summary>
    private static readonly TimeSpan UpdateCheckBudget = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Checks NuGet for a newer tool version (unless the command line or the environment says not to),
    /// then runs the Spectre.Console command app with the given arguments.
    /// </summary>
    /// <param name="args">The command-line arguments; no arguments shows the help.</param>
    /// <returns>The exit code of the executed command.</returns>
    public static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var shouldCheckForUpdates =
            CliVersionHelper.ShouldCheckForUpdates(args) &&
            !CliVersionHelper.IsUpdateCheckDisabled(Environment.GetEnvironmentVariable);

        args = SetHelpArgumentIfNeeded(args);

        // Redirected output (a pipe, a CI log) is not wrapped at a window's 80 columns
        if (System.Console.IsOutputRedirected)
        {
            AnsiConsole.Profile.Width = ConsoleWidthHelper.ForRedirectedOutput(Environment.GetEnvironmentVariable("COLUMNS"));
        }

        var consoleLoggerConfiguration = new ConsoleLoggerConfiguration();
        ProgramCsHelper.SetMinimumLogLevelIfNeeded(args, consoleLoggerConfiguration);

        var serviceCollection = ServiceCollectionFactory.Create(consoleLoggerConfiguration);
        ConfigureServices(serviceCollection);

        if (shouldCheckForUpdates)
        {
            await using var startupProvider = serviceCollection.BuildServiceProvider();
            await CheckForUpdatesAsync(startupProvider.GetRequiredService<INugetPackageVersionService>());
        }

        var app = CommandAppFactory.Create(serviceCollection);
        app.ConfigureCommands();

        return await app.RunAsync(args);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The update check must never affect the command that runs after it.")]
    private static async Task CheckForUpdatesAsync(
        INugetPackageVersionService nugetService)
    {
        try
        {
            var currentVersion = CliHelper.GetCurrentVersion();

            // Skip check for development versions
            if (currentVersion == new Version(1, 0, 0, 0))
            {
                return;
            }

            var latestVersion = await CliVersionHelper.TryGetNewerVersionAsync(nugetService, currentVersion, UpdateCheckBudget);
            if (latestVersion is null)
            {
                return;
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Panel(
                Markup.FromInterpolated(
                    CultureInfo.InvariantCulture,
                    $"[yellow]A new version is available![/]\n\n  Current: [dim]{currentVersion}[/]\n  Latest:  [green]{latestVersion}[/]\n\n[blue]dotnet tool update --global atc-rest-api-gen[/]"))
                .Header("[yellow] ⬆ Update Available [/]")
                .BorderColor(Color.Yellow)
                .Padding(1, 0, 1, 0));
            AnsiConsole.WriteLine();
        }
        catch (Exception)
        {
            // Nothing about the update check may stop the command.
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services
            .AddHttpClient<INugetPackageVersionService, NugetPackageVersionService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("atc-rest-api-gen");
            })
            .RemoveAllLoggers();
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