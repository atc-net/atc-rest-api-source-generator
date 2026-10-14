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

    // Every enum whose [JsonConverter] names the factory must be one the factory converts - inline property enums,
    // inline parameter enums and enums of a path segment included - or System.Text.Json refuses every model holding it
    [Theory]
    [InlineData("Server")]
    [InlineData("TypedClient")]
    [InlineData("EndpointPerOperation")]
    public void TheKey_EveryEnumNamingTheFactory_IsOneItConverts(string side)
    {
        // Arrange
        var key = "\"readEnumMemberNames\": true";
        var assembly = side == "Server"
            ? CompilationVerificationHarness.EmitAndLoad(
                ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", $$"""{ {{key}} }""", includeMinimalApi: false, ExtensibleEnumWireTests.Yaml))
            : CompilationVerificationHarness.EmitAndLoad(
                ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", $$"""{ "generationMode": "{{side}}", "extensibleEnums": false, {{key}} }""", includeMinimalApi: false, ExtensibleEnumWireTests.Yaml));
        var factoryType = TypeOf(assembly, "EnumWireConverterFactory");
        var factory = (JsonConverterFactory)Activator.CreateInstance(factoryType)!;
        var options = OptionsFor(assembly, "Attribute");

        // Act
        var named = assembly
            .GetTypes()
            .Where(t => t.IsEnum && t.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType == factoryType)
            .ToList();

        // Assert
        Assert.Contains(named, t => t.Name == "DeviceStatus");
        Assert.Contains(named, t => t.Name.EndsWith("Priority", StringComparison.Ordinal));
        foreach (var type in named)
        {
            Assert.True(factory.CanConvert(type), type.FullName);
            var first = Enum.GetValues(type).GetValue(0)!;
            Assert.Equal(first, JsonSerializer.Deserialize(JsonSerializer.Serialize(first, type, options), type, options));
        }

        // A whole model, written by a v2 peer: every enum by its member name
        var device = JsonSerializer.Deserialize(
            """{"id":"1","status":"InMaintenance","previousStatus":"online","history":["Offline","in-maintenance"],"priority":"High"}""",
            TypeOf(assembly, "Device"),
            options)!;
        var written = JsonNode.Parse(JsonSerializer.Serialize(device, device.GetType(), options))!;
        Assert.Equal("in-maintenance", (string?)written["status"]);
        Assert.Equal("online", (string?)written["previousStatus"]);
        Assert.Equal("""["offline","in-maintenance"]""", written["history"]!.ToJsonString());
        Assert.Equal("high", (string?)written["priority"]);
    }

    // A v2 client against a v3 server: its JSON body carries member names. Parameters bind the member name with or
    // without the key (what Upgrading to v3 tells the consumer); the body only with it.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Server_TheMemberNamesAV2ClientSends_InABodyOnlyWithTheKey_InParametersAlways(
        bool readEnumMemberNames)
    {
        // Arrange
        await using var host = await XmlTestHost.StartAsync(
            ServerYaml,
            ServerHandlers,
            "ErrorBodies.RuntimeTest.CameraRegistration",
            serverMarker: readEnumMemberNames
                ? ErrorResponseBodyRuntimeTests.ServerMarker.Replace("{", "{ \"readEnumMemberNames\": true,", StringComparison.Ordinal)
                : null);

        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using var content = new StringContent(
            """{"protocol":"Rtsp","overlayPosition":"TopLeft","jobState":"InProgress"}""",
            Encoding.UTF8,
            "application/json");
        using var get = new HttpRequestMessage(HttpMethod.Get, "/cameras/Rtsp?position=TopLeft");
        get.Headers.Add("JobState", "InProgress");

        // Act
        using var posted = await client.PostAsync(new Uri("/cameras", UriKind.Relative), content, TestContext.Current.CancellationToken);
        var body = await posted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var got = await client.SendAsync(get, TestContext.Current.CancellationToken);
        var parameters = await got.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        if (readEnumMemberNames)
        {
            Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
            Assert.Equal("""{"protocol":"rtsp","overlayPosition":"topLeft","jobState":"in-progress"}""", body);
        }
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest, posted.StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, got.StatusCode);
        Assert.Equal("\"Rtsp|TopLeft|InProgress\"", parameters);
    }

    [Fact]
    public async Task TheKey_KeepsTheGeneratedCodeTrimAndAotSafe()
    {
        // Arrange
        var key = "\"readEnumMemberNames\": true";
        var server = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", $$"""{ {{key}} }""", includeMinimalApi: false, Yaml);
        var client = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", $$"""{ "generationMode": "EndpointPerOperation", "extensibleEnums": false, {{key}} }""", includeMinimalApi: false, Yaml);

        // Act
        var problems = (await AotAnalyzerHarness.AnalyzeAsync(server, includeMinimalApi: false))
            .Concat(await AotAnalyzerHarness.AnalyzeAsync(client, includeMinimalApi: false))
            .ToList();

        // Assert
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    // The key is for a rollout: once every peer is on v3 it should go. Each build says it is on (Info, so warnings as
    // errors are not broken), so a project does not keep reading member names forever without knowing
    [Theory]
    [InlineData(".atc-rest-api-server", """{ "readEnumMemberNames": true }""", true)]
    [InlineData(".atc-rest-api-client", """{ "generationMode": "TypedClient", "readEnumMemberNames": true }""", true)]
    [InlineData(".atc-rest-api-client", """{ "generationMode": "EndpointPerOperation", "readEnumMemberNames": true }""", true)]
    [InlineData(".atc-rest-api-server", """{ }""", false)]
    [InlineData(".atc-rest-api-client", """{ "generationMode": "TypedClient" }""", false)]
    public void TheKey_IsReportedAsGEN023_SoTheRolloutIsNotForgotten(
        string markerFile,
        string marker,
        bool reported)
    {
        // Arrange
        var compilation = CSharpCompilation.Create(
            "Contoso.Host",
            [],
            CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi: true),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        IIncrementalGenerator generator = markerFile == ".atc-rest-api-server" ? new ApiServerGenerator() : new ApiClientGenerator();

        // Act
        CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(
            [
                new CompilationVerificationHarness.InMemoryAdditionalText("Cameras.yaml", Yaml),
                new CompilationVerificationHarness.InMemoryAdditionalText(markerFile, marker),
            ])
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics, CancellationToken.None);

        // Assert
        var info = diagnostics.Where(d => d.Id == "ATC_API_GEN023").ToList();
        if (!reported)
        {
            Assert.Empty(info);
            return;
        }

        var diagnostic = Assert.Single(info);
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Contains("readEnumMemberNames", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private const string ServerYaml = """
        openapi: 3.1.0
        info:
          title: ErrorBodies
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
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Camera'
                '400':
                  description: Bad Request
          /cameras/{protocol}:
            get:
              operationId: getCamera
              parameters:
                - name: protocol
                  in: path
                  required: true
                  schema:
                    $ref: '#/components/schemas/Protocol'
                - name: position
                  in: query
                  schema:
                    $ref: '#/components/schemas/OverlayPosition'
                - name: JobState
                  in: header
                  schema:
                    $ref: '#/components/schemas/JobState'
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: string
                '400':
                  description: Bad Request
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

    private const string ServerHandlers = """
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class CreateCameraHandlerImpl : ICreateCameraHandler
        {
            public System.Threading.Tasks.Task<CreateCameraResult> ExecuteAsync(CreateCameraParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(CreateCameraResult.Ok(parameters.Request));
        }

        public sealed class GetCameraHandlerImpl : IGetCameraHandler
        {
            public System.Threading.Tasks.Task<GetCameraResult> ExecuteAsync(GetCameraParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(GetCameraResult.Ok($"{parameters.Protocol}|{parameters.Position}|{parameters.JobState}"));
        }

        public static class CameraRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddScoped<ICreateCameraHandler, CreateCameraHandlerImpl>();
                services.AddScoped<IGetCameraHandler, GetCameraHandlerImpl>();
            }
        }
        """;

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