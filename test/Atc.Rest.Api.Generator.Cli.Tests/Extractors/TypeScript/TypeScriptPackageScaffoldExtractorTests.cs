namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptPackageScaffoldExtractorTests
{
    [Fact]
    public void GeneratePackageJson_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptPackageScaffoldExtractor.GeneratePackageJson("pkg", "1.0.0", description: null, config: null!));
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
    public void GeneratePackageJson_EmitsExportsAndDistEntryPoints()
    {
        // ES modules + bundler resolution rely on these exact entry points; missing one
        // breaks consumers that import from the package root.
        var json = TypeScriptPackageScaffoldExtractor.GeneratePackageJson("pkg", "1.0.0", description: null, new TypeScriptClientConfig());

        Assert.Contains("\"main\": \"./dist/index.js\"", json, StringComparison.Ordinal);
        Assert.Contains("\"types\": \"./dist/index.d.ts\"", json, StringComparison.Ordinal);
        Assert.Contains("\"exports\":", json, StringComparison.Ordinal);
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
}