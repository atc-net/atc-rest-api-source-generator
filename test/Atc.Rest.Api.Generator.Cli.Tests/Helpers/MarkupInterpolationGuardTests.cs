namespace Atc.Rest.Api.Generator.Cli.Tests.Helpers;

/// <summary>
/// A value interpolated straight into Spectre markup is parsed as markup, so a
/// <c>[</c> in a path or an exception message crashes the CLI. Every interpolated markup call in the
/// CLI must go through the interpolated overloads (<c>MarkupLineInterpolated</c>,
/// <c>MarkupInterpolated</c>, <c>Markup.FromInterpolated</c>), which escape every hole.
/// </summary>
public sealed partial class MarkupInterpolationGuardTests
{
    /// <summary>
    /// Calls whose first argument is not a string literal: the value must come from a source that
    /// already escaped it. Each entry is "relative file path|line content fragment".
    /// </summary>
    private static readonly string[] NonLiteralAllowList =
    [
        "Commands/SpecValidateCommand.cs|AnsiConsole.Markup(markup);", // DiagnosticMessageFormatter.FormatSpectreMarkup escapes every value it embeds.
    ];

    [Fact]
    public void CliSource_HasNoRawInterpolatedMarkupCalls()
    {
        var offenders = FindOffenders(RawInterpolatedMarkupRegex(), allowList: []);

        Assert.True(
            offenders.Count == 0,
            "Interpolated markup must use MarkupLineInterpolated/MarkupInterpolated/Markup.FromInterpolated so every hole is escaped:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void CliSource_MarkupCallsWithNonLiteralArgument_AreAllowListed()
    {
        var offenders = FindOffenders(NonLiteralMarkupRegex(), NonLiteralAllowList);

        Assert.True(
            offenders.Count == 0,
            "Markup calls with a computed argument must be escaped at the source and allow-listed here:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, offenders));
    }

    [GeneratedRegex(@"(?:AnsiConsole\.Markup(?:Line)?|new Markup)\(\s*\$""", RegexOptions.NonBacktracking)]
    private static partial Regex RawInterpolatedMarkupRegex();

    [GeneratedRegex(@"(?:AnsiConsole\.Markup(?:Line)?|new Markup)\(\s*(?!\$?""|string\.Empty)[A-Za-z_]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NonLiteralMarkupRegex();

    private static List<string> FindOffenders(
        Regex regex,
        IReadOnlyCollection<string> allowList)
    {
        var sourceDirectory = CliTestHelper.GetCliSourceDirectory();
        Assert.True(Directory.Exists(sourceDirectory), $"CLI source directory not found: {sourceDirectory}");

        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(sourceDirectory, file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!regex.IsMatch(line))
                {
                    continue;
                }

                var allowed = allowList.Any(entry =>
                {
                    var parts = entry.Split('|', 2);
                    return string.Equals(parts[0], relativePath, StringComparison.Ordinal) &&
                           line.Contains(parts[1], StringComparison.Ordinal);
                });

                if (!allowed)
                {
                    offenders.Add($"{relativePath}:{i + 1}: {line.Trim()}");
                }
            }
        }

        return offenders;
    }
}