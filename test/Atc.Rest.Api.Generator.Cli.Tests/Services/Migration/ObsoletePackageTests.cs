namespace Atc.Rest.Api.Generator.Cli.Tests.Services.Migration;

/// <summary>
/// The packages the source generator makes obsolete are reported with their direct dependencies: removing one drops
/// what it brought in transitively, which the host may still use.
/// </summary>
public sealed class ObsoletePackageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atc-migrate-packages-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Analyze_CentrallyManagedReferences_AreFoundWithTheirCentralVersion()
    {
        Write("Directory.Packages.props", """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Atc.Rest.Extended" Version="1.0.9" />
                <PackageVersion Include="Atc.Rest.MinimalApi" Version="1.0.87" />
              </ItemGroup>
            </Project>
            """);
        var project = Write("src/Contoso.Api/Contoso.Api.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="Atc.Rest.Extended" />
                <PackageReference Include="Atc.Rest.MinimalApi" />
              </ItemGroup>
            </Project>
            """);

        var result = PackageReferenceAnalyzer.Analyze([project]);

        Assert.Equal("1.0.9", result.AtcPackages["Atc.Rest.Extended"]);
        Assert.Equal("1.0.87", result.AtcRestMinimalApiVersion);

        // Atc.Rest.MinimalApi is what the source generator's server code builds on - it is upgraded, not obsolete
        Assert.Equal(["Atc.Rest.Extended"], result.PackagesToRemove);

        // ... to the version the generated server needs: the first build stops with ATC_API_DEP013 otherwise
        var upgrade = Assert.Single(result.PackagesToUpgrade);
        Assert.Equal(("Atc.Rest.MinimalApi", "1.0.87", "3.1.0", "ATC_API_DEP013"), (upgrade.Package, upgrade.Version, upgrade.Minimum, upgrade.RuleId));
    }

    [Theory]
    [InlineData("Atc.Rest.MinimalApi", "2.0.41", "3.1.0")]
    [InlineData("Atc.Rest.MinimalApi", "3.0.1", "3.1.0")]
    [InlineData("Atc.Rest.Client", "1.0.79", "2.7.0")]
    [InlineData("Atc.Rest.Client", "2.6.0", "2.7.0")]
    public void Analyze_APackageOlderThanTheGeneratedCodeNeeds_IsToBeUpgraded(
        string package,
        string version,
        string minimum)
    {
        var project = Write("src/Contoso.Api/Contoso.Api.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="{package}" Version="{version}" />
              </ItemGroup>
            </Project>
            """);

        var result = PackageReferenceAnalyzer.Analyze([project]);

        var upgrade = Assert.Single(result.PackagesToUpgrade);
        Assert.Equal((package, version, minimum), (upgrade.Package, upgrade.Version, upgrade.Minimum));
    }

    [Theory]
    [InlineData("Atc.Rest.MinimalApi", "3.1.0")]
    [InlineData("Atc.Rest.Client", "2.7.0")]
    [InlineData("Atc.Rest.Client", "2.7.1-preview.3")]
    public void Analyze_APackageTheGeneratedCodeCanUse_IsNotToBeUpgraded(
        string package,
        string version)
    {
        var project = Write("src/Contoso.Api/Contoso.Api.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="{package}" Version="{version}" />
              </ItemGroup>
            </Project>
            """);

        Assert.Empty(PackageReferenceAnalyzer.Analyze([project]).PackagesToUpgrade);
    }

    [Fact]
    public void DirectDependencies_AreReadFromTheNuspecInTheGlobalPackagesFolder()
    {
        var packages = Path.Combine(root, "packages");
        Write("packages/atc.rest.extended/1.0.9/atc.rest.extended.nuspec", """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>Atc.Rest.Extended</id>
                <version>1.0.9</version>
                <dependencies>
                  <group targetFramework="net8.0">
                    <dependency id="Contoso.Telemetry" version="2.0.0" />
                    <dependency id="Contoso.Versioning.ApiExplorer" version="6.0.0" />
                  </group>
                </dependencies>
              </metadata>
            </package>
            """);

        Assert.Equal(["Contoso.Telemetry", "Contoso.Versioning.ApiExplorer"], NuGetPackageDependencies.Read("Atc.Rest.Extended", "1.0.9", packages));
        Assert.Null(NuGetPackageDependencies.Read("Atc.Rest.Extended", "9.9.9", packages));
    }

    [Fact]
    public void DescribeObsoletePackages_NamesTheDependenciesToKeep_OrSaysTheyAreUnknown()
    {
        var packages = Path.Combine(root, "packages");
        Write("packages/atc.rest.extended/1.0.9/atc.rest.extended.nuspec", """
            <package><metadata><dependencies><dependency id="Contoso.Telemetry" version="2.0.0" /></dependencies></metadata></package>
            """);
        var references = new PackageReferenceResult
        {
            AtcPackages = { ["Atc.Rest.Extended"] = "1.0.9", ["Atc.Rest.Extended.Options"] = "1.0.9" },
            PackagesToRemove = { "Atc.Rest.Extended", "Atc.Rest.Extended.Options" },
        };

        var lines = MigrateExecuteCommand.DescribeObsoletePackages(references, packages);

        Assert.Equal(
            [
                "Atc.Rest.Extended 1.0.9 - add these as direct references if the build needs them: Contoso.Telemetry",
                "Atc.Rest.Extended.Options 1.0.9 - its dependencies are unknown (the package is not in the NuGet cache)",
            ],
            lines);
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