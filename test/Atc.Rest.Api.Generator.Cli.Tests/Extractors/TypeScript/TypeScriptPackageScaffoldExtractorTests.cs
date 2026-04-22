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