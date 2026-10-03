namespace Atc.Rest.Api.Generator.Cli.Tests.Helpers;

public sealed class ApiOptionsHelperTests : IDisposable
{
    private readonly string tempPath = Path.Combine(Path.GetTempPath(), "atc-options-helper-" + Guid.NewGuid().ToString("N"));

    public ApiOptionsHelperTests()
        => Directory.CreateDirectory(tempPath);

    public void Dispose()
    {
        if (Directory.Exists(tempPath))
        {
            Directory.Delete(tempPath, recursive: true);
        }
    }

    [Fact]
    public void CreateDefaultOptionsFile_DirectoryPath_WritesDefaultFileIntoIt()
    {
        var created = ApiOptionsHelper.CreateDefaultOptionsFile(tempPath);

        Assert.True(created);
        var filePath = Path.Combine(tempPath, ApiGeneratorOptions.FileName);
        Assert.True(File.Exists(filePath));
        Assert.Contains("\"clientSuffix\": \"Client\"", File.ReadAllText(filePath), StringComparison.Ordinal);
    }

    [Fact]
    public void CreateDefaultOptionsFile_JsonFilePathInMissingFolder_CreatesFolderAndFile()
    {
        var filePath = Path.Combine(tempPath, "config", "MyOptions.json");

        var created = ApiOptionsHelper.CreateDefaultOptionsFile(filePath);

        Assert.True(created);
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void ResolveOptionsFilePath_ExplicitDirectory_AppendsDefaultFileName()
    {
        ApiOptionsHelper.CreateDefaultOptionsFile(tempPath);

        var resolved = ApiOptionsHelper.ResolveOptionsFilePath(tempPath, specificationPath: null);

        Assert.Equal(Path.Combine(tempPath, ApiGeneratorOptions.FileName), resolved);
    }

    [Fact]
    public void ResolveOptionsFilePath_ExplicitPathMissing_ReturnsNullWithoutFallingBack()
    {
        // An explicit --options wins even when it points nowhere: the specification folder is not consulted.
        ApiOptionsHelper.CreateDefaultOptionsFile(tempPath);
        var specPath = Path.Combine(tempPath, "api.yaml");

        var resolved = ApiOptionsHelper.ResolveOptionsFilePath(Path.Combine(tempPath, "missing.json"), specPath);

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveOptionsFilePath_NoExplicitPath_FindsFileNextToSpecification()
    {
        ApiOptionsHelper.CreateDefaultOptionsFile(tempPath);
        var specPath = Path.Combine(tempPath, "api.yaml");

        var resolved = ApiOptionsHelper.ResolveOptionsFilePath(optionsPath: null, specPath);

        Assert.Equal(Path.Combine(tempPath, ApiGeneratorOptions.FileName), resolved);
    }

    [Fact]
    public void LoadOptions_FileNextToSpecification_ReadsItsValues()
    {
        File.WriteAllText(
            Path.Combine(tempPath, ApiGeneratorOptions.FileName),
            """{ "client": { "clientSuffix": "Gateway" }, "general": { "includeDeprecated": true } }""");

        var options = ApiOptionsHelper.LoadOptions(optionsPath: null, Path.Combine(tempPath, "api.yaml"));

        Assert.Equal("Gateway", options.Client.ClientSuffix);
        Assert.True(options.General.IncludeDeprecated);
    }

    [Fact]
    public void LoadOptions_InvalidJson_ReturnsDefaults()
    {
        File.WriteAllText(Path.Combine(tempPath, ApiGeneratorOptions.FileName), "{ not json");

        var options = ApiOptionsHelper.LoadOptions(tempPath, specificationPath: null);

        Assert.Equal("Client", options.Client.ClientSuffix);
    }

    [Fact]
    public void ValidateOptionsFile_DefaultFile_IsValidWithoutWarnings()
    {
        ApiOptionsHelper.CreateDefaultOptionsFile(tempPath);

        var result = ApiOptionsHelper.ValidateOptionsFile(tempPath);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.NotNull(result.Options);
    }

    [Fact]
    public void ValidateOptionsFile_UnknownStubImplementation_WarnsButStaysValid()
    {
        File.WriteAllText(
            Path.Combine(tempPath, ApiGeneratorOptions.FileName),
            """{ "server": { "defaultApiVersion": "latest", "domain": { "stubImplementation": "explode" } } }""");

        var result = ApiOptionsHelper.ValidateOptionsFile(tempPath);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("stubImplementation 'explode'", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("defaultApiVersion 'latest'", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateOptionsFile_InvalidJson_ReportsError()
    {
        var filePath = Path.Combine(tempPath, ApiGeneratorOptions.FileName);
        File.WriteAllText(filePath, "{ not json");

        var result = ApiOptionsHelper.ValidateOptionsFile(filePath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.StartsWith("Invalid JSON format", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateOptionsFile_MissingFile_ReportsError()
    {
        var result = ApiOptionsHelper.ValidateOptionsFile(Path.Combine(tempPath, "nope.json"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.StartsWith("Options file not found", StringComparison.Ordinal));
    }
}