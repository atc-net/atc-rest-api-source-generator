using GeneratorSeverity = Atc.Rest.Api.Generator.Models.DiagnosticSeverity;

namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A broken specification is reported, never a crash, and a specification that is not reported generates code that
/// compiles: every scenario specification, mutated (a member removed, a value of another kind, an odd string, a
/// schema removed, a schema cycle, a colliding operationId or schema name, a media type with a parameter, a wildcard
/// status, a number out of range, a 3.1 type array, or two of those at once), goes through the server, handler and
/// client generators and the TypeScript client generator. None may report its "generation failed" diagnostic
/// (ATC_API_GEN001/003/005), let an exception out (CS8785, or any exception from the TypeScript generator), or stay
/// silent and generate C# that does not compile. The same holds for a hostile marker file, part file and overlay.
/// The mutations are drawn from a fixed seed per scenario and mutant, so a failure reproduces; a failure is a
/// generator bug to fix, not a case to skip.
/// <para>
/// The default run keeps the suite fast: three mutants per scenario, the first silent mutant of each generator
/// compiled. A deep run (<c>ATC_FUZZ_MUTANTS=25 ATC_FUZZ_COMPILE=all</c>) takes more mutants and compiles every
/// silent one.
/// </para>
/// </summary>
[SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Seeded on purpose: a mutant must reproduce from its seed; nothing here is a secret.")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any exception out of a generator is the crash the test is for.")]
public sealed class GeneratorRobustnessFuzzTests : IClassFixture<FuzzResultsLog>
{
    internal static readonly int MutantsPerScenario =
        int.TryParse(Environment.GetEnvironmentVariable("ATC_FUZZ_MUTANTS"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count > 0
            ? count
            : 3;

    internal static readonly bool CompileEverySilentMutant =
        string.Equals(Environment.GetEnvironmentVariable("ATC_FUZZ_COMPILE"), "all", StringComparison.OrdinalIgnoreCase);

    private const string SpecificationFileName = "Contoso.yaml";

    private static readonly string[] CrashDiagnostics = ["ATC_API_GEN001", "ATC_API_GEN003", "ATC_API_GEN005", "CS8785"];

    private static readonly (IIncrementalGenerator Generator, string Marker)[] Generators =
    [
        (new ApiServerGenerator(), ".atc-rest-api-server"),
        (new ApiServerDomainGenerator(), ".atc-rest-api-server-handlers"),
        (new ApiClientGenerator(), ".atc-rest-api-client"),
    ];

    private readonly FuzzResultsLog log;

    public GeneratorRobustnessFuzzTests(FuzzResultsLog log)
        => this.log = log;

    internal static IEnumerable<string> GeneratorMarkers
        => Generators.Select(g => g.Marker);

    /// <summary>
    /// Marker files a project may hold: not JSON, JSON of another shape, values of the wrong kind, names that are
    /// no identifier, files that do not exist.
    /// </summary>
    public static TheoryData<string> HostileMarkers() =>
    [
        "",
        "{",
        "[]",
        "\"text\"",
        "null",
        "{\"namespace\": \"123 bad.name\"}",
        "{\"namespace\": null}",
        "{\"namespace\": 42}",
        "{\"validateSpecificationStrategy\": \"Nope\"}",
        "{\"useProblemDetails\": \"yes\"}",
        "{\"overlays\": [\"missing.overlay.yaml\"]}",
        "{\"overlays\": 42}",
        "{\"overlays\": [null, 7]}",
        "{\"multiPart\": {\"baseFile\": 5}}",
        "{\"multiPart\": \"text\"}",
        "{\"unknownSetting\": {\"deep\": [1, 2, 3]}}",
        "{\"minimalApi\": {\"useFilters\": \"maybe\"}}",
        "{\"excludeOperations\": \"notAList\"}",
    ];

    /// <summary>
    /// Overlays a project may list: wrong document kind, a target that is no path, a target that hits nothing, an
    /// update of the wrong kind, a removal of everything, an extends to a file that is not there.
    /// </summary>
    public static TheoryData<string> HostileOverlays() =>
    [
        "",
        "[]",
        "overlay: 1.0.0\ninfo: { title: x, version: '1' }\nactions: 42\n",
        "overlay: 1.0.0\ninfo: { title: x, version: '1' }\nactions:\n  - target: '$.paths['\n    update: { x: 1 }\n",
        "overlay: 1.0.0\ninfo: { title: x, version: '1' }\nactions:\n  - target: $.paths['/nothing'].get\n    update: { operationId: x }\n",
        "overlay: 1.0.0\ninfo: { title: x, version: '1' }\nactions:\n  - target: $.paths\n    update: text\n",
        "overlay: 1.0.0\ninfo: { title: x, version: '1' }\nactions:\n  - target: $.paths\n    remove: true\n",
        "overlay: 1.0.0\ninfo: { title: x, version: '1' }\nactions:\n  - target: $.components.schemas\n    remove: true\n",
        "overlay: 1.0.0\ninfo: { title: x, version: '1' }\nactions:\n  - target: $..*\n    update: { type: 123 }\n",
        "overlay: 1.0.0\ninfo: { title: x, version: '1' }\nactions:\n  - target: $.info\n    update: { title: 42, version: [1] }\n",
        "overlay: 1.0.0\ninfo: { title: x, version: '1' }\nextends: Missing.yaml\nactions: []\n",
        "overlay: 1.0.0\ninfo: { title: x, version: '1' }\nactions:\n  - update: { x: 1 }\n",
        "overlay: 1.0.0\ninfo: { title: x, version: '1' }\nactions:\n  - target: $.components.schemas.*\n    update: { allOf: [ { $ref: '#/components/schemas/Missing' } ] }\n",
    ];

    // Third-party specifications from sample/ are seeds too: written by others, with what our scenarios do not think
    // of. Monta (0.9 MB) runs only when ATC_FUZZ_SCENARIO names it - every mutant of it takes seconds.
    private static readonly Dictionary<string, string> ThirdPartySeeds = new(StringComparer.Ordinal)
    {
        ["ThirdParty.Eloverblik"] = "sample/ThirdParty-EPO-Clients/Eloverblik.ThirdPartyApi.Client/api-1.yaml",
        ["ThirdParty.EloverblikCustomer"] = "sample/ThirdParty-EPO-Clients/EloverblikCustomerApiClient/api-1.yaml",
        ["ThirdParty.Monta"] = "sample/ThirdParty-EPO-Clients/MontaClient/Monta.yaml",
    };

    // ATC_FUZZ_SCENARIO=HostileNames narrows a run to one scenario, to reproduce or bisect
    public static IEnumerable<object[]> Scenarios()
    {
        var only = Environment.GetEnvironmentVariable("ATC_FUZZ_SCENARIO");
        var scenarios = new DirectoryInfo(GeneratorSnapshotHarness.GetScenariosRoot())
            .GetDirectories()
            .Where(d => File.Exists(Path.Combine(d.FullName, d.Name + ".yaml")))
            .Select(d => d.Name)
            .Concat(ThirdPartySeeds.Keys.Where(name => name != "ThirdParty.Monta" || string.Equals(only, name, StringComparison.OrdinalIgnoreCase)));
        return scenarios
            .Where(name => string.IsNullOrEmpty(only) || string.Equals(name, only, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new object[] { name });
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void MutatedSpecification_IsReportedNotCrashedOn_AndCompilesWhenSilent(
        string scenario)
    {
        var yaml = ReadScenario(scenario);
        var crashes = new List<string>();

        using var folder = new TempFolder();
        var compiled = new bool[Generators.Length];
        var mutants = new JsonArray();
        for (var mutant = 0; mutant < MutantsPerScenario; mutant++)
        {
            var (specification, mutation) = Mutate(yaml, StableSeed(scenario) + mutant);
            var label = $"mutant {mutant} ({mutation})";
            var before = crashes.Count;
            var (reported, silent, compiledNow) = RunEveryGenerator(folder.Path, "{}", specification, label, crashes, compiled);
            var typeScript = FuzzTypeScript(specification, label, crashes);
            mutants.Add(new JsonObject
            {
                ["mutation"] = mutation.Length > 160 ? mutation[..160] + "..." : mutation,
                ["kind"] = FuzzResultsLog.KindOf(mutation),
                ["reported"] = reported,
                ["silent"] = silent,
                ["compiled"] = compiledNow,
                ["typeScript"] = typeScript,
                ["crashes"] = new JsonArray([.. crashes.Skip(before).Select(c => (JsonNode)c)]),
            });
        }

        log.AddScenario(scenario, StableSeed(scenario), mutants);
        Assert.True(crashes.Count == 0, string.Join("\n", crashes));
    }

    /// <summary>
    /// A part file that is a mutant of the base: the merge, and everything after it, reports or compiles.
    /// </summary>
    [Theory]
    [MemberData(nameof(Scenarios))]
    public void MutatedPartFile_IsReportedNotCrashedOn(string scenario)
    {
        var yaml = ReadScenario(scenario);
        var crashes = new List<string>();

        using var folder = new TempFolder();
        for (var mutant = 0; mutant < System.Math.Min(MutantsPerScenario, 2); mutant++)
        {
            var (part, mutation) = Mutate(yaml, StableSeed(scenario + "_part") + mutant);
            var label = $"part mutant {mutant} ({mutation})";
            foreach (var (generator, marker) in Generators)
            {
                var (diagnostics, _) = Run(generator, folder.Path, "{}", Json(yaml), ("Contoso_Extra.yaml", part));
                crashes.AddRange(Crashes(diagnostics, label + " " + marker));
            }
        }

        log.AddPart(scenario, System.Math.Min(MutantsPerScenario, 2), crashes.Count);
        Assert.True(crashes.Count == 0, string.Join("\n", crashes));
    }

    [Theory]
    [MemberData(nameof(HostileMarkers))]
    public void HostileMarker_IsReportedNotCrashedOn(string markerContent)
    {
        var yaml = Json(ReadScenario("PetStoreSimple"));
        var crashes = new List<string>();

        using var folder = new TempFolder();
        foreach (var (generator, marker) in Generators)
        {
            var (diagnostics, _) = Run(generator, folder.Path, markerContent, yaml);
            crashes.AddRange(Crashes(diagnostics, marker));
        }

        log.AddMarker(crashes);
        Assert.True(crashes.Count == 0, string.Join("\n", crashes));
    }

    [Theory]
    [MemberData(nameof(HostileOverlays))]
    public void HostileOverlay_IsReportedNotCrashedOn(string overlayContent)
    {
        var yaml = Json(ReadScenario("PetStoreSimple"));
        var crashes = new List<string>();

        using var folder = new TempFolder();
        RunEveryGenerator(
            folder.Path,
            "{\"overlays\": [\"Contoso.overlay.yaml\"]}",
            yaml,
            "overlay",
            crashes,
            new bool[Generators.Length],
            (Path.Combine(folder.Path, "Contoso.overlay.yaml"), overlayContent));

        log.AddOverlay(crashes);
        Assert.True(crashes.Count == 0, string.Join("\n", crashes));
    }

    /// <summary>
    /// The mutations are not blind: each changes the document, and the same seed gives the same mutant.
    /// </summary>
    [Fact]
    public void Mutate_ChangesTheDocument_TheSameWayForTheSameSeed()
    {
        const string yaml = """
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /things:
                get:
                  operationId: listThings
                  responses:
                    '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
                post:
                  operationId: addThing
                  responses:
                    '201': { description: Created }
            components:
              schemas:
                Thing: { type: object, properties: { name: { type: string }, size: { type: integer } } }
                Other: { type: object, properties: { id: { type: string } } }
            """;

        var kinds = new HashSet<string>(StringComparer.Ordinal);
        for (var seed = 0; seed < 60; seed++)
        {
            var (first, mutation) = Mutate(yaml, seed);
            var (again, _) = Mutate(yaml, seed);
            Assert.Equal(first, again);
            Assert.NotEqual(Json(yaml), first, StringComparer.Ordinal);
            Assert.False(string.IsNullOrEmpty(mutation));
            kinds.Add(mutation.Split(' ')[0]);
        }

        // Every kind of mutation turns up over a run of seeds
        Assert.True(kinds.Count >= 10, "only these kinds: " + string.Join(", ", kinds));
    }

    private static string ReadScenario(string scenario)
        => ThirdPartySeeds.TryGetValue(scenario, out var samplePath)
            ? File.ReadAllText(Path.Combine(GeneratorSnapshotHarness.GetScenariosRoot(), "..", "..", samplePath))
            : File.ReadAllText(Path.Combine(GeneratorSnapshotHarness.GetScenariosRoot(), scenario, scenario + ".yaml"));

    /// <summary>
    /// Runs the three generators over one input. A generator that reports no error and emits code has that code
    /// compiled (the handler scaffolds together with the server output they implement against) - the first time per
    /// generator, or every time on a deep run - and code that does not compile is a hole in the validation.
    /// </summary>
    private static (int Reported, int Silent, int Compiled) RunEveryGenerator(
        string folder,
        string markerContent,
        string specification,
        string label,
        List<string> crashes,
        bool[] compiled,
        params (string Path, string Content)[] extraFiles)
    {
        var serverSources = new List<(string HintName, string Source)>();
        var (reported, silentCount, compiledCount) = (0, 0, 0);
        for (var index = 0; index < Generators.Length; index++)
        {
            var (generator, marker) = Generators[index];
            var (diagnostics, sources) = Run(generator, folder, markerContent, specification, extraFiles);
            crashes.AddRange(Crashes(diagnostics, label + " " + marker));
            if (generator is ApiServerGenerator)
            {
                serverSources = sources;
            }

            var silent = !diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error) && sources.Count > 0;
            if (silent)
            {
                silentCount++;
            }
            else
            {
                reported++;
            }

            if (!silent || (compiled[index] && !CompileEverySilentMutant))
            {
                continue;
            }

            compiled[index] = true;
            compiledCount++;
            List<(string HintName, string Source)> toCompile = generator is ApiServerDomainGenerator ? [.. serverSources, .. sources] : sources;
            var errors = CompilationVerificationHarness.CompileGeneratedSources(toCompile, includeMinimalApi: true);
            if (errors.Count > 0)
            {
                crashes.Add($"{label} {marker}: silent, but the generated code does not compile:\n    " + string.Join("\n    ", errors.Take(5)));
            }
        }

        return (reported, silentCount, compiledCount);
    }

    private static IEnumerable<string> Crashes(
        IEnumerable<Diagnostic> diagnostics,
        string label)
        => diagnostics
            .Where(d => CrashDiagnostics.Contains(d.Id, StringComparer.Ordinal))
            .Select(d => $"{label}: {d.Id} {d.GetMessage(CultureInfo.InvariantCulture)}");

    /// <summary>
    /// The CLI's way: parse, validate (Strict), and when nothing is an error, generate the Fetch and the Axios client
    /// with every optional output on. The CLI turns an exception into "Error generating TypeScript files" - that is a
    /// crash, not a report.
    /// </summary>
    private static string FuzzTypeScript(
        string specification,
        string label,
        List<string> crashes)
    {
        var outcome = "generated";
        foreach (var httpClient in new[] { TypeScriptHttpClient.Fetch, TypeScriptHttpClient.Axios })
        {
            OpenApiDocument? document;
            try
            {
                var (parsed, diagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(specification, SpecificationFileName);
                if (parsed is null)
                {
                    return "not parsed";
                }

                var diagnostics = OpenApiDocumentValidator.Validate(
                    ValidateSpecificationStrategy.Strict,
                    parsed,
                    diagnostic?.Errors ?? [],
                    SpecificationFileName,
                    specification);
                if (diagnostics.Any(d => d.Severity == GeneratorSeverity.Error))
                {
                    return "reported";
                }

                document = parsed;
            }
            catch (Exception ex)
            {
                crashes.Add($"{label} TypeScript parse/validate: {ex.GetType().Name}: {ex.Message}");
                return "crashed";
            }

            try
            {
                TypeScriptClientGenerationService.Generate(
                    document,
                    outputPath: "n/a",
                    new TypeScriptClientConfig
                    {
                        HttpClient = httpClient,
                        DryRun = true,
                        GenerateZodSchemas = true,
                        GenerateMswHandlers = true,
                        HooksStyle = TypeScriptHooksStyle.ReactQuery,
                        ConvertDates = true,
                        BrandedIds = true,
                    });
            }
            catch (Exception ex)
            {
                crashes.Add($"{label} TypeScript {httpClient}: {ex.GetType().Name}: {ex.Message}");
                outcome = "crashed";
            }
        }

        return outcome;
    }

    private static (List<Diagnostic> Diagnostics, List<(string HintName, string Source)> Sources) Run(
        IIncrementalGenerator generator,
        string folder,
        string markerContent,
        string specification,
        params (string Path, string Content)[] extraFiles)
    {
        var compilation = CSharpCompilation.Create(
            "Contoso.Fuzz",
            references: CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi: true),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var marker = Generators.First(g => ReferenceEquals(g.Generator, generator)).Marker;
        var additionalTexts = new List<AdditionalText>
        {
            new CompilationVerificationHarness.InMemoryAdditionalText(Path.Combine(folder, marker), markerContent),
            new CompilationVerificationHarness.InMemoryAdditionalText(Path.Combine(folder, SpecificationFileName), specification),
        };
        additionalTexts.AddRange(extraFiles.Select(f => (AdditionalText)new CompilationVerificationHarness.InMemoryAdditionalText(
            Path.IsPathRooted(f.Path) ? f.Path : Path.Combine(folder, f.Path),
            f.Content)));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts([.. additionalTexts]);

        // The generators' own diagnostics, an exception that escaped a generator (CS8785), and what was generated
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics, CancellationToken.None);
        var sources = driver.GetRunResult().GeneratedTrees
            .Select(t => (t.FilePath, t.GetText().ToString()))
            .ToList();
        return ([.. diagnostics], sources);
    }

    /// <summary>
    /// One mutant of the specification (as JSON, which is YAML too) and what was changed.
    /// </summary>
    private static (string Specification, string Mutation) Mutate(
        string yaml,
        int seed)
    {
        var random = new Random(seed);
        var document = JsonNode.Parse(Json(yaml))!;
        var mutation = random.Next(12) switch
        {
            11 => MutateOnce(document, random) + " + " + MutateOnce(document, random),
            var kind => MutateOnce(document, random, kind),
        };

        return (document.ToJsonString(), mutation);
    }

    private static string MutateOnce(
        JsonNode document,
        Random random,
        int? kind = null)
    {
        var nodes = Nodes(document).ToList();
        return (kind ?? random.Next(11)) switch
        {
            0 => RemoveMember(nodes, random),
            1 => ChangeKind(nodes, random),
            2 => OddString(nodes, random),
            3 => RemoveComponentSchema(document, random) ?? RemoveMember(nodes, random),
            4 => SchemaCycle(document, random) ?? RemoveMember(nodes, random),
            5 => CollidingOperationId(document, random) ?? RemoveMember(nodes, random),
            6 => CaseCollidingSchema(document, random) ?? RemoveMember(nodes, random),
            7 => MediaTypeParameter(nodes, random) ?? RemoveMember(nodes, random),
            8 => WildcardStatus(nodes, random) ?? RemoveMember(nodes, random),
            9 => NumberOutOfRange(nodes, random) ?? RemoveMember(nodes, random),
            _ => TypeArray(document, nodes, random) ?? RemoveMember(nodes, random),
        };
    }

    private static string RemoveMember(
        List<JsonNode> nodes,
        Random random)
    {
        var members = nodes.OfType<JsonObject>().SelectMany(o => o.Select(m => (Owner: o, m.Key))).ToList();
        var (owner, key) = members[random.Next(members.Count)];
        owner.Remove(key);
        return $"removed {owner.GetPath()}.{key}";
    }

    private static string ChangeKind(
        List<JsonNode> nodes,
        Random random)
    {
        var target = nodes.Where(n => n.Parent is not null).ElementAt(random.Next(nodes.Count(n => n.Parent is not null)));
        JsonNode? replacement = random.Next(5) switch
        {
            0 => JsonValue.Create(42),
            1 => JsonValue.Create("text"),
            2 => new JsonArray(JsonValue.Create(1)),
            3 => new JsonObject { ["x"] = 1 },
            _ => null,
        };

        var path = target.GetPath();
        Replace(target, replacement);
        return $"kind: {path} became {replacement?.ToJsonString() ?? "null"}";
    }

    private static string OddString(
        List<JsonNode> nodes,
        Random random)
    {
        var strings = nodes.OfType<JsonValue>().Where(v => v.GetValueKind() == JsonValueKind.String && v.Parent is not null).ToList();
        string[] odd = [string.Empty, " ", "class", "123abc", "Ünïcødé-名前", "a.b/c{d}", "#/components/schemas/Missing", new('x', 600)];
        var target = strings[random.Next(strings.Count)];
        var value = odd[random.Next(odd.Length)];
        var path = target.GetPath();
        Replace(target, JsonValue.Create(value));
        return $"string: {path} became \"{(value.Length > 20 ? value[..20] + "..." : value)}\"";
    }

    private static string? RemoveComponentSchema(
        JsonNode document,
        Random random)
    {
        if (Schemas(document) is not { Count: > 0 } schemas)
        {
            return null;
        }

        var name = schemas.Select(s => s.Key).ToList()[random.Next(schemas.Count)];
        schemas.Remove(name);
        return $"dangling: removed the schema {name}";
    }

    /// <summary>A schema that includes itself through allOf, directly or through another schema.</summary>
    private static string? SchemaCycle(
        JsonNode document,
        Random random)
    {
        if (Schemas(document) is not { Count: > 0 } schemas)
        {
            return null;
        }

        var names = schemas.Select(s => s.Key).ToList();
        var first = names[random.Next(names.Count)];
        var second = names[random.Next(names.Count)];
        schemas[first] = new JsonObject { ["allOf"] = new JsonArray(new JsonObject { ["$ref"] = "#/components/schemas/" + second }) };
        if (second != first)
        {
            schemas[second] = new JsonObject { ["allOf"] = new JsonArray(new JsonObject { ["$ref"] = "#/components/schemas/" + first }) };
        }

        return $"cycle: {first} -> {second} -> {first}";
    }

    /// <summary>Two operations with the same operationId, or with ids that differ only by case.</summary>
    private static string? CollidingOperationId(
        JsonNode document,
        Random random)
    {
        var operations = Operations(document).ToList();
        if (operations.Count < 2)
        {
            return null;
        }

        var source = operations[random.Next(operations.Count)];
        var target = operations[random.Next(operations.Count)];
        if (ReferenceEquals(source, target))
        {
            target = operations.First(o => !ReferenceEquals(o, source));
        }

        var id = StringValue(source["operationId"]) ?? "contosoOperation";
        var collision = random.Next(2) == 0 ? id : id.ToUpperInvariant();
        target["operationId"] = collision;
        source["operationId"] = id;
        return $"operationId: {target.GetPath()} became {collision} like {source.GetPath()}";
    }

    /// <summary>A second schema whose name differs from an existing one by case only.</summary>
    private static string? CaseCollidingSchema(
        JsonNode document,
        Random random)
    {
        if (Schemas(document) is not { Count: > 0 } schemas)
        {
            return null;
        }

        var name = schemas.Select(s => s.Key).ToList()[random.Next(schemas.Count)];
        var upper = name.ToUpperInvariant();
        var twin = string.Equals(upper, name, StringComparison.Ordinal) ? name.ToLowerInvariant() : upper;
        if (string.Equals(twin, name, StringComparison.Ordinal) || schemas.ContainsKey(twin))
        {
            return null;
        }

        schemas[twin] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["contoso"] = new JsonObject { ["type"] = "string" } } };
        return $"schemaCase: added {twin} beside {name}";
    }

    /// <summary>A media type key with a parameter, in another case, or a wildcard.</summary>
    private static string? MediaTypeParameter(
        List<JsonNode> nodes,
        Random random)
    {
        var contents = nodes.OfType<JsonObject>().Where(o => PropertyName(o) == "content" && o.Count > 0).ToList();
        if (contents.Count == 0)
        {
            return null;
        }

        var content = contents[random.Next(contents.Count)];
        var key = content.Select(m => m.Key).ToList()[random.Next(content.Count)];
        var replacement = random.Next(4) switch
        {
            0 => key + "; charset=utf-8",
            1 => key.ToUpperInvariant(),
            2 => "*/*",
            _ => key + ";q=0.9; boundary=----",
        };

        var value = content[key];
        content.Remove(key);
        content[replacement] = value;
        return $"mediaType: {content.GetPath()}.{key} became {replacement}";
    }

    /// <summary>A response keyed by a wildcard status, beside or instead of the exact one.</summary>
    private static string? WildcardStatus(
        List<JsonNode> nodes,
        Random random)
    {
        var responses = nodes.OfType<JsonObject>().Where(o => PropertyName(o) == "responses" && o.Any(r => r.Key.Length == 3 && char.IsDigit(r.Key[0]))).ToList();
        if (responses.Count == 0)
        {
            return null;
        }

        var owner = responses[random.Next(responses.Count)];
        var key = owner.Select(m => m.Key).First(k => k.Length == 3 && char.IsDigit(k[0]));
        var wildcard = key[0] + "XX";
        var value = owner[key];
        if (random.Next(2) == 0)
        {
            owner.Remove(key);
        }

        owner[wildcard] = value?.DeepClone();
        return $"status: {owner.GetPath()}.{key} became {wildcard}";
    }

    /// <summary>A number no C# type holds, on a schema's bounds, default or example.</summary>
    private static string? NumberOutOfRange(
        List<JsonNode> nodes,
        Random random)
    {
        var schemas = nodes.OfType<JsonObject>().Where(o => StringValue(o["type"]) is "integer" or "number" or "string").ToList();
        if (schemas.Count == 0)
        {
            return null;
        }

        var schema = schemas[random.Next(schemas.Count)];
        var (member, value) = random.Next(5) switch
        {
            0 => ("maximum", "1e400"),
            1 => ("minimum", "-99999999999999999999"),
            2 => ("default", "99999999999999999999"),
            3 => ("example", "1e400"),
            _ => ("multipleOf", "0"),
        };

        schema[member] = JsonNode.Parse(value);
        return $"number: {schema.GetPath()}.{member} became {value}";
    }

    /// <summary>A 3.1 type array (nullable, or two types) under a document that says 3.1.</summary>
    private static string? TypeArray(
        JsonNode document,
        List<JsonNode> nodes,
        Random random)
    {
        var typed = nodes.OfType<JsonObject>().Where(o => StringValue(o["type"]) is not null).ToList();
        if (typed.Count == 0)
        {
            return null;
        }

        var schema = typed[random.Next(typed.Count)];
        var type = StringValue(schema["type"])!;
        var array = random.Next(3) switch
        {
            0 => new JsonArray(type, "null"),
            1 => new JsonArray(type, type == "string" ? "integer" : "string"),
            _ => new JsonArray("null"),
        };

        document["openapi"] = "3.1.0";
        schema["type"] = array;
        return $"typeArray: {schema.GetPath()}.type became {array.ToJsonString()}";
    }

    // The member name a node sits under; null for the root and for an array element
    private static string? PropertyName(JsonNode node)
        => node.Parent is JsonObject ? node.GetPropertyName() : null;

    // The node's string; null when it is missing or (an earlier mutation, a 3.1 type array) not a string
    private static string? StringValue(JsonNode? node)
        => node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    private static JsonObject? Schemas(JsonNode document)
        => document["components"]?["schemas"] as JsonObject;

    private static IEnumerable<JsonObject> Operations(JsonNode document)
        => (document["paths"] as JsonObject ?? [])
            .Select(p => p.Value as JsonObject)
            .Where(p => p is not null)
            .SelectMany(p => p!.Where(m => m.Key is "get" or "put" or "post" or "delete" or "patch" or "head" or "options" or "trace"))
            .Select(m => m.Value as JsonObject)
            .Where(o => o is not null)!;

    private static void Replace(
        JsonNode target,
        JsonNode? replacement)
    {
        switch (target.Parent)
        {
            case JsonObject owner:
                owner[target.GetPropertyName()] = replacement;
                break;
            case JsonArray array:
                array[target.GetElementIndex()] = replacement;
                break;
        }
    }

    private static IEnumerable<JsonNode> Nodes(JsonNode node)
    {
        yield return node;
        var children = node switch
        {
            JsonObject o => o.Select(m => m.Value),
            JsonArray a => a.AsEnumerable(),
            _ => [],
        };

        foreach (var child in children.OfType<JsonNode>())
        {
            foreach (var descendant in Nodes(child))
            {
                yield return descendant;
            }
        }
    }

    private static string Json(string yaml)
    {
        var stream = new SharpYaml.Serialization.YamlStream();
        stream.Load(new StringReader(yaml));
        return Microsoft.OpenApi.YamlReader.YamlConverter.ToJsonNode(stream.Documents[0]).ToJsonString();
    }

    // string.GetHashCode differs per process: a seed must not
    private static int StableSeed(string text)
        => text.Aggregate(17, (hash, c) => unchecked((hash * 31) + c));

    /// <summary>
    /// A folder of the test's own: the handler generator writes its scaffolds next to the marker.
    /// </summary>
    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atc-fuzz-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A temp folder is harmless, a red test is not
            }
        }
    }
}