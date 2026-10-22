namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Resolves the root namespace used for generated code.
/// </summary>
/// <remarks>
/// The OpenAPI specification file name must not <i>control</i> the namespace. A better source is
/// tried first, but the file name is retained as the last-resort fallback so that existing
/// projects keep their current namespace.
/// <para>Precedence:</para>
/// <list type="number">
/// <item><description>The <c>namespace</c> value from the marker file.</description></item>
/// <item><description>The document <c>info.title</c>, but only when it already is a valid
/// dot-separated C# identifier (see <see cref="TryGetNamespaceFromTitle"/>).</description></item>
/// <item><description>The specification file name without extension.</description></item>
/// </list>
/// <para>
/// The MSBuild <c>RootNamespace</c> property is deliberately <i>not</i> part of the chain. MSBuild
/// always defaults it to the project file name, so it could never act as a silent fallback: it
/// would replace the file name rule rather than sit above it, renaming the generated namespace of
/// every project that does not pin <c>namespace</c> in its marker file.
/// </para>
/// </remarks>
public static class NamespaceResolver
{
    /// <summary>
    /// Resolves the root namespace using the three-rule precedence chain.
    /// </summary>
    /// <param name="configNamespace">The <c>namespace</c> value from the marker file, if any.</param>
    /// <param name="documentTitle">The OpenAPI <c>info.title</c> value, if any.</param>
    /// <param name="yamlPath">The path of the specification file, used as last resort.</param>
    /// <returns>The resolved root namespace.</returns>
    public static string Resolve(
        string? configNamespace,
        string? documentTitle,
        string yamlPath)
    {
        if (!string.IsNullOrWhiteSpace(configNamespace))
        {
            return ToValidNamespace(configNamespace!.Trim());
        }

        var fromTitle = TryGetNamespaceFromTitle(documentTitle);
        if (fromTitle is not null)
        {
            return fromTitle;
        }

        return ToValidNamespace(Path.GetFileNameWithoutExtension(yamlPath));
    }

    /// <summary>
    /// Makes a name a valid C# namespace, keeping one that already is: each dot-separated segment that is no
    /// identifier is rebuilt from its words with a capital first letter (<c>my-api</c> becomes <c>MyApi</c>), a
    /// segment starting with a digit gets an underscore (<c>1api</c> becomes <c>_1api</c>), a keyword is capitalised
    /// (<c>class</c> becomes <c>Class</c>), and empty segments are dropped.
    /// </summary>
    /// <param name="name">The name - a file name, a marker value.</param>
    /// <returns>A valid namespace; <c>Api</c> when nothing of the name can be used.</returns>
    public static string ToValidNamespace(string? name)
    {
        var segments = new List<string>();
        foreach (var segment in (name ?? string.Empty).Split('.'))
        {
            var valid = ToValidSegment(segment);
            if (valid.Length > 0)
            {
                segments.Add(valid);
            }
        }

        return segments.Count == 0 ? "Api" : string.Join(".", segments);
    }

    /// <summary>
    /// Returns the <paramref name="title"/> when it already is a valid dot-separated C# identifier,
    /// otherwise <see langword="null"/>.
    /// </summary>
    /// <param name="title">The OpenAPI <c>info.title</c> value.</param>
    /// <returns>The qualifying namespace, or <see langword="null"/> when the title does not qualify.</returns>
    /// <remarks>
    /// The gate performs no normalization on purpose. A prose title such as "Swagger Petstore" is
    /// rejected rather than converted into "SwaggerPetstore", because normalizing would change the
    /// namespace of existing projects that currently fall through to the specification file name.
    /// </remarks>
    public static string? TryGetNamespaceFromTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var trimmed = title!.Trim();

        var segments = trimmed.Split('.');
        foreach (var segment in segments)
        {
            if (!IsValidIdentifier(segment))
            {
                return null;
            }
        }

        return trimmed;
    }

    private static bool IsValidIdentifier(string segment)
    {
        if (segment.Length == 0 ||
            CSharpKeywords.IsKeyword(segment))
        {
            return false;
        }

        var first = segment[0];
        if (first != '_' &&
            !char.IsLetter(first))
        {
            return false;
        }

        for (var i = 1; i < segment.Length; i++)
        {
            var current = segment[i];
            if (current != '_' &&
                !char.IsLetterOrDigit(current))
            {
                return false;
            }
        }

        return true;
    }

    private static string ToValidSegment(string segment)
    {
        if (IsValidIdentifier(segment))
        {
            return segment;
        }

        if (CSharpKeywords.IsKeyword(segment))
        {
            return char.ToUpperInvariant(segment[0]) + segment.Substring(1);
        }

        var sb = new StringBuilder(segment.Length + 1);
        var startOfWord = true;
        foreach (var c in segment)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                startOfWord = true;
                continue;
            }

            sb.Append(startOfWord ? char.ToUpperInvariant(c) : c);
            startOfWord = false;
        }

        if (sb.Length > 0 && char.IsDigit(sb[0]))
        {
            sb.Insert(0, '_');
        }

        return sb.ToString();
    }
}