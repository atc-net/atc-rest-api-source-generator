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
            TimeSpan.FromSeconds(3));

        // Stopped, and long before the minute the tool would have taken (what it had written by then is not asserted:
        // on a busy machine the tool may not have got as far as its first line)
        Assert.True(result.TimedOut);
        Assert.Null(result.ExitCode);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(40), $"took {watch.Elapsed}");
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
        // Far more than a pipe holds (64 KB): a reader that waits for standard output first would never get there. The
        // lines are written by one command, not by a loop of 6000 - on a busy Windows runner such a loop in cmd alone
        // took longer than the time the test allows
        var file = Path.Combine(Path.GetTempPath(), $"atc-external-tool-{Guid.NewGuid():N}.txt");
        File.WriteAllLines(file, Enumerable.Range(1, 6000).Select(i => $"an error line that is long enough to fill a pipe quickly {i}"));
        try
        {
            var result = ExternalTool.Run(
                Shell,
                OperatingSystem.IsWindows() ? $"/c \"type \"{file}\" 1>&2\"" : $"-c \"cat '{file}' 1>&2\"",
                Path.GetTempPath(),
                TimeSpan.FromSeconds(60));

            var lines = result.Error.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.False(result.TimedOut, $"timed out after {lines} line(s)");
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(6000, lines);
        }
        finally
        {
            File.Delete(file);
        }
    }
}