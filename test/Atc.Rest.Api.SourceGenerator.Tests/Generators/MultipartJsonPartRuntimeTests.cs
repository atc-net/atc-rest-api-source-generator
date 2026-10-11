namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A multipart field that is an object, a map or a composition (or a list of one) is an application/json part, as
/// OpenAPI encodes it: the generated server reads it - sent as a form value, or as a file part (what a browser's
/// <c>FormData</c> sends for a <c>Blob</c>, which the TypeScript client appends) - through the app's JSON options, answers
/// a part that is no such value with a problem naming the field, and both C# clients send it.
/// </summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The form disposes its parts.")]
[Collection(nameof(MultipartJsonPartRuntimeTests))]
[CollectionDefinition(nameof(MultipartJsonPartRuntimeTests), DisableParallelization = true)]
public sealed class MultipartJsonPartRuntimeTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /shipments:
            post:
              operationId: createShipment
              requestBody:
                required: true
                content:
                  multipart/form-data:
                    schema:
                      $ref: '#/components/schemas/ShipmentForm'
              responses:
                '200':
                  description: What the server received
                  content:
                    application/json:
                      schema:
                        type: string
        components:
          schemas:
            Address:
              type: object
              required: [street]
              properties:
                street:
                  type: string
                post_code:
                  type: string
            ShipmentForm:
              type: object
              required: [label, address]
              properties:
                label:
                  type: string
                address:
                  $ref: '#/components/schemas/Address'
                stops:
                  type: array
                  items:
                    $ref: '#/components/schemas/Address'
                tags:
                  type: object
                  additionalProperties:
                    type: string
                manifest:
                  type: string
                  format: binary
        """;

    /// <summary>
    /// Answers what the server bound, and keeps it for a test that called through a client.
    /// </summary>
    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class CreateShipmentHandlerImpl : ICreateShipmentHandler
        {
            public static string? Received { get; set; }

            public System.Threading.Tasks.Task<CreateShipmentResult> ExecuteAsync(CreateShipmentParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                var form = parameters.Request;
                Received = string.Join(
                    "|",
                    form.Label,
                    form.Address.Street,
                    form.Address.PostCode ?? "-",
                    form.Stops is null ? "-" : string.Join(",", form.Stops.Select(s => s.Street)),
                    form.Tags is null ? "-" : string.Join(",", form.Tags.Select(t => t.Key + "=" + t.Value)),
                    form.Manifest?.FileName ?? "-");
                return System.Threading.Tasks.Task.FromResult(CreateShipmentResult.Ok(Received));
            }
        }

        public static class ShipmentRegistration
        {
            public static void Register(IServiceCollection services)
                => services.AddScoped<ICreateShipmentHandler, CreateShipmentHandlerImpl>();
        }
        """";

    /// <summary>
    /// Sends a shipment through a generated client against the host at the given address.
    /// </summary>
    private const string ClientProbeSource = """"
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class ShipmentProbe
        {
            public static async Task RunAsync(Uri baseAddress, bool typedClient)
            {
                var assembly = typeof(ShipmentProbe).Assembly;
                var form = new ShipmentForm(
                    Label: "parcel",
                    Address: new Address(Street: "Main Street 1", PostCode: "1000"),
                    Stops: [new Address(Street: "A", PostCode: null), new Address(Street: "B", PostCode: null)],
                    Tags: new Dictionary<string, string> { ["fragile"] = "yes" },
                    Manifest: null);

                if (typedClient)
                {
                    var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("CreateShipmentAsync") is not null);
                    var client = Activator.CreateInstance(clientType, new HttpClient { BaseAddress = baseAddress })!;
                    var method = clientType.GetMethod("CreateShipmentAsync")!;
                    var parameters = Activator.CreateInstance(method.GetParameters()[0].ParameterType, form);
                    await (Task)method.Invoke(client, [parameters, CancellationToken.None])!;
                }
                else
                {
                    var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                    var services = new ServiceCollection();
                    services.AddHttpClient(httpClientName, c => c.BaseAddress = baseAddress);
                    assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services, null]);
                    using var provider = services.BuildServiceProvider();
                    var endpointType = assembly.GetTypes().Single(t => t.Name == "ICreateShipmentEndpoint");
                    var parameters = Activator.CreateInstance(assembly.GetTypes().Single(t => t.Name == "CreateShipmentParameters"), form);
                    var endpoint = provider.GetRequiredService(endpointType);
                    await (Task)endpointType.GetMethod("ExecuteAsync")!.Invoke(endpoint, [parameters, httpClientName, CancellationToken.None])!;
                }
            }
        }
        """";

    private const string Expected = "parcel|Main Street 1|1000|A,B|fragile=yes|-";

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Server_ReadsTheJsonParts(
        bool asFileParts,
        bool useRequestDelegateGenerator)
    {
        await using var host = await StartAsync(useRequestDelegateGenerator);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("parcel"), "label");
        AddJson(form, "address", """{"street":"Main Street 1","post_code":"1000"}""", asFileParts);
        AddJson(form, "stops", """{"street":"A"}""", asFileParts);
        AddJson(form, "stops", """{"street":"B"}""", asFileParts);
        AddJson(form, "tags", """{"fragile":"yes"}""", asFileParts);

        var (status, body) = await PostAsync(host, form);

        Assert.Equal(200, status);
        Assert.Equal($"\"{Expected}\"", body);
    }

    [Theory]
    [InlineData("[1]")] // a list, not an address
    [InlineData("{ not json")]
    [InlineData("null")] // the field is required
    [InlineData(null)] // not sent; the field is required
    public async Task Server_AnAddressPartThatIsNoAddress_IsA400NamingTheField(
        string? address)
    {
        await using var host = await StartAsync(useRequestDelegateGenerator: false);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("parcel"), "label");
        if (address is not null)
        {
            AddJson(form, "address", address, asFilePart: false);
        }

        var (status, body) = await PostAsync(host, form);

        Assert.Equal(400, status);
        var errors = JsonNode.Parse(body)!["errors"]!.AsObject();
        Assert.Equal(["address"], errors.Select(e => e.Key), StringComparer.Ordinal);
        Assert.DoesNotContain("not json", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Server_AListPartThatIsNoValue_IsA400NamingTheField()
    {
        await using var host = await StartAsync(useRequestDelegateGenerator: false);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("parcel"), "label");
        AddJson(form, "address", """{"street":"Main Street 1"}""", asFilePart: false);
        AddJson(form, "stops", """{"street":"A"}""", asFilePart: false);
        AddJson(form, "stops", "{ not json", asFilePart: false);

        var (status, body) = await PostAsync(host, form);

        Assert.Equal(400, status);
        var errors = JsonNode.Parse(body)!["errors"]!.AsObject();
        Assert.Equal(["stops"], errors.Select(e => e.Key), StringComparer.Ordinal);
        Assert.DoesNotContain("not json", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Clients_SendTheJsonParts_TheServerReadsThem(
        bool typedClient)
    {
        await using var host = await StartAsync(useRequestDelegateGenerator: false);
        var marker = typedClient
            ? """{ "generationMode": "TypedClient" }"""
            : ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails");
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        var usings = sources
            .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
            .Where(ns => ns.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Select(ns => $"global using {ns};");
        sources.Add(("ShipmentProbe.cs", string.Join(Environment.NewLine, usings) + Environment.NewLine + ClientProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var received = host.Assembly.GetType("ErrorBodies.RuntimeTest.CreateShipmentHandlerImpl", throwOnError: true)!.GetProperty("Received")!;
        received.SetValue(null, null);

        await (Task)assembly.GetType("ErrorBodies.RuntimeTest.ShipmentProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [host.BaseAddress, typedClient])!;

        Assert.Equal(Expected, received.GetValue(null));
    }

    private static Task<XmlTestHost> StartAsync(
        bool useRequestDelegateGenerator)
        => XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.ShipmentRegistration", useRequestDelegateGenerator: useRequestDelegateGenerator);

    // An application/json part: a form value, or - as a browser sends a Blob - a file part named "blob"
    private static void AddJson(
        MultipartFormDataContent form,
        string name,
        string json,
        bool asFilePart)
    {
        var part = new StringContent(json, Encoding.UTF8, "application/json");
        if (asFilePart)
        {
            form.Add(part, name, "blob");
        }
        else
        {
            form.Add(part, name);
        }
    }

    private static async Task<(int Status, string Body)> PostAsync(
        XmlTestHost host,
        MultipartFormDataContent form)
    {
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using var response = await client.PostAsync(new Uri("/shipments", UriKind.Relative), form, TestContext.Current.CancellationToken);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}