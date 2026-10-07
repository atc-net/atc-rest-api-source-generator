namespace Atc.Rest.Api.Generator.Tests.JsonConverters;

/// <summary>
/// A marker's <c>versioningStrategy</c>: an unknown or unreadable value is no versioning, the default.
/// </summary>
public class VersioningStrategyTypeConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new VersioningStrategyTypeConverter() } };

    [Theory]
    [InlineData("None", VersioningStrategyType.None)]
    [InlineData("none", VersioningStrategyType.None)]
    [InlineData("QueryString", VersioningStrategyType.QueryString)]
    [InlineData("querystring", VersioningStrategyType.QueryString)]
    [InlineData("query-string", VersioningStrategyType.QueryString)]
    [InlineData("UrlSegment", VersioningStrategyType.UrlSegment)]
    [InlineData("URL-SEGMENT", VersioningStrategyType.UrlSegment)]
    [InlineData("url-segment", VersioningStrategyType.UrlSegment)]
    [InlineData("Header", VersioningStrategyType.Header)]
    [InlineData("header", VersioningStrategyType.Header)]
    public void Read_EverySpelling_IgnoringCase(
        string spelling,
        VersioningStrategyType expected)
        => Assert.Equal(expected, JsonSerializer.Deserialize<VersioningStrategyType>($"\"{spelling}\"", Options));

    [Theory]
    [InlineData("")]
    [InlineData("query")]
    [InlineData("url")]
    [InlineData("path-segment")]
    [InlineData("X-Api-Version")]
    [InlineData("header ")]
    public void Read_UnknownSpelling_IsNone(string spelling)
        => Assert.Equal(VersioningStrategyType.None, JsonSerializer.Deserialize<VersioningStrategyType>($"\"{spelling}\"", Options));

    [Theory]
    [InlineData("2")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("{ \"strategy\": \"Header\" }")]
    [InlineData("[\"Header\"]")]
    public void Read_ValueThatIsNotAString_IsNone_AndTheNextSettingIsRead(
        string json)
    {
        var config = JsonSerializer.Deserialize<ClientConfig>(
            $$"""{ "versioningStrategy": {{json}}, "versionHeaderName": "X-Contoso-Version" }""",
            JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(VersioningStrategyType.None, config.VersioningStrategy);
        Assert.Equal("X-Contoso-Version", config.VersionHeaderName);
    }

    [Fact]
    public void Read_FromTheMarker_TheUrlSegment()
    {
        var config = JsonSerializer.Deserialize<ClientConfig>("""{ "versioningStrategy": "url-segment" }""", JsonHelper.ConfigOptions);

        Assert.Equal(VersioningStrategyType.UrlSegment, config!.VersioningStrategy);
    }

    [Theory]
    [InlineData(VersioningStrategyType.None, "\"None\"")]
    [InlineData(VersioningStrategyType.QueryString, "\"QueryString\"")]
    [InlineData(VersioningStrategyType.UrlSegment, "\"UrlSegment\"")]
    [InlineData(VersioningStrategyType.Header, "\"Header\"")]
    [InlineData((VersioningStrategyType)42, "\"None\"")]
    public void Write_IsTheName_AndAValueOutsideTheEnumIsNone(
        VersioningStrategyType value,
        string expected)
        => Assert.Equal(expected, JsonSerializer.Serialize(value, Options));

    [Fact]
    public void Write_ThenRead_GivesEveryValueBack()
    {
        foreach (var value in Enum.GetValues<VersioningStrategyType>())
        {
            Assert.Equal(value, JsonSerializer.Deserialize<VersioningStrategyType>(JsonSerializer.Serialize(value, Options), Options));
        }
    }

    [Fact]
    public void NamesAndSpellings_AreTheMarkerValues()
    {
        var converter = new VersioningStrategyTypeConverter();

        Assert.Equal(["None", "QueryString", "UrlSegment", "Header"], converter.Names);
        Assert.Equal(["None", "QueryString", "query-string", "UrlSegment", "url-segment", "Header"], converter.Spellings);
        Assert.False(converter.AcceptsBooleans);
        Assert.Equal("None", converter.NameOf(null));
        Assert.Equal("Header", converter.NameOf(VersioningStrategyType.Header));
    }

    [Theory]
    [InlineData("\"Query-String\"", true)]
    [InlineData("\"none\"", true)]
    [InlineData("\"v1\"", false)]
    [InlineData("false", false)]
    [InlineData("[]", false)]
    public void Accepts_OnlyTheSpellings(
        string json,
        bool expected)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Equal(expected, new VersioningStrategyTypeConverter().Accepts(document.RootElement));
    }
}