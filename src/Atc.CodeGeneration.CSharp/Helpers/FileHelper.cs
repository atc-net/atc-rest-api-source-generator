namespace Atc.CodeGeneration.CSharp.Helpers;

/// <summary>
/// Helper methods for file operations in code generation.
/// </summary>
public static class FileHelper
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes a C# file with trailing whitespace removed.
    /// </summary>
    /// <param name="filePath">The file path to write to.</param>
    /// <param name="content">The content to write.</param>
    public static void WriteCsFile(
        string filePath,
        string content)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            filePath,
            content.NormalizeForSourceOutput(),
            Utf8NoBom);
    }
}