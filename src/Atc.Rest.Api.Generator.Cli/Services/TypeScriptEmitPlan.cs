namespace Atc.Rest.Api.Generator.Cli.Services;

/// <summary>
/// The files one TypeScript client generation produces, decided before anything is written: each by its path in the
/// output folder. A path added again replaces the content added before.
/// </summary>
public sealed class TypeScriptEmitPlan
{
    private readonly List<TypeScriptEmittedFile> files = [];
    private readonly Dictionary<string, int> indexByPath = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the files, in the order they were first added.
    /// </summary>
    public IReadOnlyList<TypeScriptEmittedFile> Files => files;

    /// <summary>
    /// Adds a generated file: rewritten by every run, removed by the run that no longer produces it.
    /// </summary>
    /// <param name="relativePath">The path in the output folder, with <c>/</c> between folders (<c>models/Pet.ts</c>).</param>
    /// <param name="content">The content.</param>
    public void Add(
        string relativePath,
        string content)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        ArgumentNullException.ThrowIfNull(content);

        Put(new TypeScriptEmittedFile(relativePath, content, IsScaffold: false));
    }

    /// <summary>
    /// Adds a scaffold file (<c>package.json</c>, a <c>tsconfig</c>, <c>README.md</c>): written once, then the user's.
    /// </summary>
    /// <param name="relativePath">The path in the output folder, with <c>/</c> between folders.</param>
    /// <param name="content">The content.</param>
    public void AddScaffold(
        string relativePath,
        string content)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        ArgumentNullException.ThrowIfNull(content);

        Put(new TypeScriptEmittedFile(relativePath, content, IsScaffold: true));
    }

    /// <summary>
    /// Whether the plan has a file at the path.
    /// </summary>
    /// <param name="relativePath">The path in the output folder, with <c>/</c> between folders.</param>
    /// <returns><see langword="true"/> when a file was added at the path.</returns>
    public bool Contains(string relativePath)
        => indexByPath.ContainsKey(relativePath);

    private void Put(TypeScriptEmittedFile file)
    {
        if (indexByPath.TryGetValue(file.RelativePath, out var index))
        {
            files[index] = file;
            return;
        }

        indexByPath[file.RelativePath] = files.Count;
        files.Add(file);
    }
}