namespace Atc.Rest.Api.Generator.Cli.Tests.Commands.Settings;

public class SpecSplitCommandSettingsTests
{
    [Theory]
    [InlineData("ByTag", SplitStrategy.ByTag)]
    [InlineData("bypathsegment", SplitStrategy.ByPathSegment)]
    [InlineData("BYDOMAIN", SplitStrategy.ByDomain)]
    public void GetSplitStrategyEnum_KnownName_ParsesCaseInsensitively(
        string strategy,
        SplitStrategy expected)
    {
        var settings = new SpecSplitCommandSettings { Strategy = strategy };

        Assert.Equal(expected, settings.GetSplitStrategyEnum());
    }

    [Fact]
    public void GetSplitStrategyEnum_UnknownName_FallsBackToByTag()
    {
        var settings = new SpecSplitCommandSettings { Strategy = "Whatever" };

        Assert.Equal(SplitStrategy.ByTag, settings.GetSplitStrategyEnum());
    }
}