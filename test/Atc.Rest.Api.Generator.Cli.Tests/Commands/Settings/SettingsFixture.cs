namespace Atc.Rest.Api.Generator.Cli.Tests.Commands.Settings;

/// <summary>
/// A temp folder with the files the settings validators look at: a spec, a file with the wrong
/// extension, an options file and a solution.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort cleanup in tests.")]
public sealed class SettingsFixture : IDisposable
{
    public SettingsFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "atc-rest-api-cli-settings", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);

        SpecPath = Path.Combine(Root, "api.yaml");
        File.Copy(CliTestHelper.GetScenarioYamlPath("Demo"), SpecPath);

        TextPath = Path.Combine(Root, "notes.txt");
        File.WriteAllText(TextPath, "not a spec");

        OptionsPath = Path.Combine(Root, "ApiGeneratorOptions.json");
        File.WriteAllText(OptionsPath, "{}");

        SolutionDirectory = Path.Combine(Root, "sln");
        Directory.CreateDirectory(SolutionDirectory);
        SolutionPath = Path.Combine(SolutionDirectory, "Contoso.sln");
        File.WriteAllText(SolutionPath, "Microsoft Visual Studio Solution File, Format Version 12.00");

        MissingPath = Path.Combine(Root, "missing.yaml");

        OverlayPath = Path.Combine(Root, "fixes.overlay.yaml");
        File.WriteAllText(
            OverlayPath,
            """
            overlay: 1.0.0
            info:
              title: Fixes
              version: 1.0.0
            actions:
              - target: $.info
                update:
                  description: Patched
            """);

        MissingOverlayPath = Path.Combine(Root, "missing.overlay.yaml");
    }

    public string Root { get; }

    public string OverlayPath { get; }

    public string MissingOverlayPath { get; }

    public string SpecPath { get; }

    public string TextPath { get; }

    public string OptionsPath { get; }

    public string SolutionDirectory { get; }

    public string SolutionPath { get; }

    public string MissingPath { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch
        {
            // Best effort cleanup
        }
    }
}