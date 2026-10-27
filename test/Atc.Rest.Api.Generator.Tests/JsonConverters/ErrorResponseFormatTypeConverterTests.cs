namespace Atc.Rest.Api.Generator.Tests.JsonConverters;

/// <summary>
/// The client marker's <c>errorResponseFormat</c>: an unknown or unreadable value is ProblemDetails, the default.
/// </summary>
public class ErrorResponseFormatTypeConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new ErrorResponseFormatTypeConverter() } };

    [Theory]
    [InlineData("ProblemDetails", ErrorResponseFormatType.ProblemDetails)]
    [InlineData("problemdetails", ErrorResponseFormatType.ProblemDetails)]
    [InlineData("problem-details", ErrorResponseFormatType.ProblemDetails)]
    [InlineData("PlainText", ErrorResponseFormatType.PlainText)]
    [InlineData("plain-text", ErrorResponseFormatType.PlainText)]
    [InlineData("PLAINTEXT", ErrorResponseFormatType.PlainText)]
    [InlineData("PlainTextOnly", ErrorResponseFormatType.PlainTextOnly)]
    [InlineData("plain-text-only", ErrorResponseFormatType.PlainTextOnly)]
    [InlineData("plaintextonly", ErrorResponseFormatType.PlainTextOnly)]
    [InlineData("Custom", ErrorResponseFormatType.Custom)]
    [InlineData("custom", ErrorResponseFormatType.Custom)]
    public void Read_EverySpelling_IgnoringCase(
        string spelling,
        ErrorResponseFormatType expected)
        => Assert.Equal(expected, JsonSerializer.Deserialize<ErrorResponseFormatType>($"\"{spelling}\"", Options));

    [Theory]
    [InlineData("")]
    [InlineData("problem")]
    [InlineData("plain-text-")]
    [InlineData("plain_text")]
    [InlineData("application/problem+json")]
    [InlineData("custom-model")]
    public void Read_UnknownSpelling_IsProblemDetails(string spelling)
        => Assert.Equal(ErrorResponseFormatType.ProblemDetails, JsonSerializer.Deserialize<ErrorResponseFormatType>($"\"{spelling}\"", Options));

    [Theory]
    [InlineData("3")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("{ \"format\": \"PlainText\" }")]
    [InlineData("[\"PlainText\", \"Custom\"]")]
    public void Read_ValueThatIsNotAString_IsProblemDetails_AndTheNextSettingIsRead(
        string json)
    {
        var config = JsonSerializer.Deserialize<ClientConfig>(
            $$"""{ "errorResponseFormat": {{json}}, "clientSuffix": "Gateway" }""",
            JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(ErrorResponseFormatType.ProblemDetails, config.ErrorResponseFormat);
        Assert.Equal("Gateway", config.ClientSuffix);
    }

    [Theory]
    [InlineData(ErrorResponseFormatType.ProblemDetails, "\"ProblemDetails\"")]
    [InlineData(ErrorResponseFormatType.PlainText, "\"PlainText\"")]
    [InlineData(ErrorResponseFormatType.PlainTextOnly, "\"PlainTextOnly\"")]
    [InlineData(ErrorResponseFormatType.Custom, "\"Custom\"")]
    [InlineData((ErrorResponseFormatType)4, "\"ProblemDetails\"")]
    public void Write_IsTheName_AndAValueOutsideTheEnumIsProblemDetails(
        ErrorResponseFormatType value,
        string expected)
        => Assert.Equal(expected, JsonSerializer.Serialize(value, Options));

    [Fact]
    public void Write_ThenRead_GivesEveryValueBack()
    {
        foreach (var value in Enum.GetValues<ErrorResponseFormatType>())
        {
            Assert.Equal(value, JsonSerializer.Deserialize<ErrorResponseFormatType>(JsonSerializer.Serialize(value, Options), Options));
        }
    }

    [Fact]
    public void NamesAndSpellings_AreTheMarkerValues()
    {
        var converter = new ErrorResponseFormatTypeConverter();

        Assert.Equal(["ProblemDetails", "PlainText", "PlainTextOnly", "Custom"], converter.Names);
        Assert.Equal(
            ["ProblemDetails", "problem-details", "PlainText", "plain-text", "PlainTextOnly", "plain-text-only", "Custom"],
            converter.Spellings);
        Assert.False(converter.AcceptsBooleans);
        Assert.Equal("ProblemDetails", converter.NameOf(null));
        Assert.Equal("PlainTextOnly", converter.NameOf(ErrorResponseFormatType.PlainTextOnly));
    }

    [Theory]
    [InlineData("\"plain-text-only\"", true)]
    [InlineData("\"CUSTOM\"", true)]
    [InlineData("\"text\"", false)]
    [InlineData("true", false)]
    [InlineData("0", false)]
    public void Accepts_OnlyTheSpellings(
        string json,
        bool expected)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Equal(expected, new ErrorResponseFormatTypeConverter().Accepts(document.RootElement));
    }
}