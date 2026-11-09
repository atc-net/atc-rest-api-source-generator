namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A project folder of a test's own, one level below a folder of its own in the temp folder.
/// <para>
/// The handler generator reads the server namespace from a marker in a sibling folder. A folder straight in the temp
/// folder has every other test's folder as a sibling - one with a server marker of its own changes the namespace.
/// Here the only sibling folders are this test's.
/// </para>
/// </summary>
internal sealed class IsolatedProjectFolder : IDisposable
{
    private readonly string root;

    public IsolatedProjectFolder(string prefix)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
        Path = System.IO.Path.Combine(root, "Project");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder left behind is harmless, a red test is not
        }
        catch (UnauthorizedAccessException)
        {
            // As above
        }
    }
}