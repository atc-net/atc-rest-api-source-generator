namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// A C# enum in generated code.
/// </summary>
/// <param name="FullName">The enum's fully qualified name, with <c>global::</c>.</param>
/// <param name="IsInteger">Whether it is an integer on the wire (its converter is <c>JsonNumberEnumConverter&lt;T&gt;</c>).</param>
/// <param name="EmptyMember">The member whose value is the empty string, if the specification lists it: System.Text.Json
/// takes no empty <c>[JsonStringEnumMemberName]</c>, so the generated converter writes and reads it itself.</param>
public sealed record GeneratedEnumDeclaration(
    string FullName,
    bool IsInteger,
    string? EmptyMember = null)
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    // A generated file has one file-scoped namespace and top-level enum declarations
    private static readonly Regex NamespaceRegex = new(
        @"^namespace\s+(?<name>[\w.]+)\s*;",
        RegexOptions.Multiline | RegexOptions.ExplicitCapture,
        RegexTimeout);

    private static readonly Regex EnumRegex = new(
        @"^public enum (?<name>\w+)\b",
        RegexOptions.Multiline | RegexOptions.ExplicitCapture,
        RegexTimeout);

    /// <summary>
    /// The enums a generated file declares.
    /// </summary>
    /// <param name="content">The file's content.</param>
    /// <returns>The enums, in order.</returns>
    public static IReadOnlyList<GeneratedEnumDeclaration> FindIn(string content)
    {
        if (string.IsNullOrEmpty(content) ||
            content.IndexOf("public enum ", StringComparison.Ordinal) < 0 ||
            NamespaceRegex.Match(content) is not { Success: true } namespaceMatch)
        {
            return [];
        }

        var namespaceName = namespaceMatch.Groups["name"].Value;
        return [.. EnumRegex
            .Matches(content)
            .Cast<Match>()
            .Select(m => m.Groups["name"].Value)
            .Select(name => new GeneratedEnumDeclaration(
                $"global::{namespaceName}.{name}",
                content.IndexOf($"JsonNumberEnumConverter<{name}>", StringComparison.Ordinal) >= 0,
                EmptyMemberOf(content, name)))];
    }

    // The enum's ToWireValue switch names the member of the empty string: `Mark.Empty => "",`
    private static string? EmptyMemberOf(
        string content,
        string name)
        => content.IndexOf(" => \"\",", StringComparison.Ordinal) >= 0 &&
           Regex.Match(content, $@"^\s*{name}\.(?<member>\w+) => """",", RegexOptions.Multiline | RegexOptions.ExplicitCapture, RegexTimeout) is { Success: true } match
            ? match.Groups["member"].Value
            : null;
}