namespace Atc.Rest.Api.Generator.Tests.JsonConverters;

public class TypedClientResultStyleTypeConverterTests
{
    [Theory]
    [InlineData("Result", TypedClientResultStyleType.Result)]
    [InlineData("result", TypedClientResultStyleType.Result)]
    [InlineData("RESULT", TypedClientResultStyleType.Result)]
    [InlineData("Throw", TypedClientResultStyleType.Throw)]
    [InlineData("throw", TypedClientResultStyleType.Throw)]
    public void Read_KnownValue_ReturnsExpectedStyle(
        string value,
        TypedClientResultStyleType expected)
    {
        var json = $$"""{"typedClientResultStyle":"{{value}}"}""";

        var config = Deserialize(json);

        Assert.Equal(expected, config!.TypedClientResultStyle);
    }

    [Theory]
    [InlineData("envelope")]
    [InlineData("")]
    [InlineData("SomethingElse")]
    public void Read_UnknownValue_FallsBackToThrow(string value)
    {
        var json = $$"""{"typedClientResultStyle":"{{value}}"}""";

        var config = Deserialize(json);

        // A malformed marker file must never silently reshape every operation signature.
        Assert.Equal(TypedClientResultStyleType.Throw, config!.TypedClientResultStyle);
    }

    [Fact]
    public void Read_MissingValue_DefaultsToThrow()
    {
        var config = Deserialize("""{"clientSuffix":"Client"}""");

        Assert.Equal(TypedClientResultStyleType.Throw, config!.TypedClientResultStyle);
    }

    [Theory]
    [InlineData(TypedClientResultStyleType.Result, "Result")]
    [InlineData(TypedClientResultStyleType.Throw, "Throw")]
    public void Write_RoundTrips(
        TypedClientResultStyleType style,
        string expected)
    {
        var json = JsonSerializer.Serialize(new ClientConfig { TypedClientResultStyle = style });

        Assert.Contains($"\"{expected}\"", json, StringComparison.Ordinal);
    }

    private static ClientConfig? Deserialize(string json)
        => JsonSerializer.Deserialize<ClientConfig>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
}