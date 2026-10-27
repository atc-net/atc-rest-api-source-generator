namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Writes the files the domain generator keeps in the project (handler scaffolds, their signatures, GlobalUsings.cs).
/// The generator runs in several hosts at once - the IDE on a save, a build, a build per target framework - so a file is
/// rewritten through a temporary file next to it that is moved into place: a host never reads half a file, and a
/// scaffold another host has just written is never overwritten.
/// A new scaffold is created in place instead, so exactly one host creates it and the others find it there.
/// </summary>
[SuppressMessage("", "RS1035:The symbol 'File' is banned for use by analyzers: Do not do file IO in analyzers", Justification = "The scaffolds are the project's own files; see the class remarks.")]
internal static class ScaffoldFileWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Creates a C# file that is not there yet (trailing whitespace removed, UTF-8 without a BOM).
    /// </summary>
    /// <param name="filePath">The file to create.</param>
    /// <param name="content">The file's content.</param>
    /// <returns>True when this call created the file; false when it was there already (written by another host meanwhile).</returns>
    public static bool TryCreate(
        string filePath,
        string content)
    {
        if (File.Exists(filePath))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Creating a file that must not be there is one atomic step on every OS, so exactly one host wins. A move is
        // not: on Linux it checks for the file and then renames, so two hosts could both "create" it, the last one winning
        using var stream = OpenNew(filePath);
        if (stream is null)
        {
            return false;
        }

        var bytes = Utf8NoBom.GetBytes(content.NormalizeForSourceOutput());
        stream.Write(bytes, 0, bytes.Length);
        return true;
    }

    /// <summary>
    /// Writes a C# file, replacing its content when it is there (trailing whitespace removed, UTF-8 without a BOM).
    /// </summary>
    /// <param name="filePath">The file to write.</param>
    /// <param name="content">The file's content.</param>
    public static void Write(
        string filePath,
        string content)
    {
        // A file that already says it is left alone: no write for an editor or another host to collide with
        var normalized = content.NormalizeForSourceOutput();
        if (File.Exists(filePath) && string.Equals(File.ReadAllText(filePath), normalized, StringComparison.Ordinal))
        {
            return;
        }

        Replace(filePath, normalized, Utf8NoBom);
    }

    /// <summary>
    /// Replaces a file's content as given, in the encoding given (the file as it was read, with or without a BOM).
    /// </summary>
    /// <param name="filePath">The file to write.</param>
    /// <param name="content">The file's content.</param>
    /// <param name="encoding">The encoding to write it in.</param>
    public static void Replace(
        string filePath,
        string content,
        Encoding encoding)
    {
        var temporaryPath = WriteTemporary(filePath, content, encoding);
        try
        {
            if (File.Exists(filePath))
            {
                File.Replace(temporaryPath, filePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, filePath);
            }
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or IOException)
        {
            // A file system without an atomic replace, or a file another process has open (an editor, the compiler):
            // copied over instead, as a plain write would
            File.Copy(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            DeleteIfThere(temporaryPath);
        }
    }

    // The new file opened for writing, or null when it is there already (created by another host meanwhile)
    private static FileStream? OpenNew(string filePath)
    {
        try
        {
            return new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (IOException) when (File.Exists(filePath))
        {
            return null;
        }
    }

    // Next to the file (one volume, so the move is a rename), under a name no other host uses
    private static string WriteTemporary(
        string filePath,
        string content,
        Encoding encoding)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporaryPath, content, encoding);
        return temporaryPath;
    }

    private static void DeleteIfThere(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A temporary file left behind is harmless
        }
        catch (UnauthorizedAccessException)
        {
            // A temporary file left behind is harmless
        }
    }
}