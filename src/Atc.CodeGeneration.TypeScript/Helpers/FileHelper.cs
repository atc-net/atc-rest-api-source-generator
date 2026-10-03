namespace Atc.CodeGeneration.TypeScript.Helpers;

/// <summary>
/// Writes generated TypeScript files to disk.
/// </summary>
public static class FileHelper
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes the content as UTF-8 without BOM and without trailing whitespace, creating the directory if needed.
    /// </summary>
    /// <param name="filePath">The path of the file to write.</param>
    /// <param name="content">The TypeScript source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is null or empty.</exception>
    public static void WriteTsFile(
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

        File.WriteAllText(filePath, content.NormalizeForSourceOutput(), Utf8NoBom);
    }
}