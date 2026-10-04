namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// Every marker file in the repository - the samples and the test scenarios - sets only settings and values the
/// generators know.
/// </summary>
public sealed class RepositoryMarkerFilesTests
{
    public static TheoryData<string> MarkerFiles()
    {
        var root = FindRepositoryRoot();
        var data = new TheoryData<string>();
        foreach (var file in new[] { "sample", Path.Combine("test", "Scenarios") }
                     .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), ".atc-rest-api-*", SearchOption.AllDirectories))
                     .Where(file => ConfigTypeOf(file) is not null &&
                                    !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                                    !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                     .OrderBy(file => file, StringComparer.Ordinal))
        {
            data.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
        }

        return data;
    }

    [Fact]
    public void TheRepository_HasMarkerFiles()
        => Assert.True(MarkerFiles().Count > 130);

    [Theory]
    [MemberData(nameof(MarkerFiles))]
    public void MarkerFile_HasOnlyKnownSettingsAndValues(string markerFile)
    {
        var path = Path.Combine(FindRepositoryRoot(), markerFile);

        var problems = MarkerConfigInspector.FindProblems(File.ReadAllText(path), ConfigTypeOf(path)!);

        Assert.Empty(problems);
    }

    private static Type? ConfigTypeOf(string path)
        => Path.GetFileName(path) switch
        {
            Constants.MarkerFile.Server or Constants.MarkerFile.ServerJson => typeof(ServerConfig),
            Constants.MarkerFile.ServerHandlers or Constants.MarkerFile.ServerHandlersJson => typeof(ServerDomainConfig),
            Constants.MarkerFile.Client or Constants.MarkerFile.ClientJson => typeof(ClientConfig),
            _ => null,
        };

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