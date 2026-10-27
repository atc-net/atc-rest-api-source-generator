namespace Atc.Rest.Api.Generator.Cli.Tests.Commands;

/// <summary>
/// A dry run of <c>migrate execute</c> must not write to the consumer's files.
/// </summary>
public sealed class MigrateExecuteDryRunTests : IDisposable
{
    private const string UnsortedGlobalUsings = """
        global using Zebra.Things;
        global using System.Text;
        global using Demo.Domain;
        global using System;
        """;

    private readonly string root = Path.Combine(Path.GetTempPath(), "atc-migrate-dryrun-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void UpdateGlobalUsings_DryRun_LeavesDomainAndTestGlobalUsingsUntouched()
    {
        var domainFile = CreateProject("Demo.Domain");
        var testFile = CreateProject("Demo.Domain.Tests");
        var report = new MigrationValidationReport
        {
            DetectedProjectName = "Demo",
            ProjectStructure =
            {
                DomainProject = Path.Combine(Path.GetDirectoryName(domainFile)!, "Demo.Domain.csproj"),
                DomainTestProject = Path.Combine(Path.GetDirectoryName(testFile)!, "Demo.Domain.Tests.csproj"),
            },
        };

        MigrateExecuteCommand.UpdateGlobalUsings(report, "Demo", dryRun: true, new MigrationSummary());

        Assert.Equal(UnsortedGlobalUsings, File.ReadAllText(domainFile));
        Assert.Equal(UnsortedGlobalUsings, File.ReadAllText(testFile));
    }

    [Fact]
    public void UpdateHostProject_DryRun_LeavesHostGlobalUsingsUntouched()
    {
        var hostFile = CreateProject("Demo.Api");
        var report = new MigrationValidationReport
        {
            DetectedProjectName = "Demo",
            ProjectStructure =
            {
                HostApiProject = Path.Combine(Path.GetDirectoryName(hostFile)!, "Demo.Api.csproj"),
            },
        };

        MigrateExecuteCommand.UpdateHostProject(report, "Demo", dryRun: true, new MigrationSummary());

        Assert.Equal(UnsortedGlobalUsings, File.ReadAllText(hostFile));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private string CreateProject(string name)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        var globalUsings = Path.Combine(directory, "GlobalUsings.cs");
        File.WriteAllText(globalUsings, UnsortedGlobalUsings);
        return globalUsings;
    }
}