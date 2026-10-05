namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A <c>type: string</c> enum is a string on the wire: the server's C# enum is written as its value even in a host
/// that registers no <c>JsonStringEnumConverter</c> (v2 wrote the member's number there, unless a value needed
/// <c>[EnumMember]</c>). A converter the host does register still decides.
/// </summary>
public sealed class ServerEnumWireTests
{
    private const string Yaml = """
        openapi: 3.1.0
        info:
          title: Jobs
          version: 1.0.0
        paths:
          /jobs/{id}:
            get:
              operationId: getJob
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Job'
        components:
          schemas:
            JobState:
              type: string
              enum: [Pending, Running, Completed]
            Job:
              type: object
              properties:
                state:
                  $ref: '#/components/schemas/JobState'
        """;

    [Fact]
    public void StringEnum_IsWrittenAsItsValue_WithoutAHostConverter()
    {
        var state = StateType();

        var running = Enum.Parse(state, "Running");

        Assert.Equal("\"Running\"", JsonSerializer.Serialize(running, state, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(running, JsonSerializer.Deserialize("\"Running\"", state, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void AConverterTheHostRegisters_StillDecides()
    {
        var state = StateType();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));

        Assert.Equal("\"running\"", JsonSerializer.Serialize(Enum.Parse(state, "Running"), state, options));
    }

    private static Type StateType()
        => CompilationVerificationHarness.EmitAndLoad(
                ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml),
                includeMinimalApi: true)
            .GetTypes()
            .Single(t => t.Name == "JobState");
}