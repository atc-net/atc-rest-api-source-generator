namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// Watches a specification file and its multi-part files (<c>{Base}_{Part}.yaml</c>, <c>.yml</c>, <c>.json</c>) for <c>--watch</c>, and calls
/// back - debounced - when one of them is saved.
/// </summary>
public sealed class SpecificationWatcher : IDisposable
{
    private const int DebounceMilliseconds = 300;

    private readonly FileSystemWatcher watcher;
    private readonly Timer debounceTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpecificationWatcher"/> class.
    /// </summary>
    /// <param name="specPath">The full path of the base specification file.</param>
    /// <param name="onChanged">Called once per burst of saves, on a timer thread.</param>
    public SpecificationWatcher(
        string specPath,
        Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(specPath);
        ArgumentNullException.ThrowIfNull(onChanged);

        debounceTimer = new Timer(_ => onChanged());

        // An editor that saves by writing a temporary file and renaming it over the specification raises Renamed, not
        // Changed - the file name is watched too, and the new name is the one matched against the filters.
        watcher = new FileSystemWatcher(Path.GetDirectoryName(specPath)!, Path.GetFileName(specPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.Size,
        };

        // The parts, with any specification extension, as the build finds them
        foreach (var extension in new[] { ".yaml", ".yml", ".json" })
        {
            watcher.Filters.Add(Path.GetFileNameWithoutExtension(specPath) + "_*" + extension);
        }

        watcher.Changed += (_, _) => Bump();
        watcher.Created += (_, _) => Bump();
        watcher.Renamed += (_, _) => Bump();
        watcher.EnableRaisingEvents = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        watcher.Dispose();
        debounceTimer.Dispose();
    }

    private void Bump()
        => debounceTimer.Change(DebounceMilliseconds, Timeout.Infinite);
}