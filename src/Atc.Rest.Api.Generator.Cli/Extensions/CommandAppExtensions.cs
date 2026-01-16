namespace Atc.Rest.Api.Generator.Cli.Extensions;

/// <summary>
/// Extension methods for configuring the command application.
/// </summary>
public static class CommandAppExtensions
{
    /// <summary>
    /// Configures all CLI commands for the application.
    /// </summary>
    /// <param name="app">The command application.</param>
    public static void ConfigureCommands(this CommandApp app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Configure(config =>
        {
            config.SetApplicationName("atc-rest-api-gen");
            config.SetApplicationVersion(CliHelper.GetCurrentVersion().ToString());

            config.AddBranch("spec", ConfigureSpecCommands());
            config.AddBranch("generate", ConfigureGenerateCommands());
            config.AddBranch("options", ConfigureOptionsCommands());
            config.AddBranch("migrate", ConfigureMigrateCommands());
        });
    }

    private static Action<IConfigurator<CommandSettings>> ConfigureSpecCommands()
        => node =>
        {
            node.SetDescription("Commands for working with OpenAPI specification files.");

            node
                .AddCommand<SpecValidateCommand>("validate")
                .WithDescription("Validate an OpenAPI specification file (strict mode by default).")
                .WithExample("spec", "validate", "-s", "api.yaml")
                .WithExample("spec", "validate", "-s", "api.yaml", "--no-strict")
                .WithExample("spec", "validate", "-s", "api.yaml", "--multi-part")
                .WithExample("spec", "validate", "--files", "api.yaml,api_Users.yaml,api_Orders.yaml");

            node
                .AddCommand<SpecMergeCommand>("merge")
                .WithDescription("Merge multiple OpenAPI specification files into one.")
                .WithExample("spec", "merge", "-s", "api.yaml", "-o", "merged.yaml")
                .WithExample("spec", "merge", "-s", "api.yaml", "-o", "merged.yaml", "--validate")
                .WithExample("spec", "merge", "--files", "api.yaml,api_Users.yaml,api_Orders.yaml", "-o", "merged.yaml")
                .WithExample("spec", "merge", "-s", "api.yaml", "--preview");

            node
                .AddCommand<SpecSplitCommand>("split")
                .WithDescription("Split an OpenAPI specification file into multiple part files.")
                .WithExample("spec", "split", "-s", "api.yaml", "-o", "./parts")
                .WithExample("spec", "split", "-s", "api.yaml", "-o", "./parts", "--strategy", "ByPathSegment")
                .WithExample("spec", "split", "-s", "api.yaml", "-o", "./parts", "--extract-common")
                .WithExample("spec", "split", "-s", "api.yaml", "--preview");
        };

    private static Action<IConfigurator<CommandSettings>> ConfigureGenerateCommands()
        => node =>
        {
            node.SetDescription("Commands for generating code from OpenAPI specifications.");

            node
                .AddCommand<GenerateClientCommand>("client")
                .WithDescription("Generate a client project from an OpenAPI specification (strict validation by default).")
                .WithExample("generate", "client", "-s", "api.yaml", "-o", "output/MyApp.Client", "-n", "MyApp.Client")
                .WithExample("generate", "client", "-s", "api.yaml", "-o", "output/MyApp.Client", "-n", "MyApp.Client", "--no-strict");

            node
                .AddCommand<GenerateServerCommand>("server")
                .WithDescription("Generate server contracts and domain handler scaffolds from an OpenAPI specification.")
                .WithExample("generate", "server", "-s", "api.yaml", "-o", "output/MyApp.Api.Contracts", "-n", "MyApp.Api.Contracts")
                .WithExample("generate", "server", "-s", "api.yaml", "-o", "output/MyApp.Api.Contracts", "-n", "MyApp.Api.Contracts", "--no-domain")
                .WithExample("generate", "server", "-s", "api.yaml", "-o", "output/MyApp.Api.Contracts", "-n", "MyApp.Api.Contracts", "--versioning-strategy", "UrlSegment");
        };

    private static Action<IConfigurator<CommandSettings>> ConfigureOptionsCommands()
        => node =>
        {
            node.SetDescription("Commands for managing ApiGeneratorOptions.json configuration files.");

            node
                .AddCommand<OptionsCreateCommand>("create")
                .WithDescription("Create a default ApiGeneratorOptions.json file.")
                .WithExample("options", "create", "-o", "./")
                .WithExample("options", "create", "-o", "./config/ApiGeneratorOptions.json", "--force");

            node
                .AddCommand<OptionsValidateCommand>("validate")
                .WithDescription("Validate an ApiGeneratorOptions.json file.")
                .WithExample("options", "validate", "-o", "./")
                .WithExample("options", "validate", "-o", "./config/ApiGeneratorOptions.json");
        };

    private static Action<IConfigurator<CommandSettings>> ConfigureMigrateCommands()
        => node =>
        {
            node.SetDescription("Commands for migrating old CLI-generated projects to source generators.");

            node
                .AddCommand<MigrateValidateCommand>("validate")
                .WithDescription("Validate an old-generated API project for migration to source generators.")
                .WithExample("migrate", "validate", "-s", "D:\\Code\\MyProject", "-p", "specs\\api.yaml")
                .WithExample("migrate", "validate", "-s", "D:\\Code\\MyProject", "-p", "specs\\api.yaml", "--verbose")
                .WithExample("migrate", "validate", "-s", "D:\\Code\\MyProject", "-p", "specs\\api.yaml", "--output-report", "report.json");

            node
                .AddCommand<MigrateExecuteCommand>("execute")
                .WithDescription("Execute migration from old CLI-generated API to source generators.")
                .WithExample("migrate", "execute", "-s", "D:\\Code\\MyProject", "-p", "specs\\api.yaml")
                .WithExample("migrate", "execute", "-s", "D:\\Code\\MyProject", "-p", "specs\\api.yaml", "--dry-run")
                .WithExample("migrate", "execute", "-s", "D:\\Code\\MyProject", "-p", "specs\\api.yaml", "--force");
        };
}