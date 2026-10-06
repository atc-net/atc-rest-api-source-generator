using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Atc.Rest.Api.Tests.Node;

/// <summary>
/// The one npm workspace every TypeScript gate and runtime matrix uses: the packages in
/// <c>test/NodeWorkspace/package.json</c>, installed once per machine with <c>npm ci</c> from the committed
/// lockfile into a folder named after the lockfile's hash, and linked into each test's work directory as its
/// <c>node_modules</c>. No test run asks the registry for the newest version of anything, and twenty tests
/// starting at once share one install instead of racing one npm cache. Linked into both test projects as source.
/// </summary>
internal static class NodeWorkspace
{
    private static readonly Lock Gate = new();
    private static string? sharedNodeModules;

    /// <summary>
    /// The repository folder holding <c>package.json</c> and <c>package-lock.json</c>.
    /// </summary>
    public static string SourceDirectory { get; } = FindSourceDirectory();

    /// <summary>
    /// Gives the work directory the shared packages as its <c>node_modules</c>, installing them first when this
    /// machine has not yet installed this lockfile.
    /// </summary>
    /// <param name="workDirectory">The test's work directory.</param>
    /// <returns>The shared <c>node_modules</c> folder.</returns>
    public static string Link(string workDirectory)
    {
        var shared = EnsureInstalled();
        var target = Path.Combine(workDirectory, "node_modules");
        if (Directory.Exists(target))
        {
            return shared;
        }

        if (OperatingSystem.IsWindows())
        {
            // A junction needs no privilege, a symbolic link does
            var link = Run("cmd.exe", $"/c mklink /J \"{target}\" \"{shared}\"", workDirectory, TimeSpan.FromMinutes(1));
            if (link.ExitCode != 0)
            {
                throw new InvalidOperationException($"Could not link node_modules into {workDirectory}:\n{link.Output}");
            }
        }
        else
        {
            Directory.CreateSymbolicLink(target, shared);
        }

        return shared;
    }

    /// <summary>
    /// Removes the link (never the shared packages), so the work directory can be deleted.
    /// </summary>
    /// <param name="workDirectory">The test's work directory.</param>
    public static void Unlink(string workDirectory)
    {
        var target = Path.Combine(workDirectory, "node_modules");
        if (Directory.Exists(target))
        {
            // Deleting a junction or a symbolic link removes the link only
            Directory.Delete(target);
        }
    }

    /// <summary>
    /// Removes the link and then the work directory. A folder a just-exited node process still holds for a moment
    /// is retried a few times and then left to the OS: a temp folder is harmless, a red test is not.
    /// </summary>
    /// <param name="workDirectory">The test's work directory.</param>
    public static void Delete(string workDirectory)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Unlink(workDirectory);
                if (Directory.Exists(workDirectory))
                {
                    Directory.Delete(workDirectory, recursive: true);
                }

                return;
            }
            catch (IOException)
            {
                Thread.Sleep(500);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(500);
            }
        }
    }

    private static string EnsureInstalled()
    {
        lock (Gate)
        {
            if (sharedNodeModules is not null)
            {
                return sharedNodeModules;
            }
        }

        var lockfile = Path.Combine(SourceDirectory, "package-lock.json");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(lockfile)))[..16].ToLowerInvariant();
        var root = Path.Combine(Path.GetTempPath(), "atc-rest-api-gates", "node-" + hash);
        var nodeModules = Path.Combine(root, "node_modules");
        var stamp = Path.Combine(root, ".installed");
        Directory.CreateDirectory(root);

        // One install per machine, whichever process gets there first; the others wait for it
        using (AcquireLock(Path.Combine(root, ".lock")))
        {
            if (!File.Exists(stamp))
            {
                File.Copy(Path.Combine(SourceDirectory, "package.json"), Path.Combine(root, "package.json"), overwrite: true);
                File.Copy(lockfile, Path.Combine(root, "package-lock.json"), overwrite: true);
                var install = Run("npm", "ci --no-audit --no-fund --loglevel=error", root, TimeSpan.FromMinutes(10));
                if (install.ExitCode != 0)
                {
                    throw new InvalidOperationException($"npm ci of the shared test workspace failed (exit {install.ExitCode}):\n{install.Output}");
                }

                File.WriteAllText(stamp, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            }
        }

        lock (Gate)
        {
            sharedNodeModules = nodeModules;
        }

        return nodeModules;
    }

    private static FileStream AcquireLock(string path)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(15);
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(500);
            }
        }
    }

    private static string FindSourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "test", "NodeWorkspace", "package-lock.json");
            if (File.Exists(candidate))
            {
                return Path.GetDirectoryName(candidate)!;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("test/NodeWorkspace/package-lock.json was not found above " + AppContext.BaseDirectory);
    }

    private static (int ExitCode, string Output) Run(
        string tool,
        string arguments,
        string workingDirectory,
        TimeSpan timeout)
    {
        // npm is a .cmd shim on Windows: route through cmd.exe so PATH and PATHEXT apply
        var (fileName, fullArguments) = OperatingSystem.IsWindows() && !tool.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? ("cmd.exe", tool == "cmd.exe" ? arguments : "/c " + tool + " " + arguments)
            : (tool, arguments);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = fullArguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        var output = new StringBuilder();
        var sync = new Lock();
        process.OutputDataReceived += (_, e) => Append(e.Data);
        process.ErrorDataReceived += (_, e) => Append(e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited
            }

            return (-1, $"Timed out after {timeout.TotalSeconds:N0}s running: {tool} {arguments}");
        }

        process.WaitForExit();
        lock (sync)
        {
            return (process.ExitCode, output.ToString());
        }

        void Append(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (sync)
            {
                output.AppendLine(line);
            }
        }
    }
}