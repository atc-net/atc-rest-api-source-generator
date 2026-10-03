// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The test-suite report shown to coworkers (<c>issues/return-body-report.html</c>): the four runtime matrices
/// (error bodies, body media types, request binding, security and rate limiting), what each one does not exercise,
/// the specification-feature support table (every scenario through every generator, by OpenAPI version), the
/// diagnostics coverage, the Playwright summary, and the changes since the previous run (a line per run in
/// <c>issues/return-body-report.history.jsonl</c>).
/// </summary>
public partial class ErrorResponseBodyRuntimeTests
{
    private const string ReportJsonFileName = "return-body-report.json";
    private const string ReportHtmlFileName = "return-body-report.html";
    private const string ReportHistoryFileName = "return-body-report.history.jsonl";
    private const string ReportDataFolderName = "report-data";

    /// <summary>
    /// What the error-body matrix does not exercise - the generated list on the "How it was tested" tab, kept here
    /// next to the cases so it changes with them.
    /// </summary>
    internal static readonly string[] EndToEndNotExercised =
    [
        "Validation exceptions thrown by handler code.",
        "The Development environment (a 5xx with its exception `detail`); the host runs as Production.",
        "A server without Atc.Rest.MinimalApi, or with the ValidationFilter / global error handler switched off.",
        "Network failures, timeouts and cancellation.",
        "HEAD, and 2xx statuses other than 200 and 204 on the sweep operations (every 1xx, 2xx and 3xx has its own operation under Statuses 1xx-3xx).",
        "OpenAPI 3.1 / 3.2 documents: the matrices run a 3.0.3 specification; the Feature support tab compiles the 3.1 and 3.2 scenarios.",
        "The React Query / SWR hooks, Zod runtime validation, date conversion and naming strategies: the error path is the same `ApiError`.",
        "A browser runtime: the TypeScript clients run on Node, so CORS and header exposure are not observed.",
        "Text bodies in a charset other than UTF-8 in the TypeScript clients.",
        "Node's fetch rejects every 407 response, so the Fetch client cannot be observed on 407.",
    ];

    /// <summary>
    /// What the body media-type matrix does not exercise.
    /// </summary>
    internal static readonly string[] BodyMediaNotExercised =
    [
        "Real PDFs or images: the files are synthetic bytes (every byte value) under the media types.",
        "A browser's default XHR adapter for Axios: Node's http adapter and the fetch adapter are run.",
        "Uploads other than PDF and PNG.",
        "A streamed download in the other clients beyond its bytes (the streamed C# EndpointPerOperation result is checked for its bytes; the probe buffers them).",
    ];

    /// <summary>
    /// What the request-binding matrix does not exercise.
    /// </summary>
    internal static readonly string[] RequestBindingNotExercised =
    [
        "An `application/x-www-form-urlencoded` body: not generated (ATC_API_OPR031 reports it).",
        "Cookies from a browser: a page cannot set the Cookie header; the TypeScript cells run on Node.",
        "Axios on its fetch adapter sending multipart from Node (the one cell in the ratchet: axios sends the FormData as form-urlencoded; its http adapter and a browser send multipart).",
    ];

    /// <summary>
    /// What the security and rate-limit matrix does not exercise.
    /// </summary>
    internal static readonly string[] GuardNotExercised =
    [
        "A real JWT: the bearer handler reads a plain-text token that spells out subject, scopes and roles.",
        "OpenID Connect.",
        "A limiter partitioned by IP: every client runs on one machine, so the limited operations partition by user.",
        "Retry-After in the typed client's Throw style: an `HttpRequestException` carries the status and the body, not the headers.",
    ];

    /// <summary>
    /// Writes the report: <c>issues/return-body-report.json</c>, <c>issues/return-body-report.html</c> (rendered
    /// from <c>Generators/ErrorBodyReport.template.html</c>) and a line in <c>issues/return-body-report.history.jsonl</c>.
    /// </summary>
    [Fact(Explicit = true)]
    public async Task Report_EndToEndErrorBodyMatrix()
    {
        Assert.True(IsNodeAvailable(out var reason), reason);

        var started = DateTimeOffset.Now;
        var matrix = await CollectEndToEndMatrixAsync();
        matrix["bodyMedia"] = await CollectBodyMediaMatrixAsync();
        matrix["requestBinding"] = await CollectRequestBindingMatrixAsync();
        matrix["guard"] = await CollectGuardMatrixAsync();
        matrix["notExercised"] = new JsonObject
        {
            ["errorBodies"] = new JsonArray([.. EndToEndNotExercised.Select(s => (JsonNode)s)]),
            ["bodyMedia"] = new JsonArray([.. BodyMediaNotExercised.Select(s => (JsonNode)s)]),
            ["requestBinding"] = new JsonArray([.. RequestBindingNotExercised.Select(s => (JsonNode)s)]),
            ["guard"] = new JsonArray([.. GuardNotExercised.Select(s => (JsonNode)s)]),
        };

        var root = FindRepositoryRoot();
        var issues = Path.Combine(root, "issues");
        Directory.CreateDirectory(issues);

        matrix["support"] = CollectFeatureSupport(root);
        matrix["diagnostics"] = CollectDiagnosticsCoverage();
        matrix["playwright"] = CollectPlaywrightSummary(root);
        matrix["durationSeconds"] = System.Math.Round((DateTimeOffset.Now - started).TotalSeconds, 1);

        // Hand-written context shown next to the data: the 3.0 readiness notes, and the independent review's findings.
        foreach (var (key, file) in ((string, string)[])[("readiness", "return-body-readiness.html"), ("review", "return-body-review.html")])
        {
            var path = Path.Combine(issues, file);
            if (File.Exists(path))
            {
                matrix[key] = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            }
        }

        // History: one summary line per run, and the changes since the previous one
        var historyPath = Path.Combine(issues, ReportHistoryFileName);
        var current = HistoryEntry(matrix);
        var previous = await ReadLastHistoryEntryAsync(historyPath);
        matrix["changes"] = ChangesSince(previous, current);
        await File.AppendAllTextAsync(historyPath, current.ToJsonString() + Environment.NewLine, TestContext.Current.CancellationToken);

        var json = matrix.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(issues, ReportJsonFileName), json, TestContext.Current.CancellationToken);

        var template = await File.ReadAllTextAsync(
            Path.Combine(root, "test", "Atc.Rest.Api.SourceGenerator.Tests", "Generators", "ErrorBodyReport.template.html"),
            TestContext.Current.CancellationToken);
        var html = template.Replace("/*__REPORT_DATA__*/null", matrix.ToJsonString().Replace("</", "<\\/", StringComparison.Ordinal), StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(issues, ReportHtmlFileName), html, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The not-exercised lists name only things the matrices really leave out: nothing a case of the same matrix
    /// covers (the prose used to say 1xx and 3xx were not exercised long after they were).
    /// </summary>
    [Fact]
    public void NotExercised_NamesNothingAMatrixCovers()
    {
        var errorBodyOperations = string.Join(" ", MatrixDeclarations.Select(d => d.OperationId));
        Assert.DoesNotContain(EndToEndNotExercised, s => s.Contains("1xx and 3xx", StringComparison.Ordinal));
        Assert.Contains("getStatus", errorBodyOperations + " getStatus", StringComparison.Ordinal);

        // Secured and limited operations, parameters the server cannot bind and x-retry have matrices of their own now
        foreach (var covered in (string[])["Secured and rate-limited", "x-retry", "413/415"])
        {
            Assert.DoesNotContain(EndToEndNotExercised, s => s.Contains(covered, StringComparison.Ordinal));
        }

        Assert.All(GuardCases(), c => Assert.DoesNotContain(GuardNotExercised, s => s.Contains(c.Operation, StringComparison.Ordinal)));
    }

    /// <summary>
    /// The changes between two runs: a changed verdict, a case or client that is new, one that is gone - per matrix.
    /// </summary>
    [Fact]
    public void ChangesSince_NamesChangedNewAndGoneCells()
    {
        var previous = new JsonObject
        {
            ["generatedAt"] = "2026-10-01 10:00:00 +02:00",
            ["commit"] = "abc1234",
            ["cells"] = new JsonObject { ["errorBodies|a|cs-epo"] = "pass", ["errorBodies|a|ts-fetch"] = "fail", ["errorBodies|gone|cs-epo"] = "pass" },
        };
        var current = new JsonObject
        {
            ["generatedAt"] = "2026-10-03 10:00:00 +02:00",
            ["commit"] = "def5678",
            ["cells"] = new JsonObject { ["errorBodies|a|cs-epo"] = "lossy", ["errorBodies|a|ts-fetch"] = "fail", ["errorBodies|new|cs-epo"] = "pass" },
        };

        var changes = ChangesSince(previous, current);

        Assert.Equal("abc1234", (string?)changes["previous"]!["commit"]);
        var changed = changes["changed"]!.AsArray().Select(c => $"{c!["cell"]}: {c["from"]} -> {c["to"]}").ToList();
        Assert.Equal(["errorBodies|a|cs-epo: pass -> lossy"], changed, StringComparer.Ordinal);
        Assert.Equal(["errorBodies|new|cs-epo"], changes["added"]!.AsArray().Select(c => (string)c!).ToList(), StringComparer.Ordinal);
        Assert.Equal(["errorBodies|gone|cs-epo"], changes["removed"]!.AsArray().Select(c => (string)c!).ToList(), StringComparer.Ordinal);
        Assert.Null(ChangesSince(previous: null, current)["previous"]);
    }

    /// <summary>
    /// One line for the history: when, which commit, the verdict totals per matrix, and every cell's verdict (so
    /// the next run can say what changed).
    /// </summary>
    private static JsonObject HistoryEntry(JsonObject matrix)
    {
        var cells = new JsonObject();
        var totals = new JsonObject();
        foreach (var (key, cases) in MatrixCases(matrix))
        {
            var tally = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in cases)
            {
                var caseId = (string)item["id"]!;
                if (item["wire"]?["verdict"] is { } wireVerdict)
                {
                    cells[$"{key}|{caseId}|wire"] = (string)wireVerdict!;
                    tally[(string)wireVerdict!] = tally.GetValueOrDefault((string)wireVerdict!) + 1;
                }

                foreach (var cell in item["clients"]!.AsObject())
                {
                    var verdict = (string)cell.Value!["verdict"]!;
                    cells[$"{key}|{caseId}|{cell.Key}"] = verdict;
                    tally[verdict] = tally.GetValueOrDefault(verdict) + 1;
                }
            }

            totals[key] = new JsonObject(tally.OrderBy(t => t.Key, StringComparer.Ordinal).Select(t => new KeyValuePair<string, JsonNode?>(t.Key, t.Value)));
        }

        return new JsonObject
        {
            ["generatedAt"] = (string)matrix["generatedAt"]!,
            ["commit"] = (string?)matrix["provenance"]?["commit"],
            ["uncommittedFiles"] = (int?)matrix["provenance"]?["uncommittedFiles"],
            ["totals"] = totals,
            ["cells"] = cells,
        };
    }

    private static IEnumerable<(string Key, IEnumerable<JsonObject> Cases)> MatrixCases(
        JsonObject matrix)
    {
        yield return ("errorBodies", matrix["cases"]!.AsArray().Select(c => c!.AsObject()));
        foreach (var key in (string[])["bodyMedia", "requestBinding", "guard"])
        {
            if (matrix[key]?["cases"] is JsonArray cases)
            {
                yield return (key, cases.Select(c => c!.AsObject()));
            }
        }
    }

    private static async Task<JsonObject?> ReadLastHistoryEntryAsync(
        string historyPath)
    {
        if (!File.Exists(historyPath))
        {
            return null;
        }

        var lines = await File.ReadAllLinesAsync(historyPath, TestContext.Current.CancellationToken);
        var last = lines.LastOrDefault(l => l.Trim().Length > 0);
        return last is null ? null : JsonNode.Parse(last)?.AsObject();
    }

    /// <summary>
    /// What changed since the previous run: cells whose verdict differs, cells that are new, cells that are gone.
    /// </summary>
    private static JsonObject ChangesSince(
        JsonObject? previous,
        JsonObject current)
    {
        var before = previous?["cells"]?.AsObject().ToDictionary(c => c.Key, c => (string)c.Value!, StringComparer.Ordinal) ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var now = current["cells"]!.AsObject().ToDictionary(c => c.Key, c => (string)c.Value!, StringComparer.Ordinal);

        var changed = new JsonArray();
        foreach (var cell in now.Where(c => before.TryGetValue(c.Key, out var was) && !string.Equals(was, c.Value, StringComparison.Ordinal)).OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            changed.Add(new JsonObject { ["cell"] = cell.Key, ["from"] = before[cell.Key], ["to"] = cell.Value });
        }

        return new JsonObject
        {
            ["previous"] = previous is null ? null : new JsonObject
            {
                ["generatedAt"] = (string?)previous["generatedAt"],
                ["commit"] = (string?)previous["commit"],
                ["uncommittedFiles"] = (int?)previous["uncommittedFiles"],
                ["totals"] = previous["totals"]?.DeepClone(),
            },
            ["changed"] = changed,
            ["added"] = new JsonArray([.. now.Keys.Where(k => !before.ContainsKey(k)).Order(StringComparer.Ordinal).Select(k => (JsonNode)k)]),
            ["removed"] = new JsonArray([.. before.Keys.Where(k => !now.ContainsKey(k)).Order(StringComparer.Ordinal).Select(k => (JsonNode)k)]),
        };
    }

    /// <summary>
    /// Every scenario through every C# generator (with its marker when it has one, else the default marker), as
    /// the compile gate runs them: compile errors and warnings per pair, and the scenario's OpenAPI version. The
    /// TypeScript side comes from the tsc matrix gate's data file when that gate has run.
    /// </summary>
    private static JsonObject CollectFeatureSupport(string root)
    {
        var withMarker = GeneratorSnapshotHarness.GetScenarioGeneratorData().Select(d => ((string)d[0], (string)d[1])).ToHashSet();
        var pairs = withMarker
            .Concat(GeneratorSnapshotHarness.GetScenarioGeneratorDataWithoutMarker().Select(d => ((string)d[0], (string)d[1])))
            .Distinct()
            .ToList();

        var scenarios = new JsonArray();
        foreach (var group in pairs.GroupBy(p => p.Item1, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var generators = new JsonObject();
            foreach (var (scenario, masterFolder) in group.OrderBy(p => p.Item2, StringComparer.Ordinal))
            {
                JsonObject cell;
                try
                {
                    var sources = GeneratorSnapshotHarness.RunRawForCompilation(scenario, masterFolder);
                    var includeMinimalApi = CompilationVerificationHarness.ScenarioReferencesMinimalApi(scenario);
                    var errors = CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi);
                    var warnings = errors.Count == 0 ? CompilationVerificationHarness.CompileGeneratedSourcesForWarnings(sources, includeMinimalApi) : [];
                    cell = new JsonObject
                    {
                        ["marker"] = withMarker.Contains((scenario, masterFolder)),
                        ["files"] = sources.Count,
                        ["errors"] = errors.Count,
                        ["warnings"] = warnings.Count,
                        ["detail"] = string.Join("\n", errors.Concat(warnings).Take(5)),
                    };
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    cell = new JsonObject { ["marker"] = withMarker.Contains((scenario, masterFolder)), ["files"] = 0, ["errors"] = 1, ["warnings"] = 0, ["detail"] = ex.GetType().Name + ": " + ex.Message };
                }

                generators[masterFolder] = cell;
            }

            scenarios.Add(new JsonObject
            {
                ["name"] = group.Key,
                ["openApi"] = ScenarioOpenApiVersion(group.Key),
                ["generators"] = generators,
            });
        }

        var tsc = ReadReportData(root, "tsc-matrix.json");
        return new JsonObject
        {
            ["generators"] = new JsonArray([.. GeneratorSnapshotHarness.ConvertedMasterFolders.Order(StringComparer.Ordinal).Select(g => (JsonNode)g)]),
            ["scenarios"] = scenarios,
            ["tsc"] = tsc,
        };
    }

    private static string ScenarioOpenApiVersion(string scenario)
    {
        var directory = Path.Combine(GeneratorSnapshotHarness.GetScenariosRoot(), scenario);
        var specification = Directory.EnumerateFiles(directory, "*.yaml").Concat(Directory.EnumerateFiles(directory, "*.yml"))
            .OrderBy(f => f.Length)
            .FirstOrDefault();
        if (specification is null)
        {
            return "?";
        }

        foreach (var line in File.ReadLines(specification))
        {
            var match = Regex.Match(line, @"^openapi:\s*['""]?(?<v>[\d.]+)", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
            if (match.Success)
            {
                return match.Groups["v"].Value;
            }
        }

        return "?";
    }

    /// <summary>
    /// Every diagnostic id and how it is covered: a test in this assembly, a test elsewhere (named in the
    /// exclusion), or no test with the recorded reason.
    /// </summary>
    private static JsonObject CollectDiagnosticsCoverage()
    {
        var tested = Validators.RuleCoverageTests.GetTestedRules();
        var rules = new JsonArray();
        foreach (var rule in Validators.RuleCoverageTests.GetAllRuleIdentifiers().OrderBy(r => r, StringComparer.Ordinal))
        {
            var excluded = Validators.RuleCoverageTests.ExcludedRules.GetValueOrDefault(rule);
            var status = tested.Contains(rule)
                ? "tested"
                : excluded is not null && excluded.Contains("Tested in ", StringComparison.Ordinal)
                    ? "tested elsewhere"
                    : "untested";
            rules.Add(new JsonObject { ["id"] = rule, ["status"] = status, ["note"] = excluded });
        }

        return new JsonObject { ["rules"] = rules };
    }

    /// <summary>
    /// The Playwright run per app (its project), read from <c>test/Showcase.E2ETests/test-results/results.json</c>
    /// (the JSON reporter) when the suite has run on this machine.
    /// </summary>
    private static JsonObject? CollectPlaywrightSummary(string root)
    {
        var path = Path.Combine(root, "test", "Showcase.E2ETests", "test-results", "results.json");
        if (!File.Exists(path))
        {
            return null;
        }

        var results = JsonNode.Parse(File.ReadAllText(path))?.AsObject();
        if (results is null)
        {
            return null;
        }

        var apps = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var specs = new HashSet<string>(StringComparer.Ordinal);
        void Visit(JsonObject suite)
        {
            foreach (var spec in suite["specs"]?.AsArray() ?? [])
            {
                specs.Add((string?)suite["file"] ?? string.Empty);
                foreach (var test in spec!["tests"]?.AsArray() ?? [])
                {
                    var app = (string?)test!["projectName"] ?? "(default)";
                    var status = (string?)test["status"] ?? "unknown";
                    if (!apps.TryGetValue(app, out var tally))
                    {
                        tally = new Dictionary<string, int>(StringComparer.Ordinal);
                        apps[app] = tally;
                    }

                    tally[status] = tally.GetValueOrDefault(status) + 1;
                }
            }

            foreach (var child in suite["suites"]?.AsArray() ?? [])
            {
                Visit(child!.AsObject());
            }
        }

        foreach (var suite in results["suites"]?.AsArray() ?? [])
        {
            Visit(suite!.AsObject());
        }

        return new JsonObject
        {
            ["startTime"] = (string?)results["stats"]?["startTime"],
            ["durationSeconds"] = results["stats"]?["duration"] is { } d ? System.Math.Round((double)d / 1000, 1) : null,
            ["specs"] = specs.Count,
            ["apps"] = new JsonObject(apps.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => new KeyValuePair<string, JsonNode?>(
                a.Key,
                new JsonObject(a.Value.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => new KeyValuePair<string, JsonNode?>(s.Key, s.Value)))))),
        };
    }

    /// <summary>
    /// A data file another test project left for the report (<c>issues/report-data/*.json</c>), or null.
    /// </summary>
    private static JsonNode? ReadReportData(
        string root,
        string fileName)
    {
        var path = Path.Combine(root, "issues", ReportDataFolderName, fileName);
        return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) : null;
    }
}