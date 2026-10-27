namespace Atc.Rest.Api.Generator.IntegrationTests;

/// <summary>
/// The generator packed once, with a version of its own, into a local feed the consumers of
/// <see cref="PackageInstallTests"/> restore from.
/// </summary>
public sealed class PackedGeneratorFixture : IAsyncLifetime
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(10);

    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("atc-package-install-");

    public string RepositoryRoot { get; } = FindRepositoryRoot();

    // A version of its own, so no cache serves an older package under it
    public string Version { get; } = $"0.0.0-local.{Guid.NewGuid():N}";

    public string Feed => Path.Combine(work.FullName, "feed");

    public string Packages => Path.Combine(work.FullName, "packages");

    public string NewDirectory(string name)
        => Directory.CreateDirectory(Path.Combine(work.FullName, name)).FullName;

    public async ValueTask InitializeAsync()
    {
        var pack = await RunDotnetAsync(
            RepositoryRoot,
            "pack",
            Path.Combine(RepositoryRoot, "src", "Atc.Rest.Api.SourceGenerator", "Atc.Rest.Api.SourceGenerator.csproj"),
            "-c",
            "Release",
            "-o",
            Feed,
            $"-p:Version={Version}",
            "-p:UseSharedCompilation=false");
        Assert.True(pack.ExitCode == 0, $"dotnet pack failed:\n{pack.Output}");
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            work.Delete(recursive: true);
        }
        catch (IOException)
        {
            // A build server can still hold a file; the temp folder is the OS's to clean then
        }
        catch (UnauthorizedAccessException)
        {
            // As above
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// A nuget.config for a consumer: the local feed first; the rest from nuget.org, read from the machine's package
    /// folder where it is there already.
    /// </summary>
    public void WriteNuGetConfig(string directory)
    {
        var machinePackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ??
                              Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        File.WriteAllText(
            Path.Combine(directory, "nuget.config"),
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <configuration>
               <config>
                 <add key="globalPackagesFolder" value="{Packages}" />
               </config>
               <fallbackPackageFolders>
                 <add key="machine" value="{machinePackages}" />
               </fallbackPackageFolders>
               <packageSources>
                 <clear />
                 <add key="local" value="{Feed}" />
                 <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
               </packageSources>
             </configuration>
             """);
    }

    public static Task<(int ExitCode, string Output)> RunDotnetAsync(
        string workingDirectory,
        params string[] arguments)
        => RunAsync("dotnet", workingDirectory, arguments);

    public static async Task<(int ExitCode, string Output)> RunAsync(
        string fileName,
        string workingDirectory,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"{fileName} did not start.");
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(CommandTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return (process.ExitCode, await output + await error);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Atc.Rest.Api.SourceGenerator.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}