namespace Atc.Rest.Api.Generator.Tests.JsonConverters;

/// <summary>
/// The marker setting converters share one table of values: what they read, what they write, and what the check
/// for unknown values and the JSON schema accept.
/// </summary>
public sealed class MarkerValueConverterTests
{
    [Fact]
    public void Names_AreTheWrittenSpellings_OnePerValue()
    {
        var values = new SubFolderStrategyTypeConverter();

        Assert.Equal(["None", "FirstPathSegment", "OpenApiTag"], values.Names);
        Assert.Equal("None", values.FallbackName);
    }

    [Fact]
    public void Spellings_HoldEveryAcceptedSpelling_AndThePascalCaseName()
    {
        var values = new ValidateSpecificationStrategyTypeConverter();

        Assert.Equal(["none", "None", "standard", "Standard", "strict", "Strict"], values.Spellings);
    }

    [Theory]
    [InlineData("\"first-path-segment\"", true)]
    [InlineData("\"FIRSTPATHSEGMENT\"", true)]
    [InlineData("\"BySegment\"", false)]
    [InlineData("1", false)]
    [InlineData("true", false)]
    public void Accepts_KnowsTheSpellings(
        string json,
        bool expected)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Equal(expected, new SubFolderStrategyTypeConverter().Accepts(document.RootElement));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", true)]
    [InlineData("\"true\"", true)]
    [InlineData("\"yes\"", false)]
    public void Accepts_Booleans_WhereTheSettingDoes(
        string json,
        bool expected)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Equal(expected, new MinimalApiPackageModeConverter().Accepts(document.RootElement));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("{ \"mode\": \"EndpointPerOperation\" }")]
    [InlineData("[\"EndpointPerOperation\"]")]
    public void Read_AValueThatIsNotAString_IsTheFallback_AndKeepsTheOtherSettings(
        string json)
    {
        var config = JsonSerializer.Deserialize<ClientConfig>(
            $$"""{ "generationMode": {{json}}, "clientSuffix": "Gateway" }""",
            JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(GenerationModeType.TypedClient, config.GenerationMode);
        Assert.Equal("Gateway", config.ClientSuffix);
    }

    [Theory]
    [InlineData(MergeStrategy.ErrorOnDuplicate, "ErrorOnDuplicate")]
    [InlineData(MergeStrategy.LastWins, "LastWins")]
    public void Write_UsesTheName(
        MergeStrategy value,
        string expected)
    {
        var options = new JsonSerializerOptions { Converters = { new MergeStrategyConverter() } };

        Assert.Equal($"\"{expected}\"", JsonSerializer.Serialize(value, options));
    }
}