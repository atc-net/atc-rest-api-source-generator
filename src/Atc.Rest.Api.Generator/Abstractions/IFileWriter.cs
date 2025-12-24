namespace Atc.Rest.Api.Generator.Abstractions;

/// <summary>
/// Abstraction for writing generated files.
/// Implemented by:
/// - RoslynFileWriter (source generator - uses context.AddSource, no physical files)
/// - DiskFileWriter (CLI tool - writes to disk via File.WriteAllText)
/// </summary>
public interface IFileWriter
{
    /// <summary>
    /// Writes content to a file.
    /// </summary>
    /// <param name="path">The file path to write to.</param>
    /// <param name="content">The content to write.</param>
    /// <param name="overwrite">Whether to overwrite existing files. Default is true.</param>
    void WriteFile(
        string path,
        string content,
        bool overwrite = true);

    /// <summary>
    /// Ensures a directory exists, creating it if necessary.
    /// </summary>
    /// <param name="path">The directory path to ensure exists.</param>
    void EnsureDirectory(string path);

    /// <summary>
    /// Checks if a file exists at the specified path.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <returns>True if the file exists, false otherwise.</returns>
    bool FileExists(string path);
}