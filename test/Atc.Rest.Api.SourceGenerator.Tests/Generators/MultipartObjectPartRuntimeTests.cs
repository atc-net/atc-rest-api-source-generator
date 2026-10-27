namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A multipart field whose <c>encoding</c> names XML or <c>application/x-www-form-urlencoded</c> is sent in that media
/// type, as OpenAPI encodes it: a model as an XML part (as an XML body of it is written), a flat model as a URL-encoded
/// part. The generated server reads either - sent as a form value, or as a file part as a browser sends a Blob - answers
/// a part that is no such value with a problem naming the field, and both C# clients send them.
/// </summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The form disposes its parts.")]
[Collection(nameof(MultipartObjectPartRuntimeTests))]
[CollectionDefinition(nameof(MultipartObjectPartRuntimeTests), DisableParallelization = true)]
public sealed class MultipartObjectPartRuntimeTests
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
                    encoding:
                      address:
                        contentType: application/xml
                      stops:
                        contentType: application/xml; charset=utf-8
                      window:
                        contentType: application/x-www-form-urlencoded
                      windows:
                        contentType: application/x-www-form-urlencoded
                      labels:
                        contentType: application/x-www-form-urlencoded
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
            Window:
              type: object
              required: [start]
              properties:
                start:
                  type: integer
                end:
                  type: integer
                express:
                  type: boolean
                note:
                  type: string
            Labels:
              type: object
              properties:
                color:
                  type: string
                size:
                  type: integer
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
                window:
                  $ref: '#/components/schemas/Window'
                windows:
                  type: array
                  items:
                    $ref: '#/components/schemas/Window'
                labels:
                  $ref: '#/components/schemas/Labels'
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
                static string Describe(Window? window)
                    => window is null ? "-" : $"{window.Start}-{window.End?.ToString() ?? "?"}-{window.Express?.ToString() ?? "?"}-{window.Note ?? "?"}";

                Received = string.Join(
                    "|",
                    form.Label,
                    form.Address.Street,
                    form.Address.PostCode ?? "-",
                    form.Stops is null ? "-" : string.Join(",", form.Stops.Select(s => s.Street)),
                    Describe(form.Window),
                    form.Windows is null ? "-" : string.Join(",", form.Windows.Select(Describe)),
                    form.Labels is null ? "-" : $"{form.Labels.Color ?? "?"}-{form.Labels.Size?.ToString() ?? "?"}");
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
                    Address: new Address(Street: "Main Street 1 & 2", PostCode: "1000"),
                    Stops: [new Address(Street: "A", PostCode: null), new Address(Street: "B", PostCode: null)],
                    Window: new Window(Start: 1, End: 5, Express: true, Note: "back door & gate"),
                    Windows: [new Window(Start: 2, End: null, Express: null, Note: null), new Window(Start: 3, End: 4, Express: false, Note: null)],
                    Labels: new Labels(Color: "red", Size: null));

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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Server_ReadsTheXmlAndUrlEncodedParts(
        bool asFileParts,
        bool useRequestDelegateGenerator)
    {
        await using var host = await StartAsync(useRequestDelegateGenerator);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("parcel"), "label");
        AddPart(form, "address", "<Address><street>Main Street 1</street><post_code>1000</post_code></Address>", "application/xml", asFileParts);
        AddPart(form, "stops", "<Address><street>A</street></Address>", "application/xml", asFileParts);
        AddPart(form, "stops", "<Address><street>B</street></Address>", "application/xml", asFileParts);
        AddPart(form, "window", "start=1&end=5&express=true&note=back%20door&unknown=x", "application/x-www-form-urlencoded", asFileParts);
        AddPart(form, "windows", "start=2", "application/x-www-form-urlencoded", asFileParts);
        AddPart(form, "windows", "start=3&end=4&express=false", "application/x-www-form-urlencoded", asFileParts);
        AddPart(form, "labels", "color=red", "application/x-www-form-urlencoded", asFileParts);

        var (status, body) = await PostAsync(host, form);

        Assert.True(status == 200, $"{status}: {body}");
        Assert.Equal("\"parcel|Main Street 1|1000|A,B|1-5-True-back door|2-?-?-?,3-4-False-?|red-?\"", body);
    }

    [Theory]
    [InlineData("address", "<Address><street>", "application/xml")] // no XML
    [InlineData("address", "{\"street\":\"Main Street 1\"}", "application/xml")] // JSON where XML is declared
    [InlineData("address", "<Address><post_code>1000</post_code></Address>", "application/xml")] // street is required
    [InlineData("window", "start=soon", "application/x-www-form-urlencoded")] // no number
    [InlineData("window", "start=1&express=maybe", "application/x-www-form-urlencoded")] // no boolean
    [InlineData("window", "end=5", "application/x-www-form-urlencoded")] // start is required
    public async Task Server_APartThatIsNoSuchValue_IsA400NamingTheField(
        string field,
        string text,
        string mediaType)
    {
        await using var host = await StartAsync(useRequestDelegateGenerator: false);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("parcel"), "label");
        if (field != "address")
        {
            AddPart(form, "address", "<Address><street>Main Street 1</street></Address>", "application/xml", asFilePart: false);
        }

        AddPart(form, field, text, mediaType, asFilePart: false);

        var (status, body) = await PostAsync(host, form);

        Assert.True(status == 400, $"{status}: {body}");
        var errors = JsonNode.Parse(body)!["errors"]!.AsObject();
        Assert.Equal([field], errors.Select(e => e.Key), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Clients_SendTheXmlAndUrlEncodedParts_TheServerReadsThem(
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

        Assert.Equal("parcel|Main Street 1 & 2|1000|A,B|1-5-True-back door & gate|2-?-?-?,3-4-False-?|red-?", received.GetValue(null));
    }

    private static Task<XmlTestHost> StartAsync(
        bool useRequestDelegateGenerator)
        => XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.ShipmentRegistration", useRequestDelegateGenerator: useRequestDelegateGenerator);

    // A part of the media type: a form value, or - as a browser sends a Blob - a file part named "blob"
    private static void AddPart(
        MultipartFormDataContent form,
        string name,
        string text,
        string mediaType,
        bool asFilePart)
    {
        var part = new StringContent(text, Encoding.UTF8, mediaType);
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