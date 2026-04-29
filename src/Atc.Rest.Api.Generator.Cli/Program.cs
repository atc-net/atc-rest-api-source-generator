namespace Atc.Rest.Api.Generator.Cli;

[ExcludeFromCodeCoverage]
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        args = SetHelpArgumentIfNeeded(args);

        var consoleLoggerConfiguration = new ConsoleLoggerConfiguration();
        ProgramCsHelper.SetMinimumLogLevelIfNeeded(args, consoleLoggerConfiguration);

        var serviceCollection = ServiceCollectionFactory.Create(consoleLoggerConfiguration);
        ConfigureServices(serviceCollection);

        await using (var startupProvider = serviceCollection.BuildServiceProvider())
        {
            await CheckForUpdatesAsync(startupProvider.GetRequiredService<INugetPackageVersionService>());
        }

        var app = CommandAppFactory.Create(serviceCollection);
        app.ConfigureCommands();

        return await app.RunAsync(args);
    }

    private static async Task CheckForUpdatesAsync(
        INugetPackageVersionService nugetService)
    {
        if (!NetworkInformationHelper.HasHttpConnection())
        {
            AnsiConsole.MarkupLine("[yellow]⚠[/] [dim] No internet connection. Version check skipped.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        if (await CliVersionHelper.IsLatestVersionAsync(nugetService))
        {
            return;
        }

        var latestVersion = await CliVersionHelper.GetLatestVersionAsync(nugetService);
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
        services.AddHttpClient<INugetPackageVersionService, NugetPackageVersionService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("atc-rest-api-gen");
        });
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