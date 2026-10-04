namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Rewrites the references to the old generator's contracts in a C# file to the source generator's namespaces, using a
/// <see cref="ContractsNamespaceMap"/>: a <c>using</c> of a segment becomes the namespaces that segment has, a qualified
/// reference goes to the namespace of the type it names. Whatever stands before <c>Api.Generated</c> is kept as
/// written (so a relative reference stays relative), and only <c>.Contracts</c> is rewritten - a hand-written
/// namespace of the old project (<c>…Api.Generated.Extensions</c>) is the user's and is left alone.
/// </summary>
internal static class ContractsNamespaceRewriter
{
    private const string NewMiddle = "Api.Contracts.Generated";

    private static readonly string[] Kinds = ["Models", "Handlers", "Parameters", "Results"];

    // A using or global using directive of the old contracts root, a segment, or a segment and a kind
    private static readonly Regex UsingPattern = new(
        @"^(?<lead>[ \t]*(?:global[ \t]+)?using[ \t]+)(?<prefix>(?:global::)?(?:[A-Za-z_]\w*\.)*?)Api\.Generated\.Contracts(?:\.(?<segment>[A-Za-z_]\w*)(?:\.(?<kind>Models|Handlers|Parameters|Results))?)?[ \t]*;[ \t]*(?<eol>\r?\n)?",
        RegexOptions.Multiline | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    // A qualified reference: [prefix.]Api.Generated.Contracts.A.B.C...
    private static readonly Regex QualifiedPattern = new(
        @"(?<![\w.])(?<prefix>(?:global::)?(?:[A-Za-z_]\w*\.)*?)Api\.Generated\.Contracts\.(?<chain>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)",
        RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Rewrites <paramref name="content"/>.
    /// </summary>
    /// <returns>The new content and the distinct changes made (<c>old → new</c>).</returns>
    public static (string Content, IReadOnlyList<string> Replacements) Rewrite(
        string content,
        ContractsNamespaceMap map)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(map);

        var replacements = new List<string>();

        void Note(
            string old,
            string updated)
        {
            var change = $"{old} → {updated}";
            if (!replacements.Contains(change, StringComparer.Ordinal))
            {
                replacements.Add(change);
            }
        }

        var result = UsingPattern.Replace(content, match =>
        {
            var namespaces = UsingTargets(match, map);
            var lines = string.Concat(namespaces.Select(ns =>
                match.Groups["lead"].Value + match.Groups["prefix"].Value + NewMiddle + Tail(ns, map) + ";" + EndOfLine(match)));
            Note(match.Value.Trim(), namespaces.Count == 0 ? "(removed)" : string.Join(", ", namespaces));
            return lines;
        });

        result = QualifiedPattern.Replace(result, match =>
        {
            var parts = match.Groups["chain"].Value.Split('.');
            for (var i = 0; i < parts.Length; i++)
            {
                if (map.TryGetTypeNamespace(parts[i], out var namespaceName))
                {
                    var updated = match.Groups["prefix"].Value + NewMiddle + Tail(namespaceName, map) + "." + string.Join(".", parts[i..]);
                    Note(match.Value, updated);
                    return updated;
                }

                // Only segment and kind words may stand before the type
                if (i > 0 && !Kinds.Contains(parts[i], StringComparer.Ordinal))
                {
                    break;
                }
            }

            return match.Value;
        });

        return (result, replacements);
    }

    private static List<string> UsingTargets(
        Match match,
        ContractsNamespaceMap map)
    {
        var segment = match.Groups["segment"].Value;
        if (segment.Length == 0)
        {
            return map.SharedModelsNamespace is { } shared ? [shared] : [];
        }

        // using ...Contracts.Address; names a type, not a namespace - leave it to the qualified rule's lookup
        var namespaces = map.GetSegmentNamespaces(segment)?.ToList() ?? [];
        var kind = match.Groups["kind"].Value;
        return kind.Length == 0
            ? namespaces
            : namespaces.Where(ns => ns.EndsWith("." + kind, StringComparison.Ordinal)).ToList();
    }

    private static string Tail(
        string namespaceName,
        ContractsNamespaceMap map)
        => namespaceName[map.GeneratedRoot.Length..];

    private static string EndOfLine(Match match)
        => match.Groups["eol"].Success ? match.Groups["eol"].Value : string.Empty;
}