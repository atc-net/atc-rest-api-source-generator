namespace Atc.OpenApi.Tests.Helpers;

/// <summary>
/// A keyword the OpenAPI reader leaves as raw JSON may hold any kind of value: only a string is read, anything else
/// is absent - never an exception.
/// </summary>
public class JsonKeywordReaderTests
{
    [Fact]
    public void StringOrNull_AbsentKeyword_IsNull()
        => Assert.Null(JsonKeywordReader.StringOrNull(null));

    [Theory]
    [InlineData("\"Contoso\"", "Contoso")]
    [InlineData("\"\"", "")]
    [InlineData("\"  \"", "  ")]
    [InlineData("\"\\u0041\\n\"", "A\n")]
    [InlineData("\"null\"", "null")]
    [InlineData("\"42\"", "42")]
    public void StringOrNull_ParsedString_IsTheString(
        string json,
        string expected)
        => Assert.Equal(expected, JsonKeywordReader.StringOrNull(JsonNode.Parse(json)));

    [Theory]
    [InlineData("42")]
    [InlineData("-1.5e3")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("{ \"$ref\": \"#/components/schemas/Pet\" }")]
    [InlineData("[\"Pet\"]")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void StringOrNull_ParsedValueOfAnotherKind_IsNull(string json)
        => Assert.Null(JsonKeywordReader.StringOrNull(JsonNode.Parse(json)));

    [Fact]
    public void StringOrNull_JsonNull_IsNull()
    {
        // JsonNode.Parse gives null for a JSON null, and a JsonValue can hold no null of its own
        Assert.Null(JsonKeywordReader.StringOrNull(JsonNode.Parse("null")));
    }

    [Fact]
    public void StringOrNull_StringCreatedInCode_IsTheString()
        => Assert.Equal("Contoso", JsonKeywordReader.StringOrNull(JsonValue.Create("Contoso")));

    [Fact]
    public void StringOrNull_StringKindValueOfAnotherType_IsItsText()
    {
        // A value set in code (an overlay, a test) may hold a Guid: its kind is a string, and it is read as one
        var id = new Guid("6f1d2a43-7c9e-4c55-9a3b-2f0e5d8c1b77");

        Assert.Equal("6f1d2a43-7c9e-4c55-9a3b-2f0e5d8c1b77", JsonKeywordReader.StringOrNull(JsonValue.Create(id)));
    }

    [Fact]
    public void StringOrNull_CharCreatedInCode_IsItsText()
        => Assert.Equal("\"", JsonKeywordReader.StringOrNull(JsonValue.Create('"')));

    [Fact]
    public void StringOrNull_NumberCreatedInCode_IsNull()
        => Assert.Null(JsonKeywordReader.StringOrNull(JsonValue.Create(42)));

    [Fact]
    public void StringOrNull_EntryOfAnArray_IsReadOnItsOwn()
    {
        // The way prefixItems is read: one entry at a time, each of any kind
        var array = JsonNode.Parse("""["Pet", 1, null, { "type": "string" }]""")!.AsArray();

        Assert.Equal(
            ["Pet", null, null, null],
            array.Select(JsonKeywordReader.StringOrNull).ToArray());
    }

    [Fact]
    public void StringOrNull_PropertyOfAnObject_IsReadOnItsOwn()
    {
        var root = JsonNode.Parse("""{ "title": "Pet", "minItems": 1 }""")!.AsObject();

        Assert.Equal("Pet", JsonKeywordReader.StringOrNull(root["title"]));
        Assert.Null(JsonKeywordReader.StringOrNull(root["minItems"]));
        Assert.Null(JsonKeywordReader.StringOrNull(root["missing"]));
    }
}