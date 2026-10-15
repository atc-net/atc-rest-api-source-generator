namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Runtime proof of per-operation retry (<c>x-retry-*</c>) in the generated C# clients: a stub server
/// counts the attempts each request makes through the generated <c>AddApiResilience</c> handler.
/// </summary>
/// <remarks>
/// Covers the method rule (POST/PATCH are not retried unless the operation opts in), path- and
/// operation-level <c>x-retry-enabled</c> / <c>x-retry-policy</c>, the Retry-After cap (a 429 or 503 asking for
/// longer than the policy timeout goes back to the caller), that "no retry" keeps the timeout, and that the
/// timeout and the retry cover the body: a body that stalls after the headers times out, and one the connection
/// breaks off is retried.
/// <para>
/// Each case's requests carry the case's number in the path, and the server counts per case: an attempt a case gave up
/// on can reach the server after the next case started. The class runs alone - beside the rest of the suite the thread
/// pool starves, and an attempt could time out on its one-second policy before it reached the server.
/// </para>
/// </remarks>
[Collection(nameof(RetryPolicyRuntimeTests))]
[CollectionDefinition(nameof(RetryPolicyRuntimeTests), DisableParallelization = true)]
public class RetryPolicyRuntimeTests
{
    private const string ProjectName = "Retries";

    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Retries
          version: 1.0.0
        x-retry-policy: standard
        x-retry-max-attempts: 2
        x-retry-delay-seconds: 0.01
        x-retry-backoff: constant
        x-retry-use-jitter: false
        x-retry-timeout-seconds: 1
        paths:
          /items/{mode}:
            parameters:
              - $ref: '#/components/parameters/Mode'
            get:
              operationId: getItem
              responses:
                '204':
                  description: No Content
            put:
              operationId: putItem
              responses:
                '204':
                  description: No Content
            delete:
              operationId: deleteItem
              responses:
                '204':
                  description: No Content
            post:
              operationId: postItem
              responses:
                '204':
                  description: No Content
            patch:
              operationId: patchItem
              responses:
                '204':
                  description: No Content
          /disabled/{mode}:
            x-retry-enabled: false
            parameters:
              - $ref: '#/components/parameters/Mode'
            get:
              operationId: getDisabled
              responses:
                '204':
                  description: No Content
          /quiet/{mode}:
            parameters:
              - $ref: '#/components/parameters/Mode'
            get:
              operationId: getQuiet
              x-retry-enabled: false
              x-retry-timeout-seconds: 0.3
              responses:
                '204':
                  description: No Content
          /optin/{mode}:
            parameters:
              - $ref: '#/components/parameters/Mode'
            post:
              operationId: postOptin
              x-retry-enabled: true
              responses:
                '204':
                  description: No Content
          /own/{mode}:
            parameters:
              - $ref: '#/components/parameters/Mode'
            post:
              operationId: postOwn
              x-retry-policy: own
              x-retry-max-attempts: 4
              responses:
                '204':
                  description: No Content
          /wide/{mode}:
            x-retry-policy: wide
            x-retry-max-attempts: 4
            parameters:
              - $ref: '#/components/parameters/Mode'
            get:
              operationId: getWide
              responses:
                '204':
                  description: No Content
            post:
              operationId: postWide
              responses:
                '204':
                  description: No Content
        components:
          parameters:
            Mode:
              name: mode
              in: path
              required: true
              schema:
                type: string
        """;

    private const string OperationMarker = """
        {
          "generationMode": "EndpointPerOperation",
          "validateSpecificationStrategy": "None"
        }
        """;

    private const string TypedMarker = """
        {
          "generationMode": "TypedClient",
          "validateSpecificationStrategy": "None"
        }
        """;

    /// <summary>
    /// (operation, segment, mode, expected attempts, expected outcome fragment or null).
    /// standard = 2 retries (3 attempts); own / wide = 4 retries (5 attempts).
    /// </summary>
    private static readonly (string OperationId, string Segment, string Mode, int Attempts, string? Outcome)[] Cases =
    [
        ("getItem", "Items", "503", 3, null),
        ("getItem", "Items", "429-short", 3, null),
        ("getItem", "Items", "429-long", 1, null),

        // A 503 is answered with Retry-After as a 429 is (RFC 9110, 10.2.3): followed when it fits the attempt's timeout,
        // back to the caller when longer. A Retry-After on another 5xx means nothing, so that one is retried on the policy delay.
        ("getItem", "Items", "503-short", 3, null),
        ("getItem", "Items", "503-long", 1, null),
        ("getItem", "Items", "500-long", 3, null),
        ("postOptin", "Optin", "503-long", 1, null),
        ("putItem", "Items", "503", 3, null),
        ("putItem", "Items", "429-short", 3, null),
        ("deleteItem", "Items", "503", 3, null),
        ("postItem", "Items", "503", 1, null),
        ("postItem", "Items", "429-short", 1, null),
        ("postItem", "Items", "slow", 1, "Timeout"),
        ("patchItem", "Items", "503", 1, null),
        ("getDisabled", "Disabled", "503", 1, null),
        ("getQuiet", "Quiet", "503", 1, null),
        ("getQuiet", "Quiet", "slow", 1, "Timeout"),
        ("postOptin", "Optin", "503", 3, null),
        ("postOptin", "Optin", "429-short", 3, null),
        ("postOptin", "Optin", "429-long", 1, null),
        ("postOwn", "Own", "503", 5, null),
        ("getWide", "Wide", "503", 5, null),
        ("postWide", "Wide", "503", 1, null),

        // The body is read inside the pipeline: a body that stalls after the headers runs into the policy's timeout
        // (not HttpClient.Timeout), and a body the connection breaks off is retried like any failed attempt
        ("getItem", "Items", "stall", 3, "TimeoutRejectedException"),
        ("getQuiet", "Quiet", "stall", 1, "TimeoutRejectedException"),
        ("postItem", "Items", "stall", 1, "TimeoutRejectedException"),
        ("getItem", "Items", "reset", 3, "HttpRequestException"),
        ("postItem", "Items", "reset", 1, "HttpRequestException"),
    ];

    [Theory]
    [InlineData("EndpointPerOperation")]
    [InlineData("TypedClient")]
    public Task EveryOperation_RetriesAsItsSpecificationSays(
        string generationMode)
        => RunCasesAsync(generationMode, Cases.Select(c => (c.OperationId, c.Mode, c.Attempts, c.Outcome)));

    /// <summary>
    /// Every error status code: a GET and an opted-in POST are retried only for the transient ones
    /// (408, 429, every 5xx), a plain POST never.
    /// </summary>
    [Theory]
    [InlineData("EndpointPerOperation")]
    [InlineData("TypedClient")]
    public Task EveryStatusCode_IsRetriedOnlyWhenTransient(
        string generationMode)
        => RunCasesAsync(
            generationMode,
            ErrorResponseBodyRuntimeTests.AllErrorStatusCodes.SelectMany(code =>
            {
                var mode = code.ToString(CultureInfo.InvariantCulture);
                var transient = code is 408 or 429 or >= 500;
                return new (string, string, int, string?)[]
                {
                    ("getItem", mode, transient ? 3 : 1, null),
                    ("postOptin", mode, transient ? 3 : 1, null),
                    ("postItem", mode, 1, null),
                };
            }));

    private static async Task RunCasesAsync(
        string generationMode,
        IEnumerable<(string OperationId, string Mode, int Attempts, string? Outcome)> cases)
    {
        var isOperationMode = string.Equals(generationMode, "EndpointPerOperation", StringComparison.Ordinal);
        var sources = ErrorResponseBodyRuntimeTests.Generate(
            new ApiClientGenerator(),
            ".atc-rest-api-client",
            isOperationMode ? OperationMarker : TypedMarker,
            yaml: Yaml);
        sources.Add(("Probe.cs", BuildProbeSource(isOperationMode)));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var probe = assembly.GetType($"{ProjectName}.RuntimeTest.Probe", throwOnError: true)!.GetMethod("RunAsync")!;

        await using var server = await CountingServer.StartAsync();

        var mismatches = new List<string>();
        var caseNumber = 0;
        foreach (var (operationId, mode, expectedAttempts, expectedOutcome) in cases)
        {
            var caseKey = $"{mode}~{++caseNumber}";
            var outcome = await (Task<string>)probe.Invoke(null, [server.BaseAddress.ToString(), operationId, caseKey])!;
            var attempts = server.AttemptsOf(caseKey);

            if (attempts != expectedAttempts ||
                (expectedOutcome is not null && !outcome.Contains(expectedOutcome, StringComparison.Ordinal)))
            {
                mismatches.Add($"{operationId} {mode}: {attempts} attempt(s), outcome '{outcome}' - expected {expectedAttempts}{(expectedOutcome is null ? string.Empty : $" and '{expectedOutcome}'")}");
            }
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    private static string BuildProbeSource(bool isOperationMode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine(CultureInfo.InvariantCulture, $"using {ProjectName}.Generated;");
        sb.AppendLine(CultureInfo.InvariantCulture, $"using {ProjectName}.Generated.Resilience;");
        foreach (var segment in Cases.Select(c => c.Segment).Distinct(StringComparer.Ordinal))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"using {ProjectName}.Generated.{segment}.Client;");
            if (isOperationMode)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"using {ProjectName}.Generated.{segment}.Endpoints.Interfaces;");
            }
        }

        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"namespace {ProjectName}.RuntimeTest;");
        sb.AppendLine();
        sb.AppendLine("public static class Probe");
        sb.AppendLine("{");
        sb.AppendLine("    public static async Task<string> RunAsync(string baseUrl, string operationId, string mode)");
        sb.AppendLine("    {");
        sb.AppendLine("        var services = new ServiceCollection();");

        // HttpClient.Timeout above every policy's timeout (hang protection): a call that ends with it was not cut by its policy
        sb.AppendLine("        void Configure(HttpClient c)");
        sb.AppendLine("        {");
        sb.AppendLine("            c.BaseAddress = new Uri(baseUrl);");
        sb.AppendLine("            c.Timeout = TimeSpan.FromSeconds(10);");
        sb.AppendLine("        }");
        sb.AppendLine();
        // The document-level policy is the default; the class is called by name with Microsoft's
        // Microsoft.Extensions.DependencyInjection imported too (no CS0104)
        sb.AppendLine("        services");
        sb.AppendLine("            .AddHttpClient(\"retries\", Configure)");
        sb.AppendLine("            .AddApiResilience();");
        sb.AppendLine("        ApiResilienceHttpClientBuilderExtensions.AddApiResilience(services.AddHttpClient(\"by-class-name\"));");
        if (isOperationMode)
        {
            sb.AppendLine("        services.AddHttpClient(Constants.HttpClientName, Configure).AddApiResilience(ResiliencePolicies.Standard);");
            sb.AppendLine(CultureInfo.InvariantCulture, $"        services.Add{ProjectName}Endpoints();");
        }

        sb.AppendLine("        using var provider = services.BuildServiceProvider();");
        sb.AppendLine("        var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(\"retries\");");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            switch (operationId)");
        sb.AppendLine("            {");
        foreach (var (operationId, segment) in Cases.Select(c => (c.OperationId, c.Segment)).Distinct())
        {
            var pascal = char.ToUpperInvariant(operationId[0]) + operationId[1..];
            sb.AppendLine(CultureInfo.InvariantCulture, $"                case \"{operationId}\":");
            if (isOperationMode)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"                    var {operationId}Result = await provider.GetRequiredService<I{pascal}Endpoint>().ExecuteAsync(new {pascal}Parameters(mode));");
                sb.AppendLine(CultureInfo.InvariantCulture, $"                    return ((int){operationId}Result.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);");
            }
            else
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"                    await new {segment}Client(http).{pascal}Async(new {pascal}Parameters(mode));");
                sb.AppendLine("                    return \"ok\";");
            }
        }

        sb.AppendLine("                default:");
        sb.AppendLine("                    throw new ArgumentOutOfRangeException(nameof(operationId), operationId, null);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            return (ex is HttpRequestException ? \"HttpRequestException \" : string.Empty) + ex.GetType().Name + \": \" + ex.Message;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// A stub server that counts requests per case and answers by the last path segment, "{mode}~{case}" (a status
    /// code, "429-short" / "503-short" with Retry-After: 0, "429-long" / "503-long" / "500-long" with Retry-After: 120, "slow" after two seconds, "stall"
    /// headers and part of the body and then nothing, "reset" headers and part of the body and then a connection reset).
    /// </summary>
    private sealed class CountingServer : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly ConcurrentDictionary<string, int> attempts = new(StringComparer.Ordinal);

        private CountingServer(WebApplication app)
        {
            this.app = app;
            BaseAddress = new Uri(app.Urls.First());
        }

        public Uri BaseAddress { get; }

        public int AttemptsOf(string caseKey)
            => attempts.GetValueOrDefault(caseKey);

        public static async Task<CountingServer> StartAsync()
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            var webApp = builder.Build();
            webApp.Urls.Add("http://127.0.0.1:0");

            CountingServer? server = null;
            webApp.Run(async context =>
            {
                var caseKey = context.Request.Path.Value!.Split('/')[^1];
                server!.attempts.AddOrUpdate(caseKey, 1, (_, count) => count + 1);
                var mode = caseKey.Split('~')[0];
                switch (mode)
                {
                    case "503":
                        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                        break;
                    case "429-short":
                        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                        context.Response.Headers.RetryAfter = "0";
                        break;
                    case "429-long":
                        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                        context.Response.Headers.RetryAfter = "120";
                        break;
                    case "503-short":
                        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                        context.Response.Headers.RetryAfter = "0";
                        break;
                    case "503-long":
                        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                        context.Response.Headers.RetryAfter = "120";
                        break;
                    case "500-long":
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        context.Response.Headers.RetryAfter = "120";
                        break;
                    case "slow":
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(2), context.RequestAborted);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }

                        context.Response.StatusCode = StatusCodes.Status204NoContent;
                        break;
                    case "stall" or "reset":
                        // A 200 that promises 64 bytes and sends 12; then "reset" aborts the connection, "stall" holds it
                        // until the client goes (thirty seconds at most)
                        context.Response.StatusCode = StatusCodes.Status200OK;
                        context.Response.ContentType = "application/json";
                        context.Response.ContentLength = 64;
                        await context.Response.WriteAsync("{\"name\":\"cut", context.RequestAborted);
                        await context.Response.Body.FlushAsync(context.RequestAborted);
                        if (mode == "stall")
                        {
                            try
                            {
                                await Task.Delay(TimeSpan.FromSeconds(30), context.RequestAborted);
                            }
                            catch (OperationCanceledException)
                            {
                                // The client went
                            }
                        }

                        context.Abort();
                        break;
                    default:
                        // A status code sweep: "418" answers 418; a 429 comes with Retry-After: 0.
                        if (int.TryParse(mode, NumberStyles.None, CultureInfo.InvariantCulture, out var status))
                        {
                            context.Response.StatusCode = status;
                            if (status == StatusCodes.Status429TooManyRequests)
                            {
                                context.Response.Headers.RetryAfter = "0";
                            }
                        }
                        else
                        {
                            context.Response.StatusCode = StatusCodes.Status204NoContent;
                        }

                        break;
                }
            });

            await webApp.StartAsync(TestContext.Current.CancellationToken);
            server = new CountingServer(webApp);
            return server;
        }

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync(CancellationToken.None);
            await app.DisposeAsync();
        }
    }
}