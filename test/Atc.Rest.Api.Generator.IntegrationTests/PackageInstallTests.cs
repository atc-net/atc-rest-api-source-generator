namespace Atc.Rest.Api.Generator.IntegrationTests;

/// <summary>
/// The generator as a consumer gets it: packed, restored from a feed and loaded by the compiler - not the project
/// reference every sample and test uses. A dependency left out of <c>analyzers/dotnet/cs</c> shows only here.
/// </summary>
/// <remarks>
/// The consumer is strict: <c>ImplicitUsings</c> off, nullable on, warnings as errors. A client only - server code
/// needs Atc.Rest.MinimalApi 3.0, which is not on nuget.org yet.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class PackageInstallTests
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(10);

    [Fact]
    public async Task APackedGenerator_GeneratesAClientThatCompiles_InAStrictConsumer()
    {
        var repositoryRoot = FindRepositoryRoot();
        var work = Directory.CreateTempSubdirectory("atc-package-install-");
        try
        {
            // A version of its own, so no cache serves an older package under it
            var version = $"0.0.0-local.{Guid.NewGuid():N}";
            var feed = Directory.CreateDirectory(Path.Combine(work.FullName, "feed")).FullName;
            var consumer = Directory.CreateDirectory(Path.Combine(work.FullName, "consumer")).FullName;

            var pack = await RunDotnetAsync(
                repositoryRoot,
                "pack",
                Path.Combine(repositoryRoot, "src", "Atc.Rest.Api.SourceGenerator", "Atc.Rest.Api.SourceGenerator.csproj"),
                "-c",
                "Release",
                "-o",
                feed,
                $"-p:Version={version}",
                "-p:UseSharedCompilation=false");
            Assert.True(pack.ExitCode == 0, $"dotnet pack failed:\n{pack.Output}");

            WriteConsumer(repositoryRoot, consumer, feed, Path.Combine(work.FullName, "packages"), version);

            var build = await RunDotnetAsync(consumer, "build", "-p:UseSharedCompilation=false", "-p:EmitCompilerGeneratedFiles=true");
            Assert.True(build.ExitCode == 0, $"The consumer did not build:\n{build.Output}");

            // Validation ran from the package too: the sample specification's style warnings are reported
            Assert.Contains("ATC_API_SCH003", build.Output, StringComparison.Ordinal);

            // The generator ran from the package: its files are there, and the consumer's own code used them
            var generated = Directory.GetFiles(Path.Combine(consumer, "obj"), "*.g.cs", SearchOption.AllDirectories);
            Assert.Contains(generated, f => f.Contains("Atc.Rest.Api.SourceGenerator", StringComparison.Ordinal));
        }
        finally
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
        }
    }

    private static void WriteConsumer(
        string repositoryRoot,
        string consumer,
        string feed,
        string packages,
        string version)
    {
        File.Copy(
            Path.Combine(repositoryRoot, "sample", "PetStoreSimple", "PetStoreSimple.yaml"),
            Path.Combine(consumer, "PetStoreSimple.yaml"));
        File.WriteAllText(
            Path.Combine(consumer, ".atc-rest-api-client"),
            """{ "generationMode": "EndpointPerOperation", "httpClientName": "PetStoreSimple-ApiClient" }""");

        File.WriteAllText(
            Path.Combine(consumer, "Consumer.csproj"),
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 <TargetFramework>net10.0</TargetFramework>
                 <ImplicitUsings>disable</ImplicitUsings>
                 <Nullable>enable</Nullable>
                 <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                 <!-- The sample specification's style warnings: reported, not the subject here -->
                 <WarningsNotAsErrors>ATC_API_SCH001;ATC_API_SCH003;ATC_API_OPR003;ATC_API_OPR014</WarningsNotAsErrors>
               </PropertyGroup>
               <ItemGroup>
                 <PackageReference Include="Atc.Rest.Api.SourceGenerator" Version="{version}" />
                 <PackageReference Include="Atc.Rest.Client" Version="2.1.0" />
                 <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.12" />
               </ItemGroup>
               <ItemGroup>
                 <AdditionalFiles Include="PetStoreSimple.yaml" />
                 <AdditionalFiles Include=".atc-rest-api-client" />
               </ItemGroup>
             </Project>
             """);

        // The consumer's own code uses the generated client, so the build fails if nothing was generated
        File.WriteAllText(
            Path.Combine(consumer, "UsesTheClient.cs"),
            """
            namespace Consumer;

            public static class UsesTheClient
            {
                public static System.Type Model => typeof(PetStoreSimple.Generated.Models.Pet);
            }
            """);

        // Its own feed first; the rest from nuget.org, read from the machine's package folder where it is there already
        var machinePackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ??
                              Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        File.WriteAllText(
            Path.Combine(consumer, "nuget.config"),
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <configuration>
               <config>
                 <add key="globalPackagesFolder" value="{packages}" />
               </config>
               <fallbackPackageFolders>
                 <add key="machine" value="{machinePackages}" />
               </fallbackPackageFolders>
               <packageSources>
                 <clear />
                 <add key="local" value="{feed}" />
                 <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
               </packageSources>
             </configuration>
             """);
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

    private static async Task<(int ExitCode, string Output)> RunDotnetAsync(
        string workingDirectory,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
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

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("dotnet did not start.");
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
}