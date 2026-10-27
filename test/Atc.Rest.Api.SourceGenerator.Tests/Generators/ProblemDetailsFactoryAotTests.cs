namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The <c>ProblemDetailsFactory</c> of an endpoint-per-operation client reads the ProblemDetails types it generates
/// itself, a JSON string body and the extension members of a problem+xml body without reflection-based
/// serialization, so they are trim- and AOT-safe with nothing for the consumer to supply - and read exactly as
/// before (Web defaults: names in any case, a number in a string; a member of the wrong type gives no problem body).
/// </summary>
public sealed class ProblemDetailsFactoryAotTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /notes:
            get:
              operationId: getNote
              responses:
                '200':
                  description: OK
                  content:
                    application/xml:
                      schema:
                        $ref: '#/components/schemas/Note'
                '400':
                  description: Bad request
                  content:
                    application/problem+json:
                      schema:
                        $ref: '#/components/schemas/Note'
        components:
          schemas:
            Note:
              type: object
              properties:
                title: { type: string }
        """;

    private static readonly Lazy<List<(string HintName, string Source)>> Sources = new(
        () => ErrorResponseBodyRuntimeTests.Generate(
            new ApiClientGenerator(),
            ".atc-rest-api-client",
            ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"),
            includeMinimalApi: false,
            Yaml));

    private static readonly Lazy<Type> Factory = new(
        () => CompilationVerificationHarness.EmitAndLoad(Sources.Value).GetType("ErrorBodies.Generated.ProblemDetailsFactory", throwOnError: true)!);

    // Only the read of the consumer's own error model (ModelFromContent) is left: its metadata comes with the
    // JSON serializer context the generator emits for the consumer's types
    [Fact]
    public async Task TheFactory_ReadsJsonWithoutReflection_ExceptTheConsumersErrorModel()
    {
        var factory = Sources.Value.Single(s => s.HintName.EndsWith(".ProblemDetailsFactory.g.cs", StringComparison.Ordinal));
        var lines = factory.Source.Split('\n');

        var problems = (await AotAnalyzerHarness.AnalyzeAsync(Sources.Value, includeMinimalApi: false))
            .Where(p => p.Contains("ProblemDetailsFactory.g.cs(", StringComparison.Ordinal))
            .Where(p => !lines[LineOf(p) - 1].Contains("LenientOptions)", StringComparison.Ordinal))
            .ToList();

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));

        static int LineOf(string problem)
        {
            var start = problem.IndexOf(".g.cs(", StringComparison.Ordinal) + ".g.cs(".Length;
            return int.Parse(problem[start..problem.IndexOf(',', start)], CultureInfo.InvariantCulture);
        }
    }

    [Theory]
    [InlineData("""{"type":"t","title":"T","status":400,"detail":"d","instance":"i"}""", "t|T|400|d|i")]
    [InlineData("""{"TYPE":"t","Title":"T","Status":"409"}""", "t|T|409||")]
    [InlineData("""  {"title":"T"}""", "|T|404||")]
    [InlineData("""{"title":null,"status":null}""", "||404||")]
    [InlineData("""{"title":"\u00e6"}""", "|æ|404||")]
    [InlineData("""{"title":"a","title":"b"}""", "|b|404||")]
    [InlineData("""{"title":"T","traceId":"abc","n":1.50,"o":{"a":[1, 2]},"errors":{"x":["y"]}}""", """|T|404|||errors={"x":["y"]};n=1.50;o={"a":[1, 2]};traceId="abc" """)]
    [InlineData("""{"title":5}""", """https://httpstatuses.io/404|Not Found|404|{"title":5}|""")]
    [InlineData("""{"status":404.5}""", """https://httpstatuses.io/404|Not Found|404|{"status":404.5}|""")]
    [InlineData("""{"status":"abc"}""", """https://httpstatuses.io/404|Not Found|404|{"status":"abc"}|""")]
    [InlineData("""{"title":"T"} x""", """https://httpstatuses.io/404|Not Found|404|{"title":"T"} x|""")]
    [InlineData("""{/*c*/"title":"T"}""", """https://httpstatuses.io/404|Not Found|404|{/*c*/"title":"T"}|""")]
    [InlineData("""{"title":""", """https://httpstatuses.io/404|Not Found|404|{"title":|""")]
    [InlineData("""[1]""", "https://httpstatuses.io/404|Not Found|404|[1]|")]
    [InlineData("\"not found\"", "https://httpstatuses.io/404|Not Found|404|not found|")]
    public void FromContent_ReadsAProblemBody_OrMakesTheBodyTheDetail(
        string body,
        string expected)
        => Assert.Equal(expected.TrimEnd(), Render(Invoke("FromContent", HttpStatusCode.NotFound, body)));

    [Theory]
    [InlineData("""{"title":"V","errors":{"name":["required","short"],"":["root"]}}""", "|V|400|||!name=[required,short];=[root]")]
    [InlineData("""{"Errors":{"a":["x"]}}""", "||400|||!a=[x]")]
    [InlineData("""{"errors":null}""", "||400||")]
    [InlineData("""{"errors":{"a":null,"b":[null]}}""", "||400|||!a=null;b=[null]")]
    [InlineData("""{"errors":{"a":"x"}}""", """https://httpstatuses.io/400|Bad Request|400|{"errors":{"a":"x"}}|""")]
    [InlineData("""{"errors":{"a":[1]}}""", """https://httpstatuses.io/400|Bad Request|400|{"errors":{"a":[1]}}|""")]
    [InlineData("""{"errors":["a"]}""", """https://httpstatuses.io/400|Bad Request|400|{"errors":["a"]}|""")]
    public void ValidationFromContent_ReadsTheFieldErrors_OrMakesTheBodyTheDetail(
        string body,
        string expected)
        => Assert.Equal(expected, Render(Invoke("ValidationFromContent", HttpStatusCode.BadRequest, body)));

    [Theory]
    [InlineData("""{"errors":{"a":["x","y"]}}""", "a=[x,y]")]
    [InlineData("""{"title":"T"}""", "(none)")]
    [InlineData("""{"errors":{"a":"x"}}""", "(none)")]
    [InlineData("not json", "(none)")]
    public void ValidationErrorsFromContent_ReadsTheFieldErrorsOnly(
        string body,
        string expected)
        => Assert.Equal(
            expected,
            Factory.Value.GetMethod("ValidationErrorsFromContent")!.Invoke(null, [body]) is Dictionary<string, string[]> errors
                ? RenderErrors(errors)
                : "(none)");

    // The members of a problem+xml body that are no ProblemDetails member are its extensions, each a JSON string
    [Fact]
    public void WithXmlProblemBody_KeepsTheOtherMembersAsJsonStrings()
    {
        var response = new Atc.Rest.Client.EndpointResponse(
            false,
            HttpStatusCode.Conflict,
            "<problem xmlns=\"urn:ietf:rfc:7807\"><title>T</title><traceId>æ \"1\" &lt;x&gt;</traceId><count>3</count></problem>",
            null,
            new Dictionary<string, IEnumerable<string>>(StringComparer.Ordinal) { ["Content-Type"] = ["application/problem+xml"] });

        var read = (Atc.Rest.Client.EndpointResponse)Factory.Value.GetMethod("WithXmlProblemBody")!.Invoke(null, [response])!;

        // Escaped by the default encoder, as the serializer writes a string
        Assert.Equal(
            $"|T||||count=\"3\";traceId=\"{JsonEncodedText.Encode("æ \"1\" <x>")}\"",
            Render(read.ContentObject!));
        Assert.Contains("\\u00E6", Render(read.ContentObject!), StringComparison.Ordinal);
    }

    private static object Invoke(
        string method,
        HttpStatusCode statusCode,
        string body)
        => Factory.Value.GetMethod(method)!.Invoke(null, [statusCode, body])!;

    // type|title|status|detail|instance, then |extensions (name=raw JSON) and |!errors when there are any
    private static string Render(object problem)
    {
        string? Get(string name)
            => problem.GetType().GetProperty(name)!.GetValue(problem)?.ToString();

        var text = $"{Get("Type")}|{Get("Title")}|{Get("Status")}|{Get("Detail")}|{Get("Instance")}";
        if (problem.GetType().GetProperty("Extensions")!.GetValue(problem) is Dictionary<string, JsonElement> extensions)
        {
            text += "|" + string.Join(";", extensions.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => $"{e.Key}={e.Value.GetRawText()}"));
        }

        if (problem.GetType().GetProperty("Errors")?.GetValue(problem) is Dictionary<string, string[]> errors)
        {
            text += "|!" + RenderErrors(errors);
        }

        return text;
    }

    private static string RenderErrors(Dictionary<string, string[]> errors)
        => string.Join(";", errors.Select(e => $"{e.Key}={(e.Value is null ? "null" : $"[{string.Join(",", e.Value.Select(v => v ?? "null"))}]")}"));
}