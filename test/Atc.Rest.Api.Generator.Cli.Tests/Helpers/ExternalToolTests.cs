namespace Atc.Rest.Api.Generator.Cli.Tests.Helpers;

/// <summary>
/// A tool the CLI runs (git, atc-coding-rules-updater) cannot hold a command up: one that does not end is stopped
/// after its time, one that asks a question gets no answer instead of waiting for one, and one that writes much to
/// standard error is not blocked on a pipe nobody reads.
/// </summary>
public sealed class ExternalToolTests
{
    private static readonly string Shell = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";

    [Fact]
    public void Run_AToolThatEnds_GivesItsExitCodeAndBothStreams()
    {
        var result = ExternalTool.Run(
            Shell,
            OperatingSystem.IsWindows() ? "/c \"echo out& echo err 1>&2& exit /b 3\"" : "-c \"echo out; echo err 1>&2; exit 3\"",
            Path.GetTempPath(),
            TimeSpan.FromSeconds(30));

        Assert.False(result.TimedOut);
        Assert.Equal(3, result.ExitCode);
        Assert.Equal("out", result.Output.Trim());
        Assert.Equal("err", result.Error.Trim());
    }

    [Fact]
    public void Run_AToolThatDoesNotEnd_IsStoppedAfterItsTime()
    {
        var watch = Stopwatch.StartNew();

        var result = ExternalTool.Run(
            Shell,
            OperatingSystem.IsWindows() ? "/c \"echo started& ping -n 60 127.0.0.1 >nul\"" : "-c \"echo started; sleep 60\"",
            Path.GetTempPath(),
            TimeSpan.FromSeconds(2));

        Assert.True(result.TimedOut);
        Assert.Null(result.ExitCode);
        Assert.Equal("started", result.Output.Trim());
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30), $"took {watch.Elapsed}");
    }

    [Fact]
    public void Run_AToolThatAsksAQuestion_GetsNoAnswerAndEnds()
    {
        // Reading a line from a closed standard input ends at once; with an open one the tool would wait for a key
        var result = ExternalTool.Run(
            Shell,
            OperatingSystem.IsWindows() ? "/c \"set /p answer=Continue? & echo done\"" : "-c \"read answer; echo done\"",
            Path.GetTempPath(),
            TimeSpan.FromSeconds(30));

        Assert.False(result.TimedOut);
        Assert.Contains("done", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_AToolThatWritesMuchToStandardError_IsNotBlocked()
    {
        // Far more than a pipe holds (64 KB): a reader that waits for standard output first would never get there
        var result = ExternalTool.Run(
            Shell,
            OperatingSystem.IsWindows()
                ? "/c \"for /L %i in (1,1,6000) do @echo an error line that is long enough to fill a pipe quickly %i 1>&2\""
                : "-c \"i=0; while [ $i -lt 6000 ]; do echo 'an error line that is long enough to fill a pipe quickly' $i 1>&2; i=$((i+1)); done\"",
            Path.GetTempPath(),
            TimeSpan.FromSeconds(60));

        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(6000, result.Error.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }
}