namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// The file writes of the commands that write into a project, recorded in the open <see cref="FileTransaction"/> (if any)
/// before they are made, so a command that fails half-way puts them back.
/// </summary>
public static class TrackedFile
{
    /// <summary>
    /// Writes a text file (UTF-8, as <see cref="File.WriteAllText(string, string?)"/>).
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="contents">Its content.</param>
    public static void WriteAllText(
        string path,
        string contents)
    {
        FileTransaction.Current?.TrackFile(path);
        File.WriteAllText(path, contents);
    }

    /// <summary>
    /// Copies a file (as <see cref="File.Copy(string, string, bool)"/>).
    /// </summary>
    /// <param name="sourceFileName">The file to copy.</param>
    /// <param name="destFileName">The copy.</param>
    /// <param name="overwrite">Whether a file that is there is overwritten.</param>
    public static void Copy(
        string sourceFileName,
        string destFileName,
        bool overwrite = false)
    {
        FileTransaction.Current?.TrackFile(destFileName);
        File.Copy(sourceFileName, destFileName, overwrite);
    }

    /// <summary>
    /// Creates a folder and the folders above it that are not there (as <see cref="Directory.CreateDirectory(string)"/>).
    /// </summary>
    /// <param name="path">The folder.</param>
    /// <returns>The information of the folder.</returns>
    public static DirectoryInfo CreateDirectory(string path)
    {
        FileTransaction.Current?.TrackDirectory(path);
        return Directory.CreateDirectory(path);
    }
}