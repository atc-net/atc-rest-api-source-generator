namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class WireValueHelperTests
{
    private const string Invariant = "global::System.Globalization.CultureInfo.InvariantCulture";

    private const string Yaml = """
        openapi: 3.0.0
        info: { title: T, version: 1.0.0 }
        paths: {}
        components:
          schemas:
            Color:
              type: string
              enum: [red, dark-blue]
            Colors:
              type: array
              items: { $ref: '#/components/schemas/Color' }
            Level:
              type: integer
              enum: [1, 2, 3]
            Rank:
              enum: [1, 2, 3]
            Name: { type: string }
            Day: { type: string, format: date }
            When: { type: string, format: date-time }
            Ratio: { type: number, format: double }
            Count: { type: integer, format: int32 }
            Filter:
              type: object
              properties:
                color: { $ref: '#/components/schemas/Color' }
                colors: { $ref: '#/components/schemas/Colors' }
                inlineColor:
                  type: string
                  enum: [small, large]
                level: { $ref: '#/components/schemas/Level' }
                name: { $ref: '#/components/schemas/Name' }
                day: { $ref: '#/components/schemas/Day' }
        """;

    [Theory]
    [InlineData("color", true)]
    [InlineData("colors", true)]
    [InlineData("inlineColor", true)]
    [InlineData("level", false)]
    [InlineData("name", false)]
    [InlineData("day", false)]
    public void IsStringEnum_ResolvesReferencesAndArrayItems(
        string property,
        bool expected)
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var schema = document.Components!.Schemas!["Filter"].Properties![property];

        Assert.Equal(expected, WireValueHelper.IsStringEnum(schema));
    }

    [Fact]
    public void IsStringEnum_NullSchema_ReturnsFalse()
        => Assert.False(WireValueHelper.IsStringEnum(schema: null));

    [Theory]
    [InlineData("Day", "\"yyyy-MM-dd\"")]
    [InlineData("When", "\"yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK\"")]
    [InlineData("Ratio", "null")]
    public void TryBuildWireExpression_Schema_DatesAndNumbersAreFormattedInvariant(
        string schemaName,
        string format)
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var schema = document.Components!.Schemas![schemaName];

        var result = WireValueHelper.TryBuildWireExpression("parameters.Value", schema);

        Assert.Equal($"((global::System.IFormattable?)parameters.Value)?.ToString({format}, {Invariant})", result);
    }

    [Fact]
    public void TryBuildWireExpression_Schema_ReferenceToDate_IsResolved()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var schema = document.Components!.Schemas!["Filter"].Properties!["day"];

        var result = WireValueHelper.TryBuildWireExpression("parameters.Day", schema);

        Assert.Equal($"((global::System.IFormattable?)parameters.Day)?.ToString(\"yyyy-MM-dd\", {Invariant})", result);
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("Color")]
    public void TryBuildWireExpression_Schema_StringsAndStringEnums_ReturnNull(
        string schemaName)
    {
        // A string is sent as is and a string enum is written with ToWireValue()
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var schema = document.Components!.Schemas![schemaName];

        Assert.Null(WireValueHelper.TryBuildWireExpression("parameters.Value", schema));
    }

    [Theory]
    [InlineData("Level")]
    [InlineData("Rank")]
    public void TryBuildWireExpression_Schema_IntegerEnum_IsItsNumber(string schemaName)
    {
        // The member's name is a C# name; the specification's value is the number - also for an enum without a type
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var schema = document.Components!.Schemas![schemaName];

        Assert.True(WireValueHelper.IsIntegerEnum(schema));
        Assert.Equal($"((global::System.IFormattable?)parameters.Value)?.ToString(\"D\", {Invariant})", WireValueHelper.TryBuildWireExpression("parameters.Value", schema));
    }

    [Fact]
    public void TryBuildWireExpression_Schema_Null_ReturnsNull()
        => Assert.Null(WireValueHelper.TryBuildWireExpression("parameters.Value", schema: null));

    [Theory]
    [InlineData("DateOnly", "\"yyyy-MM-dd\"")]
    [InlineData("DateOnly?", "\"yyyy-MM-dd\"")]
    [InlineData("DateTimeOffset", "\"yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK\"")]
    [InlineData("DateTime?", "\"yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK\"")]
    [InlineData("TimeOnly", "\"HH:mm:ss.FFFFFFF\"")]
    [InlineData("float", "null")]
    [InlineData("double?", "null")]
    [InlineData("decimal", "null")]
    [InlineData("int", "null")]
    [InlineData("long?", "null")]
    public void TryBuildWireExpression_CSharpType_FormatsWithTheInvariantCulture(
        string csharpType,
        string format)
    {
        var result = WireValueHelper.TryBuildWireExpression("parameters.From", csharpType);

        Assert.Equal($"((global::System.IFormattable?)parameters.From)?.ToString({format}, {Invariant})", result);
    }

    [Theory]
    [InlineData("string")]
    [InlineData("bool")]
    [InlineData("Guid")]
    [InlineData("Color")]
    public void TryBuildWireExpression_CSharpType_TypesWithoutCulture_ReturnNull(
        string csharpType)
        => Assert.Null(WireValueHelper.TryBuildWireExpression("parameters.From", csharpType));
}