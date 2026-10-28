namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Writes the XML doc comment pieces the generated host methods share. IDE tooltips join the lines of a doc comment
/// into one paragraph, and Visual Studio's tooltip runs a <c>&lt;list&gt;</c> into one line too, so a list item is
/// a paragraph of its own (<c>&lt;para&gt;• item&lt;/para&gt;</c>) - one line per item in every tooltip.
/// </summary>
public static class XmlDocCommentHelper
{
    /// <summary>
    /// Appends a bullet list: one <c>&lt;para&gt;• entry&lt;/para&gt;</c> per entry.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="indent">The indentation of the doc comment.</param>
    /// <param name="items">The items, already XML-escaped (they may carry <c>&lt;c&gt;</c> or <c>&lt;see&gt;</c>).</param>
    public static void AppendBulletList(
        StringBuilder builder,
        int indent,
        IEnumerable<string> items)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        foreach (var item in items)
        {
            builder.AppendLine(indent, $"/// <para>• {item}</para>");
        }
    }

    /// <summary>
    /// Appends a paragraph (<c>&lt;para&gt;</c>), so it starts on its own line in a tooltip.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="indent">The indentation of the doc comment.</param>
    /// <param name="text">The text, already XML-escaped.</param>
    public static void AppendParagraph(
        StringBuilder builder,
        int indent,
        string text)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.AppendLine(indent, $"/// <para>{text}</para>");
    }
}