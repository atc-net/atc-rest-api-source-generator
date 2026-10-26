namespace Atc.CodeGeneration.CSharp.Helpers;

/// <summary>
/// Turns text that comes from a specification (summaries, descriptions, defaults, enum values)
/// into C# source that compiles and means the same text - whatever characters it holds.
/// </summary>
public static class CSharpLiteralHelper
{
    private static readonly char[] LineBreaks = [(char)13, (char)10];

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
        "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
        "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
        "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
        "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
    };

    // An ampersand that does not start an entity, and a "<" that does not start a documentation tag
    // (the generators write <c>, <see cref="..."/>, <para> ... into the texts themselves).
    private static readonly System.Text.RegularExpressions.Regex BareAmpersand = new(
        "&(?!(?:amp|lt|gt|quot|apos|#[0-9]+|#x[0-9a-fA-F]+);)",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly System.Text.RegularExpressions.Regex NonDocTagStart = new(
        "<(?!/?(?:c|code|para|see|seealso|paramref|typeparamref|list|listheader|item|term|description|example|br|inheritdoc|langword)(?![A-Za-z0-9]))",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Returns the value as a quoted C# string literal.
    /// </summary>
    /// <param name="value">The text the literal must hold.</param>
    /// <returns>A regular (non-verbatim) string literal, including its quotes.</returns>
    public static string ToStringLiteral(string value)
        => "\"" + EscapeString(value) + "\"";

    /// <summary>
    /// Escapes the value for the inside of a regular C# string literal (no quotes added).
    /// </summary>
    /// <param name="value">The text to escape.</param>
    /// <returns>The escaped text.</returns>
    public static string EscapeString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            // The halves of a surrogate pair are written as they are; a lone surrogate (from a JSON escape) has no
            // UTF-8 form, so written raw the generated file would hold U+FFFD instead
            if (char.IsSurrogate(c))
            {
                var paired = char.IsHighSurrogate(c)
                    ? i + 1 < value.Length && char.IsLowSurrogate(value[i + 1])
                    : i > 0 && char.IsHighSurrogate(value[i - 1]);
                if (paired)
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                }

                continue;
            }

            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\0':
                    sb.Append("\\0");
                    break;
                default:
                    // Other control characters and the Unicode line separators end a literal's line.
                    if (char.IsControl(c) || c == (char)0x2028 || c == (char)0x2029)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Escapes the value for the text of an XML documentation comment.
    /// </summary>
    /// <param name="value">The text to escape.</param>
    /// <returns>The text with a bare <c>&amp;</c> and a non-tag <c>&lt;</c> as entities.</returns>
    public static string EscapeXmlDoc(string value)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : NonDocTagStart.Replace(BareAmpersand.Replace(value, "&amp;"), "&lt;");

    /// <summary>
    /// Escapes the value and puts it on one or more <c>///</c> lines: a line break in the text
    /// continues the comment instead of ending it.
    /// </summary>
    /// <param name="value">The text to place after a <c>/// </c> prefix.</param>
    /// <returns>The escaped text, later lines prefixed with <c>/// </c>.</returns>
    public static string ToXmlDocText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var lines = EscapeXmlDoc(value)
            .Split(LineBreaks, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd())
            .Where(l => l.Length > 0);

        return string.Join(Environment.NewLine + "/// ", lines);
    }

    /// <summary>
    /// Returns true when the name can be used as a C# identifier as it is.
    /// </summary>
    /// <param name="name">The candidate name.</param>
    /// <returns>True for a valid identifier that is not a keyword.</returns>
    public static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name) ||
            !(char.IsLetter(name[0]) || name[0] == '_') ||
            Keywords.Contains(name))
        {
            return false;
        }

        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Makes an enum member name out of any enum value. A value that already is a valid
    /// identifier is returned unchanged.
    /// </summary>
    /// <param name="value">The enum value (or a name already derived from it).</param>
    /// <returns>A valid C# identifier.</returns>
    public static string ToEnumMemberName(string value)
    {
        if (IsValidIdentifier(value))
        {
            return value;
        }

        var sb = new StringBuilder();
        var capitalizeNext = true;
        var previousWasDigit = false;
        var pendingSeparator = false;

        foreach (var c in value ?? string.Empty)
        {
            if (c == '\'')
            {
                // An apostrophe joins: "it's" is one word.
                continue;
            }

            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                pendingSeparator = sb.Length > 0;
                capitalizeNext = true;
                continue;
            }

            // Keep two numbers apart: "1.0" is not "10".
            if (pendingSeparator && previousWasDigit && char.IsDigit(c))
            {
                sb.Append('_');
            }

            // Upper-casing follows the platform's Unicode data (ICU on Linux), which can map a letter to one this
            // runtime does not know as a letter yet: keep the letter as written then
            var upper = capitalizeNext ? char.ToUpperInvariant(c) : c;
            sb.Append(char.IsLetter(upper) || !char.IsLetter(c) ? upper : c);
            capitalizeNext = false;
            pendingSeparator = false;
            previousWasDigit = char.IsDigit(c);
        }

        if (sb.Length == 0 || char.IsDigit(sb[0]))
        {
            sb.Insert(0, "Value");
        }

        return sb.ToString();
    }
}