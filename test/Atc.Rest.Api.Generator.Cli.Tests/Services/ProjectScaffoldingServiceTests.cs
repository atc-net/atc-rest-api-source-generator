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

    [Fact]
    public void GenerateHostProject_ProgramWiresProblemDetailsForEveryError()
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
        Assert.Contains("builder.Services.AddProblemDetails();", program, StringComparison.Ordinal);
        var exceptionHandler = program.IndexOf("app.UseExceptionHandler();", StringComparison.Ordinal);
        var statusCodePages = program.IndexOf("app.UseStatusCodePages();", StringComparison.Ordinal);
        Assert.True(exceptionHandler > program.IndexOf("var app = builder.Build();", StringComparison.Ordinal), program);
        Assert.True(statusCodePages > exceptionHandler, program);
        Assert.True(statusCodePages < program.IndexOf("app.MapOpenApi();", StringComparison.Ordinal), program);
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