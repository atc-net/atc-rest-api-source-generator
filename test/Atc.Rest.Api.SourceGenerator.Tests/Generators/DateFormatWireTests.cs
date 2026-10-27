namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// <c>format: date</c> is a calendar date (RFC 3339 <c>full-date</c>, <c>1990-02-28</c>): a <c>DateOnly</c> in C#
/// and <c>yyyy-MM-dd</c> on the wire - in a body, a path, a query string and a header, on the server and in both
/// C# clients, whatever the culture of the machine. <c>format: date-time</c> stays a <c>DateTimeOffset</c>.
/// </summary>
public sealed class DateFormatWireTests
{
    internal const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /events/{day}:
            get:
              operationId: getEvent
              parameters:
                - name: day
                  in: path
                  required: true
                  schema:
                    type: string
                    format: date
                - name: from
                  in: query
                  schema:
                    type: string
                    format: date
                - name: since
                  in: query
                  schema:
                    type: string
                    format: date-time
                - name: valid-on
                  in: header
                  schema:
                    type: string
                    format: date
              responses:
                '200':
                  description: The event
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Event'
        components:
          schemas:
            Event:
              type: object
              required:
                - id
                - day
                - createdAt
              properties:
                id:
                  type: string
                day:
                  type: string
                  format: date
                createdAt:
                  type: string
                  format: date-time
                endDay:
                  type: string
                  format: date
                validOn:
                  type: string
                  format: date
        """;

    /// <summary>
    /// Echoes every date it was given, so the response shows what the server bound.
    /// </summary>
    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class GetEventHandlerImpl : IGetEventHandler
        {
            public System.Threading.Tasks.Task<GetEventResult> ExecuteAsync(GetEventParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                System.DateOnly day = parameters.Day;
                System.DateOnly? from = parameters.From;
                System.DateOnly? validOn = parameters.ValidOn;
                System.DateTimeOffset createdAt = parameters.Since ?? System.DateTimeOffset.UnixEpoch;
                return System.Threading.Tasks.Task.FromResult(GetEventResult.Ok(new Event(
                    Id: "1",
                    Day: day,
                    CreatedAt: createdAt,
                    EndDay: from,
                    ValidOn: validOn)));
            }
        }

        public static class DateRegistration
        {
            public static void Register(IServiceCollection services)
                => services.AddScoped<IGetEventHandler, GetEventHandlerImpl>();
        }
        """";

    /// <summary>
    /// Calls <c>getEvent</c> through a generated client under a Danish culture (dates are <c>28-02-1990</c> there)
    /// against a stub that records the request line and the header, and answers with an event.
    /// Returns: the path and query, the header value, the C# type of <c>Event.Day</c>, and the day it read.
    /// </summary>
    private const string ClientProbeSource = """"
        using System.Globalization;
        using System.Net;
        using System.Reflection;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class DateProbe
        {
            private static readonly List<string> Wire = [];

            public static async Task<List<string>> RunAsync(bool typedClient)
            {
                var previous = CultureInfo.CurrentCulture;
                CultureInfo.CurrentCulture = new CultureInfo("da-DK");
                try
                {
                    var assembly = typeof(DateProbe).Assembly;
                    var parametersType = assembly.GetTypes().Single(t => t.Name == "GetEventParameters");
                    var ctor = parametersType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
                    var parameters = ctor.Invoke(ctor.GetParameters().Select(p => Value(p.Name!, p.ParameterType)).ToArray());

                    object result;
                    if (typedClient)
                    {
                        var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("GetEventAsync") is not null);
                        var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                        var task = (Task)clientType.GetMethod("GetEventAsync")!.Invoke(client, [parameters, CancellationToken.None])!;
                        await task;
                        result = task.GetType().GetProperty("Result")!.GetValue(task)!;
                    }
                    else
                    {
                        var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                        var services = new ServiceCollection();
                        services
                            .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                            .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                        assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services, null]);
                        using var provider = services.BuildServiceProvider();
                        var endpointType = assembly.GetTypes().Single(t => t.Name == "IGetEventEndpoint");
                        var endpoint = provider.GetRequiredService(endpointType);
                        var task = (Task)endpointType.GetMethod("ExecuteAsync")!.Invoke(endpoint, [parameters, httpClientName, CancellationToken.None])!;
                        await task;
                        var endpointResult = task.GetType().GetProperty("Result")!.GetValue(task)!;
                        result = endpointResult.GetType().GetProperty("OkContent")!.GetValue(endpointResult)!;
                    }

                    var day = result.GetType().GetProperty("Day")!;
                    Wire.Add(day.PropertyType.Name);
                    Wire.Add(((IFormattable)day.GetValue(result)!).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    return Wire;
                }
                finally
                {
                    CultureInfo.CurrentCulture = previous;
                }
            }

            // The values, in whatever type the generated parameter has
            private static object? Value(string name, Type type)
            {
                var (year, month, dayOfMonth) = name switch
                {
                    "Day" => (1990, 2, 28),
                    "From" => (1990, 3, 1),
                    "ValidOn" => (1990, 3, 2),
                    _ => (2026, 10, 1),
                };

                var target = Nullable.GetUnderlyingType(type) ?? type;
                return target == typeof(DateOnly)
                    ? new DateOnly(year, month, dayOfMonth)
                    : new DateTimeOffset(year, month, dayOfMonth, 12, 30, 0, TimeSpan.FromHours(2));
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    Wire.Add(request.RequestUri!.PathAndQuery);
                    Wire.Add(request.Headers.TryGetValues("valid-on", out var values) ? string.Join(",", values) : "(none)");
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"id\":\"1\",\"day\":\"1990-02-28\",\"createdAt\":\"2026-10-01T12:30:00+02:00\"}",
                            System.Text.Encoding.UTF8,
                            "application/json"),
                    });
                }
            }
        }
        """";

    [Fact]
    public async Task Server_BindsAndWritesACalendarDate()
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.DateRegistration");
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/events/1990-02-28?from=1990-03-01&since=2026-10-01T12:30:00%2B02:00");
        request.Headers.Add("valid-on", "1990-03-02");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("1990-02-28", (string?)body["day"]);
        Assert.Equal("1990-03-01", (string?)body["endDay"]);
        Assert.Equal("1990-03-02", (string?)body["validOn"]);
        Assert.Equal("2026-10-01T12:30:00+02:00", (string?)body["createdAt"]);
    }

    [Theory]
    [InlineData("/events/28-02-1990")]
    [InlineData("/events/1990-02-28T00:00:00Z")]
    [InlineData("/events/1990-02-28?from=1990-13-01")]
    public async Task Server_RejectsWhatIsNotACalendarDate(string pathAndQuery)
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.DateRegistration");

        var (status, _, _) = await host.SendRawAsync(HttpMethod.Get, pathAndQuery, accept: null);

        Assert.Equal(400, status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clients_SendAndReadACalendarDate_WhateverTheCulture(
        bool typedClient)
    {
        var marker = typedClient
            ? """{ "generationMode": "TypedClient" }"""
            : ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails");
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("DateProbe.cs", ClientProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var wire = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.DateProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [typedClient])!;

        var pathAndQuery = new Uri("http://stub" + wire[0]);
        var query = System.Web.HttpUtility.ParseQueryString(pathAndQuery.Query);
        Assert.Equal("/events/1990-02-28", pathAndQuery.AbsolutePath);
        Assert.Equal("1990-03-01", query["from"]);
        Assert.Equal(
            new DateTimeOffset(2026, 10, 1, 12, 30, 0, TimeSpan.FromHours(2)),
            DateTimeOffset.Parse(query["since"]!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        Assert.StartsWith("2026-10-01T12:30:00", query["since"], StringComparison.Ordinal);
        Assert.Equal("1990-03-02", wire[1]);
        Assert.Equal("DateOnly", wire[2]);
        Assert.Equal("1990-02-28", wire[3]);
    }
}