namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptPackageScaffoldExtractorTests
{
    [Fact]
    public void GeneratePackageJson_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptPackageScaffoldExtractor.GeneratePackageJson("pkg", "1.0.0", description: null, config: null));
    }

    [Fact]
    public void GeneratePackageJson_EmitsRequiredFieldsAndModuleType()
    {
        var json = TypeScriptPackageScaffoldExtractor.GeneratePackageJson("my-api-client", "0.1.0", description: null, new TypeScriptClientConfig());

        Assert.Contains("\"name\": \"my-api-client\"", json, StringComparison.Ordinal);
        Assert.Contains("\"version\": \"0.1.0\"", json, StringComparison.Ordinal);
        Assert.Contains("\"type\": \"module\"", json, StringComparison.Ordinal);
        Assert.Contains("\"private\": true", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratePackageJson_EmitsDualEsmCjsEntryPoints()
    {
        // Dual-publish layout: `main` resolves to CJS for legacy `require()`, `module`
        // is the bundler hint pointing at ESM, `types` is the canonical .d.ts. Missing
        // any of these breaks one consumer flavor.
        var json = TypeScriptPackageScaffoldExtractor.GeneratePackageJson("pkg", "1.0.0", description: null, new TypeScriptClientConfig());

        Assert.Contains("\"main\": \"./dist/cjs/index.js\"", json, StringComparison.Ordinal);
        Assert.Contains("\"module\": \"./dist/index.js\"", json, StringComparison.Ordinal);
        Assert.Contains("\"types\": \"./dist/index.d.ts\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratePackageJson_ExportsMapCoversImportRequireAndDefault()
    {
        // The exports map needs `import` (ESM), `require` (CJS), and `default` (anything
        // else) for the dual-publish to actually resolve under all consumer flavors.
        // `types` must come first so TypeScript reads it before falling through.
        var json = TypeScriptPackageScaffoldExtractor.GeneratePackageJson("pkg", "1.0.0", description: null, new TypeScriptClientConfig());

        Assert.Contains("\"types\": \"./dist/index.d.ts\",", json, StringComparison.Ordinal);
        Assert.Contains("\"import\": \"./dist/index.js\",", json, StringComparison.Ordinal);
        Assert.Contains("\"require\": \"./dist/cjs/index.js\",", json, StringComparison.Ordinal);
        Assert.Contains("\"default\": \"./dist/index.js\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratePackageJson_BuildScriptInvokesEsmCjsAndPostbuildInOrder()
    {
        // The `&&` chain matters — npm run build:esm has to land before build:cjs so
        // the ESM .d.ts files exist, and postbuild-cjs has to land last so the sub-
        // package.json override appears AFTER tsc has written dist/cjs/. The serializer
        // must not Unicode-escape `&` — that would produce `&&` which npm
        // can't parse as a logical-and.
        var json = TypeScriptPackageScaffoldExtractor.GeneratePackageJson("pkg", "1.0.0", description: null, new TypeScriptClientConfig());

        Assert.Contains("\"build\": \"npm run build:esm && npm run build:cjs && node ./scripts/postbuild-cjs.mjs\"", json, StringComparison.Ordinal);
        Assert.Contains("\"build:esm\": \"tsc -p tsconfig.json\"", json, StringComparison.Ordinal);
        Assert.Contains("\"build:cjs\": \"tsc -p tsconfig.cjs.json\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratePackageJson_DescriptionIncludedWhenProvided()
    {
        var json = TypeScriptPackageScaffoldExtractor.GeneratePackageJson("pkg", "1.0.0", description: "My API client", new TypeScriptClientConfig());

        Assert.Contains("\"description\": \"My API client\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratePackageJson_DescriptionOmittedWhenWhitespace()
    {
        var json = TypeScriptPackageScaffoldExtractor.GeneratePackageJson("pkg", "1.0.0", description: "   ", new TypeScriptClientConfig());

        Assert.DoesNotContain("\"description\":", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratePackageJson_ReactQueryHooks_DeclareReactQueryAndReact()
    {
        var json = TypeScriptPackageScaffoldExtractor.GeneratePackageJson("pkg", "1.0.0", description: null, new TypeScriptClientConfig { HooksStyle = TypeScriptHooksStyle.ReactQuery });

        Assert.Contains("\"@tanstack/react-query\": \"^5.81.0\"", json, StringComparison.Ordinal);
        Assert.Contains("\"react\": \"^18.0.0 || ^19.0.0\"", json, StringComparison.Ordinal);
        Assert.Contains("\"@types/react\": \"^18.0.0 || ^19.0.0\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"swr\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratePackageJson_SwrHooks_DeclareSwrAndReact()
    {
        // The generated SWR hooks import 'swr', 'swr/infinite', 'swr/mutation' and 'react'; tsconfig includes
        // every .ts file, so a scaffold without these does not build.
        var json = TypeScriptPackageScaffoldExtractor.GeneratePackageJson("pkg", "1.0.0", description: null, new TypeScriptClientConfig { HooksStyle = TypeScriptHooksStyle.Swr });

        Assert.Contains("\"swr\": \"^2.2.0\"", json, StringComparison.Ordinal);
        Assert.Contains("\"react\": \"^18.0.0 || ^19.0.0\"", json, StringComparison.Ordinal);
        Assert.Contains("\"@types/react\": \"^18.0.0 || ^19.0.0\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("@tanstack/react-query", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratePackageJson_MswHandlers_DeclareMswAsADevDependency()
    {
        // mocks/*.ts import 'msw'.
        var json = TypeScriptPackageScaffoldExtractor.GeneratePackageJson("pkg", "1.0.0", description: null, new TypeScriptClientConfig { GenerateMswHandlers = true });

        Assert.Contains("\"msw\": \"^2.7.0\"", json, StringComparison.Ordinal);
        Assert.True(json.IndexOf("\"devDependencies\"", StringComparison.Ordinal) < json.IndexOf("\"msw\"", StringComparison.Ordinal));
    }

    [Fact]
    public void GeneratePackageJson_SignalRHubs_DeclareSignalR_AndReactOnlyWithReactHooks()
    {
        // hubs/*Hub.ts import '@microsoft/signalr' (the hubs come from the document's x-signalr-hubs, not from a
        // flag, so the service tells the scaffold whether any were emitted); the React hooks over them, and so
        // 'react', come only with a React hooks style
        var json = TypeScriptPackageScaffoldExtractor.GeneratePackageJson("pkg", "1.0.0", description: null, new TypeScriptClientConfig(), hasSignalRHubs: true);
        var withHooks = TypeScriptPackageScaffoldExtractor.GeneratePackageJson("pkg", "1.0.0", description: null, new TypeScriptClientConfig { HooksStyle = TypeScriptHooksStyle.ReactQuery }, hasSignalRHubs: true);

        Assert.Contains("\"@microsoft/signalr\": \"^8.0.0 || ^9.0.0 || ^10.0.0\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"react\"", json, StringComparison.Ordinal);
        Assert.Contains("\"@microsoft/signalr\": \"^8.0.0 || ^9.0.0 || ^10.0.0\"", withHooks, StringComparison.Ordinal);
        Assert.Contains("\"react\": \"^18.0.0 || ^19.0.0\"", withHooks, StringComparison.Ordinal);
        Assert.Contains("\"@types/react\": \"^18.0.0 || ^19.0.0\"", withHooks, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratePackageJson_NoFeatures_DeclaresOnlyTypeScript()
    {
        var json = TypeScriptPackageScaffoldExtractor.GeneratePackageJson("pkg", "1.0.0", description: null, new TypeScriptClientConfig());

        Assert.DoesNotContain("\"react\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"msw\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"swr\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("@microsoft/signalr", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"dependencies\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateReadme_UsesTheApiClientConstructorThatExists()
    {
        // The ApiClient is `new ApiClient(baseUrl, options)`, its options take `getAccessToken` and a
        // `defaultHeaders` record - not an options bag with baseUrl, nor a defaultHeaders function.
        var readme = TypeScriptPackageScaffoldExtractor.GenerateReadme(
            "pet-store",
            title: "Pet Store",
            description: null,
            new[] { "PetsClient" },
            new TypeScriptClientConfig { HooksStyle = TypeScriptHooksStyle.None });

        Assert.Contains("const api = new ApiClient('https://api.example.com');", readme, StringComparison.Ordinal);
        Assert.Contains("getAccessToken: () => getToken(),", readme, StringComparison.Ordinal);
        Assert.Contains("apiKeys: { ApiKeyAuth: apiKey },", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("new ApiClient({", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("defaultHeaders: () =>", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateTsConfig_EmitsStrictModernCompilerOptions()
    {
        var json = TypeScriptPackageScaffoldExtractor.GenerateTsConfig();

        Assert.Contains("\"target\": \"ES2020\"", json, StringComparison.Ordinal);
        Assert.Contains("\"module\": \"ESNext\"", json, StringComparison.Ordinal);
        Assert.Contains("\"strict\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"declaration\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"moduleResolution\": \"bundler\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateTsConfig_EmitsExplicitLibAndExtraStrictness()
    {
        // The scaffolded tsconfig should be belt-and-braces strict so
        // generated code stays type-safe even when the consuming project hasn't dialed
        // in their own strictness. Explicit `lib` is included so the inferred default
        // from `target` doesn't drift if `target` is later bumped.
        var json = TypeScriptPackageScaffoldExtractor.GenerateTsConfig();

        Assert.Contains("\"lib\": [\"ES2020\", \"DOM\"]", json, StringComparison.Ordinal);
        Assert.Contains("\"noImplicitAny\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"strictNullChecks\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"noUncheckedIndexedAccess\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"sourceMap\": true", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateTsConfig_DocumentsSkipLibCheckTradeOff()
    {
        // tsconfig.json supports JSONC comments. `skipLibCheck` is a meaningful
        // trade-off — readers benefit from understanding why it's on by default.
        var json = TypeScriptPackageScaffoldExtractor.GenerateTsConfig();

        Assert.Contains("// skipLibCheck", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateTsConfigCjs_ExtendsBaseConfigAndOverridesCjsKnobs()
    {
        // The CJS config keeps the strictness / lib / target from the ESM config via
        // `extends`. It only overrides the bits that differ for CJS: module format,
        // output folder, and turning off declaration/sourcemap emission so we don't
        // duplicate those (the ESM pass already writes them).
        var json = TypeScriptPackageScaffoldExtractor.GenerateTsConfigCjs();

        Assert.Contains("\"extends\": \"./tsconfig.json\"", json, StringComparison.Ordinal);
        Assert.Contains("\"module\": \"CommonJS\"", json, StringComparison.Ordinal);
        Assert.Contains("\"moduleResolution\": \"node\"", json, StringComparison.Ordinal);
        Assert.Contains("\"outDir\": \"./dist/cjs\"", json, StringComparison.Ordinal);
        Assert.Contains("\"declaration\": false", json, StringComparison.Ordinal);
        Assert.Contains("\"sourceMap\": false", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratePostbuildCjsScript_WritesCommonjsSubPackageJson()
    {
        // The root package.json declares `"type": "module"`, which Node would apply
        // to dist/cjs/*.js too without this sub-package.json override. The script must
        // mkdir the target (in case CJS tsc emitted into a fresh dir) and write the
        // {"type":"commonjs"} stamp.
        var script = TypeScriptPackageScaffoldExtractor.GeneratePostBuildCjsScript();

        Assert.Contains("mkdirSync('dist/cjs'", script, StringComparison.Ordinal);
        Assert.Contains("'dist/cjs/package.json'", script, StringComparison.Ordinal);
        Assert.Contains("type: 'commonjs'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateReadme_HookStyle_EmitsApiProviderQuickStart()
    {
        // Scaffolded README must show the `ApiProvider` wrap when hooks
        // are enabled — that was the recurring trip-wire when wiring Showcase by hand.
        var config = new TypeScriptClientConfig { HooksStyle = TypeScriptHooksStyle.ReactQuery };
        var segments = new[] { "PetsClient", "OwnersClient" };

        var readme = TypeScriptPackageScaffoldExtractor.GenerateReadme(
            "pet-store",
            title: "Pet Store",
            description: "A demo pet-store API.",
            segments,
            config);

        Assert.Contains("# Pet Store", readme, StringComparison.Ordinal);
        Assert.Contains("A demo pet-store API.", readme, StringComparison.Ordinal);
        Assert.Contains("npm install pet-store", readme, StringComparison.Ordinal);
        Assert.Contains("ApiProvider", readme, StringComparison.Ordinal);
        Assert.Contains("QueryClientProvider", readme, StringComparison.Ordinal);
        Assert.Contains("- `PetsClient`", readme, StringComparison.Ordinal);
        Assert.Contains("- `OwnersClient`", readme, StringComparison.Ordinal);
        Assert.Contains("atc-rest-api-gen generate client-typescript", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateReadme_NoHooks_EmitsPlainClientQuickStart()
    {
        // Without hooks the README pivots to a plain `new ApiClient + new XxxClient(api)`
        // example so consumers don't see React/TanStack boilerplate they don't need.
        var config = new TypeScriptClientConfig { HooksStyle = TypeScriptHooksStyle.None };
        var segments = new[] { "PetsClient" };

        var readme = TypeScriptPackageScaffoldExtractor.GenerateReadme(
            "pet-store",
            title: "Pet Store",
            description: null,
            segments,
            config);

        Assert.Contains("new ApiClient", readme, StringComparison.Ordinal);
        Assert.Contains("new PetsClient(api)", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("ApiProvider", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("QueryClientProvider", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateReadme_NoSegments_OmitsClientList()
    {
        // Spec with no operations → no client list section, no broken bullet.
        var config = new TypeScriptClientConfig();

        var readme = TypeScriptPackageScaffoldExtractor.GenerateReadme(
            "empty-api",
            title: "Empty",
            description: null,
            Array.Empty<string>(),
            config);

        Assert.DoesNotContain("## Available clients", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateReadme_NoTitle_FallsBackToPackageName()
    {
        // Spec without info.title → README heading uses the kebab package name so the
        // file isn't headed with "# " (empty heading).
        var config = new TypeScriptClientConfig();

        var readme = TypeScriptPackageScaffoldExtractor.GenerateReadme(
            "my-api-client",
            title: null,
            description: null,
            Array.Empty<string>(),
            config);

        Assert.Contains("# my-api-client", readme, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("My Demo API - Full", "my-demo-api-full")]
    [InlineData("PetStore", "petstore")]
    [InlineData("  ", "generated-api-client")]
    [InlineData("", "generated-api-client")]
    [InlineData("Account@Service!v2", "account-service-v2")]
    public void DerivePackageName_ProducesKebabCaseOrFallback(
        string title,
        string expected)
    {
        Assert.Equal(expected, TypeScriptPackageScaffoldExtractor.DerivePackageName(title));
    }

    /// <summary>
    /// Every range the scaffold writes takes the versions the generated TypeScript is proven with here: the TypeScript
    /// gates' workspace (compiled and run by the test suites) and the Showcase React apps (built and run by the
    /// Playwright suite). A dependency update in the repository that the ranges shut out - React 19, SignalR 10 - fails
    /// this test instead of a consumer's npm install.
    /// </summary>
    [Theory]
    [InlineData(TypeScriptHooksStyle.ReactQuery)]
    [InlineData(TypeScriptHooksStyle.Swr)]
    public void GeneratePackageJson_EveryRange_TakesTheVersionsTheGeneratedCodeIsTestedWith(
        TypeScriptHooksStyle hooksStyle)
    {
        var config = new TypeScriptClientConfig
        {
            HttpClient = TypeScriptHttpClient.Axios,
            GenerateZodSchemas = true,
            GenerateMswHandlers = true,
            HooksStyle = hooksStyle,
        };
        var scaffold = JsonNode.Parse(TypeScriptPackageScaffoldExtractor.GeneratePackageJson("pkg", "1.0.0", description: null, config, hasSignalRHubs: true))!;
        var ranges = new[] { "dependencies", "peerDependencies", "devDependencies" }
            .SelectMany(section => scaffold[section]?.AsObject() ?? [])
            .ToDictionary(p => p.Key, p => p.Value!.GetValue<string>(), StringComparer.Ordinal);

        var root = Path.GetFullPath(Path.Combine(CliTestHelper.GetCliSourceDirectory(), "..", ".."));
        var tested = new[]
            {
                "test/NodeWorkspace",
                "sample/Showcase/Showcase.ReactApp.Fetch",
                "sample/Showcase/Showcase.ReactApp.Axios",
            }
            .SelectMany(folder =>
            {
                var package = JsonNode.Parse(File.ReadAllText(Path.Combine(root, folder, "package.json")))!;
                return new[] { "dependencies", "devDependencies" }
                    .SelectMany(section => package[section]?.AsObject() ?? [])
                    .Select(p => (Folder: folder, Package: p.Key, Version: p.Value!.GetValue<string>().TrimStart('^', '~')));
            })
            .Where(t => ranges.ContainsKey(t.Package))
            .ToList();

        Assert.NotEmpty(tested);
        var shutOut = tested
            .Where(t => !Satisfies(ranges[t.Package], t.Version))
            .Select(t => $"{t.Package} {t.Version} ({t.Folder}) is not in \"{ranges[t.Package]}\"")
            .ToList();
        Assert.True(shutOut.Count == 0, string.Join(Environment.NewLine, shutOut));
    }

    // The ranges the scaffold writes: caret ranges, alternatives joined by ||
    private static bool Satisfies(
        string range,
        string version)
    {
        var v = Version.Parse(version);
        return range.Split("||", StringSplitOptions.TrimEntries).Any(part =>
        {
            Assert.StartsWith("^", part, StringComparison.Ordinal);
            var floor = Version.Parse(part.AsSpan(1));
            return v.Major == floor.Major && v >= floor;
        });
    }
}