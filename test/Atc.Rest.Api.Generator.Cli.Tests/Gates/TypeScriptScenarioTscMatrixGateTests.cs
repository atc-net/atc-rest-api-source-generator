// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Strict <c>tsc</c> over the generated TypeScript output of <b>every</b> scenario spec, for every HTTP
/// client (Fetch, Axios) and hooks flavour (none, React Query, SWR) - against the real
/// <c>@tanstack/react-query</c>, <c>swr</c> and <c>axios</c> types, under <c>strict</c>,
/// <c>noUnusedLocals</c> and <c>noUncheckedIndexedAccess</c>.
/// <para>
/// Snapshot tests only prove the output did not change; this gate proves it compiles. Combinations
/// known not to compile yet are listed in <see cref="KnownNonCompilingCombinations"/> - a ratchet: the
/// gate fails when an unlisted combination breaks <b>and</b> when a listed one starts compiling (remove
/// it). Self-skips without a Node toolchain, but fails under CI.
/// </para>
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptScenarioTscMatrixGateTests
{
    private const string PackageJson = """
        {
          "name": "strict-tsc-matrix-gate",
          "version": "0.0.0",
          "private": true,
          "type": "module",
          "dependencies": {
            "@tanstack/react-query": "^5.81.0",
            "axios": "^1.7.0",
            "msw": "^2.4.0",
            "react": "^18.3.0",
            "swr": "^2.2.5",
            "zod": "^3.23.0"
          },
          "devDependencies": {
            "@types/react": "^18.3.0",
            "typescript": "^5.6.0"
          }
        }
        """;

    private const string TsConfig = """
        {
          "compilerOptions": {
            "target": "ES2020",
            "lib": ["ES2020", "DOM", "DOM.Iterable"],
            "module": "ESNext",
            "moduleResolution": "bundler",
            "jsx": "react-jsx",
            "strict": true,
            "noUnusedLocals": true,
            "noUncheckedIndexedAccess": true,
            "esModuleInterop": true,
            "forceConsistentCasingInFileNames": true,
            "skipLibCheck": true,
            "isolatedModules": true,
            "noEmit": true
          },
          "include": ["gen/**/*.ts", "gen/**/*.tsx"],
          "exclude": ["node_modules"]
        }
        """;

    private static readonly TypeScriptHttpClient[] HttpClients =
    [
        TypeScriptHttpClient.Fetch,
        TypeScriptHttpClient.Axios,
    ];

    private static readonly TypeScriptHooksStyle[] HooksStyles =
    [
        TypeScriptHooksStyle.None,
        TypeScriptHooksStyle.ReactQuery,
        TypeScriptHooksStyle.Swr,
    ];

    private static readonly string[] Flavours =
    [
        "Default",
        "Full",
    ];

    /// <summary>
    /// "{scenario}/{client}/{hooks}/{flavour}" combinations that do not compile yet. Must be empty for a release.
    /// </summary>
    private static readonly HashSet<string> KnownNonCompilingCombinations = new(StringComparer.Ordinal)
    {
    };

    [Fact]
    public void EveryScenario_GeneratedTypeScript_CompilesUnderStrictTsc()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the tsc matrix gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var workDir = Path.Combine(Path.GetTempPath(), "atc-tsc-matrix", Guid.NewGuid().ToString("N"));
        var generatedDir = Path.Combine(workDir, "gen");
        Directory.CreateDirectory(generatedDir);

        try
        {
            var failing = new SortedSet<string>(StringComparer.Ordinal);
            var combinations = new List<string>();

            foreach (var (scenario, specPath) in DiscoverScenarioSpecs())
            {
                var doc = LoadScenarioDocument(specPath);

                foreach (var httpClient in HttpClients)
                {
                    foreach (var hooks in HooksStyles)
                    {
                        foreach (var flavour in Flavours)
                        {
                            var combination = $"{scenario}/{httpClient}/{hooks}/{flavour}";
                            combinations.Add(combination);
                            if (doc is null)
                            {
                                failing.Add(combination);
                                continue;
                            }

                            try
                            {
                                TypeScriptClientGenerationService.Generate(
                                    doc,
                                    Path.Combine(generatedDir, DirectoryName(combination)),
                                    CreateConfig(httpClient, hooks, flavour));
                            }
                            catch (Exception ex) when (ex is not OutOfMemoryException)
                            {
                                failing.Add(combination);
                            }
                        }
                    }
                }
            }

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "tsconfig.json"), TsConfig);

            var install = NodeToolchain.RunNpm(
                "install --no-audit --no-fund --loglevel=error",
                workDir,
                TimeSpan.FromMinutes(5));
            Assert.True(
                install.ExitCode == 0,
                $"npm install failed (exit {install.ExitCode}):\n{install.Output}");

            var tsc = NodeToolchain.RunNpx(
                "tsc --noEmit --pretty false -p tsconfig.json",
                workDir,
                TimeSpan.FromMinutes(10));

            var errorsByCombination = ParseErrors(tsc.Output, combinations);
            foreach (var combination in errorsByCombination.Keys)
            {
                failing.Add(combination);
            }

            WriteReportData(combinations, failing);

            var newlyFailing = failing.Where(c => !KnownNonCompilingCombinations.Contains(c)).ToList();
            var nowCompiling = KnownNonCompilingCombinations.Where(c => !failing.Contains(c)).ToList();

            var report = new StringBuilder();
            if (newlyFailing.Count > 0)
            {
                report.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"{newlyFailing.Count} combination(s) do not compile:");
                foreach (var combination in newlyFailing)
                {
                    report.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  {combination}");
                    if (errorsByCombination.TryGetValue(combination, out var errors))
                    {
                        foreach (var error in errors.Take(5))
                        {
                            report.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"      {error}");
                        }
                    }
                }
            }

            if (nowCompiling.Count > 0)
            {
                report.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"{nowCompiling.Count} known non-compiling combination(s) now compile - remove them from the list:");
                foreach (var combination in nowCompiling)
                {
                    report.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  {combination}");
                }
            }

            Assert.True(report.Length == 0, report.ToString());
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    /// <summary>
    /// Default: the out-of-the-box options. Full: every optional feature at once - suspense and standard
    /// hooks, Zod schemas with runtime validation, MSW handlers, branded ids, Date conversion, mutable
    /// models and TypeScript enums with runtime values.
    /// </summary>
    /// <summary>
    /// A scenario with an overlay ({scenario}.overlay.yaml, listed by its markers) is type-checked as the build
    /// generates it: the gate used to compile the raw specification, a client no build produces. A multi-part
    /// scenario gets the overlay after the merge.
    /// </summary>
    [Theory]
    [InlineData("Overlays", "listDevices")]
    [InlineData("OverlaysMultiPart", "listOrders")]
    public void LoadScenarioDocument_AppliesTheScenariosOverlay(
        string scenario,
        string operationId)
    {
        var specPath = Path.Combine(AppContext.BaseDirectory, "Scenarios", scenario, scenario + ".yaml");

        var document = LoadScenarioDocument(specPath);

        Assert.NotNull(document);
        Assert.Contains(
            document.Paths.Values.SelectMany(p => p.Operations?.Values.AsEnumerable() ?? []),
            o => string.Equals(o.OperationId, operationId, StringComparison.Ordinal));
    }

    private static TypeScriptClientConfig CreateConfig(
        TypeScriptHttpClient httpClient,
        TypeScriptHooksStyle hooks,
        string flavour)
    {
        var config = new TypeScriptClientConfig
        {
            HttpClient = httpClient,
            HooksStyle = hooks,
            GenerateFileHeaders = true,
            Scaffold = false,
        };

        if (string.Equals(flavour, "Full", StringComparison.Ordinal))
        {
            config.HooksMode = TypeScriptHooksMode.Both;
            config.GenerateZodSchemas = true;
            config.ZodRuntimeValidate = true;
            config.GenerateMswHandlers = true;
            config.BrandedIds = true;
            config.ConvertDates = true;
            config.MutableModels = true;
            config.EnumStyle = TypeScriptEnumStyle.Enum;
            config.EnumRuntimeValues = true;
        }

        return config;
    }

    private static string DirectoryName(string combination)
        => combination.Replace('/', '-');

    /// <summary>
    /// Groups tsc error lines ("gen/{scenario}-{client}-{hooks}/path.ts(1,2): error TS...") by combination.
    /// </summary>
    private static Dictionary<string, List<string>> ParseErrors(
        string output,
        List<string> combinations)
    {
        var byDirectory = combinations.ToDictionary(DirectoryName, c => c, StringComparer.Ordinal);
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("gen/", StringComparison.Ordinal) ||
                !trimmed.Contains("error TS", StringComparison.Ordinal))
            {
                continue;
            }

            var directory = trimmed.Split('/')[1];
            if (!byDirectory.TryGetValue(directory, out var combination))
            {
                continue;
            }

            if (!result.TryGetValue(combination, out var errors))
            {
                errors = [];
                result[combination] = errors;
            }

            errors.Add(trimmed);
        }

        return result;
    }

    /// <summary>
    /// A scenario's specification, loaded like <c>generate client-typescript --overlay</c> does: multi-part specs
    /// merge their part files, then the scenario's overlay ({scenario}.overlay.yaml) applies to the whole.
    /// </summary>
    private static OpenApiDocument? LoadScenarioDocument(string specPath)
    {
        var merged = Atc.Rest.Api.Generator.Services.SpecificationService.ReadAndMergeResolved(specPath).Specification;
        var overlayPath = Path.ChangeExtension(specPath, ".overlay.yaml");
        if (merged?.Document is null || !File.Exists(overlayPath))
        {
            return merged?.Document;
        }

        var (effective, problems) = Atc.Rest.Api.Generator.Overlays.SpecificationOverlays.Apply(
            merged.Content,
            [new Atc.Rest.Api.Generator.Overlays.OverlayFile(overlayPath, File.ReadAllText(overlayPath))]);
        Assert.True(effective is not null, $"The overlay {overlayPath} cannot be applied: {string.Join("; ", problems.Select(p => p.Problem.Message))}");

        return OpenApiDocumentHelper.TryParseYamlWithDiagnostic(effective, specPath).Document;
    }

    /// <summary>
    /// Leaves the matrix's result for the test-suite report (<c>Report_EndToEndErrorBodyMatrix</c> in the
    /// source-generator tests reads <c>docs/test-suite-report.tsc-matrix.json</c>); nothing when the repository root
    /// is not above the test binaries (a packaged run).
    /// </summary>
    private static void WriteReportData(
        IReadOnlyCollection<string> combinations,
        IReadOnlyCollection<string> failing)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "test", "Scenarios")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            return;
        }

        var folder = Path.Combine(directory.FullName, "docs");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "test-suite-report.tsc-matrix.json");
        var combinationsNode = new System.Text.Json.Nodes.JsonArray([.. combinations.Select(c => (System.Text.Json.Nodes.JsonNode)c)]);
        var failingNode = new System.Text.Json.Nodes.JsonArray([.. failing.Select(c => (System.Text.Json.Nodes.JsonNode)c)]);

        // The file is tracked: an unchanged result keeps it as it is, so a test run does not dirty the tree
        if (File.Exists(path) &&
            System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path)) is { } previous &&
            System.Text.Json.Nodes.JsonNode.DeepEquals(previous["combinations"], combinationsNode) &&
            System.Text.Json.Nodes.JsonNode.DeepEquals(previous["failing"], failingNode))
        {
            return;
        }

        var data = new System.Text.Json.Nodes.JsonObject
        {
            ["generatedAt"] = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", System.Globalization.CultureInfo.InvariantCulture),
            ["combinations"] = combinationsNode,
            ["failing"] = failingNode,
        };
        File.WriteAllText(path, data.ToJsonString());
    }

    /// <summary>
    /// Every "{scenario}/{scenario}.yaml" copied next to the test assembly (the base spec of a
    /// multi-part scenario; its part files are merged from there).
    /// </summary>
    private static IEnumerable<(string Scenario, string SpecPath)> DiscoverScenarioSpecs()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Scenarios");
        Assert.True(Directory.Exists(root), $"Scenario specs not found at {root}");

        foreach (var directory in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.Ordinal))
        {
            var scenario = Path.GetFileName(directory);
            var specPath = Path.Combine(directory, scenario + ".yaml");
            if (File.Exists(specPath))
            {
                yield return (scenario, specPath);
            }
        }
    }

    private static bool IsContinuousIntegration()
        => string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup.
        }
    }
}