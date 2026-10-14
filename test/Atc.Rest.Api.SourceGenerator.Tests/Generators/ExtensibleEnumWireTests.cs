namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A string enum in a generated C# client is an extensible enum: a <c>readonly record struct</c> over the value on
/// the wire, with the specification's values as named members. A value the client does not know is kept - with
/// <c>IsKnown</c> false - instead of failing the whole response. The server keeps a strict C# enum, and a client
/// marker with <c>"extensibleEnums": false</c> gets the C# enum back.
/// </summary>
public sealed class ExtensibleEnumWireTests
{
    private const string Yaml = """
        openapi: 3.1.0
        info:
          title: Fleet
          version: 1.0.0
        paths:
          /devices:
            get:
              operationId: listDevices
              parameters:
                - name: status
                  in: query
                  schema:
                    $ref: '#/components/schemas/DeviceStatus'
                - name: statuses
                  in: query
                  schema:
                    type: array
                    items:
                      $ref: '#/components/schemas/DeviceStatus'
                - name: sortOrder
                  in: query
                  schema:
                    type: string
                    enum: [ascending, descending]
                    default: ascending
                - name: X-Job-State
                  in: header
                  schema:
                    $ref: '#/components/schemas/JobState'
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Device'
          /devices/{id}:
            get:
              operationId: getDevice
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: string
                - name: view
                  in: query
                  schema:
                    $ref: '#/components/schemas/DeviceView'
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Device'
                    application/xml:
                      schema:
                        $ref: '#/components/schemas/Device'
          /devices/{status}/count:
            get:
              operationId: countDevices
              parameters:
                - name: status
                  in: path
                  required: true
                  schema:
                    $ref: '#/components/schemas/DeviceStatus'
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: integer
          /awkward:
            post:
              operationId: postAwkward
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/AwkwardHolder'
              responses:
                '204':
                  description: No Content
        components:
          schemas:
            DeviceStatus:
              type: string
              description: Where a device is in its life.
              enum: [online, offline, in-maintenance]
            JobState:
              type: string
              enum: [Pending, Running, Completed]
            DeviceView:
              type: string
              enum: [Summary, Full]
              default: Summary
            Level:
              type: integer
              enum: [1, 2, 3]
            Awkward:
              type: string
              enum: [Value, Names, IsKnown, KnownValues, Parse, TryParse, ToString, ToWireValue, Equals, GetHashCode, Awkward]
            AwkwardHolder:
              type: object
              properties:
                awkward:
                  $ref: '#/components/schemas/Awkward'
            Device:
              type: object
              required:
                - id
                - status
              properties:
                id:
                  type: string
                status:
                  $ref: '#/components/schemas/DeviceStatus'
                previousStatus:
                  $ref: '#/components/schemas/DeviceStatus'
                jobState:
                  allOf:
                    - $ref: '#/components/schemas/JobState'
                  default: Pending
                level:
                  $ref: '#/components/schemas/Level'
                priority:
                  type: string
                  enum: [low, high]
                history:
                  type: array
                  items:
                    $ref: '#/components/schemas/DeviceStatus'
        """;

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static TheoryData<string> Clients
        => ["TypedClient", "EndpointPerOperation"];

    [Theory]
    [MemberData(nameof(Clients))]
    public void ClientEnum_IsAValueTypeOverTheWireValue_NotACSharpEnum(
        string client)
    {
        var status = TypeOf(Load(client), "DeviceStatus");

        Assert.True(status.IsValueType);
        Assert.False(status.IsEnum);
        Assert.Equal("online", Value(Static(status, "Online")));
        Assert.Equal("in-maintenance", Value(Static(status, "InMaintenance")));
        Assert.Equal("in-maintenance", status.GetNestedType("Names")!.GetField("InMaintenance")!.GetRawConstantValue());
    }

    [Theory]
    [MemberData(nameof(Clients))]
    public void UnknownValue_IsKept_AndWrittenBackUnchanged(string client)
    {
        var status = TypeOf(Load(client), "DeviceStatus");

        var rebooting = JsonSerializer.Deserialize("\"rebooting\"", status, Web)!;

        Assert.Equal("rebooting", Value(rebooting));
        Assert.False(IsKnown(rebooting));
        Assert.Equal("rebooting", rebooting.ToString());
        Assert.Equal("\"rebooting\"", JsonSerializer.Serialize(rebooting, status, Web));
    }

    [Theory]
    [MemberData(nameof(Clients))]
    public void KnownValue_IsKnown_AndEqualsItsMember_WhateverItsCase(
        string client)
    {
        var status = TypeOf(Load(client), "DeviceStatus");

        var online = JsonSerializer.Deserialize("\"online\"", status, Web)!;
        var shouted = JsonSerializer.Deserialize("\"ONLINE\"", status, Web)!;

        Assert.True(IsKnown(online));
        Assert.Equal(Static(status, "Online"), online);
        Assert.Equal(Static(status, "Online"), shouted);
        Assert.Equal("online", Value(shouted));
        Assert.Equal("\"online\"", JsonSerializer.Serialize(shouted, status, Web));
    }

    /// <summary>
    /// A server whose host has no string converter writes a C# enum as its number - the member's position, which is the
    /// specification's order. A v2 client's C# enum read that; the extensible enum reads it too, and a number outside
    /// the list is kept as an unknown value instead of failing the response.
    /// </summary>
    [Theory]
    [MemberData(nameof(Clients))]
    public void Number_IsReadAsTheListedValueAtThatPosition_AndAnyOtherNumberIsKept(
        string client)
    {
        var status = TypeOf(Load(client), "DeviceStatus");

        var offline = JsonSerializer.Deserialize("1", status, Web)!;
        var unknown = JsonSerializer.Deserialize("99", status, Web)!;

        Assert.Equal(Static(status, "Offline"), offline);
        Assert.Equal("\"offline\"", JsonSerializer.Serialize(offline, status, Web));
        Assert.Equal("99", Value(unknown));
        Assert.False(IsKnown(unknown));
    }

    [Theory]
    [MemberData(nameof(Clients))]
    public void Model_WithAnUnknownValue_ReadsTheRestOfTheResponse(
        string client)
    {
        var device = TypeOf(Load(client), "Device");

        var read = JsonSerializer.Deserialize(
            """{"id":"d-1","status":"rebooting","previousStatus":"online","priority":"urgent","history":["online","decommissioned"]}""",
            device,
            Web)!;

        Assert.Equal("d-1", device.GetProperty("Id")!.GetValue(read));
        Assert.Equal("rebooting", Value(device.GetProperty("Status")!.GetValue(read)!));
        Assert.Equal("online", Value(device.GetProperty("PreviousStatus")!.GetValue(read)!));
        Assert.Equal("urgent", Value(device.GetProperty("Priority")!.GetValue(read)!));
        var history = ((System.Collections.IEnumerable)device.GetProperty("History")!.GetValue(read)!).Cast<object>().Select(Value).ToList();
        Assert.Equal(["online", "decommissioned"], history);

        var written = JsonSerializer.Serialize(read, device, Web);
        Assert.Contains("\"status\":\"rebooting\"", written, StringComparison.Ordinal);
        Assert.Contains("\"history\":[\"online\",\"decommissioned\"]", written, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Clients))]
    public void NullableEnum_ReadsNull_AndAValue(string client)
    {
        var status = TypeOf(Load(client), "DeviceStatus");
        var nullable = typeof(Nullable<>).MakeGenericType(status);

        Assert.Null(JsonSerializer.Deserialize("null", nullable, Web));
        Assert.Equal("offline", Value(JsonSerializer.Deserialize("\"offline\"", nullable, Web)!));
    }

    [Theory]
    [MemberData(nameof(Clients))]
    public void Dictionary_KeyedByTheEnum_RoundTrips(string client)
    {
        var status = TypeOf(Load(client), "DeviceStatus");
        var dictionary = typeof(Dictionary<,>).MakeGenericType(status, typeof(int));

        var read = JsonSerializer.Deserialize("""{"online":3,"rebooting":1}""", dictionary, Web)!;

        Assert.Equal("""{"online":3,"rebooting":1}""", JsonSerializer.Serialize(read, dictionary, Web));
    }

    [Theory]
    [MemberData(nameof(Clients))]
    public void KnownValues_ListsTheSpecificationsValues_InOrder(string client)
    {
        var status = TypeOf(Load(client), "DeviceStatus");

        var known = ((System.Collections.IEnumerable)status.GetProperty("KnownValues")!.GetValue(null)!).Cast<object>().Select(Value).ToList();

        Assert.Equal(["online", "offline", "in-maintenance"], known);
    }

    [Theory]
    [MemberData(nameof(Clients))]
    public void Parse_AcceptsAnyValue_AndConvertsFromString(string client)
    {
        var status = TypeOf(Load(client), "DeviceStatus");

        var parsed = status.GetMethod("Parse", [typeof(string), typeof(IFormatProvider)])!.Invoke(null, ["Offline", null])!;
        Assert.Equal(Static(status, "Offline"), parsed);

        object?[] tryArguments = ["rebooting", null, null];
        Assert.True((bool)status.GetMethod("TryParse", [typeof(string), typeof(IFormatProvider), status.MakeByRefType()])!.Invoke(null, tryArguments)!);
        Assert.False(IsKnown(tryArguments[2]!));

        object?[] nullArguments = [null, null, null];
        Assert.False((bool)status.GetMethod("TryParse", [typeof(string), typeof(IFormatProvider), status.MakeByRefType()])!.Invoke(null, nullArguments)!);

        // Written out only: an implicit conversion took a null for a string and threw
        Assert.Null(status.GetMethod("op_Implicit", [typeof(string)]));
        var explicitConversion = status.GetMethod("op_Explicit", [typeof(string)])!;
        Assert.Equal(Static(status, "InMaintenance"), explicitConversion.Invoke(null, ["in-maintenance"]));
    }

    /// <summary>
    /// A string becomes an extensible enum only when the code says so (<c>(DeviceStatus)"online"</c>,
    /// <c>new DeviceStatus("online")</c>): with an implicit conversion, <c>none ? null : DeviceStatus.Online</c> took the
    /// null as a string, typed the conditional as <c>DeviceStatus</c> and threw - now it is a <c>DeviceStatus?</c> that is
    /// null - and <c>DeviceStatus s = "online";</c> does not compile.
    /// </summary>
    [Theory]
    [MemberData(nameof(Clients))]
    public void AStringBecomesTheEnum_OnlyWhenWrittenOut_SoANullInAConditionalIsNoValue(
        string client)
    {
        const string consumer = """
            namespace Consumer;

            public static class UsesTheEnum
            {
                public static DeviceStatus? Pick(bool none) => none ? null : DeviceStatus.Online;

                public static DeviceStatus Written(string value) => (DeviceStatus)value;
            }
            """;
        var sources = Sources(client);
        var usings = string.Join(
            Environment.NewLine,
            sources
                .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
                .Where(ns => ns.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Select(ns => $"global using {ns};"));
        sources.Add(("Consumer.cs", usings + Environment.NewLine + consumer));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var uses = assembly.GetType("Consumer.UsesTheEnum", throwOnError: true)!;
        Assert.Null(uses.GetMethod("Pick")!.Invoke(null, [true]));
        Assert.Equal("online", Value(uses.GetMethod("Written")!.Invoke(null, ["online"])!));

        const string implicitUse = """
            namespace Consumer;

            public static class Implicit
            {
                public static DeviceStatus Status => "online";
            }
            """;
        sources[^1] = ("Consumer.cs", usings + Environment.NewLine + implicitUse);
        var errors = CompilationVerificationHarness.CreateCompilation(sources).GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.Id).ToList();
        Assert.Contains("CS0266", errors, StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Clients))]
    public void ToWireValue_IsTheValue_ForTheValueAndForANullable(string client)
    {
        var assembly = Load(client);
        var status = TypeOf(assembly, "DeviceStatus");
        var inMaintenance = Static(status, "InMaintenance");

        Assert.Equal("in-maintenance", status.GetMethod("ToWireValue", Type.EmptyTypes)!.Invoke(inMaintenance, null));

        var nullableExtension = assembly.GetTypes().Single(t => t.Name == "DeviceStatusExtensions").GetMethod("ToWireValue")!;
        Assert.Null(nullableExtension.Invoke(null, [null]));
        Assert.Equal("in-maintenance", nullableExtension.Invoke(null, [inMaintenance]));
    }

    [Theory]
    [MemberData(nameof(Clients))]
    public void Default_HasNoValue_IsNotKnown_AndIsWrittenAsNull(string client)
    {
        var status = TypeOf(Load(client), "DeviceStatus");
        var empty = Activator.CreateInstance(status)!;

        Assert.Null(Value(empty));
        Assert.False(IsKnown(empty));
        Assert.Equal(string.Empty, empty.ToString());
        Assert.Equal("null", JsonSerializer.Serialize(empty, status, Web));
    }

    [Theory]
    [MemberData(nameof(Clients))]
    public void ValuesThatClashWithTheStructsOwnMembers_AreStillMembers(
        string client)
    {
        var awkward = TypeOf(Load(client), "Awkward");

        var known = ((System.Collections.IEnumerable)awkward.GetProperty("KnownValues")!.GetValue(null)!).Cast<object>().Select(Value).ToList();

        Assert.Equal(["Value", "Names", "IsKnown", "KnownValues", "Parse", "TryParse", "ToString", "ToWireValue", "Equals", "GetHashCode", "Awkward"], known);
        Assert.True(IsKnown(JsonSerializer.Deserialize("\"Value\"", awkward, Web)!));
    }

    [Theory]
    [MemberData(nameof(Clients))]
    public void IntegerEnum_StaysACSharpEnum_AndAnUnknownNumberIsRead(
        string client)
    {
        var level = TypeOf(Load(client), "Level");

        Assert.True(level.IsEnum);
        Assert.Equal(7, (int)JsonSerializer.Deserialize("7", level, Web)!);
    }

    [Theory]
    [MemberData(nameof(Clients))]
    public void ExtensibleEnumsFalse_GivesTheCSharpEnumBack(string client)
    {
        var marker = client == "TypedClient"
            ? """{ "generationMode": "TypedClient", "extensibleEnums": false }"""
            : """{ "generationMode": "EndpointPerOperation", "extensibleEnums": false }""";
        var assembly = CompilationVerificationHarness.EmitAndLoad(
            ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml));

        Assert.True(TypeOf(assembly, "DeviceStatus").IsEnum);
    }

    [Fact]
    public void Server_KeepsAStrictCSharpEnum()
    {
        var assembly = CompilationVerificationHarness.EmitAndLoad(
            ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml),
            includeMinimalApi: true);

        Assert.True(TypeOf(assembly, "DeviceStatus").IsEnum);
    }

    private static Assembly Load(string client)
        => CompilationVerificationHarness.EmitAndLoad(Sources(client));

    private static List<(string HintName, string Source)> Sources(string client)
        => ErrorResponseBodyRuntimeTests.Generate(
            new ApiClientGenerator(),
            ".atc-rest-api-client",
            client == "TypedClient" ? """{ "generationMode": "TypedClient" }""" : ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"),
            includeMinimalApi: false,
            Yaml);

    private static Type TypeOf(
        Assembly assembly,
        string name)
        => assembly.GetTypes().Single(t => t.Name == name);

    private static object Static(
        Type type,
        string name)
        => type.GetProperty(name, BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

    private static string? Value(object value)
        => (string?)value.GetType().GetProperty("Value")!.GetValue(value);

    private static bool IsKnown(object value)
        => (bool)value.GetType().GetProperty("IsKnown")!.GetValue(value)!;
}