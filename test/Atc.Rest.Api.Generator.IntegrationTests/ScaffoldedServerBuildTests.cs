namespace Atc.Rest.Api.Generator.IntegrationTests;

/// <summary>
/// What <c>generate server</c> scaffolds builds: each project structure, written by the CLI, restored from the packed
/// generator and built as a whole solution - the host's Program.cs on the generated <c>Add/Use/Map{Api}Api()</c>, the
/// project references (no cycle), and the test project. Without the coding rules (<c>--no-coding-rules</c>), so
/// the scaffold has to stand on its own: no Directory.Build.props brings a target framework, usings or packages.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ScaffoldedServerBuildTests(PackedGeneratorFixture package) : IClassFixture<PackedGeneratorFixture>
{
    [Theory]
    [InlineData("SingleProject", "")]
    [InlineData("TwoProjects", "")]
    [InlineData("ThreeProjects", "")]
    [InlineData("ThreeProjects", "--namespace Acme.Shop --domain-project Contoso.Core")]
    [InlineData("TwoProjects", "--namespace Acme.Shop --domain-project Contoso.Core --domain-output lib/Contoso.Core")]
    public async Task GenerateServer_WritesASolutionThatBuilds(
        string projectStructure,
        string options)
    {
        var output = package.NewDirectory($"scaffold-{projectStructure}-{(uint)options.GetHashCode(StringComparison.Ordinal)}");
        var cli = Path.Combine(AppContext.BaseDirectory, "atc-rest-api-gen.dll");
        var specification = Path.Combine(AppContext.BaseDirectory, "Scenarios", "Demo", "Demo.yaml");

        string[] arguments =
        [
            cli,
            "generate",
            "server",
            "-s",
            specification,
            "-o",
            output,
            "-n",
            "Contoso",
            "--project-structure",
            projectStructure,
            "--no-coding-rules",
            .. options.Split(' ', StringSplitOptions.RemoveEmptyEntries),
        ];
        var scaffold = await PackedGeneratorFixture.RunDotnetAsync(output, arguments);
        Assert.True(scaffold.ExitCode == 0, $"generate server failed:\n{scaffold.Output}");

        // The scaffold references the generator version the CLI ships with: build against the packed one instead
        foreach (var project in Directory.EnumerateFiles(output, "*.csproj", SearchOption.AllDirectories))
        {
            var content = await File.ReadAllTextAsync(project, TestContext.Current.CancellationToken);
            var pinned = Regex.Replace(
                content,
                "Include=\"Atc.Rest.Api.SourceGenerator\" Version=\"[^\"]*\"",
                $"Include=\"Atc.Rest.Api.SourceGenerator\" Version=\"{package.Version}\"",
                RegexOptions.None,
                TimeSpan.FromSeconds(1));
            await File.WriteAllTextAsync(project, pinned, TestContext.Current.CancellationToken);
        }

        package.WriteNuGetConfig(output);

        var build = await PackedGeneratorFixture.RunDotnetAsync(output, "build", "Contoso.slnx", "-p:UseSharedCompilation=false");
        Assert.True(build.ExitCode == 0, $"dotnet build of the {projectStructure} scaffold failed:\n{build.Output}");
    }
}