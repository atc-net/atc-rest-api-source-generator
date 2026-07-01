namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Post-processes fully-rendered generated file content to add <c>[ExcludeFromCodeCoverage]</c>
/// next to every <c>[GeneratedCode]</c> attribute, when enabled via marker file configuration.
/// Operating on the final text (rather than threading the flag through every extractor that
/// builds a <c>[GeneratedCode]</c> attribute) guarantees every emitted type is covered, including
/// ones assembled from multiple extractors into a single file.
/// </summary>
public static class GeneratedCodeAttributeHelper
{
    // [ExcludeFromCodeCoverage] is only valid on assembly/module/class/struct/constructor/method/
    // property/event — NOT interface, enum, or delegate. Skip attaching it to those declarations.
    private static readonly string[] UnsupportedDeclarationTokens = ["interface", "enum", "delegate"];

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex CodeDomCompilerUsingRegex = new(
        @"(?<using>using System\.CodeDom\.Compiler;[ \t]*)(?<eol>\r?\n)",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        RegexTimeout);

    private static readonly Regex NullableEnableRegex = new(
        @"(?<directive>#nullable enable[ \t]*)(?<eol>\r?\n)",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        RegexTimeout);

    /// <summary>
    /// Inserts <c>[ExcludeFromCodeCoverage]</c> immediately after every <c>[GeneratedCode(...)]</c>
    /// line in <paramref name="content"/> that precedes a declaration the attribute is valid on
    /// (class/struct/record — not interface/enum/delegate), and ensures the required
    /// <c>System.Diagnostics.CodeAnalysis</c> using directive is present. No-op when
    /// <paramref name="excludeFromCodeCoverage"/> is <see langword="false"/> or the content has no
    /// eligible <c>[GeneratedCode]</c> attribute.
    /// </summary>
    public static string ApplyExcludeFromCodeCoverage(
        string content,
        bool excludeFromCodeCoverage)
    {
        if (!excludeFromCodeCoverage ||
            string.IsNullOrEmpty(content) ||
            content.IndexOf("[GeneratedCode(", StringComparison.Ordinal) < 0)
        {
            return content;
        }

        var lines = SplitLines(content);
        var result = new StringBuilder();
        var didInsert = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var (text, eol) = lines[i];
            result.Append(text).Append(eol);

            var trimmed = text.TrimStart();
            if (!trimmed.StartsWith("[GeneratedCode(", StringComparison.Ordinal) ||
                !SupportsExcludeFromCodeCoverage(lines, i + 1))
            {
                continue;
            }

            var indent = text.Substring(0, text.Length - trimmed.Length);
            result.Append(indent).Append("[ExcludeFromCodeCoverage]").Append(eol);
            didInsert = true;
        }

        return didInsert ? EnsureUsingDirective(result.ToString()) : content;
    }

    /// <summary>
    /// Scans forward past blank lines, further attributes, and doc comments to find the
    /// declaration line that follows a <c>[GeneratedCode]</c> attribute, and checks whether it is a
    /// kind that supports <c>[ExcludeFromCodeCoverage]</c>.
    /// </summary>
    private static bool SupportsExcludeFromCodeCoverage(
        List<(string Text, string Eol)> lines,
        int startIndex)
    {
        for (var i = startIndex; i < lines.Count; i++)
        {
            var trimmed = lines[i].Text.Trim();
            if (trimmed.Length == 0 ||
                trimmed.StartsWith("[", StringComparison.Ordinal) ||
                trimmed.StartsWith("///", StringComparison.Ordinal))
            {
                continue;
            }

            var tokens = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            return !UnsupportedDeclarationTokens.Any(token => tokens.Contains(token, StringComparer.Ordinal));
        }

        return false;
    }

    private static string EnsureUsingDirective(string content)
    {
        if (content.IndexOf("System.Diagnostics.CodeAnalysis", StringComparison.Ordinal) >= 0)
        {
            return content;
        }

        if (CodeDomCompilerUsingRegex.IsMatch(content))
        {
            return CodeDomCompilerUsingRegex.Replace(
                content,
                match => match.Groups["using"].Value + match.Groups["eol"].Value + "using System.Diagnostics.CodeAnalysis;" + match.Groups["eol"].Value,
                1);
        }

        // Defensive fallback for content that carries [GeneratedCode] without the usual
        // System.CodeDom.Compiler using (should not normally happen).
        return NullableEnableRegex.Replace(
            content,
            match => match.Groups["directive"].Value + match.Groups["eol"].Value + "using System.Diagnostics.CodeAnalysis;" + match.Groups["eol"].Value,
            1);
    }

    /// <summary>
    /// Splits content into lines, keeping each line's original terminator (<c>\r\n</c>, <c>\n</c>,
    /// or empty for a final line with no trailing newline) so reassembly is byte-for-byte faithful.
    /// </summary>
    private static List<(string Text, string Eol)> SplitLines(string content)
    {
        var lines = new List<(string Text, string Eol)>();
        var start = 0;

        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] != '\n')
            {
                continue;
            }

            var hasCr = i > start && content[i - 1] == '\r';
            var textEnd = hasCr ? i - 1 : i;
            lines.Add((content.Substring(start, textEnd - start), content.Substring(textEnd, i - textEnd + 1)));
            start = i + 1;
        }

        if (start < content.Length)
        {
            lines.Add((content.Substring(start), string.Empty));
        }

        return lines;
    }
}