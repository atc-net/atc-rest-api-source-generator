namespace Atc.Rest.Api.Generator.Tests.JsonConverters;

/// <summary>
/// The client marker's <c>clientGranularity</c>: an unknown or unreadable value is one client per area, so a
/// malformed marker never switches a project into single-client mode.
/// </summary>
public class ClientGranularityTypeConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new ClientGranularityTypeConverter() } };

    [Theory]
    [InlineData("Single", ClientGranularityType.Single)]
    [InlineData("single", ClientGranularityType.Single)]
    [InlineData("SINGLE", ClientGranularityType.Single)]
    [InlineData("PerArea", ClientGranularityType.PerArea)]
    [InlineData("perarea", ClientGranularityType.PerArea)]
    [InlineData("per-area", ClientGranularityType.PerArea)]
    [InlineData("PER-AREA", ClientGranularityType.PerArea)]
    public void Read_EverySpelling_IgnoringCase(
        string spelling,
        ClientGranularityType expected)
        => Assert.Equal(expected, JsonSerializer.Deserialize<ClientGranularityType>($"\"{spelling}\"", Options));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Single ")]
    [InlineData("per_area")]
    [InlineData("single-client")]
    [InlineData("1")]
    public void Read_UnknownSpelling_IsPerArea(string spelling)
        => Assert.Equal(ClientGranularityType.PerArea, JsonSerializer.Deserialize<ClientGranularityType>($"\"{spelling}\"", Options));

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("{ \"value\": \"Single\" }")]
    [InlineData("[\"Single\"]")]
    public void Read_ValueThatIsNotAString_IsPerArea_AndTheNextSettingIsRead(
        string json)
    {
        var config = JsonSerializer.Deserialize<ClientConfig>(
            $$"""{ "clientGranularity": {{json}}, "clientSuffix": "Gateway" }""",
            JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(ClientGranularityType.PerArea, config.ClientGranularity);
        Assert.Equal("Gateway", config.ClientSuffix);
    }

    [Fact]
    public void Read_FromTheMarker_TheSingleClient()
    {
        var config = JsonSerializer.Deserialize<ClientConfig>("""{ "clientGranularity": "single" }""", JsonHelper.ConfigOptions);

        Assert.Equal(ClientGranularityType.Single, config!.ClientGranularity);
    }

    [Theory]
    [InlineData(ClientGranularityType.Single, "\"Single\"")]
    [InlineData(ClientGranularityType.PerArea, "\"PerArea\"")]
    [InlineData((ClientGranularityType)99, "\"PerArea\"")]
    public void Write_IsTheName_AndAValueOutsideTheEnumIsPerArea(
        ClientGranularityType value,
        string expected)
        => Assert.Equal(expected, JsonSerializer.Serialize(value, Options));

    [Fact]
    public void Write_ThenRead_GivesEveryValueBack()
    {
        foreach (var value in Enum.GetValues<ClientGranularityType>())
        {
            Assert.Equal(value, JsonSerializer.Deserialize<ClientGranularityType>(JsonSerializer.Serialize(value, Options), Options));
        }
    }

    [Fact]
    public void NamesAndSpellings_AreTheMarkerValues()
    {
        var converter = new ClientGranularityTypeConverter();

        Assert.Equal(["Single", "PerArea"], converter.Names);
        Assert.Equal(["Single", "PerArea", "per-area"], converter.Spellings);
        Assert.False(converter.AcceptsBooleans);
    }

    [Fact]
    public void NameOf_AValueThatIsNotAGranularity_IsPerArea()
    {
        var converter = new ClientGranularityTypeConverter();

        Assert.Equal("Single", converter.NameOf(ClientGranularityType.Single));
        Assert.Equal("PerArea", converter.NameOf(null));
        Assert.Equal("PerArea", converter.NameOf("Single"));
        Assert.Equal("PerArea", converter.NameOf(GenerationModeType.EndpointPerOperation));
    }

    [Theory]
    [InlineData("\"per-area\"", true)]
    [InlineData("\"SINGLE\"", true)]
    [InlineData("\"area\"", false)]
    [InlineData("true", false)]
    [InlineData("1", false)]
    [InlineData("null", false)]
    public void Accepts_OnlyTheSpellings(
        string json,
        bool expected)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Equal(expected, new ClientGranularityTypeConverter().Accepts(document.RootElement));
    }
}