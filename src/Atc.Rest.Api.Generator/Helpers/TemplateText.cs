namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// A fixed template of generated code, written as raw string chunks: the chunks are its lines, each line ends with the
/// platform's newline (as <see cref="System.Text.StringBuilder.AppendLine()"/> ends it), whatever line endings the
/// source file has.
/// </summary>
public static class TemplateText
{
    /// <summary>
    /// The template's text.
    /// </summary>
    /// <param name="chunks">The chunks, in order; each one or more lines without its last newline.</param>
    /// <returns>The lines, each ended by <see cref="Environment.NewLine"/>.</returns>
    public static string Lines(IReadOnlyList<string> chunks)
    {
        if (chunks is null)
        {
            throw new ArgumentNullException(nameof(chunks));
        }

        var text = string.Join("\n", chunks).Replace("\r\n", "\n") + "\n";
        return Environment.NewLine == "\n"
            ? text
            : text.Replace("\n", Environment.NewLine);
    }
}