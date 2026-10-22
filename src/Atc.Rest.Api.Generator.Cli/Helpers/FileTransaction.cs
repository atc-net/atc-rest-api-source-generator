namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// The files a command writes into a project (<c>generate server</c>, <c>generate client</c>, <c>spec split</c>), kept so
/// that a command that fails half-way leaves the project as it found it: each file is recorded before it is first
/// written - its content, or that it was not there - and each folder the command creates; disposed without
/// <see cref="Commit"/>, the files are put back, the new ones deleted, and the new folders removed. Reads see the files as
/// written, so a command reads back what it wrote. What an external tool writes (<c>dotnet</c>) is not tracked.
/// </summary>
public sealed class FileTransaction : IDisposable
{
    private static readonly AsyncLocal<FileTransaction?> Ambient = new();

    private readonly FileTransaction? previous;
    private readonly Dictionary<string, byte[]?> originals = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> createdDirectories = [];
    private bool committed;

    private FileTransaction(FileTransaction? previous)
        => this.previous = previous;

    /// <summary>
    /// Gets the transaction the writes on this flow go into, or null when none is open.
    /// </summary>
    public static FileTransaction? Current => Ambient.Value;

    /// <summary>
    /// Gets a value indicating whether the transaction has recorded a write.
    /// </summary>
    public bool HasChanges
        => originals.Count > 0 || createdDirectories.Count > 0;

    /// <summary>
    /// Opens a transaction for the writes on this flow until it is disposed.
    /// </summary>
    /// <returns>The transaction.</returns>
    public static FileTransaction Begin()
    {
        var transaction = new FileTransaction(Ambient.Value);
        Ambient.Value = transaction;
        return transaction;
    }

    /// <summary>
    /// Runs a command in a transaction: what it writes is kept when it returns 0, and put back - saying so - when it
    /// fails.
    /// </summary>
    /// <param name="command">The command; 0 is success.</param>
    /// <returns>The command's result.</returns>
    public static int Run(Func<int> command)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var transaction = Begin();
        var result = command();
        if (result == 0)
        {
            transaction.Commit();
        }
        else if (transaction.HasChanges)
        {
            AnsiConsole.MarkupLine("[yellow]The files written before the failure were put back.[/]");
        }

        return result;
    }

    /// <summary>
    /// Keeps what was written: disposing no longer puts it back.
    /// </summary>
    public void Commit()
        => committed = true;

    /// <summary>
    /// Puts back what was written, unless committed, and closes the transaction.
    /// </summary>
    public void Dispose()
    {
        Ambient.Value = previous;
        if (!committed)
        {
            Rollback();
        }
    }

    internal void TrackFile(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!originals.ContainsKey(fullPath))
        {
            originals[fullPath] = File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
        }
    }

    internal void TrackDirectory(string path)
    {
        // The outermost folder that is not there yet: removing it removes the rest
        var fullPath = Path.GetFullPath(path);
        string? outermost = null;
        for (var directory = fullPath; !string.IsNullOrEmpty(directory) && !Directory.Exists(directory); directory = Path.GetDirectoryName(directory))
        {
            outermost = directory;
        }

        if (outermost is not null)
        {
            createdDirectories.Add(outermost);
        }
    }

    private void Rollback()
    {
        foreach (var (path, content) in originals)
        {
            try
            {
                if (content is null)
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                else
                {
                    File.WriteAllBytes(path, content);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Put back what can be: one file that cannot be is no reason to leave the others
            }
        }

        foreach (var directory in Enumerable.Reverse(createdDirectories))
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A folder that cannot be removed is left
            }
        }
    }
}