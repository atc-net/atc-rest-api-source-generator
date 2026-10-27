namespace Atc.Rest.Api.Generator.Cli.Tests.Services.Migration;

public sealed class ProjectRenamerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atc-renamer-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void RenameProject_TargetExists_KeepsAnOldFolderThatStillHoldsFiles()
    {
        var oldDirectory = CreateProject("Demo.Api.Generated", "Extensions/MyExtensions.cs");
        CreateProject("Demo.Api.Contracts");

        var result = ProjectRenamer.RenameProject(oldDirectory, "Demo.Api.Generated", "Demo.Api.Contracts");

        Assert.True(File.Exists(Path.Combine(oldDirectory, "Extensions", "MyExtensions.cs")));
        Assert.False(result.Success);
        Assert.False(result.OldDirectoryDeleted);
        Assert.Contains("Demo.Api.Generated", result.Error, StringComparison.Ordinal);
        Assert.Contains("Demo.Api.Contracts", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void RenameProject_TargetExists_RemovesAnOldFolderThatHoldsOnlyBuildOutput()
    {
        var oldDirectory = CreateProject("Demo.Api.Generated", "bin/Debug/Demo.Api.Generated.dll", "obj/project.assets.json");
        CreateProject("Demo.Api.Contracts");

        var result = ProjectRenamer.RenameProject(oldDirectory, "Demo.Api.Generated", "Demo.Api.Contracts");

        Assert.False(Directory.Exists(oldDirectory));
        Assert.True(result.Success);
        Assert.True(result.OldDirectoryDeleted);
    }

    [Fact]
    public void RenameProject_TargetDoesNotExist_MovesTheFolderWithItsFiles()
    {
        var oldDirectory = CreateProject("Demo.Api.Generated", "Demo.Api.Generated.csproj", "Extensions/MyExtensions.cs");

        var result = ProjectRenamer.RenameProject(oldDirectory, "Demo.Api.Generated", "Demo.Api.Contracts");

        Assert.True(result.Success, result.Error);
        Assert.False(Directory.Exists(oldDirectory));
        Assert.True(File.Exists(Path.Combine(root, "Demo.Api.Contracts", "Demo.Api.Contracts.csproj")));
        Assert.True(File.Exists(Path.Combine(root, "Demo.Api.Contracts", "Extensions", "MyExtensions.cs")));
    }

    /// <summary>
    /// A file in the project that something still has open (a virus scanner, an indexer, an editor) blocks the move of
    /// its folder on Windows for a moment: the rename waits for it instead of failing.
    /// </summary>
    [Fact]
    public void RenameProject_AFileIsHeldOpenForAMoment_WaitsAndMovesTheFolder()
    {
        var oldDirectory = CreateProject("Demo.Api.Generated", "Demo.Api.Generated.csproj", "Extensions/MyExtensions.cs");
        using var held = new FileStream(Path.Combine(oldDirectory, "Extensions", "MyExtensions.cs"), FileMode.Open, FileAccess.Read, FileShare.Read);
        var waits = 0;

        // The file is let go while the rename waits: no clock in the test
        var result = ProjectRenamer.RenameProject(
            oldDirectory,
            "Demo.Api.Generated",
            "Demo.Api.Contracts",
            wait: _ =>
            {
                waits++;
                held.Dispose();
            });

        Assert.Equal(OperatingSystem.IsWindows() ? 1 : 0, waits);

        Assert.True(result.Success, result.Error);
        Assert.False(Directory.Exists(oldDirectory));
        Assert.True(File.Exists(Path.Combine(root, "Demo.Api.Contracts", "Demo.Api.Contracts.csproj")));
    }

    /// <summary>
    /// A folder that cannot be moved is an error the caller reports, not an exception - and the project is left as it
    /// was, its project file under its old name, not half renamed. (Only Windows refuses to move a folder with an open
    /// file in it.)
    /// </summary>
    [Fact]
    public void RenameProject_AFileStaysHeldOpen_ReportsTheErrorAndLeavesTheProjectAsItWas()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var oldDirectory = CreateProject("Demo.Api.Generated", "Demo.Api.Generated.csproj", "Extensions/MyExtensions.cs");
        RenameResult result;
        using (new FileStream(Path.Combine(oldDirectory, "Extensions", "MyExtensions.cs"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            result = ProjectRenamer.RenameProject(oldDirectory, "Demo.Api.Generated", "Demo.Api.Contracts", wait: _ => { });
        }

        Assert.False(result.Success);
        Assert.Contains("Demo.Api.Generated", result.Error, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(oldDirectory, "Demo.Api.Generated.csproj")));
        Assert.False(File.Exists(Path.Combine(oldDirectory, "Demo.Api.Contracts.csproj")));
        Assert.False(Directory.Exists(Path.Combine(root, "Demo.Api.Contracts")));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private string CreateProject(
        string name,
        params string[] files)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        foreach (var file in files)
        {
            var path = Path.Combine(directory, file.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "content");
        }

        return directory;
    }
}