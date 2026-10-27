namespace Atc.Rest.Api.Generator.Tests.JsonConverters;

/// <summary>
/// The client marker's <c>generationMode</c>: an unknown or unreadable value is the typed client, the default.
/// </summary>
public class GenerationModeTypeConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new GenerationModeTypeConverter() } };

    [Theory]
    [InlineData("TypedClient", GenerationModeType.TypedClient)]
    [InlineData("typedclient", GenerationModeType.TypedClient)]
    [InlineData("typed-client", GenerationModeType.TypedClient)]
    [InlineData("Typed-Client", GenerationModeType.TypedClient)]
    [InlineData("EndpointPerOperation", GenerationModeType.EndpointPerOperation)]
    [InlineData("ENDPOINTPEROPERATION", GenerationModeType.EndpointPerOperation)]
    [InlineData("endpoint-per-operation", GenerationModeType.EndpointPerOperation)]
    public void Read_EverySpelling_IgnoringCase(
        string spelling,
        GenerationModeType expected)
        => Assert.Equal(expected, JsonSerializer.Deserialize<GenerationModeType>($"\"{spelling}\"", Options));

    [Theory]
    [InlineData("")]
    [InlineData("endpoint")]
    [InlineData("endpoint_per_operation")]
    [InlineData("endpoint-per-operation ")]
    [InlineData("TypedClients")]
    [InlineData("1")]
    public void Read_UnknownSpelling_IsTheTypedClient(string spelling)
        => Assert.Equal(GenerationModeType.TypedClient, JsonSerializer.Deserialize<GenerationModeType>($"\"{spelling}\"", Options));

    [Theory]
    [InlineData("1")]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("{ \"mode\": { \"nested\": [1, 2] } }")]
    [InlineData("[[\"EndpointPerOperation\"]]")]
    public void Read_ValueThatIsNotAString_IsTheTypedClient_AndTheNextSettingIsRead(
        string json)
    {
        var config = JsonSerializer.Deserialize<ClientConfig>(
            $$"""{ "generationMode": {{json}}, "clientGranularity": "Single" }""",
            JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(GenerationModeType.TypedClient, config.GenerationMode);
        Assert.Equal(ClientGranularityType.Single, config.ClientGranularity);
    }

    [Theory]
    [InlineData(GenerationModeType.TypedClient, "\"TypedClient\"")]
    [InlineData(GenerationModeType.EndpointPerOperation, "\"EndpointPerOperation\"")]
    [InlineData((GenerationModeType)(-1), "\"TypedClient\"")]
    public void Write_IsTheName_AndAValueOutsideTheEnumIsTheTypedClient(
        GenerationModeType value,
        string expected)
        => Assert.Equal(expected, JsonSerializer.Serialize(value, Options));

    [Fact]
    public void Write_ThenRead_GivesEveryValueBack()
    {
        foreach (var value in Enum.GetValues<GenerationModeType>())
        {
            Assert.Equal(value, JsonSerializer.Deserialize<GenerationModeType>(JsonSerializer.Serialize(value, Options), Options));
        }
    }

    [Fact]
    public void NamesAndSpellings_AreTheMarkerValues()
    {
        var converter = new GenerationModeTypeConverter();

        Assert.Equal(["TypedClient", "EndpointPerOperation"], converter.Names);
        Assert.Equal(["TypedClient", "typed-client", "EndpointPerOperation", "endpoint-per-operation"], converter.Spellings);
        Assert.False(converter.AcceptsBooleans);
        Assert.Equal("TypedClient", converter.NameOf(null));
        Assert.Equal("EndpointPerOperation", converter.NameOf(GenerationModeType.EndpointPerOperation));
    }

    [Theory]
    [InlineData("\"Endpoint-Per-Operation\"", true)]
    [InlineData("\"typedclient\"", true)]
    [InlineData("\"\"", false)]
    [InlineData("false", false)]
    [InlineData("{}", false)]
    public void Accepts_OnlyTheSpellings(
        string json,
        bool expected)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Equal(expected, new GenerationModeTypeConverter().Accepts(document.RootElement));
    }
}