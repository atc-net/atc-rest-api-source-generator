namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A query parameter's name is sent URI-escaped, as its value is: a name with a character reserved in a query
/// (JSON:API's <c>page[size]</c>, or <c>a&amp;b</c>, <c>a=b</c>, a space, <c>#</c>) stays one key - for a scalar,
/// an enum, an array sent as repeated keys and an array joined into one value, from both C# clients (the
/// EndpointPerOperation client through Atc.Rest.Client's request builder).
/// </summary>
public sealed class QueryParameterKeyTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /items:
            get:
              operationId: listItems
              parameters:
                - name: page[size]
                  in: query
                  schema: { type: integer }
                - name: 'a&b=c d#e'
                  in: query
                  schema: { type: string }
                - name: sort[by]
                  in: query
                  schema: { $ref: '#/components/schemas/SortBy' }
                - name: ids[]
                  in: query
                  schema: { type: array, items: { type: integer } }
                - name: fields[items]
                  in: query
                  explode: false
                  schema: { type: array, items: { type: string } }
                - name: limit
                  in: query
                  schema: { type: integer }
              responses:
                '204':
                  description: Listed
        components:
          schemas:
            SortBy:
              type: string
              enum: [name, created-at]
        """;

    private const string ProbeSource = """"
        using System.Net;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class QueryProbe
        {
            private static readonly List<string> Wire = [];

            public static async Task<List<string>> RunAsync(bool typedClient)
            {
                var assembly = typeof(QueryProbe).Assembly;
                var sortType = assembly.GetTypes().Single(t => t.Name == "SortBy");
                var createdAt = sortType.IsEnum
                    ? Enum.Parse(sortType, "CreatedAt")
                    : (sortType.GetProperty("CreatedAt")?.GetValue(null) ?? sortType.GetField("CreatedAt")!.GetValue(null));
                var parameters = Create(
                    assembly.GetTypes().Single(t => t.Name == "ListItemsParameters"),
                    new Dictionary<string, object?>
                    {
                        ["pagesize"] = 10,
                        ["abcde"] = "x y",
                        ["sortby"] = createdAt,
                        ["ids"] = new List<int> { 1, 2 },
                        ["fieldsitems"] = new List<string> { "a", "b" },
                        ["limit"] = 5,
                    });
                if (typedClient)
                {
                    var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("ListItemsAsync") is not null);
                    var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                    await (Task)clientType.GetMethod("ListItemsAsync")!.Invoke(client, [parameters, CancellationToken.None])!;
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
                    var endpointType = assembly.GetTypes().Single(t => t.Name == "IListItemsEndpoint");
                    var endpoint = provider.GetRequiredService(endpointType);
                    await (Task)endpointType.GetMethod("ExecuteAsync")!.Invoke(endpoint, [parameters, httpClientName, CancellationToken.None])!;
                }

                return Wire;
            }

            // A record through its constructor, by parameter name (letters and digits, case-insensitive)
            private static object Create(Type type, Dictionary<string, object?> values)
            {
                var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
                var arguments = constructor.GetParameters()
                    .Select(p => values.TryGetValue(new string(p.Name!.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant(), out var value)
                        ? value
                        : throw new InvalidOperationException($"No value for '{p.Name}'"))
                    .ToArray();
                return constructor.Invoke(arguments);
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    Wire.Add(request.RequestUri!.PathAndQuery);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
                }
            }
        }
        """";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clients_SendEachName_Escaped(bool typedClient)
    {
        var marker = typedClient
            ? """{ "generationMode": "TypedClient" }"""
            : ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails");
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("QueryProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var wire = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.QueryProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [typedClient])!;

        var query = Assert.Single(wire).Split('?')[1].Split('&').Order(StringComparer.Ordinal);
        Assert.Equal(
            [
                "a%26b%3Dc%20d%23e=x%20y",
                "fields%5Bitems%5D=a%2Cb",
                "ids%5B%5D=1",
                "ids%5B%5D=2",
                "limit=5",
                "page%5Bsize%5D=10",
                "sort%5Bby%5D=created-at",
            ],
            query,
            StringComparer.Ordinal);
    }
}