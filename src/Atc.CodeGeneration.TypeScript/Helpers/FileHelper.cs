namespace Atc.CodeGeneration.TypeScript.Helpers;

/// <summary>
/// Writes generated TypeScript files to disk.
/// </summary>
public static class FileHelper
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes the content as UTF-8 without BOM and without trailing whitespace, creating the directory if needed. A file
    /// that already holds exactly that content is left untouched, so a watcher or bundler does not see it change.
    /// </summary>
    /// <param name="filePath">The path of the file to write.</param>
    /// <param name="content">The TypeScript source.</param>
    /// <returns>True when the file was written; false when it already held the content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is null or empty.</exception>
    public static bool WriteTsFile(
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

        var normalized = content.NormalizeForSourceOutput();
        if (File.Exists(filePath) &&
            string.Equals(File.ReadAllText(filePath, Utf8NoBom), normalized, StringComparison.Ordinal))
        {
            return false;
        }

        File.WriteAllText(filePath, normalized, Utf8NoBom);
        return true;
    }
}