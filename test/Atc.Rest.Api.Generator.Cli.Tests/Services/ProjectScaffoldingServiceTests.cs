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
}