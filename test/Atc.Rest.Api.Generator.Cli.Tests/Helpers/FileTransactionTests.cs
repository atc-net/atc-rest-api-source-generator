namespace Atc.Rest.Api.Generator.Cli.Tests.Helpers;

/// <summary>
/// A command that writes into a project leaves it as it found it when it fails half-way: the files it overwrote are put
/// back, the files and the folders it created are removed. A command that succeeds keeps what it wrote.
/// </summary>
public sealed class FileTransactionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atc-file-transaction-" + Guid.NewGuid().ToString("N"));

    public FileTransactionTests()
        => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Dispose_WithoutCommit_PutsTheProjectBack()
    {
        var existing = Path.Combine(root, "Api.csproj");
        var copied = Path.Combine(root, "Api.yaml");
        var source = Path.Combine(root, "source.yaml");
        File.WriteAllText(existing, "before");
        File.WriteAllText(source, "openapi: 3.0.3");
        var newFolder = Path.Combine(root, "src", "Api.Domain");

        using (FileTransaction.Begin())
        {
            TrackedFile.WriteAllText(existing, "after");
            TrackedFile.WriteAllText(existing, "after again");
            TrackedFile.Copy(source, copied, overwrite: true);
            TrackedFile.CreateDirectory(newFolder);
            TrackedFile.WriteAllText(Path.Combine(newFolder, "Program.cs"), "// new");

            // The command reads back what it wrote
            Assert.Equal("after again", File.ReadAllText(existing));
        }

        Assert.Equal("before", File.ReadAllText(existing));
        Assert.False(File.Exists(copied));
        Assert.False(Directory.Exists(Path.Combine(root, "src")));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void Commit_KeepsWhatWasWritten()
    {
        var file = Path.Combine(root, "nested", "Api.csproj");

        using (var transaction = FileTransaction.Begin())
        {
            TrackedFile.CreateDirectory(Path.GetDirectoryName(file)!);
            TrackedFile.WriteAllText(file, "written");
            Assert.True(transaction.HasChanges);
            transaction.Commit();
        }

        Assert.Equal("written", File.ReadAllText(file));
    }

    [Fact]
    public void WithoutATransaction_WritesAreMadeAsAlways()
    {
        var file = Path.Combine(root, "plain.txt");

        TrackedFile.WriteAllText(file, "plain");

        Assert.Null(FileTransaction.Current);
        Assert.Equal("plain", File.ReadAllText(file));
    }
}