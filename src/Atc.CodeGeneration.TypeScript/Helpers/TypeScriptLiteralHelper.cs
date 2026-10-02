namespace Atc.CodeGeneration.TypeScript.Helpers;

/// <summary>
/// Turns text that comes from a specification (descriptions, enum values) into TypeScript
/// source that compiles and means the same text - whatever characters it holds.
/// </summary>
public static class TypeScriptLiteralHelper
{
    /// <summary>
    /// Returns the value as a single-quoted TypeScript string literal.
    /// </summary>
    /// <param name="value">The text the literal must hold.</param>
    /// <returns>The literal, including its quotes.</returns>
    public static string ToStringLiteral(string value)
    {
        var sb = new StringBuilder((value?.Length ?? 0) + 8);
        sb.Append('\'');

        foreach (var c in value ?? string.Empty)
        {
            switch (c)
            {
                case (char)92:
                    sb.Append(@"\\");
                    break;
                case '\'':
                    sb.Append(@"\'");
                    break;
                case '\n':
                    sb.Append(@"\n");
                    break;
                case '\r':
                    sb.Append(@"\r");
                    break;
                case '\t':
                    sb.Append(@"\t");
                    break;
                default:
                    // Other control characters and the Unicode line separators end a literal's line.
                    if (char.IsControl(c) || c == (char)0x2028 || c == (char)0x2029)
                    {
                        sb.Append(@"\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        sb.Append('\'');
        return sb.ToString();
    }

    /// <summary>
    /// Escapes text for the inside of a block comment (JSDoc): the text can never end the comment.
    /// </summary>
    /// <param name="value">The comment text.</param>
    /// <returns>The text with every comment terminator broken up.</returns>
    public static string EscapeComment(string? value)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : value!.Replace("*/", @"*\/");

    /// <summary>
    /// Makes an enum member name out of any text. A valid identifier is returned unchanged.
    /// </summary>
    /// <param name="value">The enum value (or a name already derived from it).</param>
    /// <returns>A valid TypeScript identifier.</returns>
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

            sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
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

    private static bool IsValidIdentifier(string? name)
    {
        if (string.IsNullOrEmpty(name) || !(char.IsLetter(name![0]) || name[0] == '_'))
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
}