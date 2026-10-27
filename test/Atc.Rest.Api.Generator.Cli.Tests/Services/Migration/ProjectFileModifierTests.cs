namespace Atc.Rest.Api.Generator.Cli.Tests.Services.Migration;

public sealed class ProjectFileModifierTests : IDisposable
{
    private const string ProjectWithoutVersions = """
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <PackageReference Include="Atc.Rest.Client" />
          </ItemGroup>
        </Project>
        """;

    private readonly string root = Path.Combine(Path.GetTempPath(), "atc-migrate-cpm-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ModifyServerProject_CentralPackageManagement_WritesAVersionlessReferenceAndThePackageVersion()
    {
        var props = Write("Directory.Packages.props", """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Atc" Version="3.0.186" />
              </ItemGroup>
            </Project>
            """);
        var project = Write("src/Contoso.Api.Contracts/Contoso.Api.Contracts.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n</Project>\n");

        var result = ProjectFileModifier.ModifyServerProject(project, "../../Contoso.yaml", new Version(3, 0, 0));

        Assert.Null(result.Error);
        var csproj = File.ReadAllText(project);
        Assert.Contains("<PackageReference Include=\"Atc.Rest.Api.SourceGenerator\"", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("Version=", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageVersion Include=\"Atc.Rest.Api.SourceGenerator\" Version=\"3.0.0\" />", File.ReadAllText(props), StringComparison.Ordinal);
    }

    [Fact]
    public void ModifyClientProject_CentralPackageManagement_RaisesTheCentralVersion_AndAddsNoSecondReference()
    {
        var props = Write("Directory.Packages.props", """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Atc.Rest.Client" Version="1.0.84" />
              </ItemGroup>
            </Project>
            """);
        var project = Write("src/Contoso.ApiClient/Contoso.ApiClient.csproj", ProjectWithoutVersions);

        ProjectFileModifier.ModifyClientProject(project, "../../Contoso.yaml", new Version(3, 0, 0), new Version(2, 1, 0));

        var csproj = File.ReadAllText(project);
        Assert.Equal(1, csproj.Split("Include=\"Atc.Rest.Client\"").Length - 1);
        Assert.DoesNotContain("Version=", csproj, StringComparison.Ordinal);
        var central = File.ReadAllText(props);
        Assert.Contains("<PackageVersion Include=\"Atc.Rest.Client\" Version=\"2.1.0\" />", central, StringComparison.Ordinal);
        Assert.DoesNotContain("1.0.84", central, StringComparison.Ordinal);
    }

    [Fact]
    public void ModifyClientProject_WithoutCentralPackageManagement_RaisesTheVersionInTheProject()
    {
        var project = Write("src/Contoso.ApiClient/Contoso.ApiClient.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Atc.Rest.Client" Version="1.0.84" />
              </ItemGroup>
            </Project>
            """);

        ProjectFileModifier.ModifyClientProject(project, "../../Contoso.yaml", new Version(3, 0, 0), new Version(2, 1, 0));

        var csproj = File.ReadAllText(project);
        Assert.Contains("<PackageReference Include=\"Atc.Rest.Client\" Version=\"2.1.0\"", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"Atc.Rest.Api.SourceGenerator\"", csproj, StringComparison.Ordinal);
        Assert.Contains("Version=\"3.0.0\"", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void ModifyDomainProject_CentralPackageManagement_DryRun_WritesNeitherFile()
    {
        const string centralProps = "<Project>\n  <PropertyGroup>\n    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>\n  </PropertyGroup>\n</Project>\n";
        var props = Write("Directory.Packages.props", centralProps);
        var project = Write("src/Contoso.Domain/Contoso.Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n</Project>\n");

        var result = ProjectFileModifier.ModifyDomainProject(project, "../../Contoso.yaml", new Version(3, 0, 0), dryRun: true);

        Assert.True(result.WasModified);
        Assert.Equal("<Project Sdk=\"Microsoft.NET.Sdk\">\n</Project>\n", File.ReadAllText(project));
        Assert.Equal(centralProps, File.ReadAllText(props));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private string Write(
        string relativePath,
        string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}