// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// End-to-end enum-casing matrix: the generated server runs on Kestrel with handlers that answer with the enum values
/// they were given, and a raw HTTP client sends each listed value in another casing - lower case, upper case, the first
/// letter flipped - as a caller typing it by hand would (Postman, curl). Every place a value arrives is a column: the
/// path, the query (alone, as a list, in a <c>deepObject</c>), a header, a cookie, and a JSON, URL-encoded, multipart and
/// XML body. A listed value is read in any casing and reaches the handler as the specification writes it; a casing two
/// listed values share (<c>active</c>, <c>Active</c> sent as <c>ACTIVE</c>) names neither and is a 400, and an exact
/// match always wins.
/// </summary>
public partial class ErrorResponseBodyRuntimeTests
{
    /// <summary>
    /// The servers the matrix runs against: the key in the report, the marker, the host.
    /// </summary>
    internal static readonly (string Key, string Title)[] EnumCasingHosts =
    [
        ("enumCasing", "Atc.Rest.MinimalApi host"),
        ("enumCasingPlain", "MapEndpoints() host"),
        ("enumCasingRdg", "Request Delegate Generator"),
        ("enumCasingConverter", "host-wide JsonStringEnumConverter"),
        ("enumCasingExtensible", "extensibleEnums: true"),
    ];

    /// <summary>
    /// The cells each host is expected to fail, with the reason: the ratchet of the enum-casing matrix.
    /// </summary>
    internal static readonly Dictionary<string, Dictionary<string, string>> KnownEnumCasingFailures = new(StringComparer.Ordinal)
    {
        ["enumCasingPlain"] = new(StringComparer.Ordinal)
        {
            ["two values in one casing each · ACTIVE [xml]"] = "Not casing: on the MapEndpoints() host every XML body the generated reader rejects is a 500 - UseExceptionHandler() answers the BadHttpRequestException(400) it throws with 500, as for a value the specification does not list. The Atc.Rest.MinimalApi host answers 400.",
        },
    };

    /// <summary>
    /// What the enum-casing matrix does not exercise.
    /// </summary>
    internal static readonly string[] EnumCasingNotExercised =
    [
        "An integer enum: it is a number on the wire, so there is no casing.",
        "A dictionary keyed by an enum: OpenAPI 3.0 has no enum-typed property names.",
        "The C# member name of a value whose wire form is no C# name (OutOfStock for out-of-stock): that is the marker's readEnumMemberNames, not casing.",
    ];

    // The places a value arrives: the column id, its group and its label
    private static readonly (string Id, string Group, string Label)[] EnumCasingLocations =
    [
        ("path", "Parameter", "path"),
        ("query", "Parameter", "query"),
        ("query-list", "Parameter", "query list"),
        ("query-object", "Parameter", "query deepObject"),
        ("header", "Parameter", "header"),
        ("cookie", "Parameter", "cookie"),
        ("json", "Body", "JSON"),
        ("form", "Body", "URL-encoded form"),
        ("multipart", "Body", "multipart"),
        ("xml", "Body", "XML"),
    ];

    // The enum shapes: the property (and parameter) name, the schema, how it reads, its values, the values to send
    private static readonly (string Key, string Schema, string Title, string[] Values, string[] Targets)[] EnumCasingShapes =
    [
        ("camel", "Camel", "camelCase", ["available", "outOfStock"], ["outOfStock"]),
        ("pascal", "Pascal", "PascalCase", ["Available", "OutOfStock"], ["OutOfStock"]),
        ("kebab", "Kebab", "kebab-case", ["available", "out-of-stock"], ["out-of-stock"]),
        ("snake", "Snake", "snake_case", ["available", "out_of_stock"], ["out_of_stock"]),
        ("upper", "Upper", "UPPER_SNAKE_CASE", ["AVAILABLE", "OUT_OF_STOCK"], ["OUT_OF_STOCK"]),
        ("twin", "Twin", "two values in one casing each", ["active", "Active", "archived"], ["active", "Active", "archived"]),
    ];

    [Theory]
    [InlineData("enumCasing")]
    [InlineData("enumCasingPlain")]
    [InlineData("enumCasingRdg")]
    [InlineData("enumCasingConverter")]
    [InlineData("enumCasingExtensible")]
    public async Task EndToEnd_EveryEnumValueBindsInAnyCasing(string hostKey)
    {
        var matrix = await CollectEnumCasingMatrixAsync(hostKey);
        if (Environment.GetEnvironmentVariable("ENUMCASING_E2E_DIR") is { Length: > 0 } dumpDirectory)
        {
            await File.WriteAllTextAsync(Path.Combine(dumpDirectory, hostKey + ".json"), matrix.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), TestContext.Current.CancellationToken);
        }

        var known = KnownEnumCasingFailures.GetValueOrDefault(hostKey) ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var failures = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in matrix["cases"]!.AsArray())
        {
            foreach (var cell in item!["clients"]!.AsObject())
            {
                if ((string?)cell.Value!["verdict"] != VerdictPass)
                {
                    failures[$"{(string)item["id"]!} [{cell.Key}]"] = (string)cell.Value["why"]!;
                }
            }
        }

        var unexpected = failures.Where(f => !known.ContainsKey(f.Key)).ToList();
        var fixedCells = known.Keys.Where(k => !failures.ContainsKey(k)).Order(StringComparer.Ordinal).ToList();
        Assert.True(unexpected.Count == 0, $"{unexpected.Count} failing cells:\n" + string.Join("\n", unexpected.Take(150).Select(f => $"{f.Key}: {f.Value}")));
        Assert.True(fixedCells.Count == 0, $"{fixedCells.Count} cells pass now. Remove them from {nameof(KnownEnumCasingFailures)}:\n" + string.Join("\n", fixedCells));
    }

    /// <summary>
    /// The verdict is not blind: another value, the value in another casing and a 400 for a value that names one each
    /// fail; an ambiguous casing must be a 400.
    /// </summary>
    [Fact]
    public void EnumCasingVerdicts_FailAWrongValueOrStatus()
    {
        Assert.Equal(VerdictPass, (string?)EnumCasingVerdict("camel", "outOfStock", 200, """{"camel":"outOfStock"}""")["verdict"]);
        Assert.Equal(VerdictFail, (string?)EnumCasingVerdict("camel", "outOfStock", 200, """{"camel":"OUTOFSTOCK"}""")["verdict"]);
        Assert.Equal(VerdictFail, (string?)EnumCasingVerdict("camel", "outOfStock", 200, """{"camel":"available"}""")["verdict"]);
        Assert.Equal(VerdictFail, (string?)EnumCasingVerdict("camel", "outOfStock", 400, """{"status":400}""")["verdict"]);
        Assert.Equal(VerdictPass, (string?)EnumCasingVerdict("twin", null, 400, """{"status":400}""")["verdict"]);
        Assert.Equal(VerdictFail, (string?)EnumCasingVerdict("twin", null, 200, """{"twin":"active"}""")["verdict"]);

        Assert.Equal("outOfStock", EnumCasingExpected(["available", "outOfStock"], "OUTOFSTOCK"));
        Assert.Equal("Active", EnumCasingExpected(["active", "Active"], "Active"));
        Assert.Null(EnumCasingExpected(["active", "Active"], "ACTIVE"));
        Assert.Equal(["out-of-stock", "OUT-OF-STOCK", "Out-of-stock"], EnumCasingVariants("out-of-stock"));
    }

    internal static string EnumCasingYaml()
    {
        var shapes = EnumCasingShapes;
        var sb = new StringBuilder();
        sb.AppendLine("openapi: 3.0.3");
        sb.AppendLine("info:");
        sb.AppendLine("  title: ErrorBodies");
        sb.AppendLine("  version: 1.0.0");
        sb.AppendLine("paths:");

        void Operation(
            string path,
            string method,
            string operationId,
            IEnumerable<string> parameters,
            string? bodyMediaType)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {path}:");
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {method}:");
            sb.AppendLine(CultureInfo.InvariantCulture, $"      operationId: {operationId}");
            var list = parameters.ToList();
            if (list.Count > 0)
            {
                sb.AppendLine("      parameters:");
                foreach (var p in list)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"        - {p}");
                }
            }

            if (bodyMediaType is not null)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"      requestBody: {{ required: true, content: {{ {bodyMediaType}: {{ schema: {{ $ref: '#/components/schemas/Echo' }} }} }} }}");
            }

            sb.AppendLine("      responses:");
            sb.AppendLine("        '200': { description: What was bound, content: { application/json: { schema: { $ref: '#/components/schemas/Echo' } } } }");
        }

        Operation("/path/" + string.Join('/', shapes.Select(s => "{" + s.Key + "}")), "get", "byPath", shapes.Select(s => $"{{ name: {s.Key}, in: path, required: true, schema: {{ $ref: '#/components/schemas/{s.Schema}' }} }}"), null);
        Operation("/query", "get", "byQuery", shapes.Select(s => $"{{ name: {s.Key}, in: query, schema: {{ $ref: '#/components/schemas/{s.Schema}' }} }}"), null);
        Operation("/query-list", "get", "byQueryList", shapes.Select(s => $"{{ name: {s.Key}List, in: query, schema: {{ type: array, items: {{ $ref: '#/components/schemas/{s.Schema}' }} }} }}"), null);
        Operation("/query-object", "get", "byQueryObject", ["{ name: filter, in: query, style: deepObject, explode: true, schema: { $ref: '#/components/schemas/Echo' } }"], null);
        Operation("/header", "get", "byHeader", shapes.Select(s => $"{{ name: X-{s.Schema}, in: header, schema: {{ $ref: '#/components/schemas/{s.Schema}' }} }}"), null);
        Operation("/cookie", "get", "byCookie", shapes.Select(s => $"{{ name: {s.Key}, in: cookie, schema: {{ $ref: '#/components/schemas/{s.Schema}' }} }}"), null);
        Operation("/json", "post", "byJson", [], "application/json");
        Operation("/form", "post", "byForm", [], "application/x-www-form-urlencoded");
        Operation("/multipart", "post", "byMultipart", [], "multipart/form-data");
        Operation("/xml", "post", "byXml", [], "application/xml");

        sb.AppendLine("components:");
        sb.AppendLine("  schemas:");
        foreach (var s in shapes)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {s.Schema}:");
            sb.AppendLine("      type: string");
            sb.AppendLine(CultureInfo.InvariantCulture, $"      enum: [{string.Join(", ", s.Values)}]");
        }

        sb.AppendLine("    Echo:");
        sb.AppendLine("      type: object");
        sb.AppendLine("      properties:");
        foreach (var s in shapes)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"        {s.Key}: {{ $ref: '#/components/schemas/{s.Schema}' }}");
        }

        return sb.ToString();
    }

    internal static string EnumCasingHandlers()
    {
        var shapes = EnumCasingShapes;
        string Echo(
            Func<(string Key, string Schema, string Title, string[] Values, string[] Targets), string> value)
            => "new Echo { " + string.Join(", ", shapes.Select(s => $"{s.Schema} = {value(s)}")) + " }";

        var sb = new StringBuilder();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine("namespace ErrorBodies.RuntimeTest;");
        sb.AppendLine();

        void Handler(
            string operation,
            string echo)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"public sealed class {operation}HandlerImpl : I{operation}Handler");
            sb.AppendLine("{");
            sb.AppendLine(CultureInfo.InvariantCulture, $"    public System.Threading.Tasks.Task<{operation}Result> ExecuteAsync({operation}Parameters p, System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine(CultureInfo.InvariantCulture, $"        => System.Threading.Tasks.Task.FromResult({operation}Result.Ok({echo}));");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        Handler("ByPath", Echo(s => $"p.{s.Schema}"));
        Handler("ByQuery", Echo(s => $"p.{s.Schema}"));
        Handler("ByQueryList", Echo(s => $"EnumCasingRegistration.First(p.{s.Schema}List)"));
        Handler("ByQueryObject", "p.Filter ?? new Echo()");
        Handler("ByHeader", Echo(s => $"p.{s.Schema}"));
        Handler("ByCookie", Echo(s => $"p.{s.Schema}"));
        foreach (var body in (string[])["ByJson", "ByForm", "ByMultipart", "ByXml"])
        {
            Handler(body, "p.Request");
        }

        sb.AppendLine("public static class EnumCasingRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    public static T? First<T>(System.Collections.Generic.IEnumerable<T>? items)");
        sb.AppendLine("        where T : struct");
        sb.AppendLine("        => items is null ? null : System.Linq.Enumerable.FirstOrDefault(System.Linq.Enumerable.Cast<T?>(items));");
        sb.AppendLine();
        sb.AppendLine("    public static void Register(IServiceCollection services)");
        sb.AppendLine("    {");
        foreach (var operation in (string[])["ByPath", "ByQuery", "ByQueryList", "ByQueryObject", "ByHeader", "ByCookie", "ByJson", "ByForm", "ByMultipart", "ByXml"])
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"        services.AddScoped<I{operation}Handler, {operation}HandlerImpl>();");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// The values sent for a listed value: as listed, in lower case, in upper case and with the first letter flipped -
    /// each once.
    /// </summary>
    internal static IReadOnlyList<string> EnumCasingVariants(string value)
    {
        var flipped = char.IsUpper(value[0])
            ? char.ToLowerInvariant(value[0]) + value[1..]
            : char.ToUpperInvariant(value[0]) + value[1..];
        return new[] { value, value.ToLowerInvariant(), value.ToUpperInvariant(), flipped }
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The listed value a sent value names: the one it is exactly, or the only one it is in another casing; null when
    /// none is, or when more than one is in another casing.
    /// </summary>
    internal static string? EnumCasingExpected(
        IReadOnlyList<string> values,
        string sent)
    {
        if (values.FirstOrDefault(v => string.Equals(v, sent, StringComparison.Ordinal)) is { } exact)
        {
            return exact;
        }

        var matches = values.Where(v => string.Equals(v, sent, StringComparison.OrdinalIgnoreCase)).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    internal static async Task<JsonObject> CollectEnumCasingMatrixAsync(
        string hostKey)
    {
        var started = DateTimeOffset.Now;
        await using var host = await XmlTestHost.StartAsync(
            EnumCasingYaml(),
            EnumCasingHandlers(),
            "ErrorBodies.RuntimeTest.EnumCasingRegistration",
            configureServices: hostKey == "enumCasingConverter"
                ? services => services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()))
                : null,
            plainHost: hostKey == "enumCasingPlain",
            serverMarker: hostKey == "enumCasingExtensible"
                ? """{ "useMinimalApiPackage": "Enabled", "useValidationFilter": "Enabled", "useGlobalErrorHandler": "Enabled", "extensibleEnums": true }"""
                : null,
            useRequestDelegateGenerator: hostKey == "enumCasingRdg");

        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        var extensible = hostKey == "enumCasingExtensible";
        var cases = new JsonArray();
        foreach (var shape in EnumCasingShapes)
        {
            foreach (var sent in shape.Targets.SelectMany(EnumCasingVariants).Distinct(StringComparer.Ordinal))
            {
                var listed = EnumCasingExpected(shape.Values, sent);

                // An extensible enum keeps a value no listed value is in one casing, as it was sent
                var expected = listed ?? (extensible ? sent : null);
                var cells = new JsonObject();
                foreach (var location in EnumCasingLocations)
                {
                    using var request = EnumCasingRequest(shape.Key, shape.Schema, location.Id, sent);
                    using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
                    var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                    cells[location.Id] = EnumCasingVerdict(shape.Key, expected, (int)response.StatusCode, body);
                }

                cases.Add(new JsonObject
                {
                    ["id"] = $"{shape.Title} · {sent}",
                    ["group"] = $"{shape.Title}: {string.Join(", ", shape.Values)}",
                    ["request"] = $"{shape.Key} = {sent}",
                    ["expects"] = expected is null
                        ? "400: more than one listed value has this casing"
                        : listed is null
                            ? $"200, kept as sent: {expected}"
                            : $"200, bound as {expected}",
                    ["clients"] = cells,
                });
            }
        }

        return new JsonObject
        {
            ["generatedAt"] = started.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            ["durationSeconds"] = System.Math.Round((DateTimeOffset.Now - started).TotalSeconds, 1),
            ["clients"] = new JsonArray([.. EnumCasingLocations.Select(l => (JsonNode)new JsonObject { ["id"] = l.Id, ["group"] = l.Group, ["label"] = l.Label })]),
            ["cases"] = cases,
        };
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The request owns its content.")]
    internal static HttpRequestMessage EnumCasingRequest(
        string key,
        string schema,
        string location,
        string sent)
    {
        var encoded = Uri.EscapeDataString(sent);
        switch (location)
        {
            case "path":
                var segments = EnumCasingShapes.Select(s => s.Key == key ? encoded : Uri.EscapeDataString(s.Values[^1]));
                return new HttpRequestMessage(HttpMethod.Get, "/path/" + string.Join('/', segments));
            case "query":
                return new HttpRequestMessage(HttpMethod.Get, $"/query?{key}={encoded}");
            case "query-list":
                return new HttpRequestMessage(HttpMethod.Get, $"/query-list?{key}List={encoded}");
            case "query-object":
                return new HttpRequestMessage(HttpMethod.Get, $"/query-object?filter%5B{key}%5D={encoded}");
            case "header":
                var header = new HttpRequestMessage(HttpMethod.Get, "/header");
                header.Headers.TryAddWithoutValidation("X-" + schema, sent);
                return header;
            case "cookie":
                var cookie = new HttpRequestMessage(HttpMethod.Get, "/cookie");
                cookie.Headers.TryAddWithoutValidation("Cookie", $"{key}={encoded}");
                return cookie;
            case "json":
                return new HttpRequestMessage(HttpMethod.Post, "/json")
                {
                    Content = new StringContent(new JsonObject { [key] = sent }.ToJsonString(), Encoding.UTF8, "application/json"),
                };
            case "form":
                return new HttpRequestMessage(HttpMethod.Post, "/form")
                {
                    Content = new FormUrlEncodedContent([new KeyValuePair<string, string>(key, sent)]),
                };
            case "multipart":
                var multipart = new MultipartFormDataContent { { new StringContent(sent), key } };
                return new HttpRequestMessage(HttpMethod.Post, "/multipart") { Content = multipart };
            default:
                var xml = new XElement("Echo", new XElement(key, sent)).ToString(SaveOptions.DisableFormatting);
                return new HttpRequestMessage(HttpMethod.Post, "/xml")
                {
                    Content = new StringContent(xml, Encoding.UTF8, "application/xml"),
                };
        }
    }

    /// <summary>
    /// The cell: a 200 whose echo is the expected value as the specification writes it, or the 400 a value no listed
    /// value is in one casing gets.
    /// </summary>
    internal static JsonObject EnumCasingVerdict(
        string key,
        string? expected,
        int status,
        string body)
    {
        string? echoed = null;
        if (status == 200)
        {
            try
            {
                echoed = JsonNode.Parse(body)?[key]?.GetValue<string>();
            }
            catch (JsonException)
            {
                // Not the echo: the verdict says what came back
            }
        }

        var summary = status == 200 ? $"200 {echoed ?? "(no value)"}" : $"{status} {body[..System.Math.Min(body.Length, 200)]}";
        var (verdict, why) = expected is null
            ? status == 400
                ? (VerdictPass, "a 400: more than one listed value has this casing")
                : (VerdictFail, $"expected a 400 - more than one listed value has this casing - but got {summary}")
            : status == 200 && string.Equals(echoed, expected, StringComparison.Ordinal)
                ? (VerdictPass, $"bound as {expected}")
                : (VerdictFail, $"expected 200 with {expected}, got {summary}");

        return new JsonObject
        {
            ["verdict"] = verdict,
            ["why"] = why,
            ["summary"] = summary,
        };
    }
}