// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Runs the body media-type matrix (<see cref="BodyMediaCases"/>) and gives every cell its verdict.
/// </summary>
/// <remarks>
/// What a case expects - a file, a model or an error - comes from the case, never from the wire. The server's answer is
/// checked against values computed here (the file's bytes, name and media type; the handler's model; the error's
/// status and media type), and each client's reading against the server's answer: the bytes, content type and file
/// name; the model field by field, its status and media type; the error's status, media type and parsed body (a
/// ProblemDetails' title or detail and validation keys, the declared XML error model's fields, a proxy page's text).
/// </remarks>
public partial class ErrorResponseBodyRuntimeTests
{
    /// <summary>
    /// The Accept of the XML-first wire capture (what a <c>preferXml</c> client sends).
    /// </summary>
    private const string BodyMediaXmlFirstAccept = "application/xml, application/problem+xml;q=0.9, application/problem+json;q=0.8";

    /// <summary>
    /// Every generated client, called over HTTP against the generated server, reads every body the server writes -
    /// files, uploads, XML, a model or a file, and the errors on each - without losing bytes, content type, file name,
    /// fields or status (no <c>fail</c> verdict in the body media-type matrix).
    /// </summary>
    [Fact]
    [Trait("Category", "TypeScriptToolchain")]
    public async Task EndToEnd_EveryClientReadsEveryBodyMediaType()
    {
        if (!IsNodeAvailable(out var reason))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail(reason + " (running under CI where the body media-type matrix is required)");
            }

            Assert.Skip(reason);
        }

        var matrix = await CollectBodyMediaMatrixAsync();
        if (Environment.GetEnvironmentVariable("BODYMEDIA_E2E_JSON") is { Length: > 0 } dumpPath)
        {
            await File.WriteAllTextAsync(dumpPath, matrix.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), TestContext.Current.CancellationToken);
        }

        var failures = BodyMediaFailingCells(matrix);
        Assert.True(failures.Count == 0, $"{failures.Count} failing cells:\n" + string.Join("\n", failures.Take(80)));
    }

    /// <summary>
    /// The verdicts are not blind: a changed byte, content type or file name; a changed model field, status or media
    /// type; a file where a model is expected; a wrong error status, media type or body field; and a server that writes
    /// other bytes each fail - and what the server wrote passes.
    /// </summary>
    [Fact]
    public void BodyMediaVerdicts_FailAChangedFileModelOrError()
    {
        var cases = BodyMediaCases();

        // A file
        var file = cases.First(c => c.Name == "pdf bytes + file name");
        var expected = BodyMediaExpectedFile(file)!.Value;
        var wire = new BodyMediaWire(200, "application/pdf", expected.Sha256, expected.FileName, expected.Length, "(binary)");
        Assert.Equal(VerdictPass, BodyMediaWireVerdict(file, wire).Verdict);
        Assert.Equal(VerdictFail, BodyMediaWireVerdict(file, wire with { Sha256 = "00" }).Verdict);
        Assert.Equal(VerdictFail, BodyMediaWireVerdict(file, wire with { FileName = "Rapport-Å pdf" }).Verdict);
        Assert.Equal(VerdictFail, BodyMediaWireVerdict(file, wire with { MediaType = "application/json", Body = "{}" }).Verdict);

        var exact = new JsonObject { ["kind"] = "file", ["sha256"] = expected.Sha256, ["contentType"] = "application/pdf", ["fileName"] = expected.FileName };
        Assert.Equal(VerdictPass, Verdict(file, wire, exact, "epo"));
        foreach (var (key, value, loss) in ((string, string?, string)[])[("sha256", "aa12", LossBytes), ("contentType", "application/octet-stream", LossContentType), ("contentType", null, LossContentType), ("fileName", "Rapport-A pdf.pdf", LossFileName)])
        {
            var changed = exact.DeepClone().AsObject();
            changed[key] = value;
            var cell = BodyMediaVerdict(file, wire, changed, "ts", prefersXml: false);
            Assert.Equal(VerdictFail, (string?)cell["verdict"]);
            Assert.Contains(loss, (string?)cell["loss"]!["kind"], StringComparison.Ordinal);
        }

        Assert.Equal(VerdictFail, Verdict(file, wire, new JsonObject { ["kind"] = "model", ["model"] = new JsonObject() }, "epo"));

        // A model
        var model = cases.First(c => c.Name == "xml-only user");
        var modelWire = new BodyMediaWire(200, "application/xml", "bb", null, 10, "<user id=\"1\"><name>Ada &amp; &lt;Bob&gt;</name><tags><tag>a</tag><tag>b</tag></tags></user>");
        Assert.Equal(VerdictPass, BodyMediaWireVerdict(model, modelWire).Verdict);
        Assert.Equal(VerdictFail, BodyMediaWireVerdict(model, modelWire with { Body = "<user id=\"1\"><name>Ada</name></user>" }).Verdict);
        var read = new JsonObject { ["kind"] = "model", ["status"] = 200, ["mediaType"] = "application/xml", ["model"] = BodyMediaUser1.DeepClone() };
        Assert.Equal(VerdictPass, Verdict(model, modelWire, read, "epo"));
        Assert.Equal(VerdictFail, Verdict(model, modelWire, Changed(read, "mediaType", null), "epo"));
        Assert.Equal(VerdictFail, Verdict(model, modelWire, Changed(read, "status", 201), "epo"));
        var renamed = read.DeepClone().AsObject();
        renamed["model"]!["name"] = "Ada & Bob";
        Assert.Equal(VerdictFail, Verdict(model, modelWire, renamed, "epo"));
        Assert.Equal(VerdictPass, Verdict(model, modelWire, new JsonObject { ["kind"] = "model", ["model"] = BodyMediaUser1.DeepClone() }, "throw"));

        // JSON or XML: a preferXml client must get XML
        var either = cases.First(c => c.Name == "json or xml user");
        var xmlWire = modelWire;
        Assert.Equal(VerdictPass, (string?)BodyMediaVerdict(either, xmlWire, read, "epo", prefersXml: true)["verdict"]);
        Assert.Equal(VerdictFail, (string?)BodyMediaVerdict(either, xmlWire, Changed(read, "mediaType", "application/json"), "epo", prefersXml: true)["verdict"]);

        // An error
        var error = cases.First(c => c.Name == "pdf 404 handler");
        var errorWire = new BodyMediaWire(404, "application/problem+json", "cc", null, 10, "{\"title\":\"Not Found\",\"status\":404,\"traceId\":\"x\"}");
        var problem = new JsonObject { ["kind"] = "error", ["status"] = 404, ["mediaType"] = "application/problem+json", ["type"] = "ProblemDetails", ["detail"] = "Not Found" };
        Assert.Equal(VerdictPass, BodyMediaWireVerdict(error, errorWire).Verdict);
        Assert.Equal(VerdictPass, Verdict(error, errorWire, problem, "epo"));
        Assert.Equal(VerdictFail, Verdict(error, errorWire, Changed(problem, "status", 500), "epo"));
        Assert.Equal(VerdictFail, Verdict(error, errorWire, Changed(problem, "mediaType", "text/plain"), "epo"));
        Assert.Equal(VerdictFail, Verdict(error, errorWire, Changed(problem, "detail", "{\"title\":\"Not Found\"}"), "ts"));
        Assert.Equal(VerdictFail, Verdict(error, errorWire, Changed(problem, "type", "(raw)"), "epo"));
        Assert.Equal(VerdictFail, Verdict(error, errorWire, Changed(problem, "kind", "threw"), "epo"));
        Assert.Equal(VerdictPass, Verdict(error, errorWire, new JsonObject { ["kind"] = "threw", ["status"] = 404 }, "throw"));
        Assert.Equal(VerdictPass, Verdict(error, errorWire, new JsonObject { ["kind"] = "error", ["status"] = 404, ["mediaType"] = "application/problem+json", ["type"] = "(raw)", ["raw"] = "{\"title\":\"Not Found\",\"status\":404,\"traceId\":\"y\"}" }, "result"));
        Assert.Equal(VerdictFail, Verdict(error, errorWire, new JsonObject { ["kind"] = "error", ["status"] = 404, ["mediaType"] = "application/problem+json", ["type"] = "(raw)", ["raw"] = "{}" }, "result"));

        // The declared XML error model: its fields
        var errorModel = cases.First(c => c.Name == "xml-only 404 error model");
        var errorModelWire = new BodyMediaWire(404, "application/xml", "dd", null, 10, "<error code=\"U404\"><message>No user 404 &amp; more</message></error>");
        var info = new JsonObject { ["kind"] = "error", ["status"] = 404, ["mediaType"] = "application/xml", ["type"] = "ErrorInfo", ["fields"] = new JsonObject { ["code"] = "U404", ["message"] = "No user 404 & more" } };
        Assert.Equal(VerdictPass, Verdict(errorModel, errorModelWire, info, "epo"));
        var lostCode = info.DeepClone().AsObject();
        lostCode["fields"]!["code"] = null;
        Assert.Equal(VerdictFail, Verdict(errorModel, errorModelWire, lostCode, "epo"));

        // The server's error answers against the case
        Assert.Equal(VerdictFail, BodyMediaWireVerdict(errorModel, errorWire with { Status = 404 }).Verdict);
        Assert.Equal(VerdictPass, BodyMediaWireVerdict(errorModel, errorModelWire).Verdict);
        Assert.Equal(VerdictFail, BodyMediaWireVerdict(errorModel, errorModelWire with { Body = "<error code=\"X\"><message>No user 404 &amp; more</message></error>" }).Verdict);
        Assert.Equal(VerdictFail, BodyMediaWireVerdict(errorModel, errorModelWire with { Body = "<error" }).Verdict);
        Assert.Equal(VerdictFail, BodyMediaWireVerdict(error, errorWire with { Body = "{\"title\":\"boom pdf\",\"status\":404}" }).Verdict);
        var validation = cases.First(c => c.Name == "xml request body 400 validation");
        Assert.Equal(VerdictFail, BodyMediaWireVerdict(validation, new BodyMediaWire(400, "application/problem+json", "ee", null, 10, "{\"title\":\"One or more validation errors occurred.\",\"status\":400}")).Verdict);

        // XML first: the server must answer XML, and a preferXml client must get it
        var eitherJson = new BodyMediaWire(200, "application/json", "ff", null, 10, "{\"id\":1,\"name\":\"Ada \u0026 \u003CBob\u003E\",\"tags\":[\"a\",\"b\"]}");
        Assert.Equal(VerdictPass, BodyMediaWireVerdict(either, eitherJson).Verdict);
        Assert.Equal(VerdictFail, BodyMediaWireVerdict(either, eitherJson, xmlFirst: true).Verdict);
        Assert.Equal(VerdictFail, (string?)BodyMediaVerdict(either, eitherJson, Changed(read, "mediaType", "application/json"), "epo", prefersXml: true)["verdict"]);

        // An unreadable server error body fails the cell, not the run; a TypeScript error arm names the status
        Assert.Equal(VerdictFail, Verdict(errorModel, errorModelWire with { Body = "<error" }, info, "epo"));
        Assert.Equal(VerdictFail, Verdict(error, errorWire, Changed(problem, "type", "serverError"), "ts"));
        Assert.Equal(VerdictPass, Verdict(error, errorWire, Changed(problem, "type", "notFound"), "ts"));

        static string? Verdict(
            BodyMediaCase bodyCase,
            BodyMediaWire wire,
            JsonObject read,
            string client)
            => (string?)BodyMediaVerdict(bodyCase, wire, read, client, prefersXml: false)["verdict"];

        static JsonObject Changed(
            JsonObject read,
            string key,
            JsonNode? value)
        {
            var copy = read.DeepClone().AsObject();
            copy[key] = value;
            return copy;
        }
    }

    /// <summary>
    /// What the server wrote for a case (the raw HTTP answer).
    /// </summary>
    internal sealed record BodyMediaWire(
        int Status,
        string? MediaType,
        string Sha256,
        string? FileName,
        long Length,
        string Body);

    private static List<string> BodyMediaFailingCells(JsonObject matrix)
    {
        var failures = new List<string>();
        foreach (var node in matrix["cases"]!.AsArray())
        {
            var item = node!.AsObject();
            var name = (string)item["id"]!;
            foreach (var key in (string[])["wire", "wireXml"])
            {
                if ((string?)item[key]?["verdict"] == VerdictFail)
                {
                    failures.Add($"{name} [{key}]: {(string?)item[key]!["why"]}");
                }
            }

            foreach (var cell in item["clients"]!.AsObject())
            {
                if ((string?)cell.Value!["verdict"] == VerdictFail)
                {
                    failures.Add($"{name} [{cell.Key}]: {(string?)cell.Value["why"]} - {(string?)cell.Value["summary"]}");
                }
            }
        }

        return failures;
    }

    private static async Task<JsonObject> CollectBodyMediaMatrixAsync()
    {
        var started = DateTimeOffset.Now;
        var cases = BodyMediaCases();

        // A proxy in front of the API answers /.../502 with its own HTML page: no generated code runs.
        await using var host = await XmlTestHost.StartAsync(
            BodyMediaYaml,
            BodyMediaHandlerSource,
            "ErrorBodies.RuntimeTest.BodyMediaRegistration",
            app => app.Use(async (context, next) =>
            {
                if (context.Request.Path.Value?.EndsWith("/502", StringComparison.Ordinal) == true)
                {
                    context.Response.StatusCode = 502;
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync("<html><body><h1>502 Bad Gateway</h1></body></html>");
                    return;
                }

                await next(context);
            }));

        // The wire, as a client that sends no Accept gets it; a JSON-or-XML case also XML first (a preferXml client)
        var wire = new Dictionary<string, BodyMediaWire>(StringComparer.Ordinal);
        var wireXml = new Dictionary<string, BodyMediaWire>(StringComparer.Ordinal);
        using (var http = new HttpClient { BaseAddress = host.BaseAddress })
        {
            foreach (var bodyCase in cases)
            {
                wire[bodyCase.Name] = await CaptureBodyMediaWireAsync(http, bodyCase, accept: null);
                if (bodyCase.Group == "json or xml")
                {
                    wireXml[bodyCase.Name] = await CaptureBodyMediaWireAsync(http, bodyCase, BodyMediaXmlFirstAccept);
                }
            }
        }

        var casesJson = BodyMediaCasesJson(cases).ToJsonString();
        var rows = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var client in BodyMediaClients.Where(c => c.Marker.Length > 0))
        {
            var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", client.Marker, includeMinimalApi: false, BodyMediaYaml);
            var usings = sources
                .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
                .Where(ns => ns.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Select(ns => $"global using {ns};");
            sources.Add(("BodyMediaProbe.cs", string.Join(Environment.NewLine, usings) + Environment.NewLine + BodyMediaProbeSource));
            var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
            var run = (Task<Dictionary<string, string>>)assembly.GetType("ErrorBodies.RuntimeTest.BodyMediaProbe", throwOnError: true)!
                .GetMethod("RunAsync")!
                .Invoke(null, [host.BaseAddress.ToString(), casesJson, client.Typed])!;
            rows[client.Id] = await run;
        }

        var typeScript = RunBodyMediaTypeScriptClients(host.BaseAddress, casesJson);

        var casesArray = new JsonArray();
        foreach (var bodyCase in cases)
        {
            var observed = wire[bodyCase.Name];
            var observedXml = wireXml.GetValueOrDefault(bodyCase.Name);
            var clients = new JsonObject();
            foreach (var client in BodyMediaClients)
            {
                var prefersXml = client.Marker.Contains("preferXml", StringComparison.Ordinal);
                var description = client.Id.StartsWith("ts-", StringComparison.Ordinal)
                    ? typeScript[client.Id].GetValueOrDefault(bodyCase.Name)
                    : rows[client.Id].GetValueOrDefault(bodyCase.Name) is { } row ? JsonNode.Parse(row)!.AsObject() : null;
                var clientKind = client.Id switch
                {
                    "cs-typed-Throw" => "throw",
                    _ when client.Id.StartsWith("cs-typed-", StringComparison.Ordinal) => "result",
                    _ when client.Id.StartsWith("ts-", StringComparison.Ordinal) => "ts",
                    _ => "epo",
                };
                clients[client.Id] = description is null
                    ? Cell(VerdictFail, "no result recorded for the case", "-")
                    : BodyMediaVerdict(bodyCase, prefersXml && observedXml is not null ? observedXml : observed, description, clientKind, prefersXml);
            }

            var item = new JsonObject
            {
                ["id"] = bodyCase.Name,
                ["group"] = bodyCase.Group,
                ["operation"] = bodyCase.Operation,
                ["request"] = $"{bodyCase.Method} {bodyCase.Path}" + (bodyCase.Upload is { } upload ? $" ({upload}, {BodyMediaBytes(upload).Length:N0} bytes)" : bodyCase.User is JsonArray ? " (application/xml users)" : bodyCase.User is not null ? " (application/xml user)" : string.Empty),
                ["expects"] = BodyMediaExpectedShape(bodyCase),
                ["wire"] = WireJson(bodyCase, observed),
                ["clients"] = clients,
            };
            if (observedXml is not null)
            {
                item["wireXml"] = WireJson(bodyCase, observedXml);
            }

            casesArray.Add(item);
        }

        return new JsonObject
        {
            ["generatedAt"] = started.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            ["durationSeconds"] = System.Math.Round((DateTimeOffset.Now - started).TotalSeconds, 1),
            ["clients"] = new JsonArray([.. BodyMediaClients.Select(c => (JsonNode)new JsonObject { ["id"] = c.Id, ["group"] = c.Group, ["label"] = c.Label })]),
            ["cases"] = casesArray,
        };

        JsonObject WireJson(
            BodyMediaCase bodyCase,
            BodyMediaWire observed)
        {
            var verdict = BodyMediaWireVerdict(bodyCase, observed, xmlFirst: !ReferenceEquals(observed, wire[bodyCase.Name]));
            return new JsonObject
            {
                ["status"] = observed.Status,
                ["mediaType"] = observed.MediaType,
                ["sha256"] = observed.Sha256,
                ["fileName"] = observed.FileName,
                ["length"] = observed.Length,
                ["body"] = observed.Body,
                ["verdict"] = verdict.Verdict,
                ["why"] = verdict.Why,
            };
        }
    }

    private static JsonArray BodyMediaCasesJson(
        IEnumerable<BodyMediaCase> cases)
        => new([
            .. cases.Select(c => (JsonNode)new JsonObject
            {
                ["name"] = c.Name,
                ["segment"] = c.Segment,
                ["operation"] = c.Operation,
                ["method"] = char.ToLowerInvariant(c.Operation[0]) + c.Operation[1..],
                ["id"] = c.Id,
                ["upload"] = c.Upload,
                ["uploadTyped"] = c.UploadTyped,
                ["bytes"] = c.Upload is { } upload ? Convert.ToBase64String(BodyMediaBytes(upload)) : null,
                ["user"] = c.User?.DeepClone(),
            }),
        ]);

    private static async Task<BodyMediaWire> CaptureBodyMediaWireAsync(
        HttpClient http,
        BodyMediaCase bodyCase,
        string? accept)
    {
        using var request = new HttpRequestMessage(new HttpMethod(bodyCase.Method), bodyCase.Path);
        if (accept is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept", accept);
        }

        if (bodyCase.Upload is { } upload)
        {
            request.Content = new ByteArrayContent(BodyMediaBytes(upload));
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(upload);
        }
        else if (bodyCase.User is { } user)
        {
            static string UserXml(JsonNode user)
            {
                var tags = string.Concat(user["tags"]!.AsArray().Select(t => $"<tag>{(string?)t}</tag>"));
                return $"<user id=\"{(int)user["id"]!}\"><name>{(string?)user["name"]}</name><tags>{tags}</tags></user>";
            }

            var xml = user is JsonArray users ? $"<users>{string.Concat(users.Select(u => UserXml(u!)))}</users>" : UserXml(user);
            request.Content = new StringContent(xml, Encoding.UTF8, "application/xml");
        }

        using var response = await http.SendAsync(request, TestContext.Current.CancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        var disposition = response.Content.Headers.ContentDisposition;
        return new BodyMediaWire(
            (int)response.StatusCode,
            mediaType,
            Sha256Hex(bytes),
            (disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"'),
            bytes.LongLength,
            IsTextMediaType(mediaType) ? Encoding.UTF8.GetString(bytes) : "(binary)");
    }

    private static string Sha256Hex(byte[] bytes)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>
    /// Whether a body of a media type is text (JSON, XML, <c>text/*</c>), by exact rules - not by a substring
    /// (<c>…spreadsheetml.sheet</c> is no XML).
    /// </summary>
    private static bool IsTextMediaType(string? mediaType)
        => mediaType is not null &&
           (mediaType is "application/json" or "application/xml" or "text/xml" ||
            mediaType.EndsWith("+json", StringComparison.Ordinal) ||
            mediaType.EndsWith("+xml", StringComparison.Ordinal) ||
            mediaType.StartsWith("text/", StringComparison.Ordinal));

    /// <summary>
    /// The status a case's server answers with: the id's error status, a validation 400, a 201 for the created user, else 200.
    /// </summary>
    private static int BodyMediaExpectedStatus(BodyMediaCase bodyCase)
        => bodyCase switch
        {
            { Id: 404 or 500 or 502 } => bodyCase.Id.Value,
            { Name: var name } when name.Contains("400", StringComparison.Ordinal) => 400,
            { Operation: "CreateUserXml" } => 201,
            _ => 200,
        };

    /// <summary>
    /// What a case expects - from the case, never from the wire: an error, a model, or a file.
    /// </summary>
    private static string BodyMediaExpectedShape(BodyMediaCase bodyCase)
        => BodyMediaExpectedStatus(bodyCase) >= 400 ? "error" : bodyCase.Model is not null ? "model" : "file";

    /// <summary>
    /// The file a file case's server writes, computed here (the handlers' algorithm, written down twice on purpose):
    /// its SHA-256, length, file name (<see langword="null"/> inline) and media type.
    /// </summary>
    private static (string Sha256, long Length, string? FileName, string MediaType)? BodyMediaExpectedFile(
        BodyMediaCase bodyCase)
    {
        if (BodyMediaExpectedShape(bodyCase) != "file")
        {
            return null;
        }

        if (bodyCase.Operation == "GetReport")
        {
            var report = BodyMediaServerBytes("report");
            return (Sha256Hex(report), report.LongLength, "Rapport-Å.pdf", "application/pdf");
        }

        var file = BodyMediaFiles.First(f => f.Operation == bodyCase.Operation);
        var bytes = BodyMediaServerBytes(file.Kind);
        return (Sha256Hex(bytes), bytes.LongLength, bodyCase.Id == 1 ? $"Rapport-Å {file.Kind}.{file.Extension}" : null, file.MediaType);
    }

    /// <summary>
    /// The bytes the handlers write for a file kind - the same algorithm as <c>BodyMediaData.Bytes</c> in the handler source.
    /// </summary>
    private static byte[] BodyMediaServerBytes(string kind)
    {
        var bytes = new byte[70_001];
        var seed = kind.Aggregate(17, (h, c) => (h * 31) + c);
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)((i * 31) + seed + (i >> 8));
        }

        return bytes;
    }

    /// <summary>
    /// The server's answer for a case, against what the case expects.
    /// </summary>
    private static (string Verdict, string Why) BodyMediaWireVerdict(
        BodyMediaCase bodyCase,
        BodyMediaWire wire,
        bool xmlFirst = false)
    {
        var expected = BodyMediaExpectedStatus(bodyCase);
        if (wire.Status != expected)
        {
            return (VerdictFail, $"expected {expected}, the server answered {wire.Status} ({wire.MediaType})");
        }

        // The media type, from the case: XML for an XML-only body and for an XML-first request, else JSON
        if (BodyMediaExpectedMediaType(bodyCase, xmlFirst) is { } mediaType && !string.Equals(wire.MediaType, mediaType, StringComparison.Ordinal))
        {
            return (VerdictFail, $"expected {mediaType}{(xmlFirst ? " (asked for XML first)" : string.Empty)}, the server answered {wire.MediaType}");
        }

        switch (BodyMediaExpectedShape(bodyCase))
        {
            case "file":
            {
                var file = BodyMediaExpectedFile(bodyCase)!.Value;
                return wire.Body == "(binary)" &&
                       string.Equals(wire.MediaType, file.MediaType, StringComparison.Ordinal) &&
                       string.Equals(wire.Sha256, file.Sha256, StringComparison.Ordinal) &&
                       wire.Length == file.Length &&
                       string.Equals(wire.FileName, file.FileName, StringComparison.Ordinal)
                    ? (VerdictPass, $"the file: {wire.Length:N0} bytes as computed, {wire.MediaType}, " + (wire.FileName is null ? "inline" : $"file name {wire.FileName}"))
                    : (VerdictFail, $"expected {file.MediaType} {file.Length:N0} bytes sha256 {HashHead(file.Sha256)}.. {file.FileName ?? "inline"}, got {wire.MediaType} {wire.Length:N0} bytes sha256 {HashHead(wire.Sha256)}.. {wire.FileName ?? "inline"}");
            }

            case "model":
            {
                var model = ReadWireModel(wire);
                return JsonNode.DeepEquals(WithoutNulls(model), WithoutNulls(bodyCase.Model))
                    ? (VerdictPass, $"the handler's model, as {wire.MediaType}")
                    : (VerdictFail, $"expected the handler's model {bodyCase.Model?.ToJsonString()}, the {wire.MediaType} body reads {model?.ToJsonString() ?? "(not a model)"}");
            }

            default:
            {
                var error = ReadWireError(wire);
                var expectedError = BodyMediaExpectedError(bodyCase);
                if (error is null)
                {
                    return (VerdictFail, $"expected an error body, got {wire.MediaType}: {Short(wire.Body)}");
                }

                foreach (var (key, value) in expectedError)
                {
                    if (!JsonNode.DeepEquals(error[key], value))
                    {
                        return (VerdictFail, $"the error's {key}: expected {value?.ToJsonString() ?? "none"}, the server wrote {error[key]?.ToJsonString() ?? "none"}");
                    }
                }

                return wire.Body.Contains("boom", StringComparison.Ordinal)
                    ? (VerdictFail, "the exception's message leaked into the error body")
                    : (VerdictPass, $"{wire.Status} {wire.MediaType}: {error.ToJsonString()}");
            }
        }
    }

    /// <summary>
    /// The media type a case's server answers with (<see langword="null"/>: a file, checked with the file): an error is a
    /// ProblemDetails (as XML when asked for XML first), the declared XML error model, or the proxy's page; a model is
    /// XML for an XML-only body or an XML-first request, else JSON.
    /// </summary>
    private static string? BodyMediaExpectedMediaType(
        BodyMediaCase bodyCase,
        bool xmlFirst)
    {
        var xmlOnly = bodyCase.Operation is "GetUserXml" or "ListUsersXml" or "CreateUserXml" or "CreateUsersXml";
        return BodyMediaExpectedShape(bodyCase) switch
        {
            "error" when bodyCase.Id == 502 => "text/html",
            "error" when bodyCase.Id == 404 && bodyCase.Operation is "GetUserXml" or "GetStatement" => "application/xml",
            "error" => xmlFirst ? "application/problem+xml" : "application/problem+json",
            "model" => xmlOnly || xmlFirst ? "application/xml" : "application/json",
            _ => null,
        };
    }

    /// <summary>
    /// What an error case's body says, from the case: the ProblemDetails' title (and the validation key), the declared
    /// XML error model's fields, or the proxy's page.
    /// </summary>
    private static JsonObject BodyMediaExpectedError(BodyMediaCase bodyCase)
        => bodyCase switch
        {
            { Id: 502 } => new JsonObject { ["kind"] = "page", ["text"] = "<html><body><h1>502 Bad Gateway</h1></body></html>" },
            { Id: 404, Operation: "GetUserXml" } => new JsonObject { ["kind"] = "model", ["fields"] = new JsonObject { ["code"] = "U404", ["message"] = "No user 404 & more" } },
            { Id: 404, Operation: "GetStatement" } => new JsonObject { ["kind"] = "model", ["fields"] = new JsonObject { ["code"] = "S404", ["message"] = "No statement & more" } },
            { Id: 404 } => new JsonObject { ["kind"] = "problem", ["title"] = "Not Found", ["detail"] = null, ["errors"] = null },
            { Id: 500 } => new JsonObject { ["kind"] = "problem", ["title"] = "An error occurred while processing your request.", ["detail"] = null, ["errors"] = null },

            // The key is the request property's path (Atc.Rest.MinimalApi issue 13: the XML name "name" once it lands); in
            // a list, the invalid item's index (the second user)
            { User: JsonArray } => new JsonObject { ["kind"] = "problem", ["title"] = "One or more validation errors occurred.", ["errors"] = new JsonArray("Request[1].name") },
            _ => new JsonObject { ["kind"] = "problem", ["title"] = "One or more validation errors occurred.", ["errors"] = new JsonArray("Request.name") },
        };

    /// <summary>
    /// The model in a JSON or XML body (a user, a list of users, a receipt, a report), or <see langword="null"/>.
    /// </summary>
    private static JsonNode? ReadWireModel(BodyMediaWire wire)
    {
        if (wire.MediaType is "application/json")
        {
            return TryParseJson(wire.Body);
        }

        if (wire.MediaType is not ("application/xml" or "text/xml"))
        {
            return null;
        }

        try
        {
            var root = XDocument.Parse(wire.Body).Root!;
            return root.Name.LocalName == "user" ? XmlUser(root) : new JsonArray([.. root.Elements().Select(e => (JsonNode)XmlUser(e))]);
        }
        catch (XmlException)
        {
            return null;
        }

        static JsonObject XmlUser(XElement user)
        {
            var result = new JsonObject
            {
                ["id"] = (int?)user.Attribute("id"),
                ["name"] = user.Elements().FirstOrDefault(e => e.Name.LocalName == "name")?.Value,
            };
            if (user.Elements().FirstOrDefault(e => e.Name.LocalName == "tags") is { } tags)
            {
                result["tags"] = new JsonArray([.. tags.Elements().Select(t => (JsonNode)JsonValue.Create(t.Value))]);
            }

            return result;
        }
    }

    /// <summary>
    /// What an error body says, parsed from the wire: a ProblemDetails' title, detail and validation keys (JSON or
    /// RFC 9457 XML), the declared XML error model's fields, or a page's text - else <see langword="null"/>.
    /// </summary>
    private static JsonObject? ReadWireError(BodyMediaWire wire)
    {
        try
        {
            return ReadWireErrorBody(wire);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static JsonObject? ReadWireErrorBody(BodyMediaWire wire)
    {
        switch (wire.MediaType)
        {
            case "application/problem+json" when TryParseJson(wire.Body) is JsonObject problem:
                return new JsonObject
                {
                    ["kind"] = "problem",
                    ["title"] = (string?)problem["title"],
                    ["detail"] = (string?)problem["detail"],
                    ["errors"] = problem["errors"] is JsonObject errors ? new JsonArray([.. errors.Select(e => (JsonNode)JsonValue.Create(e.Key))]) : null,
                };
            case "application/problem+xml":
            {
                var root = XDocument.Parse(wire.Body).Root!;
                var child = (string name) => root.Elements().FirstOrDefault(e => e.Name.LocalName == name);
                return new JsonObject
                {
                    ["kind"] = "problem",
                    ["title"] = child("title")?.Value,
                    ["detail"] = child("detail")?.Value,
                    ["errors"] = child("errors") is { } xmlErrors ? new JsonArray([.. xmlErrors.Elements().Select(e => (JsonNode)JsonValue.Create(XmlConvert.DecodeName(e.Name.LocalName)))]) : null,
                };
            }

            case "application/xml":
            {
                var root = XDocument.Parse(wire.Body).Root!;
                return new JsonObject
                {
                    ["kind"] = "model",
                    ["fields"] = new JsonObject
                    {
                        ["code"] = (string?)root.Attribute("code"),
                        ["message"] = root.Elements().FirstOrDefault(e => e.Name.LocalName == "message")?.Value,
                    },
                };
            }

            case "text/html":
                return new JsonObject { ["kind"] = "page", ["text"] = wire.Body };
            default:
                return null;
        }
    }

    /// <summary>
    /// A client's cell: what it exposes compared with what the server wrote. <paramref name="client"/> is <c>epo</c>,
    /// <c>result</c> (TypedClient Result: an error is its raw body, by design), <c>throw</c> (TypedClient Throw: the
    /// value only, an error throws with the status) or <c>ts</c>.
    /// </summary>
    private static JsonObject BodyMediaVerdict(
        BodyMediaCase bodyCase,
        BodyMediaWire wire,
        JsonObject read,
        string client,
        bool prefersXml)
    {
        var kind = (string?)read["kind"];
        var summary = kind switch
        {
            "file" => $"file {(string?)read["contentType"] ?? "(no content type)"} {(string?)read["fileName"] ?? "(no name)"} {(long?)read["length"]:N0} bytes",
            "model" => $"{((int?)read["status"])?.ToString(CultureInfo.InvariantCulture) ?? string.Empty} {(string?)read["type"]} {(string?)read["mediaType"] ?? "(media type not exposed)"} {read["model"]?.ToJsonString()}".Trim(),
            "error" => $"{(int?)read["status"]} {(string?)read["mediaType"]} {(string?)read["type"]} {(string?)read["detail"]}" + (read["errors"] is JsonArray keys ? $" errors: {string.Join(", ", keys)}" : string.Empty) + (read["fields"] is JsonObject fields && (string?)read["type"] != "ProblemDetails" && (string?)read["type"] != "ValidationProblemDetails" ? $" {fields.ToJsonString()}" : string.Empty),
            "threw" => $"threw {(string?)read["detail"]}",
            _ => read.ToJsonString(),
        };

        return BodyMediaExpectedShape(bodyCase) switch
        {
            "error" => ErrorVerdict(),
            "file" => FileVerdict(),
            _ => ModelVerdict(),
        };

        JsonObject ErrorVerdict()
        {
            var status = (int?)read["status"];
            if (client == "throw")
            {
                return kind == "threw" && status == wire.Status
                    ? BodyMediaCell(VerdictPass, null, $"throws with the status {wire.Status}", summary)
                    : BodyMediaCell(VerdictFail, LossStatus, $"expected to throw with {wire.Status}, read {kind} {status}", summary);
            }

            if (kind != "error" || status != wire.Status)
            {
                return BodyMediaCell(VerdictFail, kind == "threw" ? LossShape : LossStatus, $"expected the error {wire.Status} as a result, read {kind} {status}", summary);
            }

            if (!string.Equals((string?)read["mediaType"], wire.MediaType, StringComparison.Ordinal))
            {
                return BodyMediaCell(VerdictFail, LossContentType, $"the error's media type: the server wrote {wire.MediaType}, the client exposes {(string?)read["mediaType"] ?? "none"}", summary);
            }

            if (ReadWireError(wire) is not { } expected)
            {
                return BodyMediaCell(VerdictFail, LossShape, $"the server's error body is unreadable ({wire.MediaType})", summary);
            }

            var type = (string?)read["type"];

            // A TypeScript error arm names the status
            if (client == "ts" && BodyMediaExpectedArm(wire.Status) is { } arm && !string.Equals(type, arm, StringComparison.Ordinal))
            {
                return BodyMediaCell(VerdictFail, LossStatus, $"expected the {arm} arm for {wire.Status}, got {type}", summary);
            }

            // The TypedClient Result style has no typed errors (by design): its raw body must be the server's
            if (client == "result")
            {
                return type == "(raw)" && string.Equals(WithoutAnyTraceId((string?)read["raw"] ?? string.Empty), WithoutAnyTraceId(wire.Body), StringComparison.Ordinal)
                    ? BodyMediaCell(VerdictPass, null, $"the error {wire.Status}: the server's body, raw (the TypedClient has no typed errors)", summary)
                    : BodyMediaCell(VerdictFail, LossFields, "the raw error body is not the server's", summary);
            }

            switch ((string?)expected["kind"])
            {
                case "problem":
                {
                    var text = (string?)expected["detail"] ?? (string?)expected["title"];
                    if (client == "epo" && type is not ("ProblemDetails" or "ValidationProblemDetails"))
                    {
                        return BodyMediaCell(VerdictFail, LossShape, $"expected a typed ProblemDetails, read {type}", summary);
                    }

                    if (!string.Equals((string?)read["detail"], text, StringComparison.Ordinal))
                    {
                        return BodyMediaCell(VerdictFail, LossFields, $"the problem says \"{text}\", the client \"{Short((string?)read["detail"] ?? string.Empty)}\"", summary);
                    }

                    if (expected["errors"] is JsonArray keys && !JsonNode.DeepEquals(Sorted(keys), Sorted(read["errors"] as JsonArray)))
                    {
                        return BodyMediaCell(VerdictFail, LossFields, $"the validation keys {keys.ToJsonString()} are not the client's {read["errors"]?.ToJsonString() ?? "(none)"}", summary);
                    }

                    return BodyMediaCell(VerdictPass, null, $"the ProblemDetails {wire.Status}: \"{text}\"" + (expected["errors"] is JsonArray k ? $", errors {k.ToJsonString()}" : string.Empty), summary);
                }

                case "model":
                    return (client != "epo" || type == "ErrorInfo") && JsonNode.DeepEquals(expected["fields"], read["fields"])
                        ? BodyMediaCell(VerdictPass, null, $"the declared XML error model: {expected["fields"]!.ToJsonString()}", summary)
                        : BodyMediaCell(VerdictFail, LossFields, $"expected the error model {expected["fields"]!.ToJsonString()}, read {type} {read["fields"]?.ToJsonString() ?? "(no fields)"}", summary);
                default:
                    return string.Equals((string?)read["raw"], wire.Body, StringComparison.Ordinal)
                        ? BodyMediaCell(VerdictPass, null, $"the error {wire.Status}: the page, raw", summary)
                        : BodyMediaCell(VerdictFail, LossFields, "the page is not the client's error body", summary);
            }
        }

        JsonObject FileVerdict()
        {
            if (kind != "file")
            {
                return BodyMediaCell(VerdictFail, LossShape, $"expected the file, read {kind}", summary);
            }

            var file = BodyMediaExpectedFile(bodyCase)!.Value;
            var losses = new List<string>();
            if (!string.Equals((string?)read["sha256"], file.Sha256, StringComparison.Ordinal))
            {
                losses.Add(LossBytes);
            }

            if (!string.Equals((string?)read["contentType"], file.MediaType, StringComparison.Ordinal))
            {
                losses.Add(LossContentType);
            }

            if (!string.Equals((string?)read["fileName"], file.FileName, StringComparison.Ordinal))
            {
                losses.Add(LossFileName);
            }

            return losses.Count == 0
                ? BodyMediaCell(VerdictPass, null, "the same bytes, content type and file name", summary)
                : BodyMediaCell(VerdictFail, string.Join(",", losses), $"lost: {string.Join(", ", losses)} (expected {file.MediaType} {file.FileName ?? "(no name)"} {HashHead(file.Sha256)}..)", summary);
        }

        JsonObject ModelVerdict()
        {
            if (kind != "model")
            {
                return BodyMediaCell(VerdictFail, LossShape, $"expected the model, read {kind}", summary);
            }

            // Every client but the Throw style exposes the status and the media type it got: both are checked (a
            // preferXml client of a JSON-or-XML body must get XML)
            var received = (string?)read["mediaType"];
            if (prefersXml && bodyCase.Group == "json or xml" && client != "throw" && received != "application/xml")
            {
                return BodyMediaCell(VerdictFail, LossShape, $"asked for XML first (preferXml), got {received ?? "(not exposed)"}", summary);
            }

            if (client != "throw")
            {
                if ((int?)read["status"] != wire.Status)
                {
                    return BodyMediaCell(VerdictFail, LossStatus, $"expected the status {wire.Status}, the client exposes {(int?)read["status"]}", summary);
                }

                if (!string.Equals(received, wire.MediaType, StringComparison.Ordinal))
                {
                    return BodyMediaCell(VerdictFail, LossShape, $"expected {wire.MediaType}{(bodyCase.Group == "json or xml" ? $" (preferXml: {prefersXml})" : string.Empty)}, the client got {received ?? "(not exposed)"}", summary);
                }
            }

            return JsonNode.DeepEquals(WithoutNulls(read["model"]), WithoutNulls(bodyCase.Model))
                ? BodyMediaCell(VerdictPass, null, client == "throw" ? "the model (the Throw style exposes neither status nor media type)" : $"the model, {(int?)read["status"]} from {received}", summary)
                : BodyMediaCell(VerdictFail, LossFields, $"the model differs from the handler's {bodyCase.Model?.ToJsonString()}", summary);
        }

        static JsonArray Sorted(JsonArray? keys)
            => new([.. (keys ?? []).Select(k => (string?)k).Order(StringComparer.Ordinal).Select(k => (JsonNode?)JsonValue.Create(k))]);
    }

    private static string? BodyMediaExpectedArm(int status)
        => status switch
        {
            400 => "badRequest",
            404 => "notFound",
            >= 500 => "serverError",
            _ => null,
        };

    private static string HashHead(string sha256)
        => sha256.Length > 12 ? sha256[..12] : sha256;

    private static string WithoutAnyTraceId(string body)
        => Regex.Replace(body, @"(""traceId""\s*:\s*""[^""]*""|<traceId>[^<]*</traceId>)", string.Empty, RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

    private static JsonObject BodyMediaCell(
        string verdict,
        string? loss,
        string why,
        string summary)
    {
        var cell = Cell(verdict, why, summary);
        if (loss is not null)
        {
            cell["loss"] = new JsonObject { ["kind"] = loss };
        }

        return cell;
    }

    /// <summary>
    /// A JSON value without null members, so an absent optional field and a null one compare equal.
    /// </summary>
    private static JsonNode? WithoutNulls(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var copy = new JsonObject();
                foreach (var (key, value) in obj)
                {
                    if (value is not null)
                    {
                        copy[key] = WithoutNulls(value);
                    }
                }

                return copy;
            }

            case JsonArray array:
                return new JsonArray([.. array.Select(WithoutNulls)]);
            default:
                return node?.DeepClone();
        }
    }

    private static Dictionary<string, Dictionary<string, JsonObject>> RunBodyMediaTypeScriptClients(
        Uri baseAddress,
        string casesJson)
    {
        var work = Path.Combine(Path.GetTempPath(), "atc-bodymedia-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var yamlPath = Path.Combine(work, "ErrorBodies.yaml");
            File.WriteAllText(yamlPath, BodyMediaYaml);

            var cli = FindCliExecutable();
            foreach (var client in (string[])["Fetch", "Axios"])
            {
                var output = Path.Combine(work, client.ToLowerInvariant());
                var generate = RunTool(cli, $"generate client-typescript -s \"{yamlPath}\" -o \"{output}\" --client-type {client} --no-strict", work, TimeSpan.FromMinutes(3));
                Assert.True(generate.ExitCode == 0, $"generate client-typescript ({client}) failed:\n{generate.Output}");
            }

            File.WriteAllText(Path.Combine(work, "package.json"), BodyMediaPackageJson);
            File.WriteAllText(Path.Combine(work, "tsconfig.json"), E2ETsConfig);
            File.WriteAllText(Path.Combine(work, "harness.mts"), BodyMediaHarness);
            File.WriteAllText(Path.Combine(work, "cases.json"), new JsonObject { ["baseUrl"] = baseAddress.ToString().TrimEnd('/'), ["cases"] = JsonNode.Parse(casesJson) }.ToJsonString());

            var install = RunTool("npm", "install --no-audit --no-fund --loglevel=error", work, TimeSpan.FromMinutes(5));
            Assert.True(install.ExitCode == 0, $"npm install failed:\n{install.Output}");

            var result = new Dictionary<string, Dictionary<string, JsonObject>>(StringComparer.Ordinal);
            foreach (var client in (string[])["fetch", "axios", "axios-fetch"])
            {
                var run = RunTool("npx", $"tsx harness.mts {client}", work, TimeSpan.FromMinutes(5));
                Assert.True(run.ExitCode == 0, $"TypeScript {client} harness failed:\n{run.Output}");

                result["ts-" + client] = JsonNode.Parse(File.ReadAllText(Path.Combine(work, $"results-{client}.json")))!.AsArray()
                    .Select(n => n!.AsObject())
                    .ToDictionary(o => (string)o["name"]!, o => o, StringComparer.Ordinal);
            }

            return result;
        }
        finally
        {
            try
            {
                Directory.Delete(work, recursive: true);
            }
            catch (IOException)
            {
                // node_modules can stay locked for a moment; the temp folder is cleaned up by the OS.
            }
            catch (UnauthorizedAccessException)
            {
                // As above.
            }
        }
    }

    private const string BodyMediaPackageJson = """
        {
          "name": "atc-bodymedia-e2e",
          "version": "0.0.0",
          "private": true,
          "type": "module",
          "dependencies": {
            "@xmldom/xmldom": "^0.9.0",
            "axios": "^1.7.0"
          },
          "devDependencies": {
            "tsx": "^4.19.0",
            "typescript": "^5.6.0"
          }
        }
        """;

    // Calls every case through the generated ApiService against the live host and describes what the client exposes -
    // the Blob's own type, the error's own status: no fallback to the response headers. Axios runs on Node's default
    // http adapter ('axios') and on its fetch adapter ('axios-fetch'); a browser's default XHR adapter is not run.
    private const string BodyMediaHarness = """
        import { readFileSync, writeFileSync } from 'node:fs';
        import { createHash } from 'node:crypto';
        import axios from 'axios';
        import { DOMParser } from '@xmldom/xmldom';

        const which = process.argv[2];
        const folder = which === 'axios-fetch' ? 'axios' : which;
        const { baseUrl, cases } = JSON.parse(readFileSync('cases.json', 'utf8'));
        if (which === 'axios-fetch') {
          axios.defaults.adapter = 'fetch';
        }

        const { setXmlParser } = await import(`./${folder}/helpers/xml.ts`);
        setXmlParser(() => new DOMParser() as never);
        const { getDownloadFileName } = await import(`./${folder}/helpers/download.ts`);
        const { ApiService } = await import(`./${folder}/client/ApiService.ts`);
        const api: any = new ApiService(baseUrl);

        const header = (r: any, name: string) => {
          const h = r?.response?.headers;
          const v = typeof h?.get === 'function' ? h.get(name) : h?.[name];
          return v ? String(v).split(';')[0].trim() : null;
        };

        const results: Record<string, unknown>[] = [];
        for (const c of cases) {
          let record: Record<string, unknown> = { name: c.name };
          try {
            const client = api[c.segment.charAt(0).toLowerCase() + c.segment.slice(1)];
            const args = c.id !== null && c.id !== undefined
              ? [c.id]
              : c.upload
                ? [c.uploadTyped ? new Blob([Buffer.from(c.bytes, 'base64')], { type: c.upload }) : new Blob([Buffer.from(c.bytes, 'base64')])]
                : c.user
                  ? [c.user]
                  : [];
            const r = await client[c.method](...args) as any;
            const adapter = r.response?.config?.adapter;
            if (which !== 'fetch' && (which === 'axios-fetch') !== (adapter === 'fetch')) {
              throw new Error('the Axios adapter is ' + JSON.stringify(adapter) + ', not the one this column runs on');
            }

            if (r.status === 'ok' || r.status === 'created') {
              if (r.data instanceof Blob) {
                const bytes = Buffer.from(await r.data.arrayBuffer());
                record = {
                  ...record,
                  kind: 'file',
                  sha256: createHash('sha256').update(bytes).digest('hex'),
                  length: bytes.length,
                  contentType: r.data.type ? r.data.type.split(';')[0].trim() : null,
                  fileName: getDownloadFileName(r.response) ?? null,
                };
              } else {
                record = { ...record, kind: 'model', status: r.response?.status ?? null, arm: r.status, type: Array.isArray(r.data) ? 'array' : typeof r.data, mediaType: header(r, 'content-type'), model: JSON.parse(JSON.stringify(r.data ?? null)) };
              }
            } else {
              const e = r.error;
              const body = e?.body;
              record = {
                ...record,
                kind: 'error',
                status: e?.status ?? null,
                type: r.status,
                mediaType: header(r, 'content-type'),
                detail: e?.message ?? null,
                raw: typeof body === 'string' ? body : null,
                fields: body && typeof body === 'object' ? JSON.parse(JSON.stringify(body)) : null,
                errors: e?.errors ? Object.keys(e.errors) : null,
              };
            }
          } catch (err: any) {
            record = { ...record, kind: 'threw', status: null, detail: String(err?.message ?? err) };
          }
          results.push(record);
        }

        writeFileSync(`results-${which}.json`, JSON.stringify(results));
        """;
}