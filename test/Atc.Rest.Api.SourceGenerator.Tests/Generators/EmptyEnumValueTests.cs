namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A specification may list the empty string as an enum value (<c>enum: ["", low, high]</c>). The member that carries it
/// (<c>Empty</c>) goes on the wire as <c>""</c> and is read from it - on the server and on both clients, through the
/// generated JSON options and through the enum's own <c>[JsonConverter]</c> - never as its member name.
/// </summary>
public sealed class EmptyEnumValueTests
{
    private const string Yaml = """
        openapi: 3.1.0
        info:
          title: Grades
          version: 1.0.0
        paths:
          /grades:
            post:
              operationId: createGrade
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Grade'
              responses:
                '204':
                  description: No Content
        components:
          schemas:
            Mark:
              type: string
              enum: ['', low, high]
            Grade:
              type: object
              properties:
                mark:
                  $ref: '#/components/schemas/Mark'
        """;

    public static TheoryData<string, string> SidesAndWaysIn
        => new()
        {
            { "Server", "Options" },
            { "Server", "Attribute" },
            { "TypedClient", "Options" },
            { "TypedClient", "Attribute" },
            { "EndpointPerOperation", "Options" },
            { "EndpointPerOperation", "Attribute" },
        };

    [Theory]
    [MemberData(nameof(SidesAndWaysIn))]
    public void TheEmptyValue_IsWrittenAndReadAsTheEmptyString(
        string side,
        string wayIn)
    {
        // Arrange
        var assembly = Load(side);
        var options = OptionsFor(assembly, wayIn);
        var mark = TypeOf(assembly, "Mark");
        var empty = Enum.Parse(mark, "Empty");

        // Act
        var written = JsonSerializer.Serialize(empty, mark, options);
        var read = JsonSerializer.Deserialize("\"\"", mark, options);
        var grade = JsonSerializer.Serialize(JsonSerializer.Deserialize("""{"mark":""}""", TypeOf(assembly, "Grade"), options), TypeOf(assembly, "Grade"), options);

        // Assert
        Assert.Equal("\"\"", written);
        Assert.Equal(empty, read);
        Assert.Equal("""{"mark":""}""", grade);
        Assert.Equal("\"low\"", JsonSerializer.Serialize(Enum.Parse(mark, "Low"), mark, options));
    }

    [Theory]
    [MemberData(nameof(SidesAndWaysIn))]
    public void TheMemberName_IsNotReadAsTheEmptyValue(
        string side,
        string wayIn)
    {
        // Arrange
        var assembly = Load(side);
        var options = OptionsFor(assembly, wayIn);

        // Act
        var exception = Record.Exception(() => JsonSerializer.Deserialize("\"Empty\"", TypeOf(assembly, "Mark"), options));

        // Assert
        Assert.IsType<JsonException>(exception, exactMatch: false);
    }

    [Theory]
    [InlineData("Server")]
    [InlineData("TypedClient")]
    [InlineData("EndpointPerOperation")]
    public void ToWireValue_OfTheEmptyMember_IsTheEmptyString(string side)
    {
        // Arrange
        var assembly = Load(side);
        var mark = TypeOf(assembly, "Mark");
        var toWireValue = TypeOf(assembly, "MarkExtensions").GetMethod("ToWireValue", [mark])!;

        // Act
        var wire = (string?)toWireValue.Invoke(null, [Enum.Parse(mark, "Empty")]);

        // Assert
        Assert.Equal(string.Empty, wire);
    }

    [Fact]
    public void ExtensibleClientEnum_TheEmptyValue_IsKnown_AndWrittenAsTheEmptyString()
    {
        // Arrange
        var assembly = CompilationVerificationHarness.EmitAndLoad(
            ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "TypedClient" }""", includeMinimalApi: false, Yaml));
        var mark = TypeOf(assembly, "Mark");
        var options = OptionsFor(assembly, "Options");

        // Act
        var read = JsonSerializer.Deserialize("\"\"", mark, options)!;
        var empty = mark.GetProperty("Empty", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                    ?? mark.GetField("Empty", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

        // Assert
        Assert.True((bool)mark.GetProperty("IsKnown")!.GetValue(read)!);
        Assert.Equal(string.Empty, (string?)mark.GetProperty("Value")!.GetValue(read));
        Assert.Equal(empty, read);
        Assert.Equal("\"\"", JsonSerializer.Serialize(empty, mark, options));
    }

    [Fact]
    public async Task TheEmptyValue_KeepsTheGeneratedCodeTrimAndAotSafe()
    {
        // Arrange
        var server = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", "{ }", includeMinimalApi: false, Yaml);
        var client = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "EndpointPerOperation", "extensibleEnums": false }""", includeMinimalApi: false, Yaml);

        // Act
        var problems = (await AotAnalyzerHarness.AnalyzeAsync(server, includeMinimalApi: false))
            .Concat(await AotAnalyzerHarness.AnalyzeAsync(client, includeMinimalApi: false))
            .ToList();

        // Assert
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static Assembly Load(string side)
        => side == "Server"
            ? CompilationVerificationHarness.EmitAndLoad(
                ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", "{ }", includeMinimalApi: false, Yaml))
            : CompilationVerificationHarness.EmitAndLoad(
                ErrorResponseBodyRuntimeTests.Generate(
                    new ApiClientGenerator(),
                    ".atc-rest-api-client",
                    $$"""{ "generationMode": "{{side}}", "extensibleEnums": false }""",
                    includeMinimalApi: false,
                    Yaml));

    // "Options": as the generated Add…Api() / client options are - the factory first, then a host-wide string converter.
    // "Attribute": options of the host's own, where the enum's [JsonConverter] decides.
    private static JsonSerializerOptions OptionsFor(
        Assembly assembly,
        string wayIn)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
        };

        if (wayIn == "Options")
        {
            options.Converters.Add((JsonConverter)Activator.CreateInstance(TypeOf(assembly, "EnumWireConverterFactory"))!);
            options.Converters.Add(new JsonStringEnumConverter());
        }

        return options;
    }

    private static Type TypeOf(
        Assembly assembly,
        string name)
        => assembly.GetTypes().Single(t => t.Name == name);
}