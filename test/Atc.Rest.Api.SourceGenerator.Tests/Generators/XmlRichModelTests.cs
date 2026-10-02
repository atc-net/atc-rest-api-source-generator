namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A rich model as XML - the Showcase sample's <c>User</c>: a nested model (<c>Address</c>), an enum, a <c>date</c> and a
/// <c>date-time</c>, a <c>uuid</c>, nullable unions (<c>[string, "null"]</c>, <c>[number, "null"]</c>), doubles, a
/// boolean and a read-only integer. The live generated server answers JSON or XML by <c>Accept</c>, and the model every
/// C# client reads from the XML is the model it reads from the JSON - with every optional field set, and with none.
/// </summary>
public sealed class XmlRichModelTests
{
    private const string Yaml = """
        openapi: 3.1.0
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /users/{userId}:
            get:
              operationId: getUserById
              parameters:
                - name: userId
                  in: path
                  required: true
                  schema:
                    type: string
                    format: uuid
              responses:
                '200':
                  description: The user
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/User'
                    application/xml:
                      schema:
                        $ref: '#/components/schemas/User'
                '404':
                  description: Not found
        components:
          schemas:
            User:
              title: User
              type: object
              required: [id, firstName, lastName, email, dateOfBirth, role, address]
              properties:
                id: { type: string, format: uuid }
                firstName: { type: string, minLength: 2, maxLength: 50 }
                lastName: { type: string, minLength: 2, maxLength: 50 }
                email: { type: string, format: email, maxLength: 254 }
                phone: { type: [string, "null"], pattern: '^\+?[1-9]\d{6,14}$' }
                website: { type: [string, "null"], format: uri, maxLength: 2048 }
                dateOfBirth: { type: string, format: date }
                age: { type: integer, minimum: 0, maximum: 150, readOnly: true }
                bio: { type: [string, "null"], maxLength: 1000 }
                avatarUrl: { type: [string, "null"], format: uri }
                isActive: { type: boolean, default: true }
                role: { $ref: '#/components/schemas/UserRole' }
                address: { $ref: '#/components/schemas/Address' }
                createdAt: { type: string, format: date-time, readOnly: true }
                updatedAt: { type: string, format: date-time, readOnly: true }
            Address:
              title: Address
              type: object
              required: [street, city, postalCode, country]
              properties:
                street: { type: string, minLength: 1, maxLength: 200 }
                city: { type: string, minLength: 1, maxLength: 100 }
                state: { type: [string, "null"], maxLength: 100 }
                postalCode: { type: string, minLength: 2, maxLength: 20 }
                country: { type: string, minLength: 2, maxLength: 100 }
                countryCode: { type: [string, "null"], minLength: 2, maxLength: 3, pattern: '^[A-Z]{2,3}$' }
                latitude: { type: [number, "null"], format: double, minimum: -90, maximum: 90 }
                longitude: { type: [number, "null"], format: double, minimum: -180, maximum: 180 }
            UserRole:
              type: string
              enum: [Admin, Manager, Employee, Guest]
        """;

    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class GetUserByIdHandlerImpl : IGetUserByIdHandler
        {
            public System.Threading.Tasks.Task<GetUserByIdResult> ExecuteAsync(GetUserByIdParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(parameters.UserId.ToString() switch
                {
                    "00000000-0000-0000-0000-000000000001" => GetUserByIdResult.Ok(new User(
                        Id: parameters.UserId,
                        FirstName: "Åse & <Bo>",
                        LastName: "Ørsted",
                        Email: "ase@example.com",
                        Phone: "+4512345678",
                        Website: new System.Uri("https://example.com/ase?x=1&y=2"),
                        DateOfBirth: new System.DateOnly(1990, 2, 28),
                        Age: 36,
                        Bio: "Line 1\nLine 2 \"quoted\"",
                        AvatarUrl: new System.Uri("https://example.com/a.png"),
                        IsActive: false,
                        Role: UserRole.Manager,
                        Address: new Address(
                            Street: "Vesterbrogade 1",
                            City: "København",
                            State: "Hovedstaden",
                            PostalCode: "1620",
                            Country: "Denmark",
                            CountryCode: "DK",
                            Latitude: 55.6761,
                            Longitude: -12.5683),
                        CreatedAt: new System.DateTimeOffset(2026, 10, 1, 8, 30, 15, 123, System.TimeSpan.FromHours(2)),
                        UpdatedAt: new System.DateTimeOffset(2026, 10, 1, 9, 0, 0, System.TimeSpan.Zero))),
                    "00000000-0000-0000-0000-000000000002" => GetUserByIdResult.Ok(new User(
                        Id: parameters.UserId,
                        FirstName: "Bo",
                        LastName: "Li",
                        Email: "bo@example.com",
                        Phone: null,
                        Website: null,
                        DateOfBirth: new System.DateOnly(2000, 1, 1),
                        Age: 0,
                        Bio: null,
                        AvatarUrl: null,
                        IsActive: true,
                        Role: UserRole.Guest,
                        Address: new Address(
                            Street: "S",
                            City: "C",
                            State: null,
                            PostalCode: "12",
                            Country: "DK",
                            CountryCode: null,
                            Latitude: null,
                            Longitude: null),
                        CreatedAt: null,
                        UpdatedAt: null)),
                    _ => GetUserByIdResult.NotFound(),
                });
        }

        public static class RichModelRegistration
        {
            public static void Register(IServiceCollection services)
                => services.AddScoped<IGetUserByIdHandler, GetUserByIdHandlerImpl>();
        }
        """";

    /// <summary>
    /// Calls the live host for each user, JSON first and XML first, and describes the model read (camelCase JSON).
    /// </summary>
    private const string ProbeSource = """"
        using System.Net.Http;
        using System.Text.Json;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class RichModelProbe
        {
            private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

            public static async System.Threading.Tasks.Task<List<string>> RunAsync(string baseAddress, bool typed)
            {
                var assembly = typeof(RichModelProbe).Assembly;
                var rows = new List<string>();
                foreach (var id in new[] { "00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000002" })
                {
                    if (typed)
                    {
                        var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("GetUserByIdAsync") is not null);
                        using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
                        var client = Activator.CreateInstance(clientType, http)!;
                        var method = clientType.GetMethod("GetUserByIdAsync")!;
                        var task = (System.Threading.Tasks.Task)method.Invoke(client, [Activator.CreateInstance(method.GetParameters()[0].ParameterType, Guid.Parse(id)), System.Threading.CancellationToken.None])!;
                        await task;
                        var user = task.GetType().GetProperty("Result")!.GetValue(task)!;
                        rows.Add(JsonSerializer.Serialize(user, user.GetType(), Camel));
                    }
                    else
                    {
                        var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                        var services = new ServiceCollection();
                        services.AddHttpClient(httpClientName, c => c.BaseAddress = new Uri(baseAddress));
                        foreach (var register in assembly.GetTypes().SelectMany(t => t.GetMethods()).Where(m => m.IsStatic && m.Name.StartsWith("AddErrorBodies", StringComparison.Ordinal) && m.Name.EndsWith("Endpoints", StringComparison.Ordinal)))
                        {
                            register.Invoke(null, [services]);
                        }

                        using var provider = services.BuildServiceProvider();
                        var endpointType = assembly.GetTypes().Single(t => t.Name == "IGetUserByIdEndpoint");
                        var execute = endpointType.GetMethod("ExecuteAsync")!;
                        var task = (System.Threading.Tasks.Task)execute.Invoke(provider.GetRequiredService(endpointType), [Activator.CreateInstance(execute.GetParameters()[0].ParameterType, Guid.Parse(id)), httpClientName, System.Threading.CancellationToken.None])!;
                        await task;
                        var result = (EndpointResponse)task.GetType().GetProperty("Result")!.GetValue(task)!;
                        var contentType = result.Headers.FirstOrDefault(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault() ?? "(none)";
                        var user = result.GetType().GetProperty("OkContent")!.GetValue(result)!;
                        rows.Add(contentType.Split(';')[0] + " " + JsonSerializer.Serialize(user, user.GetType(), Camel));
                    }
                }

                return rows;
            }
        }
        """";

    /// <summary>
    /// The XML the server writes holds every field, nested and nullable ones included, and the C# EndpointPerOperation
    /// and TypedClient clients read from it the model they read from the JSON.
    /// </summary>
    [Theory]
    [InlineData("EndpointPerOperation")]
    [InlineData("Throw")]
    public async Task Clients_ReadTheSameRichModelFromXmlAsFromJson(
        string client)
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.RichModelRegistration");

        var json = await RunAsync(host, client, preferXml: false);
        var xml = await RunAsync(host, client, preferXml: true);

        Assert.Equal(2, json.Count);
        Assert.Equal(json.Select(r => r.Replace("application/json ", string.Empty, StringComparison.Ordinal)), xml.Select(r => r.Replace("application/xml ", string.Empty, StringComparison.Ordinal)), StringComparer.Ordinal);
        if (client == "EndpointPerOperation")
        {
            Assert.All(json, r => Assert.StartsWith("application/json ", r, StringComparison.Ordinal));
            Assert.All(xml, r => Assert.StartsWith("application/xml ", r, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// The server's XML of the rich model: the nested model as an element, the enum by its value, the dates as
    /// <c>xs:dateTime</c>, a <c>format: date</c> as <c>xs:date</c> (a DateOnly in the generated models), and an absent field for a null one.
    /// </summary>
    [Fact]
    public async Task Server_WritesTheRichModelAsXml()
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.RichModelRegistration");

        var (status, mediaType, body) = await host.SendRawAsync(HttpMethod.Get, "/users/00000000-0000-0000-0000-000000000001", accept: "application/xml");
        var empty = await host.SendRawAsync(HttpMethod.Get, "/users/00000000-0000-0000-0000-000000000002", accept: "application/xml");

        Assert.Equal(200, status);
        Assert.Equal("application/xml; charset=utf-8", mediaType);
        var user = XDocument.Parse(body).Root!;
        Assert.Equal("User", user.Name.LocalName);
        Assert.Equal("Åse & <Bo>", user.Element("firstName")?.Value);

        // A `format: date` is a DateOnly in the generated models: a calendar date in JSON and in XML alike
        Assert.Equal("1990-02-28", user.Element("dateOfBirth")?.Value);
        Assert.Equal("Manager", user.Element("role")?.Value);
        Assert.Equal("København", user.Element("address")?.Element("city")?.Value);
        Assert.Equal("-12.5683", user.Element("address")?.Element("longitude")?.Value);
        Assert.Null(XDocument.Parse(empty.Body).Root!.Element("phone"));
    }

    private static Task<List<string>> RunAsync(
        XmlTestHost host,
        string client,
        bool preferXml)
    {
        var prefer = preferXml ? ", \"preferXml\": true" : string.Empty;
        var marker = client == "EndpointPerOperation"
            ? $$"""{ "generationMode": "EndpointPerOperation", "errorResponseFormat": "ProblemDetails"{{prefer}} }"""
            : $$"""{ "generationMode": "TypedClient", "typedClientResultStyle": "{{client}}"{{prefer}} }""";
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        var usings = sources
            .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
            .Where(ns => ns.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Select(ns => $"global using {ns};");
        sources.Add(("RichModelProbe.cs", string.Join(Environment.NewLine, usings) + Environment.NewLine + ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        return (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.RichModelProbe", throwOnError: true)!
            .GetMethod("RunAsync")!
            .Invoke(null, [host.BaseAddress.ToString(), client != "EndpointPerOperation"])!;
    }
}