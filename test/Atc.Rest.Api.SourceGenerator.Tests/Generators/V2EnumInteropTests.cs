namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A v2 peer next to a v3 one, without <c>readEnumMemberNames</c>. v2 wrote a string enum by its C# member name
/// (<c>Rtsp</c> for <c>rtsp</c>) and read it with <c>JsonStringEnumConverter</c>, which ignores casing; v3 writes the
/// specification's value and reads it in any casing. So where every value differs from its member name only in casing
/// (<c>rtsp</c> / <c>Rtsp</c>, <c>topLeft</c> / <c>TopLeft</c>) the two mix both ways; a value with a separator
/// (<c>in-progress</c> / <c>InProgress</c>) does not.
/// </summary>
public sealed class V2EnumInteropTests
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
            Camera:
              type: object
              properties:
                protocol:
                  $ref: '#/components/schemas/Protocol'
                overlayPosition:
                  $ref: '#/components/schemas/OverlayPosition'
                jobState:
                  $ref: '#/components/schemas/JobState'
        """;

    // What v2.1.1 generated for these enums, server and client alike: JsonStringEnumConverter on the type, and
    // [EnumMember], which System.Text.Json does not read
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum V2Protocol
    {
        [System.Runtime.Serialization.EnumMember(Value = "rtsp")]
        Rtsp,

        [System.Runtime.Serialization.EnumMember(Value = "http")]
        Http,

        [System.Runtime.Serialization.EnumMember(Value = "https")]
        Https,
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum V2OverlayPosition
    {
        [System.Runtime.Serialization.EnumMember(Value = "topLeft")]
        TopLeft,

        [System.Runtime.Serialization.EnumMember(Value = "bottomRight")]
        BottomRight,
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum V2JobState
    {
        [System.Runtime.Serialization.EnumMember(Value = "in-progress")]
        InProgress,

        [System.Runtime.Serialization.EnumMember(Value = "done")]
        Done,
    }

    public static TheoryData<string, string> V3Sides
        => new()
        {
            { "Server", "Options" },
            { "Server", "Attribute" },
            { "TypedClient", "Options" },
            { "TypedClient", "Attribute" },
            { "EndpointPerOperation", "Options" },
            { "EndpointPerOperation", "Attribute" },
        };

    [Fact]
    public void AV2Peer_WritesTheMemberName()
    {
        // Arrange - the v2 TypedClient's options
        var options = V2Options();

        // Act & Assert
        Assert.Equal("\"Rtsp\"", JsonSerializer.Serialize(V2Protocol.Rtsp, options));
        Assert.Equal("\"TopLeft\"", JsonSerializer.Serialize(V2OverlayPosition.TopLeft, options));
        Assert.Equal("\"InProgress\"", JsonSerializer.Serialize(V2JobState.InProgress, options));
    }

    [Fact]
    public void AV2Peer_ReadsWhatV3Writes_WhenTheValueDiffersFromTheMemberNameOnlyInCasing()
    {
        // Arrange
        var options = V2Options();

        // Act & Assert
        Assert.Equal(V2Protocol.Rtsp, JsonSerializer.Deserialize<V2Protocol>("\"rtsp\"", options));
        Assert.Equal(V2OverlayPosition.TopLeft, JsonSerializer.Deserialize<V2OverlayPosition>("\"topLeft\"", options));
        Assert.Equal(V2JobState.Done, JsonSerializer.Deserialize<V2JobState>("\"done\"", options));
        Assert.Equal(V2Protocol.Rtsp, JsonSerializer.Deserialize<V2Protocol>("\"rtsp\"", new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void AV2Peer_DoesNotReadAValueWithASeparator()
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<V2JobState>("\"in-progress\"", V2Options()));

    [Theory]
    [MemberData(nameof(V3Sides))]
    public void AV3Peer_ReadsWhatV2Writes_WhenTheValueDiffersFromTheMemberNameOnlyInCasing(
        string side,
        string wayIn)
    {
        // Arrange
        var assembly = Load(side, extensibleEnums: false);
        var options = OptionsFor(assembly, wayIn);

        // Act
        var camera = JsonSerializer.Deserialize(
            JsonSerializer.Serialize(new { protocol = V2Protocol.Rtsp, overlayPosition = V2OverlayPosition.TopLeft }, V2Options()),
            TypeOf(assembly, "Camera"),
            options)!;
        var written = JsonNode.Parse(JsonSerializer.Serialize(camera, camera.GetType(), options))!;

        // Assert
        Assert.Equal("rtsp", (string?)written["protocol"]);
        Assert.Equal("topLeft", (string?)written["overlayPosition"]);
    }

    [Theory]
    [MemberData(nameof(V3Sides))]
    public void AV3Peer_DoesNotReadTheMemberNameOfAValueWithASeparator(
        string side,
        string wayIn)
    {
        // Arrange
        var assembly = Load(side, extensibleEnums: false);
        var options = OptionsFor(assembly, wayIn);

        // Act
        var exception = Record.Exception(() => JsonSerializer.Deserialize("\"InProgress\"", TypeOf(assembly, "JobState"), options));

        // Assert
        Assert.IsType<JsonException>(exception, exactMatch: false);
    }

    [Fact]
    public void AV3ExtensibleClient_ReadsWhatV2Writes_AndKeepsAMemberNameWithASeparatorAsAnUnknownValue()
    {
        // Arrange
        var assembly = Load("TypedClient", extensibleEnums: true);
        var options = OptionsFor(assembly, "Options");
        var protocol = TypeOf(assembly, "Protocol");
        var jobState = TypeOf(assembly, "JobState");

        // Act
        var rtsp = JsonSerializer.Deserialize("\"Rtsp\"", protocol, options)!;
        var inProgress = JsonSerializer.Deserialize("\"InProgress\"", jobState, options)!;

        // Assert
        Assert.True((bool)protocol.GetProperty("IsKnown")!.GetValue(rtsp)!);
        Assert.Equal("rtsp", (string?)protocol.GetProperty("Value")!.GetValue(rtsp));
        Assert.False((bool)jobState.GetProperty("IsKnown")!.GetValue(inProgress)!);
        Assert.Equal("InProgress", (string?)jobState.GetProperty("Value")!.GetValue(inProgress));
    }

    // The v2 TypedClient's options; a v2 server and the v2 EndpointPerOperation client read the same way (the enum's
    // own JsonStringEnumConverter)
    private static JsonSerializerOptions V2Options()
        => new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };

    private static Assembly Load(
        string side,
        bool extensibleEnums)
        => side == "Server"
            ? CompilationVerificationHarness.EmitAndLoad(
                ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", "{ }", includeMinimalApi: false, Yaml))
            : CompilationVerificationHarness.EmitAndLoad(
                ErrorResponseBodyRuntimeTests.Generate(
                    new ApiClientGenerator(),
                    ".atc-rest-api-client",
                    $$"""{ "generationMode": "{{side}}", "extensibleEnums": {{(extensibleEnums ? "true" : "false")}} }""",
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