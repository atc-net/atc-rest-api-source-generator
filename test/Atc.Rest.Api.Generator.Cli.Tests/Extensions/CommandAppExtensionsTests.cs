namespace Atc.Rest.Api.Generator.Cli.Tests.Extensions;

public class CommandAppExtensionsTests
{
    [Fact]
    public void ConfigureCommands_NullApp_Throws()
        => Assert.Throws<ArgumentNullException>(() => ((CommandApp)null!).ConfigureCommands());

    [Theory]
    [InlineData("spec", "validate", "merge", "split")]
    [InlineData("generate", "client", "server", "client-typescript")]
    [InlineData("options", "create", "validate", "")]
    [InlineData("migrate", "validate", "execute", "")]
    public void ConfigureCommands_RegistersBranchWithItsCommands(
        string branch,
        string first,
        string second,
        string third)
    {
        var app = new CommandApp();
        app.ConfigureCommands();

        var (exitCode, output) = RunCapturingOutput(app, [branch, "--help"]);

        Assert.Equal(0, exitCode);
        Assert.Contains(first, output, StringComparison.Ordinal);
        Assert.Contains(second, output, StringComparison.Ordinal);
        if (third.Length > 0)
        {
            Assert.Contains(third, output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ConfigureCommands_UnknownBranch_FailsWithNonZeroExitCode()
    {
        var app = new CommandApp();
        app.ConfigureCommands();

        var (exitCode, _) = RunCapturingOutput(app, ["frobnicate"]);

        Assert.NotEqual(0, exitCode);
    }

    private static (int ExitCode, string Output) RunCapturingOutput(
        CommandApp app,
        string[] args)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer),
        });
        app.Configure(config => config.ConfigureConsole(console));

        var exitCode = app.Run(args);
        return (exitCode, writer.ToString());
    }
}