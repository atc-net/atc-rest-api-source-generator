namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// <c>"readEnumMemberNames": true</c> in a server or client marker: a string enum is still written as the
/// specification's value (<c>rtsp</c>), and is read by that value or by its C# member name (<c>Rtsp</c>, also in another
/// casing when only one member matches). A client or server that writes the member name - as 2.x did for a value that
/// is no member name - keeps working against one that writes the value. Without the key only the value is read.
/// Both ways in: the converter the generated JSON options put first, and the enum's own <c>[JsonConverter]</c> (a host
/// that builds its own options). Never from a number, a number as text or a list of names.
/// </summary>
public sealed class EnumMemberNameReadingTests
{
    private const string Yaml = """
        openapi: 3.1.0
        info:
          title: Cameras
          version: 1.0.0
        paths:
          /cameras:
            post:
              operationId: createCamera
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Camera'
              responses:
                '204':
                  description: No Content
        components:
          schemas:
            Protocol:
              type: string
              enum: [rtsp, http, https]
            OverlayPosition:
              type: string
              enum: [topLeft, bottomRight]
            JobState:
              type: string
              enum: [in-progress, done]
            Shouting:
              type: string
              enum: [FOO, Foo]
            Camera:
              type: object
              properties:
                protocol:
                  $ref: '#/components/schemas/Protocol'
                overlayPosition:
                  $ref: '#/components/schemas/OverlayPosition'
                jobState:
                  $ref: '#/components/schemas/JobState'
                shouting:
                  $ref: '#/components/schemas/Shouting'
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
    public void TheKey_ReadsTheValue_AndTheMemberName_InAnyCasing(
        string side,
        string wayIn)
    {
        // Arrange
        var assembly = Load(side, readEnumMemberNames: true);
        var options = OptionsFor(assembly, wayIn);

        // Act & Assert
        foreach (var (type, json, member) in new[]
                 {
                     ("Protocol", "\"rtsp\"", "Rtsp"),
                     ("Protocol", "\"Rtsp\"", "Rtsp"),
                     ("Protocol", "\"RTSP\"", "Rtsp"),
                     ("OverlayPosition", "\"topLeft\"", "TopLeft"),
                     ("OverlayPosition", "\"TopLeft\"", "TopLeft"),
                     ("JobState", "\"in-progress\"", "InProgress"),
                     ("JobState", "\"InProgress\"", "InProgress"),
                     ("JobState", "\"inprogress\"", "InProgress"),
                     ("Shouting", "\"FOO\"", "FOO"),
                     ("Shouting", "\"Foo\"", "Foo"),
                 })
        {
            Assert.Equal(member, JsonSerializer.Deserialize(json, TypeOf(assembly, type), options)!.ToString());
        }
    }

    [Theory]
    [MemberData(nameof(SidesAndWaysIn))]
    public void TheKey_StillWritesTheSpecificationsValue(
        string side,
        string wayIn)
    {
        // Arrange
        var assembly = Load(side, readEnumMemberNames: true);
        var options = OptionsFor(assembly, wayIn);

        // Act & Assert
        Assert.Equal("\"rtsp\"", JsonSerializer.Serialize(Member(assembly, "Protocol", "Rtsp"), TypeOf(assembly, "Protocol"), options));
        Assert.Equal("\"topLeft\"", JsonSerializer.Serialize(Member(assembly, "OverlayPosition", "TopLeft"), TypeOf(assembly, "OverlayPosition"), options));
        Assert.Equal("\"in-progress\"", JsonSerializer.Serialize(Member(assembly, "JobState", "InProgress"), TypeOf(assembly, "JobState"), options));
    }

    [Theory]
    [MemberData(nameof(SidesAndWaysIn))]
    public void TheKey_RefusesANumber_ANumberAsText_AListOfNames_AndANameTwoMembersShare(
        string side,
        string wayIn)
    {
        // Arrange
        var assembly = Load(side, readEnumMemberNames: true);
        var options = OptionsFor(assembly, wayIn);

        // Act & Assert
        foreach (var (type, json) in new[]
                 {
                     ("Protocol", "1"),
                     ("Protocol", "\"1\""),
                     ("Protocol", "\"Rtsp,Http\""),
                     ("Protocol", "\"udp\""),
                     ("Shouting", "\"foo\""),
                 })
        {
            var exception = Record.Exception(() => JsonSerializer.Deserialize(json, TypeOf(assembly, type), options));
            Assert.True(exception is JsonException, $"{type} {json}: {exception?.GetType().Name ?? "read"}");
        }
    }

    [Theory]
    [MemberData(nameof(SidesAndWaysIn))]
    public void TheKey_ReadsAMemberNameAsADictionaryKey_AndWritesTheValue(
        string side,
        string wayIn)
    {
        // Arrange
        var assembly = Load(side, readEnumMemberNames: true);
        var options = OptionsFor(assembly, wayIn);
        var protocol = TypeOf(assembly, "Protocol");
        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(protocol, typeof(int));

        // Act
        var read = (System.Collections.IDictionary)JsonSerializer.Deserialize("""{"Rtsp":1,"http":2}""", dictionaryType, options)!;
        var written = JsonSerializer.Serialize(read, dictionaryType, options);

        // Assert
        Assert.Equal(1, read[Member(assembly, "Protocol", "Rtsp")]);
        Assert.Equal(2, read[Member(assembly, "Protocol", "Http")]);
        Assert.Equal("""{"rtsp":1,"http":2}""", written);
    }

    [Theory]
    [MemberData(nameof(SidesAndWaysIn))]
    public void WithoutTheKey_OnlyTheValueIsRead(
        string side,
        string wayIn)
    {
        // Arrange
        var assembly = Load(side, readEnumMemberNames: false);
        var options = OptionsFor(assembly, wayIn);
        var protocol = TypeOf(assembly, "Protocol");

        // Act & Assert
        Assert.Equal("Rtsp", JsonSerializer.Deserialize("\"rtsp\"", protocol, options)!.ToString());
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize("\"Rtsp\"", protocol, options));
    }

    // An extensible enum already takes a listed value in any casing; the key adds the member name of a value that
    // differs from it by more than casing (InProgress for in-progress)
    [Theory]
    [InlineData(true, true, "in-progress")]
    [InlineData(false, false, "InProgress")]
    public void ExtensibleClientEnum_TheKey_ReadsAMemberNameAsTheKnownValue(
        bool readEnumMemberNames,
        bool isKnown,
        string value)
    {
        // Arrange
        var marker = $$"""{ "generationMode": "TypedClient", "readEnumMemberNames": {{(readEnumMemberNames ? "true" : "false")}} }""";
        var assembly = CompilationVerificationHarness.EmitAndLoad(
            ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml));
        var jobState = TypeOf(assembly, "JobState");

        // Act
        var read = JsonSerializer.Deserialize("\"InProgress\"", jobState, OptionsFor(assembly, "Options"))!;

        // Assert
        Assert.Equal(isKnown, (bool)jobState.GetProperty("IsKnown")!.GetValue(read)!);
        Assert.Equal(value, (string?)jobState.GetProperty("Value")!.GetValue(read));
        Assert.Equal($"\"{value}\"", JsonSerializer.Serialize(read, jobState, OptionsFor(assembly, "Options")));
    }

    private static Assembly Load(
        string side,
        bool readEnumMemberNames)
    {
        var key = $"\"readEnumMemberNames\": {(readEnumMemberNames ? "true" : "false")}";
        return side == "Server"
            ? CompilationVerificationHarness.EmitAndLoad(
                ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", $$"""{ {{key}} }""", includeMinimalApi: false, Yaml))
            : CompilationVerificationHarness.EmitAndLoad(
                ErrorResponseBodyRuntimeTests.Generate(
                    new ApiClientGenerator(),
                    ".atc-rest-api-client",
                    $$"""{ "generationMode": "{{side}}", "extensibleEnums": false, {{key}} }""",
                    includeMinimalApi: false,
                    Yaml));
    }

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

    private static object Member(
        Assembly assembly,
        string type,
        string member)
        => Enum.Parse(TypeOf(assembly, type), member);
}