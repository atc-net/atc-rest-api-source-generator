namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// No project in the repository references a project outside it: CI checks out this repository only, and a release
/// cannot ship on a sibling checkout (the 3.0 development ran on a project reference to the unreleased
/// Atc.Rest.MinimalApi until its package was published).
/// </summary>
public sealed class RepositoryProjectReferencesTests
{
    [Fact]
    public void NoProjectReferenceLeavesTheRepository()
    {
        var root = FindRepositoryRoot();
        var projects = Directory
            .EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(file => !InBuildOutput(file))
            .ToList();

        var outside = projects
            .SelectMany(project => ReferencesOutside(root, project, File.ReadAllText(project))
                .Select(reference => $"{Path.GetRelativePath(root, project).Replace('\\', '/')} -> {reference}"))
            .ToList();

        Assert.True(projects.Count > 40, $"Only {projects.Count} projects found under {root}");
        Assert.True(outside.Count == 0, "Project references that leave the repository:\n" + string.Join("\n", outside));
    }

    /// <summary>
    /// The guard is not blind: the temporary reference of the 3.0 development is one it reports, and a reference
    /// inside the repository is not.
    /// </summary>
    [Fact]
    public void AProjectReferenceToASiblingRepository_IsReported()
    {
        var root = Path.Combine(Path.GetTempPath(), "contoso-repository");
        var project = Path.Combine(root, "sample", "Contoso", "Contoso.Api.Contracts", "Contoso.Api.Contracts.csproj");
        const string content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\..\..\..\atc-rest-minimalapi\src\Atc.Rest.MinimalApi\Atc.Rest.MinimalApi.csproj" />
                <ProjectReference Include="..\..\..\src\Atc.Rest.Api.SourceGenerator\Atc.Rest.Api.SourceGenerator.csproj" OutputItemType="Analyzer" />
              </ItemGroup>
            </Project>
            """;

        var outside = ReferencesOutside(root, project, content);

        Assert.Equal([@"..\..\..\..\atc-rest-minimalapi\src\Atc.Rest.MinimalApi\Atc.Rest.MinimalApi.csproj"], outside);
    }

    private static List<string> ReferencesOutside(
        string root,
        string projectPath,
        string projectContent)
    {
        var rootWithSeparator = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        return System.Xml.Linq.XDocument
            .Parse(projectContent)
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => include is not null)
            .Select(include => include!)
            .Where(include => !Path
                .GetFullPath(Path.Combine(projectDirectory, include.Replace('\\', Path.DirectorySeparatorChar)))
                .StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool InBuildOutput(string file)
        => file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
           file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
           file.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "test", "Scenarios")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("test/Scenarios not found above " + AppContext.BaseDirectory);
    }
}