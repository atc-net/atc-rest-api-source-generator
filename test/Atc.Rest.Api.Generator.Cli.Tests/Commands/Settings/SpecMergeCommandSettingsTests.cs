namespace Atc.Rest.Api.Generator.Cli.Tests.Commands.Settings;

public class SpecMergeCommandSettingsTests
{
    [Theory]
    [InlineData("ErrorOnDuplicate", MergeStrategy.ErrorOnDuplicate)]
    [InlineData("mergeifidentical", MergeStrategy.MergeIfIdentical)]
    [InlineData("AppendUnique", MergeStrategy.AppendUnique)]
    [InlineData("FIRSTWINS", MergeStrategy.FirstWins)]
    [InlineData("LastWins", MergeStrategy.LastWins)]
    public void GetMergeStrategyEnum_KnownName_ParsesCaseInsensitively(
        string strategy,
        MergeStrategy expected)
    {
        var settings = new SpecMergeCommandSettings { MergeStrategy = strategy };

        Assert.Equal(expected, settings.GetMergeStrategyEnum());
    }

    [Fact]
    public void GetMergeStrategyEnum_UnknownName_FallsBackToErrorOnDuplicate()
    {
        var settings = new SpecMergeCommandSettings { MergeStrategy = "Whatever" };

        Assert.Equal(MergeStrategy.ErrorOnDuplicate, settings.GetMergeStrategyEnum());
    }
}