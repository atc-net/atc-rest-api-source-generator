namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// What the typed client's Throw style throws for an error response: the generated <c>ApiException</c> - an
/// <c>HttpRequestException</c>, so a catch of that still catches it - carrying what the message alone could not: the
/// response's headers (a 429's <c>Retry-After</c>, a correlation id), its body as text, and <c>RetryAfter</c> as a
/// wait.
/// </summary>
public sealed class TypedClientApiExceptionTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /pets/{id}:
            get:
              operationId: getPet
              parameters:
                - name: id
                  in: path
                  required: true
                  schema: { type: integer }
              responses:
                '200':
                  description: The pet
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Pet' }
                '404':
                  description: Not found
        components:
          schemas:
            Pet:
              type: object
              required: [name]
              properties:
                name: { type: string }
        """;

    /// <summary>
    /// Calls getPet against a stub answering with the given status, body and headers, and describes what was thrown:
    /// the exception's type, whether a catch of HttpRequestException caught it, and the ApiException's members.
    /// </summary>
    private const string ProbeSource = """"
        using System.Net;
        using System.Text.Json.Nodes;
        using ErrorBodies.Generated;

        namespace ErrorBodies.RuntimeTest;

        public static class ApiExceptionProbe
        {
            public static async Task<string> RunAsync(int status, string body, string[] headers)
            {
                var assembly = typeof(ApiExceptionProbe).Assembly;
                var ctor = assembly.GetTypes().Single(t => t.Name == "GetPetParameters").GetConstructors().Single();
                var parameters = ctor.Invoke([Convert.ChangeType(1, ctor.GetParameters()[0].ParameterType, System.Globalization.CultureInfo.InvariantCulture)]);
                var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("GetPetAsync") is not null);
                var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler(status, body, headers)) { BaseAddress = new Uri("http://stub/") })!;
                try
                {
                    await (Task)clientType.GetMethod("GetPetAsync")!.Invoke(client, [parameters, CancellationToken.None])!;
                    return new JsonObject { ["threw"] = false }.ToJsonString();
                }
                catch (HttpRequestException caught)
                {
                    var result = new JsonObject
                    {
                        ["threw"] = true,
                        ["type"] = caught.GetType().FullName,
                        ["message"] = caught.Message,
                        ["status"] = (int?)caught.StatusCode,
                    };
                    if (caught is ApiException ex)
                    {
                        result["content"] = ex.Content;
                        result["retryAfterSeconds"] = ex.RetryAfter?.TotalSeconds;
                        result["headers"] = new JsonObject(ex.Headers.Select(h => new KeyValuePair<string, JsonNode?>(h.Key, string.Join(",", h.Value))));
                        result["retryAfterHeader"] = ex.Headers.TryGetValue("retry-after", out var values) ? string.Join(",", values) : null;
                    }

                    return result.ToJsonString();
                }
            }

            // What a consumer's own test does: build the exception without a response
            public static string Construct()
            {
                var headers = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase) { ["X-Request-Id"] = ["abc"] };
                var ex = new ApiException("the message", HttpStatusCode.Conflict, "the body", headers, TimeSpan.FromSeconds(5));
                HttpRequestException asBase = ex;
                return $"{ex.Message}|{(int?)asBase.StatusCode}|{ex.Content}|{string.Join(",", ex.Headers["x-request-id"])}|{ex.RetryAfter?.TotalSeconds}";
            }

            private sealed class StubHandler(int status, string body, string[] headers) : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var response = new HttpResponseMessage((HttpStatusCode)status)
                    {
                        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/problem+json"),
                    };
                    foreach (var header in headers)
                    {
                        var separator = header.IndexOf(':', StringComparison.Ordinal);
                        response.Headers.TryAddWithoutValidation(header[..separator], header[(separator + 1)..].Trim());
                    }

                    return Task.FromResult(response);
                }
            }
        }
        """";

    public static TheoryData<int> ErrorStatusCodes()
        => [.. ErrorResponseBodyRuntimeTests.AllErrorStatusCodes];

    /// <summary>
    /// Every error status: an ApiException with the status, the body and the headers - and the message the Throw style
    /// has always had.
    /// </summary>
    [Theory]
    [MemberData(nameof(ErrorStatusCodes))]
    public async Task AnErrorResponse_IsAnApiExceptionWithItsStatusBodyAndHeaders(
        int status)
    {
        var body = $$"""{"title":"Nope","status":{{status}}}""";

        var thrown = await RunAsync(status, body, "X-Contoso-Trace: abc-123");

        Assert.True((bool)thrown["threw"]!);
        Assert.Equal("ErrorBodies.Generated.ApiException", (string?)thrown["type"]);
        Assert.Equal(status, (int?)thrown["status"]);
        Assert.Equal(body, (string?)thrown["content"]);
        Assert.Equal("abc-123", (string?)thrown["headers"]!["X-Contoso-Trace"]);
        Assert.StartsWith("application/problem+json", (string?)thrown["headers"]!["Content-Type"], StringComparison.Ordinal);
        Assert.StartsWith($"HTTP {status} (", (string?)thrown["message"], StringComparison.Ordinal);
        Assert.EndsWith("): " + body, (string?)thrown["message"], StringComparison.Ordinal);
        Assert.Null(thrown["retryAfterSeconds"]);
    }

    /// <summary>
    /// A 429's (or a 503's) Retry-After, as the header and as a wait: the seconds it names, or the time left until
    /// the date it names - never negative.
    /// </summary>
    [Theory]
    [InlineData(429)]
    [InlineData(503)]
    public async Task RetryAfter_InSeconds_IsThatWait(int status)
    {
        var thrown = await RunAsync(status, "{}", "Retry-After: 120");

        Assert.Equal("120", (string?)thrown["retryAfterHeader"]);
        Assert.Equal(120d, (double?)thrown["retryAfterSeconds"]);
    }

    [Fact]
    public async Task RetryAfter_AsADate_IsTheTimeLeft()
    {
        var future = await RunAsync(429, "{}", "Retry-After: " + DateTimeOffset.UtcNow.AddSeconds(90).ToString("R", CultureInfo.InvariantCulture));
        var past = await RunAsync(429, "{}", "Retry-After: " + DateTimeOffset.UtcNow.AddSeconds(-90).ToString("R", CultureInfo.InvariantCulture));

        Assert.InRange((double)future["retryAfterSeconds"]!, 60d, 91d);
        Assert.Equal(0d, (double?)past["retryAfterSeconds"]);
    }

    [Fact]
    public async Task RetryAfter_ThatIsNeitherSecondsNorADate_IsNoWait_AndTheHeaderIsKept()
    {
        var thrown = await RunAsync(429, "{}", "Retry-After: soon");

        Assert.Null(thrown["retryAfterSeconds"]);
        Assert.Equal("soon", (string?)thrown["retryAfterHeader"]);
    }

    /// <summary>
    /// A proxy's large error page is not read whole: the body is its first 4,096 bytes and an ellipsis, as in the
    /// message.
    /// </summary>
    [Fact]
    public async Task ALongBody_IsCutAt4KilobytesInTheContentAsInTheMessage()
    {
        var thrown = await RunAsync(502, new string('x', 10_000));

        Assert.Equal(new string('x', 4096) + "…", (string?)thrown["content"]);
        Assert.EndsWith("): " + new string('x', 4096) + "…", (string?)thrown["message"], StringComparison.Ordinal);
    }

    /// <summary>
    /// A consumer can build one in a test of its own, and reads it through the base type's StatusCode.
    /// </summary>
    [Fact]
    public void AnApiException_CanBeConstructedByAConsumer()
    {
        var assembly = Compile("Throw", Yaml);

        var described = (string)assembly.GetType("ErrorBodies.RuntimeTest.ApiExceptionProbe", throwOnError: true)!.GetMethod("Construct")!.Invoke(null, [])!;

        Assert.Equal("the message|409|the body|abc|5", described);
    }

    /// <summary>
    /// A specification with a schema of its own named ApiException keeps its model: the exception takes another name.
    /// </summary>
    [Fact]
    public void ASchemaNamedApiException_KeepsItsName_AndTheExceptionIsApiResponseException()
    {
        var yaml = Yaml
            .Replace("schema: { $ref: '#/components/schemas/Pet' }", "schema: { $ref: '#/components/schemas/ApiException' }", StringComparison.Ordinal)
            .Replace("    Pet:", "    ApiException:", StringComparison.Ordinal);

        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", Marker("Throw"), includeMinimalApi: false, yaml);
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var exception = Assert.Single(assembly.GetTypes(), t => typeof(HttpRequestException).IsAssignableFrom(t));
        Assert.Equal("ErrorBodies.Generated.ApiResponseException", exception.FullName);
        Assert.Contains(assembly.GetTypes(), t => t.Name == "ApiException" && !typeof(Exception).IsAssignableFrom(t));
    }

    private static string Marker(string resultStyle)
        => $$"""{ "generationMode": "TypedClient", "typedClientResultStyle": "{{resultStyle}}", "validateSpecificationStrategy": "Standard" }""";

    private static System.Reflection.Assembly Compile(
        string resultStyle,
        string yaml)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", Marker(resultStyle), includeMinimalApi: false, yaml);
        sources.Add(("ApiExceptionProbe.cs", ProbeSource));
        return CompilationVerificationHarness.EmitAndLoad(sources);
    }

    private static async Task<JsonObject> RunAsync(
        int status,
        string body,
        params string[] headers)
    {
        var run = (Task<string>)Compile("Throw", Yaml).GetType("ErrorBodies.RuntimeTest.ApiExceptionProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [status, body, headers])!;
        return JsonNode.Parse(await run)!.AsObject();
    }
}