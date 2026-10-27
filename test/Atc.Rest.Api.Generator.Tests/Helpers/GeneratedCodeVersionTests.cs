namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// Every <c>[GeneratedCode]</c> the generator writes names the generator's own version (<c>GeneratorInfo.Version</c>):
/// an extractor that writes a version as a literal stamps generated code with a version that was right once.
/// </summary>
public sealed class GeneratedCodeVersionTests
{
    private static readonly System.Text.RegularExpressions.Regex LiteralVersion = new(
        @"GeneratedCode\(\\?""Atc\.Rest\.Api\.SourceGenerator\\?"",\s*\\?""\d+\.\d+",
        System.Text.RegularExpressions.RegexOptions.None,
        TimeSpan.FromSeconds(1));

    [Fact]
    public void NoExtractor_WritesTheGeneratorVersionAsALiteral()
    {
        var src = Path.Combine(FindRepositoryRoot(), "src");
        var literal = Directory
            .EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                           !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (File: file, Line: index + 1, Text: line))
                .Where(entry => LiteralVersion.IsMatch(entry.Text)))
            .Select(entry => $"{Path.GetRelativePath(src, entry.File).Replace('\\', '/')}:{entry.Line}")
            .ToList();

        Assert.True(literal.Count == 0, "A [GeneratedCode] with a literal version:\n" + string.Join("\n", literal));
    }

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