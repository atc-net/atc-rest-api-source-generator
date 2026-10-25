namespace Atc.CodeGeneration.TypeScript.Helpers;

/// <summary>
/// Writes generated TypeScript files to disk.
/// </summary>
public static class FileHelper
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes the content as UTF-8 without BOM, with LF line endings and without trailing whitespace, creating the
    /// directory if needed. A file that already holds that content is left untouched, so a watcher or bundler does not
    /// see it change.
    /// </summary>
    /// <param name="filePath">The path of the file to write.</param>
    /// <param name="content">The TypeScript source.</param>
    /// <returns>True when the file was written; false when it already held the content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is null or empty.</exception>
    public static bool WriteTsFile(
        string filePath,
        string content)
        => WriteTextFile(filePath, content?.NormalizeForSourceOutput() ?? string.Empty);

    /// <summary>
    /// Writes the content as UTF-8 without BOM and with LF line endings, creating the directory if needed. The output
    /// is the same on every platform: content built from <c>\n</c> and from <see cref="Environment.NewLine"/> does not
    /// reach the file as a mix. A file that already holds the content, also with CRLF throughout (a git checkout with
    /// <c>core.autocrlf=true</c>), is left untouched; one with mixed line endings is rewritten.
    /// </summary>
    /// <param name="filePath">The path of the file to write.</param>
    /// <param name="content">The text.</param>
    /// <returns>True when the file was written; false when it already held the content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is null or empty.</exception>
    public static bool WriteTextFile(
        string filePath,
        string content)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var normalized = ToLf(content ?? string.Empty);
        if (File.Exists(filePath))
        {
            var existing = File.ReadAllText(filePath, Utf8NoBom);
            if (HasUniformLineEndings(existing) &&
                string.Equals(ToLf(existing), normalized, StringComparison.Ordinal))
            {
                return false;
            }
        }

        File.WriteAllText(filePath, normalized, Utf8NoBom);
        return true;
    }

    // All LF or all CRLF (no lone CR)
    private static bool HasUniformLineEndings(string text)
    {
        var cr = 0;
        var lf = 0;
        var crlf = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                cr++;
            }
            else if (text[i] == '\n')
            {
                lf++;
                if (i > 0 && text[i - 1] == '\r')
                {
                    crlf++;
                }
            }
        }

        return cr == crlf && (crlf == 0 || crlf == lf);
    }

    private static string ToLf(string text)
        => text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');
}