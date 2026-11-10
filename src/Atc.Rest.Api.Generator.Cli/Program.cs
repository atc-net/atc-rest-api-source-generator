namespace Atc.Rest.Api.Generator.Cli;

/// <summary>
/// Entry point of the atc-rest-api-gen tool.
/// </summary>
[ExcludeFromCodeCoverage]
public static class Program
{
    /// <summary>
    /// The NuGet package id of the tool, which the update check looks up.
    /// </summary>
    public const string PackageId = "atc-rest-api-gen";

    /// <summary>
    /// The environment variable that turns the start-up update check off (<c>1</c> or <c>true</c>).
    /// </summary>
    public const string NoUpdateCheckVariable = "ATC_REST_API_GEN_NO_UPDATE_CHECK";

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
            CliUpdateCheckHelper.ShouldCheckForUpdates(args) &&
            !CliUpdateCheckHelper.IsUpdateCheckDisabled(Environment.GetEnvironmentVariable, NoUpdateCheckVariable);
        var updateCheckArgs = args;

        args = ProgramCsHelper.SetHelpArgumentIfNeeded(args);

        // Redirected output (a pipe, a CI log) is not wrapped at a window's 80 columns
        ConsoleOutputWidthHelper.ApplyRedirectedOutputWidth();

        var consoleLoggerConfiguration = new ConsoleLoggerConfiguration();
        ProgramCsHelper.SetMinimumLogLevelIfNeeded(args, consoleLoggerConfiguration);

        var serviceCollection = ServiceCollectionFactory.Create(consoleLoggerConfiguration);
        ConfigureServices(serviceCollection);

        if (shouldCheckForUpdates)
        {
            await using var startupProvider = serviceCollection.BuildServiceProvider();
            await CliUpdateCheckHelper.CheckForUpdatesAsync(
                updateCheckArgs,
                CreateUpdateCheckOptions(startupProvider.GetRequiredService<INugetFeedClient>()));
        }

        var app = CommandAppFactory.Create(serviceCollection);
        app.ConfigureCommands();

        return await app.RunAsync(args);
    }

    /// <summary>
    /// The options of the start-up update check: the tool's package, its own opt-out variable, the time budget
    /// and the version of the running CLI.
    /// </summary>
    /// <param name="nugetFeedClient">The client the latest version is looked up with.</param>
    /// <returns>The options.</returns>
    public static CliUpdateCheckOptions CreateUpdateCheckOptions(
        INugetFeedClient nugetFeedClient)
        => new(PackageId)
        {
            CurrentVersion = CliHelper.GetCurrentVersion(),
            OptOutEnvironmentVariable = NoUpdateCheckVariable,
            Budget = UpdateCheckBudget,
            NugetFeedClient = nugetFeedClient,
        };

    /// <summary>
    /// Registers the services the commands are resolved with.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddHttpClient(nameof(NugetFeedClient), client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(PackageId);
            })
            .AddTypedClient<INugetFeedClient>(httpClient => new NugetFeedClient(httpClient))
            .RemoveAllLoggers();
    }
}