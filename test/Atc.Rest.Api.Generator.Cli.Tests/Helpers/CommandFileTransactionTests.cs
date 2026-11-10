namespace Atc.Rest.Api.Generator.Cli.Tests.Helpers;

/// <summary>
/// A command that writes into a project leaves it as it found it when it fails, and says so; a command that succeeds
/// keeps what it wrote. The tracking itself is <see cref="FileSystemTransaction"/>'s and tested in Atc.
/// </summary>
[Collection("AnsiConsoleSerial")]
public sealed class CommandFileTransactionTests : IDisposable
{
    private const string RolledBackMessage = "The files written before the failure were put back.";

    private readonly string root = Path.Combine(Path.GetTempPath(), "atc-command-file-transaction-" + Guid.NewGuid().ToString("N"));

    public CommandFileTransactionTests()
        => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(0, "after", false)]
    [InlineData(1, "before", true)]
    public async Task Run_KeepsWhatASucceedingCommandWrote_AndPutsBackWhatAFailingOneWrote(
        int result,
        string expected,
        bool expectMessage)
    {
        var file = Path.Combine(root, "Program.cs");
        await File.WriteAllTextAsync(file, "before", TestContext.Current.CancellationToken);

        var (returned, output) = await CaptureOutputAsync(() => Task.FromResult(CommandFileTransaction.Run(() =>
        {
            FileSystemTransaction.WriteAllText(file, "after");
            return result;
        })));

        Assert.Equal(result, returned);
        Assert.Equal(expected, await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
        Assert.Equal(expectMessage, output.Contains(RolledBackMessage, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0, "after", false)]
    [InlineData(1, "before", true)]
    public async Task RunAsync_KeepsWhatASucceedingCommandWrote_AndPutsBackWhatAFailingOneWrote(
        int result,
        string expected,
        bool expectMessage)
    {
        var file = Path.Combine(root, "Program.cs");
        await File.WriteAllTextAsync(file, "before", TestContext.Current.CancellationToken);

        var (returned, output) = await CaptureOutputAsync(() => CommandFileTransaction.RunAsync(async () =>
        {
            await Task.Yield();
            FileSystemTransaction.WriteAllText(file, "after");
            return result;
        }));

        Assert.Equal(result, returned);
        Assert.Equal(expected, await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
        Assert.Equal(expectMessage, output.Contains(RolledBackMessage, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Run_FailingCommandThatWroteNothing_SaysNothing()
    {
        var (returned, output) = await CaptureOutputAsync(() => Task.FromResult(CommandFileTransaction.Run(() => 1)));

        Assert.Equal(1, returned);
        Assert.DoesNotContain(RolledBackMessage, output, StringComparison.Ordinal);
    }

    private static async Task<(int Result, string Output)> CaptureOutputAsync(
        Func<Task<int>> action)
    {
        var writer = new StringWriter();
        var original = AnsiConsole.Console;
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer),
        });

        try
        {
            var result = await action();
            return (result, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = original;
        }
    }
}