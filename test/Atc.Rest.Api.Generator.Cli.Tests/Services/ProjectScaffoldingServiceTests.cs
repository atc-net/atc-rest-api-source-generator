namespace Atc.Rest.Api.Generator.Cli.Tests.Services;

public sealed class ProjectScaffoldingServiceTests : IDisposable
{
    private readonly string tempSrcPath = Path.Combine(Path.GetTempPath(), "atc-scaffold-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ProjectScaffoldingService sut = new();

    public ProjectScaffoldingServiceTests()
        => Directory.CreateDirectory(tempSrcPath);

    public void Dispose()
    {
        if (Directory.Exists(tempSrcPath))
        {
            Directory.Delete(tempSrcPath, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A repository path that ends in a separator would escape the closing quote of the argument (<c>-p "C:\repo\"</c>
    /// reads to the next quote); the arguments carry the path without it.
    /// </summary>
    [Theory]
    [InlineData("C:\\repo\\", "C:\\repo")]
    [InlineData("C:\\repo", "C:\\repo")]
    [InlineData("/home/user/repo/", "/home/user/repo")]
    public void BuildCodingRulesUpdaterArguments_TrimsATrailingSeparator(
        string repoPath,
        string expectedPath)
    {
        var arguments = ProjectScaffoldingService.BuildCodingRulesUpdaterArguments(repoPath, Path.Combine(expectedPath, "atc-coding-rules-updater.json"), includeNames: false, "Contoso", "Repo");

        Assert.StartsWith($"run -p \"{expectedPath}\" --optionsPath \"", arguments, StringComparison.Ordinal);
        Assert.DoesNotContain("\\\"", arguments, StringComparison.Ordinal);
        Assert.DoesNotContain("--organizationName", arguments, StringComparison.Ordinal);
        Assert.Contains("--organizationName \"Contoso\" --repositoryName \"Repo\"", ProjectScaffoldingService.BuildCodingRulesUpdaterArguments(repoPath, "o.json", includeNames: true, "Contoso", "Repo"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HostUiType.Scalar)]
    [InlineData(HostUiType.Swagger)]
    public void GenerateHostProject_WithHostUi_PinsMicrosoftOpenApiAlongsideAspNetCoreOpenApi(
        HostUiType hostUi)
    {
        // Act
        var result = sut.GenerateHostProject(
            tempSrcPath,
            "Demo.Api",
            "Demo.Api.Contracts",
            "Demo.Api.Domain",
            "net10.0",
            hostUi,
            HostUiModeType.DevelopmentOnly);

        // Assert
        Assert.True(result);
        var csprojContent = File.ReadAllText(Path.Combine(tempSrcPath, "Demo.Api", "Demo.Api.csproj"));

        Assert.Contains("<PackageReference Include=\"Microsoft.AspNetCore.OpenApi\" Version=\"10.*\" />", csprojContent, StringComparison.Ordinal);

        // Microsoft.AspNetCore.OpenApi transitively pulls a vulnerable Microsoft.OpenApi 2.0.0
        // (GHSA-v5pm-xwqc-g5wc) — the scaffold must pin a patched 2.x floor. Not 3.x: that's a
        // breaking change incompatible with Microsoft.AspNetCore.OpenApi's XmlCommentGenerator.
        Assert.Contains("<PackageReference Include=\"Microsoft.OpenApi\" Version=\"2.*\" />", csprojContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// The scaffolded host is wired with the three generated calls - services, middleware, endpoints - not by hand:
    /// AddDemoApi() registers ProblemDetails, UseDemoApi() the error handling and status code pages, MapDemoApi() the
    /// endpoints. The OpenAPI UI is mapped between Use and Map.
    /// </summary>
    [Fact]
    public void GenerateHostProject_ProgramWiresTheGeneratedAddUseAndMap()
    {
        var result = sut.GenerateHostProject(
            tempSrcPath,
            "Demo.Api",
            "Demo.Api.Contracts",
            "Demo.Api.Domain",
            "net10.0",
            HostUiType.Scalar,
            HostUiModeType.DevelopmentOnly);

        Assert.True(result);
        var program = File.ReadAllText(Path.Combine(tempSrcPath, "Demo.Api", "Program.cs"));
        var add = program.IndexOf("builder.Services.AddDemoApi();", StringComparison.Ordinal);
        var build = program.IndexOf("var app = builder.Build();", StringComparison.Ordinal);
        var use = program.IndexOf("app.UseDemoApi();", StringComparison.Ordinal);
        var openApi = program.IndexOf("app.MapOpenApi();", StringComparison.Ordinal);
        var map = program.IndexOf("app.MapDemoApi();", StringComparison.Ordinal);
        Assert.True(add >= 0 && add < build && build < use && use < openApi && openApi < map, program);

        // The generated calls replace the hand wiring
        foreach (var handWired in new[] { "AddProblemDetails()", "UseExceptionHandler()", "UseStatusCodePages()", "app.MapEndpoints()" })
        {
            Assert.DoesNotContain(handWired, program, StringComparison.Ordinal);
        }

        var globalUsings = File.ReadAllText(Path.Combine(tempSrcPath, "Demo.Api", "GlobalUsings.cs"));
        Assert.Contains("global using Demo.Generated.Extensions;", globalUsings, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateHostProject_NoHostUi_DoesNotAddOpenApiPackageReferences()
    {
        // Act
        var result = sut.GenerateHostProject(
            tempSrcPath,
            "Demo.Api",
            "Demo.Api.Contracts",
            "Demo.Api.Domain",
            "net10.0",
            HostUiType.None,
            HostUiModeType.DevelopmentOnly);

        // Assert
        Assert.True(result);
        var csprojContent = File.ReadAllText(Path.Combine(tempSrcPath, "Demo.Api", "Demo.Api.csproj"));

        Assert.DoesNotContain("Microsoft.AspNetCore.OpenApi", csprojContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.OpenApi", csprojContent, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateDirectoryStructure_CreatesSrcAndTestFolders()
    {
        var result = sut.CreateDirectoryStructure(tempSrcPath);

        Assert.True(result);
        Assert.True(Directory.Exists(Path.Combine(tempSrcPath, "src")));
        Assert.True(Directory.Exists(Path.Combine(tempSrcPath, "test")));

        // Idempotent: existing folders are kept.
        Assert.True(sut.CreateDirectoryStructure(tempSrcPath));
    }

    [Fact]
    public void GenerateSolutionFile_ListsSrcAndTestProjectsInFolders()
    {
        var result = sut.GenerateSolutionFile(
            tempSrcPath,
            "Demo.Api.Contracts",
            ["src/Demo.Api/Demo.Api.csproj", "src/Demo.Api.Contracts/Demo.Api.Contracts.csproj"],
            ["test/Demo.Tests/Demo.Tests.csproj"]);

        Assert.True(result);
        var content = File.ReadAllText(Path.Combine(tempSrcPath, "Demo.slnx"));
        Assert.StartsWith("<Solution>", content, StringComparison.Ordinal);
        Assert.Contains("<Folder Name=\"/src/\">", content, StringComparison.Ordinal);
        Assert.Contains("<Project Path=\"src/Demo.Api/Demo.Api.csproj\" />", content, StringComparison.Ordinal);
        Assert.Contains("<Project Path=\"src/Demo.Api.Contracts/Demo.Api.Contracts.csproj\" />", content, StringComparison.Ordinal);
        Assert.Contains("<Folder Name=\"/test/\">", content, StringComparison.Ordinal);
        Assert.Contains("<Project Path=\"test/Demo.Tests/Demo.Tests.csproj\" />", content, StringComparison.Ordinal);
        Assert.EndsWith("</Solution>", content, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateSolutionFile_NoTestProjects_OmitsTestFolderAndKeepsExistingFile()
    {
        Assert.True(sut.GenerateSolutionFile(tempSrcPath, "Demo.Api", ["src/Demo.Api/Demo.Api.csproj"]));
        var slnxPath = Path.Combine(tempSrcPath, "Demo.slnx");
        var original = File.ReadAllText(slnxPath);
        Assert.DoesNotContain("/test/", original, StringComparison.Ordinal);

        // A second run with other projects leaves the existing solution file alone.
        Assert.True(sut.GenerateSolutionFile(tempSrcPath, "Demo.Api", ["src/Other/Other.csproj"]));

        Assert.Equal(original, File.ReadAllText(slnxPath));
    }

    [Fact]
    public void GenerateTestProject_CreatesCsprojReferencingTheContractsAndASampleTest()
    {
        var testPath = Path.Combine(tempSrcPath, "test");

        var result = sut.GenerateTestProject(testPath, "Demo.Api.Contracts", "net10.0");

        Assert.True(result);
        var projectDir = Path.Combine(testPath, "Demo.Tests");
        var csproj = File.ReadAllText(Path.Combine(projectDir, "Demo.Tests.csproj"));
        Assert.Contains("<IsTestProject>true</IsTestProject>", csproj, StringComparison.Ordinal);
        Assert.Contains("<ProjectReference Include=\"..\\..\\src\\Demo.Api.Contracts\\Demo.Api.Contracts.csproj\" />", csproj, StringComparison.Ordinal);
        var sampleTest = File.ReadAllText(Path.Combine(projectDir, "UnitTest1.cs"));
        Assert.Contains("public class UnitTest1", sampleTest, StringComparison.Ordinal);

        // With the coding rules, test/Directory.Build.props brings the target framework, the packages and the usings
        Assert.DoesNotContain("<TargetFramework>", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", csproj, StringComparison.Ordinal);
    }

    /// <summary>
    /// Without the coding rules no Directory.Build.props brings them: the test project names its target framework,
    /// the test packages and the usings the sample test needs (<c>[Fact]</c>, <c>Assert</c>) itself.
    /// </summary>
    [Fact]
    public void GenerateTestProject_WithoutCodingRules_StandsOnItsOwn()
    {
        var testPath = Path.Combine(tempSrcPath, "test");

        var result = sut.GenerateTestProject(testPath, "Demo.Api.Contracts", "net10.0", withoutCodingRules: true);

        Assert.True(result);
        var csproj = File.ReadAllText(Path.Combine(testPath, "Demo.Tests", "Demo.Tests.csproj"));
        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", csproj, StringComparison.Ordinal);
        Assert.Contains("<ImplicitUsings>enable</ImplicitUsings>", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"xunit.v3\"", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"Microsoft.NET.Test.Sdk\"", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"xunit.runner.visualstudio\"", csproj, StringComparison.Ordinal);
        Assert.Contains("<Using Include=\"Xunit\" />", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void AddCodingRulesFiles_WritesUpdaterJsonAndScript()
    {
        var result = sut.AddCodingRulesFiles(tempSrcPath);

        Assert.True(result);
        var json = File.ReadAllText(Path.Combine(tempSrcPath, "atc-coding-rules-updater.json"));
        Assert.Contains("\"projectTarget\"", json, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(tempSrcPath, "atc-coding-rules-updater.ps1")));
    }

    [Fact]
    public void RunCodingRulesUpdater_NoUpdaterConfig_SkipsAndSucceeds()
    {
        var result = sut.RunCodingRulesUpdater(tempSrcPath, "atc-net", "demo");

        Assert.True(result);
        Assert.False(File.Exists(Path.Combine(tempSrcPath, "Directory.Build.props")));
    }

    [Fact]
    public void CreateGitIgnore_WritesDotNetIgnoreRulesAndKeepsAnExistingFile()
    {
        Assert.True(sut.CreateGitIgnore(tempSrcPath));
        var gitignorePath = Path.Combine(tempSrcPath, ".gitignore");
        var content = File.ReadAllText(gitignorePath);
        Assert.Contains("[Bb]in/", content, StringComparison.Ordinal);
        Assert.Contains("[Oo]bj/", content, StringComparison.Ordinal);

        File.WriteAllText(gitignorePath, "custom");
        Assert.True(sut.CreateGitIgnore(tempSrcPath));

        Assert.Equal("custom", File.ReadAllText(gitignorePath));
    }

    [Fact]
    public void GenerateAspireProject_ReferencesTheApiProjectWithUnderscoredName()
    {
        var result = sut.GenerateAspireProject(tempSrcPath, "Demo.Aspire", "Demo.Api", "net10.0");

        Assert.True(result);
        var projectDir = Path.Combine(tempSrcPath, "Demo.Aspire");
        var csproj = File.ReadAllText(Path.Combine(projectDir, "Demo.Aspire.csproj"));
        Assert.StartsWith("<Project Sdk=\"Aspire.AppHost.Sdk/", csproj, StringComparison.Ordinal);
        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", csproj, StringComparison.Ordinal);
        Assert.Contains("<ProjectReference Include=\"..\\Demo.Api\\Demo.Api.csproj\" />", csproj, StringComparison.Ordinal);
        var program = File.ReadAllText(Path.Combine(projectDir, "Program.cs"));
        Assert.Contains(".AddProject<Projects.Demo_Api>(\"api\")", program, StringComparison.Ordinal);
    }
}