namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A tuple schema (OpenAPI 3.1 <c>prefixItems</c>) is a JSON array on the wire - <c>[55.5, 12.5]</c> - and a
/// record with named members in C#. The generated record carries the converter that maps the two, on the
/// server and in both C# clients.
/// </summary>
public sealed class TupleWireTests
{
    private const string Yaml = """
        openapi: 3.1.0
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /places:
            get:
              operationId: getPlace
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Place'
        components:
          schemas:
            Place:
              type: object
              required:
                - name
                - position
              properties:
                name:
                  type: string
                position:
                  $ref: '#/components/schemas/Coordinate'
                reading:
                  $ref: '#/components/schemas/Reading'
            Coordinate:
              type: array
              description: Latitude and longitude.
              prefixItems:
                - type: number
                  description: Latitude
                - type: number
                  description: Longitude
            Reading:
              type: array
              description: A label, a value, and any number of sample counts.
              prefixItems:
                - type: string
                  description: Label
                - type: number
                  description: Value
              items:
                type: integer
        """;

    public static TheoryData<string> Sides
        => ["server", "TypedClient", "EndpointPerOperation"];

    [Theory]
    [MemberData(nameof(Sides))]
    public void Tuple_IsAJsonArrayOnTheWire(string side)
    {
        var assembly = CompilationVerificationHarness.EmitAndLoad(Generate(side), includeMinimalApi: side == "server");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var coordinateType = assembly.GetTypes().Single(t => t.Name == "Coordinate");
        var coordinate = Activator.CreateInstance(coordinateType, 55.5, 12.5)!;

        Assert.Equal("[55.5,12.5]", JsonSerializer.Serialize(coordinate, coordinateType, options));
        Assert.Equal(coordinate, JsonSerializer.Deserialize("[55.5, 12.5]", coordinateType, options));
    }

    [Theory]
    [MemberData(nameof(Sides))]
    public void Tuple_WithAdditionalItems_KeepsThemAfterTheFixedOnes(
        string side)
    {
        var assembly = CompilationVerificationHarness.EmitAndLoad(Generate(side), includeMinimalApi: side == "server");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var readingType = assembly.GetTypes().Single(t => t.Name == "Reading");
        var reading = JsonSerializer.Deserialize("""["temp", 21.5, 3, 4]""", readingType, options)!;

        Assert.Equal("""["temp",21.5,3,4]""", JsonSerializer.Serialize(reading, readingType, options));
    }

    [Theory]
    [MemberData(nameof(Sides))]
    public void Model_WithATupleProperty_RoundTrips(string side)
    {
        var assembly = CompilationVerificationHarness.EmitAndLoad(Generate(side), includeMinimalApi: side == "server");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var placeType = assembly.GetTypes().Single(t => t.Name == "Place");
        const string json = """{"name":"Copenhagen","position":[55.5,12.5]}""";
        var place = JsonSerializer.Deserialize(json, placeType, options)!;

        var position = placeType.GetProperty("Position")!.GetValue(place)!;
        Assert.Equal(55.5, position.GetType().GetProperty("Latitude")!.GetValue(position));
        Assert.Contains("\"position\":[55.5,12.5]", JsonSerializer.Serialize(place, placeType, options), StringComparison.Ordinal);
    }

    private static List<(string HintName, string Source)> Generate(string side)
        => side switch
        {
            "server" => ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml),
            "TypedClient" => ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "TypedClient" }""", includeMinimalApi: false, Yaml),
            _ => ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, Yaml),
        };
}