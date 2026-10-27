namespace Atc.Rest.Api.Generator.Cli.Tests.Helpers;

/// <summary>
/// The width the tool writes in when its output is redirected (a pipe, a CI log, a test): <c>COLUMNS</c> when it holds
/// a width, otherwise wide enough that a path or a message is not broken over lines - not the 80 columns a console
/// without a window would give.
/// </summary>
public class ConsoleWidthHelperTests
{
    [Theory]
    [InlineData("120", 120)]
    [InlineData(" 300 ", 300)]
    [InlineData(null, ConsoleWidthHelper.RedirectedWidth)]
    [InlineData("", ConsoleWidthHelper.RedirectedWidth)]
    [InlineData("wide", ConsoleWidthHelper.RedirectedWidth)]
    [InlineData("0", ConsoleWidthHelper.RedirectedWidth)]
    [InlineData("-5", ConsoleWidthHelper.RedirectedWidth)]
    public void ForRedirectedOutput_TakesColumnsWhenItHoldsAWidth(
        string? columns,
        int expected)
        => Assert.Equal(expected, ConsoleWidthHelper.ForRedirectedOutput(columns));

    [Fact]
    public void RedirectedWidth_IsWiderThanAConsoleWithoutAWindow()
        => Assert.True(ConsoleWidthHelper.RedirectedWidth >= 200);

    [Fact]
    public async Task ARedirectedRun_DoesNotBreakALongPathOverLines()
    {
        // A missing file whose path is longer than 80 columns, run without COLUMNS
        var path = Path.Combine(Path.GetTempPath(), new string('a', 60), new string('b', 60), "missing.yaml");

        var (exitCode, text) = await CliTestHelper.RunCliAsync(
            $"spec validate -s \"{path}\"",
            TestContext.Current.CancellationToken,
            columns: null);

        Assert.Equal(1, exitCode);
        Assert.Contains(path, text, StringComparison.Ordinal);
    }
}