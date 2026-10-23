// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The test-suite report (<c>docs/test-suite-report.html</c>, its data <c>docs/test-suite-report.json</c>, all tracked in git):
/// the five runtime matrices (error bodies, body media types, request binding, security and rate limiting, features),
/// what each one does not exercise, the ratchets (the cells expected to fail), a machine-readable summary,
/// the specification-feature support table (every scenario through every generator, by OpenAPI version), the
/// diagnostics coverage, the Playwright summary, and the changes since the previous run (that run's verdict per cell,
/// <c>docs/test-suite-report.history.jsonl</c>).
/// </summary>
public partial class ErrorResponseBodyRuntimeTests
{
    /// <summary>
    /// What the error-body matrix does not exercise - the generated list on the "How it was tested" tab, kept here
    /// next to the cases so it changes with them.
    /// </summary>
    internal static readonly string[] EndToEndNotExercised =
    [
        "Validation exceptions thrown by handler code (the feature matrix runs one).",
        "The Development environment beyond one exception per host: the matrices run as Production, and Production_A5xxFromAnException_CarriesNothingOfIt_AndDevelopmentNamesIt checks that a Production 5xx carries nothing of the exception on either host and that Development names it.",
        "Network failures, timeouts and cancellation (the feature matrix runs a reset, a hang, a stall and a mid-body cancel).",
        "HEAD, and 2xx statuses other than 200 and 204 on the sweep operations (every 1xx, 2xx and 3xx has its own operation under Statuses 1xx-3xx; a HEAD answered 200 and 404 with the GET's headers and no body is read by every client in TypedClient_Head_ReturnsNoValue_AndReadsNoBody, OperationClient_Head_IsItsStatus_WithoutAValue and the TypeScript HEAD gate).",
        "What only OpenAPI 3.1 / 3.2 can say, beyond the request-binding matrix's 3.1 body (type arrays, null on the wire, a const, a tuple): every matrix also runs from its specification as a 3.1 document, which says what the 3.0 one says; the Feature support tab compiles the 3.1 and 3.2 scenarios.",
        "The React Query / SWR hooks, Zod runtime validation, date conversion and naming strategies: the error path is the same `ApiError`.",
        "A browser runtime: the TypeScript clients run on Node, so CORS and header exposure are not observed.",
        "Node's fetch rejects every 407 response, so the Fetch client cannot be observed on 407.",
        "Node's fetch resends a request answered 421 on a reused connection on a new one (the Fetch standard), and now and then fails the resent body (UND_ERR_REQ_CONTENT_LENGTH_MISMATCH, about 2 in 100 against Kestrel): the harness makes such a call again, three times at most, and a Fetch cell that fails all three is platform.",
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
        "Cookies from a browser: a page cannot set the Cookie header; the TypeScript cells run on Node.",
    ];

    /// <summary>
    /// What the security and rate-limit matrix does not exercise.
    /// </summary>
    internal static readonly string[] GuardNotExercised =
    [
        "A real JWT in the matrix: its bearer handler reads a plain-text token that spells out subject, scopes and roles (GeneratedPolicies_ReadScopesAndRolesFromARealJwt runs the generated scope and role checks against signed JWTs read by AddJwtBearer).",
        "OpenID Connect.",
        "A limiter partitioned by IP: every client runs on one machine, so the limited operations partition by user.",
    ];

    /// <summary>
    /// What the feature matrix does not exercise.
    /// </summary>
    internal static readonly string[] FeatureNotExercised =
    [
        "A browser runtime: the React Query and SWR hooks render with React on jsdom in Node.",
        "The hooks on a 304: a browser cache sends the condition, not a hook.",
        "A cancelled SWR read: SWR hands its fetcher no signal (a hook's timeout of the caller's own is run, through the client's fetch option).",
        "Webhooks and output caching.",
    ];

    private const string ReportJsonFileName = "test-suite-report.json";

    private const string ReportHtmlFileName = "test-suite-report.html";

    private const string ReportHistoryFileName = "test-suite-report.history.jsonl";

    private const string ReportFolderName = "docs";

    /// <summary>
    /// Writes the report: <c>docs/test-suite-report.html</c> (rendered from <c>Generators/ErrorBodyReport.template.html</c>,
    /// the page the README links to), <c>docs/test-suite-report.json</c> (its data) and
    /// <c>docs/test-suite-report.history.jsonl</c> (this run's verdict per cell, which the next run is compared with) - all
    /// three tracked, so a run is compared with the last committed one.
    /// </summary>
    [Fact(Explicit = true)]
    public async Task Report_EndToEndErrorBodyMatrix()
    {
        Assert.True(IsNodeAvailable(out var reason), reason);

        var started = DateTimeOffset.Now;
        var matrix = await CollectEndToEndMatrixAsync();
        matrix["bodyMedia"] = await CollectBodyMediaMatrixAsync();
        matrix["requestBinding"] = await CollectRequestBindingMatrixAsync();

        // The README's second path: MapEndpoints() without Atc.Rest.MinimalApi, the host wiring written by hand
        matrix["errorBodiesPlain"] = await CollectEndToEndMatrixAsync(plainHost: true);
        matrix["requestBindingPlain"] = await CollectRequestBindingMatrixAsync(plainHost: true);

        // The third host: the package referenced, the ValidationFilter and the global error handler switched off
        matrix["errorBodiesOff"] = await CollectEndToEndMatrixAsync(switchedOff: true);
        matrix["requestBindingOff"] = await CollectRequestBindingMatrixAsync(switchedOff: true);

        // The request-binding specification as an OpenAPI 3.1 document, with a 3.1 body
        matrix["requestBinding31"] = await CollectRequestBindingMatrixAsync(openApi31: true);
        matrix["guard"] = await CollectGuardMatrixAsync();
        matrix["feature"] = await CollectFeatureMatrixAsync();

        // The other matrices from their specifications as OpenAPI 3.1 documents too
        matrix["errorBodies31"] = await CollectEndToEndMatrixAsync(openApi31: true);
        matrix["bodyMedia31"] = await CollectBodyMediaMatrixAsync(openApi31: true);
        matrix["guard31"] = await CollectGuardMatrixAsync(openApi31: true);
        matrix["feature31"] = await CollectFeatureMatrixAsync(openApi31: true);

        // An enum value in any casing, on each kind of host
        foreach (var (key, _) in EnumCasingHosts)
        {
            matrix[key] = await CollectEnumCasingMatrixAsync(key);
        }

        // Every constraint the generator writes a validation attribute for, valid and invalid, wherever a value arrives
        foreach (var (key, _) in ValidationHosts)
        {
            matrix[key] = await CollectValidationMatrixAsync(key);
        }

        matrix["notExercised"] = new JsonObject
        {
            ["errorBodies"] = new JsonArray([.. EndToEndNotExercised.Select(s => (JsonNode)s)]),
            ["bodyMedia"] = new JsonArray([.. BodyMediaNotExercised.Select(s => (JsonNode)s)]),
            ["requestBinding"] = new JsonArray([.. RequestBindingNotExercised.Select(s => (JsonNode)s)]),
            ["guard"] = new JsonArray([.. GuardNotExercised.Select(s => (JsonNode)s)]),
            ["feature"] = new JsonArray([.. FeatureNotExercised.Select(s => (JsonNode)s)]),
            ["enumCasing"] = new JsonArray([.. EnumCasingNotExercised.Select(s => (JsonNode)s)]),
            ["validation"] = new JsonArray([.. ValidationNotExercised.Select(s => (JsonNode)s)]),
        };

        var root = FindRepositoryRoot();
        var docs = Path.Combine(root, ReportFolderName);
        Directory.CreateDirectory(docs);

        matrix["support"] = CollectFeatureSupport(root);
        matrix["diagnostics"] = CollectDiagnosticsCoverage();
        matrix["playwright"] = CollectPlaywrightSummary(root);
        matrix["testSuites"] = CollectTestSuites(root);
        matrix["coverage"] = CollectCoverage(root);
        matrix["openApiConstructs"] = CollectOpenApiConstructs(root);
        matrix["testLayers"] = CollectTestLayers(matrix);
        matrix["fuzz"] = ReadReportData(root, "test-suite-report.fuzz.json");
        matrix["mutation"] = CollectMutation(root);
        matrix["ratchet"] = Ratchets();
        matrix["ratchetReasons"] = RatchetReasons();
        matrix["durationSeconds"] = System.Math.Round((DateTimeOffset.Now - started).TotalSeconds, 1);
        matrix["summary"] = Summary(matrix);

        // Hand-written context shown next to the data: the 3.0 readiness notes, and the independent review's findings.
        foreach (var (key, file) in ((string, string)[])[("readiness", "test-suite-report.readiness.html"), ("review", "test-suite-report.review.html")])
        {
            var path = Path.Combine(docs, file);
            if (File.Exists(path))
            {
                matrix[key] = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            }
        }

        // History: the previous run's verdict per cell, replaced by this run's - one line of a few hundred kB, so the
        // file is overwritten rather than appended to (git keeps the older runs)
        var historyPath = Path.Combine(docs, ReportHistoryFileName);
        var current = HistoryEntry(matrix);
        var previous = await ReadLastHistoryEntryAsync(historyPath);
        matrix["changes"] = ChangesSince(previous, current);
        matrix["history"] = RunHistory([.. CommittedHistoryEntries(root), (current, null, null)]);
        await File.WriteAllTextAsync(historyPath, current.ToJsonString() + Environment.NewLine, TestContext.Current.CancellationToken);

        var json = matrix.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(docs, ReportJsonFileName), json, TestContext.Current.CancellationToken);

        var template = await File.ReadAllTextAsync(
            Path.Combine(root, "test", "Atc.Rest.Api.SourceGenerator.Tests", "Generators", "ErrorBodyReport.template.html"),
            TestContext.Current.CancellationToken);
        var html = template.Replace("/*__REPORT_DATA__*/", PackReportData(matrix.ToJsonString()), StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(docs, ReportHtmlFileName), html, TestContext.Current.CancellationToken);
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
    /// A not-exercised sentence that points at ratcheted cells is only true while the matrix has a ratchet (the
    /// feature list still named the unknown-enum cells after extensible enums made them pass).
    /// </summary>
    [Fact]
    public void NotExercised_NamesRatchetedCellsOnlyWhileTheMatrixHasThem()
    {
        var ratchets = Ratchets();
        foreach (var (key, sentences) in ((string, string[])[])[
            ("errorBodies", EndToEndNotExercised),
            ("bodyMedia", BodyMediaNotExercised),
            ("requestBinding", RequestBindingNotExercised),
            ("guard", GuardNotExercised),
            ("feature", FeatureNotExercised)])
        {
            if (ratchets[key]!.AsArray().Count == 0)
            {
                Assert.DoesNotContain(sentences, s => s.Contains("ratchet", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    /// <summary>
    /// A known failure is only known with its reason: every ratcheted cell has one, and the report shows it next to
    /// the cell (the page once looked the reason up in the not-exercised sentences and found none).
    /// </summary>
    [Fact]
    public void RatchetReasons_GiveEveryKnownCellAReason()
    {
        var reasons = RatchetReasons();
        foreach (var (key, cells) in Ratchets())
        {
            Assert.All(cells!.AsArray(), cell => Assert.False(string.IsNullOrWhiteSpace((string?)reasons[key]?[(string)cell!])));
        }
    }

    /// <summary>
    /// The run history says, run by run, what changed against the run before: the totals, the cells whose verdict
    /// changed, and the cases that came and went - the newest run first, a run recorded twice only once.
    /// </summary>
    [Fact]
    public void RunHistory_SaysWhatEachRunChangedAgainstTheOneBefore()
    {
        static JsonObject Run(
            string generatedAt,
            string commit,
            params (string Cell, string Verdict)[] cells)
            => new()
            {
                ["generatedAt"] = generatedAt,
                ["commit"] = commit,
                ["uncommittedFiles"] = 0,
                ["totals"] = new JsonObject(),
                ["cells"] = new JsonObject(cells.Select(c => new KeyValuePair<string, JsonNode?>(c.Cell, c.Verdict))),
            };

        var first = Run("2026-10-01 10:00:00 +02:00", "aaaaaaaa", ("guard|open|cs-epo", "pass"), ("guard|open|ts-fetch", "fail"), ("guard|gone|cs-epo", "pass"));
        var second = Run("2026-10-02 10:00:00 +02:00", "bbbbbbbb", ("guard|open|cs-epo", "pass"), ("guard|open|ts-fetch", "pass"), ("feature|new case|cs-epo", "lossy"), ("feature|new case|ts-fetch", "pass"));

        var history = RunHistory([(first, "1111111", "docs: the first run"), (first.DeepClone().AsObject(), "1111112", "docs: the same run again"), (second, null, null)]);

        Assert.Equal(2, history.Count);
        var newest = history[0]!.AsObject();
        Assert.Equal("bbbbbbbb", (string?)newest["commit"]);
        Assert.Null(newest["reportCommit"]);
        Assert.Equal(4, (int?)newest["cells"]);
        Assert.Equal(3, (int?)newest["verdicts"]!["pass"]);
        Assert.Equal(1, (int?)newest["verdicts"]!["lossy"]);
        var changed = Assert.Single(newest["changed"]!.AsArray())!;
        Assert.Equal("guard|open|ts-fetch", (string?)changed["cell"]);
        Assert.Equal("fail", (string?)changed["from"]);
        Assert.Equal("pass", (string?)changed["to"]);
        Assert.Equal(2, (int?)newest["added"]);
        Assert.Equal(1, (int?)newest["removed"]);
        Assert.Equal("feature|new case", (string?)Assert.Single(newest["addedCases"]!.AsArray()));
        Assert.Equal("guard|gone", (string?)Assert.Single(newest["removedCases"]!.AsArray()));

        var oldest = history[1]!.AsObject();
        Assert.Equal("aaaaaaaa", (string?)oldest["commit"]);
        Assert.Equal("1111111", (string?)oldest["reportCommit"]);
        Assert.Equal("docs: the first run", (string?)oldest["subject"]);
        Assert.Equal(1, (int?)oldest["verdicts"]!["fail"]);
        Assert.Empty(oldest["changed"]!.AsArray());
        Assert.Null(oldest["added"]);
    }

    /// <summary>
    /// Playwright's statuses per test are expected, unexpected, flaky (failed, then passed on a retry) and skipped:
    /// only unexpected is a failure - CI retries, so a flaky test must not read as a failed one.
    /// </summary>
    [Fact]
    public void Summary_CountsOnlyUnexpectedPlaywrightTestsAsFailed()
    {
        JsonObject Matrix(JsonObject apps)
            => new() { ["cases"] = new JsonArray(), ["ratchet"] = new JsonObject(), ["playwright"] = new JsonObject { ["apps"] = apps } };

        var flaky = Summary(Matrix(new JsonObject { ["blazor"] = new JsonObject { ["expected"] = 3, ["flaky"] = 1, ["skipped"] = 2 } }));
        var failing = Summary(Matrix(new JsonObject { ["blazor"] = new JsonObject { ["expected"] = 3, ["unexpected"] = 1 }, ["react-axios"] = new JsonObject { ["unexpected"] = 2 } }));

        Assert.Equal(0, (int?)flaky["playwright"]!["failed"]);
        Assert.Equal(1, (int?)flaky["playwright"]!["flaky"]);
        Assert.Equal(3, (int?)failing["playwright"]!["failed"]);
    }

    /// <summary>
    /// A Playwright run that started before the commit the report is built from tested older code: it is stale, and
    /// so is one whose start time cannot be read. (A local results.json is whatever ran last on the machine.)
    /// </summary>
    [Theory]
    [InlineData("2026-10-07T10:00:00.000Z", "2026-10-07T12:00:00+02:00", false)]
    [InlineData("2026-10-07T10:30:00.000Z", "2026-10-07T12:00:00+02:00", false)]
    [InlineData("2026-10-07T09:59:59.000Z", "2026-10-07T12:00:00+02:00", true)]
    [InlineData(null, "2026-10-07T12:00:00+02:00", true)]
    [InlineData("yesterday", "2026-10-07T12:00:00+02:00", true)]
    public void APlaywrightRunOlderThanTheCommit_IsStale(
        string? startTime,
        string commitTime,
        bool expected)
        => Assert.Equal(expected, PlaywrightRunIsStale(startTime, DateTimeOffset.Parse(commitTime, CultureInfo.InvariantCulture)));

    /// <summary>
    /// A stale Playwright run is no evidence for the report's commit: the summary says so, and does not count it as
    /// green.
    /// </summary>
    [Fact]
    public void Summary_MarksAStalePlaywrightRun()
    {
        var matrix = new JsonObject
        {
            ["cases"] = new JsonArray(),
            ["ratchet"] = new JsonObject(),
            ["playwright"] = new JsonObject { ["stale"] = true, ["apps"] = new JsonObject { ["blazor"] = new JsonObject { ["expected"] = 3 } } },
        };

        Assert.True((bool?)Summary(matrix)["playwright"]!["stale"]);
    }

    /// <summary>
    /// The Feature support tab names the overlay a scenario's markers list: its row is the specification as the
    /// overlay changes it, which the raw file alone does not show.
    /// </summary>
    [Fact]
    public void ScenarioOverlay_NamesTheOverlayOfAScenarioThatHasOne()
    {
        Assert.Equal("Overlays.overlay.yaml", ScenarioOverlay("Overlays"));
        Assert.Equal("OverlaysMultiPart.overlay.yaml", ScenarioOverlay("OverlaysMultiPart"));
        Assert.Null(ScenarioOverlay("Demo"));
    }

    /// <summary>
    /// The hand-written notes shown next to the data carry no test counts: a count typed by hand is stale after the
    /// next commit (the readiness notes said 6,669 tests long after the suite had grown), the page's own numbers
    /// come from the run.
    /// </summary>
    [Fact]
    public void HandWrittenNotes_CarryNoTestCounts()
    {
        var docs = Path.Combine(FindRepositoryRoot(), ReportFolderName);
        foreach (var file in (string[])["test-suite-report.readiness.html", "test-suite-report.review.html"])
        {
            var path = Path.Combine(docs, file);
            if (!File.Exists(path))
            {
                continue;
            }

            var counts = Regex.Matches(File.ReadAllText(path), @"\b\d[\d,]*\s+(?:tests|specs)\b", RegexOptions.None, TimeSpan.FromSeconds(1));
            Assert.True(counts.Count == 0, $"{file}: {string.Join(", ", counts.Select(m => m.Value))}");
        }
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
    /// The machine-readable summary of the run: per matrix the verdict counts and the known failures; the compile
    /// gate, the diagnostics coverage and Playwright in one number each (a flaky Playwright test - failed, then passed
    /// on a retry - is counted apart, not as a failure); and the overall state - <c>green</c> (no
    /// fail), <c>known-failures</c> (every fail is in a ratchet) or <c>regressions</c> (a fail no ratchet lists).
    /// </summary>
    internal static JsonObject Summary(JsonObject matrix)
    {
        var matrices = new JsonObject();
        var unexpected = new JsonArray();
        var knownCount = 0;
        foreach (var (key, cases) in MatrixCases(matrix))
        {
            var known = matrix["ratchet"]?[key]?.AsArray().Select(k => (string)k!).ToHashSet(StringComparer.Ordinal) ?? [];
            var tally = new Dictionary<string, int>(StringComparer.Ordinal);
            var caseCount = 0;
            var clients = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in cases)
            {
                caseCount++;
                if (item["wire"]?["verdict"] is { } wireVerdict)
                {
                    tally[(string)wireVerdict!] = tally.GetValueOrDefault((string)wireVerdict!) + 1;
                    if (string.Equals((string)wireVerdict!, "fail", StringComparison.Ordinal))
                    {
                        // A server answer the matrix's ratchet lists (as "case [wire]") is known, like a client cell
                        if (known.Contains($"{item["id"]} [wire]"))
                        {
                            knownCount++;
                        }
                        else
                        {
                            unexpected.Add($"{key}|{item["id"]}|wire");
                        }
                    }
                }

                foreach (var cell in item["clients"]!.AsObject())
                {
                    clients.Add(cell.Key);
                    var verdict = (string)cell.Value!["verdict"]!;
                    tally[verdict] = tally.GetValueOrDefault(verdict) + 1;

                    // A harness that did not run is never a known failure: the column was not judged at all
                    if (verdict is not ("fail" or VerdictHarness))
                    {
                        continue;
                    }

                    if (known.Contains($"{item["id"]} [{cell.Key}]"))
                    {
                        knownCount++;
                    }
                    else
                    {
                        unexpected.Add($"{key}|{item["id"]}|{cell.Key}");
                    }
                }
            }

            matrices[key] = new JsonObject
            {
                ["cases"] = caseCount,
                ["clients"] = clients.Count,
                ["verdicts"] = new JsonObject(tally.OrderBy(t => t.Key, StringComparer.Ordinal).Select(t => new KeyValuePair<string, JsonNode?>(t.Key, t.Value))),
                ["knownFailures"] = known.Count,
            };
        }

        var support = matrix["support"]?["scenarios"]?.AsArray();
        var diagnostics = matrix["diagnostics"]?["rules"]?.AsArray();
        var playwright = matrix["playwright"]?["apps"]?.AsObject();
        var playwrightStale = (bool?)matrix["playwright"]?["stale"] ?? false;
        return new JsonObject
        {
            ["state"] = unexpected.Count > 0 ? "regressions" : knownCount > 0 ? "known-failures" : "green",
            ["unexpectedFailures"] = unexpected,
            ["matrices"] = matrices,
            ["compileGate"] = support is null ? null : new JsonObject
            {
                ["scenarios"] = support.Count,
                ["clean"] = support.Count(s => s!["generators"]!.AsObject().All(g => (int?)g.Value!["errors"] == 0 && (int?)g.Value!["warnings"] == 0)),
                ["generators"] = matrix["support"]!["generators"]!.AsArray().Count,
            },
            ["diagnostics"] = diagnostics is null ? null : new JsonObject
            {
                ["rules"] = diagnostics.Count,
                ["tested"] = diagnostics.Count(r => !string.Equals((string?)r!["status"], "untested", StringComparison.Ordinal)),
            },
            ["playwright"] = playwright is null ? null : new JsonObject
            {
                ["apps"] = playwright.Count,
                ["failed"] = playwright.Sum(a => a.Value!.AsObject().Where(s => s.Key is not ("expected" or "passed" or "skipped" or "flaky")).Sum(s => (int)s.Value!)),
                ["flaky"] = playwright.Sum(a => (int?)a.Value!["flaky"] ?? 0),
                ["stale"] = playwrightStale,
            },
        };
    }

    /// <summary>
    /// The cells each matrix expects to fail - its ratchet: a listed cell must fail, an unlisted one must pass, so a
    /// fail on the page is either known (listed here, with the reason the matrix records) or a regression.
    /// </summary>
    private static JsonObject Ratchets()
    {
        var ratchets = new JsonObject
        {
            // The error-body and body-media matrices allow no failing cell at all (their tests assert none), so their
            // ratchet is empty by construction; the others list the cells their tests expect to fail
            ["errorBodies"] = new JsonArray(),
            ["errorBodiesPlain"] = new JsonArray(),
            ["errorBodiesOff"] = new JsonArray(),
            ["bodyMedia"] = new JsonArray(),
            ["requestBinding"] = new JsonArray([.. KnownRequestBindingFailures.Keys.Order(StringComparer.Ordinal).Select(s => (JsonNode)s)]),
            ["requestBindingPlain"] = new JsonArray([.. KnownPlainRequestBindingFailures.Keys.Order(StringComparer.Ordinal).Select(s => (JsonNode)s)]),
            ["requestBindingOff"] = new JsonArray([.. KnownSwitchedOffRequestBindingFailures.Keys.Order(StringComparer.Ordinal).Select(s => (JsonNode)s)]),
            ["requestBinding31"] = new JsonArray([.. KnownRequestBinding31Failures.Keys.Order(StringComparer.Ordinal).Select(s => (JsonNode)s)]),
            ["guard"] = new JsonArray([.. KnownGuardFailures.Keys.Order(StringComparer.Ordinal).Select(s => (JsonNode)s)]),
            ["feature"] = new JsonArray([.. KnownFeatureFailures.Keys.Order(StringComparer.Ordinal).Select(s => (JsonNode)s)]),

            // The twins from an OpenAPI 3.1 document expect what their matrix expects
            ["errorBodies31"] = new JsonArray(),
            ["bodyMedia31"] = new JsonArray(),
            ["guard31"] = new JsonArray([.. KnownGuardFailures.Keys.Order(StringComparer.Ordinal).Select(s => (JsonNode)s)]),
            ["feature31"] = new JsonArray([.. KnownFeatureFailures.Keys.Order(StringComparer.Ordinal).Select(s => (JsonNode)s)]),
        };

        foreach (var (key, _) in EnumCasingHosts)
        {
            ratchets[key] = new JsonArray([.. (KnownEnumCasingFailures.GetValueOrDefault(key)?.Keys.AsEnumerable() ?? []).Order(StringComparer.Ordinal).Select(s => (JsonNode)s)]);
        }

        foreach (var (key, _) in ValidationHosts)
        {
            ratchets[key] = new JsonArray([.. (KnownValidationFailures.GetValueOrDefault(key)?.Keys.AsEnumerable() ?? []).Order(StringComparer.Ordinal).Select(s => (JsonNode)s)]);
        }

        return ratchets;
    }

    /// <summary>
    /// Why each ratcheted cell is expected to fail, per matrix: <c>case [client]</c> to the reason its
    /// <c>Known…Failures</c> entry records.
    /// </summary>
    private static JsonObject RatchetReasons()
    {
        var reasons = new JsonObject();
        foreach (var (key, known) in ((string, Dictionary<string, string>)[])[
            ("requestBinding", KnownRequestBindingFailures),
            ("requestBindingPlain", KnownPlainRequestBindingFailures),
            ("requestBindingOff", KnownSwitchedOffRequestBindingFailures),
            ("requestBinding31", KnownRequestBinding31Failures),
            ("guard", KnownGuardFailures),
            ("feature", KnownFeatureFailures),
            ("guard31", KnownGuardFailures),
            ("feature31", KnownFeatureFailures),
            .. EnumCasingHosts.Select(h => (h.Key, KnownEnumCasingFailures.GetValueOrDefault(h.Key) ?? new Dictionary<string, string>(StringComparer.Ordinal))),
            .. ValidationHosts.Select(h => (h.Key, KnownValidationFailures.GetValueOrDefault(h.Key) ?? new Dictionary<string, string>(StringComparer.Ordinal)))])
        {
            reasons[key] = new JsonObject(known.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => KeyValuePair.Create(k.Key, (JsonNode?)k.Value)));
        }

        return reasons;
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
            ["trend"] = TrendTotals(matrix),
            ["cells"] = cells,
        };
    }

    /// <summary>
    /// The run's totals beside the matrix cells, so the history can show them over time: the test suites' tests,
    /// failures and time, the merged line and branch coverage, the fuzz mutants and crashes, the Playwright tests.
    /// A part this run did not have is left out.
    /// </summary>
    private static JsonObject TrendTotals(JsonObject matrix)
    {
        var trend = new JsonObject();
        var suites = matrix["testSuites"]?["suites"]?.AsArray().Where(s => s?["missing"] is null).ToList() ?? [];
        if (suites.Count > 0)
        {
            trend["suites"] = suites.Count;
            trend["tests"] = suites.Sum(s => (int?)s!["tests"] ?? 0);
            trend["failed"] = suites.Sum(s => (int?)s!["failed"] ?? 0);
            trend["seconds"] = System.Math.Round(suites.Sum(s => (double?)s!["ms"] ?? 0) / 1000, 1);
        }

        if (matrix["coverage"] is { } coverage)
        {
            trend["linePct"] = coverage["linePct"]?.DeepClone();
            trend["branchPct"] = coverage["branchPct"]?.DeepClone();
        }

        if (matrix["fuzz"]?["scenarios"]?.AsArray() is { } fuzz)
        {
            var mutants = fuzz.SelectMany(s => s!["mutants"]!.AsArray()).ToList();
            trend["fuzzMutants"] = mutants.Count;
            trend["fuzzCrashes"] = mutants.Sum(m => m!["crashes"]!.AsArray().Count);
        }

        if (matrix["playwright"]?["apps"]?.AsObject() is { } apps)
        {
            trend["playwright"] = apps.Sum(a => a.Value!.AsObject().Sum(s => (int?)s.Value ?? 0));
            trend["playwrightFailed"] = apps.Sum(a => (int?)a.Value!["unexpected"] ?? 0);
        }

        return trend;
    }

    private static IEnumerable<(string Key, IEnumerable<JsonObject> Cases)> MatrixCases(
        JsonObject matrix)
    {
        yield return ("errorBodies", matrix["cases"]!.AsArray().Select(c => c!.AsObject()));
        foreach (var key in (string[])["errorBodiesPlain", "errorBodiesOff", "errorBodies31", "bodyMedia", "bodyMedia31", "requestBinding", "requestBindingPlain", "requestBindingOff", "requestBinding31", "guard", "guard31", "feature", "feature31", .. EnumCasingHosts.Select(h => h.Key), .. ValidationHosts.Select(h => h.Key)])
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
    /// The runs git keeps: every committed version of the history file (it holds one run), oldest first, with the
    /// commit that recorded it. A shallow clone has fewer, and without git there are none.
    /// </summary>
    private static List<(JsonObject Entry, string? ReportCommit, string? Subject)> CommittedHistoryEntries(
        string root)
    {
        var entries = new List<(JsonObject Entry, string? ReportCommit, string? Subject)>();
        var path = $"{ReportFolderName}/{ReportHistoryFileName}";
        var log = GitText(root, $"log --reverse --format=%h%x09%s -- {path}") ?? string.Empty;
        foreach (var line in log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('\t', 2);
            if (parts.Length != 2 || !Regex.IsMatch(parts[0], "^[0-9a-f]{7,40}$", RegexOptions.None, TimeSpan.FromSeconds(1)))
            {
                continue;
            }

            var json = GitText(root, $"show {parts[0]}:{path}")?.Split('\n').LastOrDefault(l => l.StartsWith("{\"generatedAt\"", StringComparison.Ordinal));
            if (json is not null && TryParseJson(json.Trim()) is JsonObject entry)
            {
                entries.Add((entry, parts[0], parts[1]));
            }
        }

        return entries;
    }

    // git's standard output as UTF-8 text (case names are not all ASCII), or null when git fails or is not there
    private static string? GitText(
        string root,
        string arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        try
        {
            using var process = System.Diagnostics.Process.Start(start);
            if (process is null)
            {
                return null;
            }

            process.ErrorDataReceived += (_, _) => { };
            process.BeginErrorReadLine();
            var text = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? text : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// The runs, newest first, each with what it changed against the run before it: its totals, the cells whose
    /// verdict changed (at most 200 are listed), and how many cells - and which cases - are new or gone. The oldest
    /// run has nothing to be compared with. A run recorded by more than one commit is one run.
    /// </summary>
    private static JsonArray RunHistory(
        IReadOnlyList<(JsonObject Entry, string? ReportCommit, string? Subject)> runs)
    {
        const int maxListed = 200;
        static string CaseOf(string cell)
            => cell[..cell.LastIndexOf('|')];

        var history = new List<JsonNode>();
        Dictionary<string, string>? before = null;
        string? lastGeneratedAt = null;
        foreach (var (entry, reportCommit, subject) in runs)
        {
            var generatedAt = (string?)entry["generatedAt"];
            if (generatedAt is not null && string.Equals(generatedAt, lastGeneratedAt, StringComparison.Ordinal))
            {
                continue;
            }

            lastGeneratedAt = generatedAt;
            var now = entry["cells"]?.AsObject().ToDictionary(c => c.Key, c => (string)c.Value!, StringComparer.Ordinal) ?? new Dictionary<string, string>(StringComparer.Ordinal);
            var run = new JsonObject
            {
                ["generatedAt"] = generatedAt,
                ["commit"] = (string?)entry["commit"],
                ["uncommittedFiles"] = (int?)entry["uncommittedFiles"],
                ["reportCommit"] = reportCommit,
                ["subject"] = subject,
                ["cells"] = now.Count,
                ["verdicts"] = new JsonObject(now.Values.GroupBy(v => v, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new KeyValuePair<string, JsonNode?>(g.Key, g.Count()))),
                ["totals"] = entry["totals"]?.DeepClone(),
                ["trend"] = entry["trend"]?.DeepClone(),
                ["changed"] = new JsonArray(),
            };
            if (before is not null)
            {
                var previous = before;
                var changed = now
                    .Where(c => previous.TryGetValue(c.Key, out var was) && !string.Equals(was, c.Value, StringComparison.Ordinal))
                    .OrderBy(c => c.Key, StringComparer.Ordinal)
                    .ToList();
                var added = now.Keys.Where(k => !previous.ContainsKey(k)).ToList();
                var removed = previous.Keys.Where(k => !now.ContainsKey(k)).ToList();
                run["changed"] = new JsonArray([.. changed.Take(maxListed).Select(c => (JsonNode)new JsonObject { ["cell"] = c.Key, ["from"] = previous[c.Key], ["to"] = c.Value })]);
                run["changedCount"] = changed.Count;
                run["added"] = added.Count;
                run["removed"] = removed.Count;
                run["addedCases"] = new JsonArray([.. added.Select(CaseOf).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(maxListed).Select(c => (JsonNode)c)]);
                run["removedCases"] = new JsonArray([.. removed.Select(CaseOf).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(maxListed).Select(c => (JsonNode)c)]);
            }

            history.Add(run);
            before = now;
        }

        history.Reverse();
        return new JsonArray([.. history]);
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
                ["overlay"] = ScenarioOverlay(group.Key),
                ["generators"] = generators,
            });
        }

        var tsc = ReadReportData(root, "test-suite-report.tsc-matrix.json");
        return new JsonObject
        {
            ["generators"] = new JsonArray([.. GeneratorSnapshotHarness.ConvertedMasterFolders.Order(StringComparer.Ordinal).Select(g => (JsonNode)g)]),
            ["scenarios"] = scenarios,
            ["tsc"] = tsc,
        };
    }

    private static string? ScenarioOverlay(string scenario)
    {
        var fileName = scenario + ".overlay.yaml";
        return File.Exists(Path.Combine(GeneratorSnapshotHarness.GetScenariosRoot(), scenario, fileName)) ? fileName : null;
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
    /// Every diagnostic id and how it is covered: the test classes behind it (marked [RuleTest] or named after it), or
    /// no test with the recorded reason.
    /// </summary>
    private static JsonObject CollectDiagnosticsCoverage()
    {
        var tests = Validators.RuleCoverageTests.GetRuleTests();
        var rules = new JsonArray();
        foreach (var rule in Validators.RuleCoverageTests.GetAllRuleIdentifiers().OrderBy(r => r, StringComparer.Ordinal))
        {
            var classes = tests.GetValueOrDefault(rule);
            rules.Add(new JsonObject
            {
                ["id"] = rule,
                ["status"] = classes is { Count: > 0 } ? "tested" : "untested",
                ["tests"] = new JsonArray([.. (classes ?? []).Select(c => (JsonNode)c)]),
                ["note"] = Validators.RuleCoverageTests.ExcludedRules.GetValueOrDefault(rule),
            });
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
        var files = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        void Count(
            Dictionary<string, Dictionary<string, int>> tallies,
            string key,
            string status)
        {
            if (!tallies.TryGetValue(key, out var tally))
            {
                tally = new Dictionary<string, int>(StringComparer.Ordinal);
                tallies[key] = tally;
            }

            tally[status] = tally.GetValueOrDefault(status) + 1;
        }

        void Visit(JsonObject suite)
        {
            foreach (var spec in suite["specs"]?.AsArray() ?? [])
            {
                var file = (string?)suite["file"] ?? string.Empty;
                foreach (var test in spec!["tests"]?.AsArray() ?? [])
                {
                    var status = (string?)test!["status"] ?? "unknown";
                    Count(apps, (string?)test["projectName"] ?? "(default)", status);
                    Count(files, file, status);
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

        // A run that started before the commit tested older code: say so rather than present it as this commit's
        var startTime = (string?)results["stats"]?["startTime"];
        var commitTime = DateTimeOffset.TryParse(RunTool("git", "log -1 --format=%cI", root).Output.Trim().Split('\n')[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var committed)
            ? committed
            : DateTimeOffset.MaxValue;

        return new JsonObject
        {
            ["startTime"] = startTime,
            ["commitTime"] = commitTime == DateTimeOffset.MaxValue ? null : commitTime.ToString("O", CultureInfo.InvariantCulture),
            ["stale"] = PlaywrightRunIsStale(startTime, commitTime),
            ["durationSeconds"] = results["stats"]?["duration"] is { } d ? System.Math.Round((double)d / 1000, 1) : null,
            ["specs"] = files.Count,
            ["apps"] = Tallies(apps),
            ["files"] = Tallies(files),
        };

        static JsonObject Tallies(
            Dictionary<string, Dictionary<string, int>> tallies)
            => new(tallies.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => new KeyValuePair<string, JsonNode?>(
                a.Key,
                new JsonObject(a.Value.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => new KeyValuePair<string, JsonNode?>(s.Key, s.Value))))));
    }

    /// <summary>
    /// Whether a Playwright run (its <c>stats.startTime</c>) started before the commit: an unreadable start is stale too.
    /// </summary>
    private static bool PlaywrightRunIsStale(
        string? startTime,
        DateTimeOffset commitTime)
        => !DateTimeOffset.TryParse(startTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var started)
           || started < commitTime;

    /// <summary>
    /// A data file another test project left for the report (<c>docs/test-suite-report.*.json</c>), or null.
    /// </summary>
    private static JsonNode? ReadReportData(
        string root,
        string fileName)
    {
        var path = Path.Combine(root, ReportFolderName, fileName);
        return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) : null;
    }

    // The page's data: gzip, then base64 - the page unpacks it with the browser's DecompressionStream. The three
    // error-body datasets are each about 1.3 MB of JSON and mostly the same text, which gzip stores once.
    private static string PackReportData(string json)
    {
        using var packed = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(packed, System.IO.Compression.CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            gzip.Write(bytes, 0, bytes.Length);
        }

        return Convert.ToBase64String(packed.ToArray());
    }
}