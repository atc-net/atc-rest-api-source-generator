namespace Atc.Rest.Api.CliGenerator;

[ExcludeFromCodeCoverage]
public static class Program
{
    public static Task<int> Main(string[] args)
    {
        ////args = ["-h"];

        ////args =
        ////[
        ////    "options", "create",
        ////    "-o", @"c:\temp\atc-rest-api-cli-tests",
        ////    "--force"
        ////];

        ////args =
        ////[
        ////    "validate", "schema",
        ////    "-s", @"D:\Code\atc-net-sandbox\atc-rest-api-source-generator\test\Scenarios\Demo\Demo.yaml"
        ////];

        ////args =
        ////[
        ////    "generate", "client",
        ////    "-s", @"D:\Code\atc-net-sandbox\atc-rest-api-source-generator\test\Scenarios\Demo\Demo.yaml",
        ////    "-o", @"c:\temp\atc-rest-api-cli-tests\Client",
        ////    "-n", "DemoApiClient",
        ////];

        ////args =
        ////[
        ////    "generate", "server",
        ////    "-s", @"D:\Code\atc-net-sandbox\atc-rest-api-source-generator\test\Scenarios\Demo\Demo.yaml",
        ////    "-o", @"c:\temp\atc-rest-api-cli-tests\API",
        ////    "-n", "MyDemo",
        ////    "--project-structure", "TreeProjects",
        ////    //"--report"
        ////];

        ArgumentNullException.ThrowIfNull(args);

        args = SetHelpArgumentIfNeeded(args);

        var consoleLoggerConfiguration = new ConsoleLoggerConfiguration();
        ProgramCsHelper.SetMinimumLogLevelIfNeeded(args, consoleLoggerConfiguration);

        var serviceCollection = ServiceCollectionFactory.Create(consoleLoggerConfiguration);
        ConfigureServices(serviceCollection);

        var app = CommandAppFactory.Create(serviceCollection);
        app.ConfigureCommands();

        return app.RunAsync(args);
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