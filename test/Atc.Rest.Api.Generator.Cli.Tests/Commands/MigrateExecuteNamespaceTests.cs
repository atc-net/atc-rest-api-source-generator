namespace Atc.Rest.Api.Generator.Cli.Tests.Commands;

/// <summary>
/// <c>migrate execute</c> rewrites the old contracts namespaces in every project of the solution (not only the domain
/// and its tests), leaves the user's own namespaces of the old project alone, and adds generated namespaces to a test
/// project only for what its code used.
/// </summary>
public sealed class MigrateExecuteNamespaceTests : IDisposable
{
    private const string Spec = """
        openapi: 3.0.1
        info:
          title: Contoso
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: listPets
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Pet'
          /owners:
            get:
              operationId: listOwners
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Owner'
        components:
          schemas:
            Pet:
              type: object
              properties:
                name:
                  type: string
            Owner:
              type: object
              properties:
                name:
                  type: string
        """;

    private readonly string root = Path.Combine(Path.GetTempPath(), "atc-migrate-ns-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void UpdateContractsReferences_RewritesEveryProject_AndSkipsBuildOutput()
    {
        var common = Write("Contoso.Common/Events/PetAdopted.cs", "namespace Contoso.Common;\npublic sealed record PetAdopted(Contoso.Api.Generated.Contracts.Pets.Pet Pet);\n");
        var functions = Write("Contoso.Functions/GlobalUsings.cs", "global using Contoso.Api.Generated.Contracts.Owners;\n");
        var buildOutput = Write("Contoso.Common/obj/Debug/Stale.cs", "Contoso.Api.Generated.Contracts.Pets.Pet x;\n");
        var report = Report();

        MigrateExecuteCommand.UpdateContractsReferences(report, "Contoso", root, dryRun: false, new MigrationSummary());

        Assert.Contains("Contoso.Api.Contracts.Generated.Pets.Models.Pet Pet", File.ReadAllText(common), StringComparison.Ordinal);
        Assert.Contains("global using Contoso.Api.Contracts.Generated.Owners.Handlers;", File.ReadAllText(functions), StringComparison.Ordinal);
        Assert.Equal("Contoso.Api.Generated.Contracts.Pets.Pet x;\n", File.ReadAllText(buildOutput));
    }

    [Fact]
    public void UpdateContractsReferences_DryRun_WritesNothing()
    {
        const string source = "global using Contoso.Api.Generated.Contracts.Owners;\n";
        var file = Write("Contoso.Functions/GlobalUsings.cs", source);

        MigrateExecuteCommand.UpdateContractsReferences(Report(), "Contoso", root, dryRun: true, new MigrationSummary());

        Assert.Equal(source, File.ReadAllText(file));
    }

    [Fact]
    public void UpdateGlobalUsings_KeepsTheUsersNamespaces_AndMapsTheOldEndpoints()
    {
        var domain = Write("Contoso.Domain/GlobalUsings.cs", "global using Contoso.Api.Generated.Extensions;\nglobal using Contoso.Api.Generated.Endpoints;\n");
        var report = Report();
        report.ProjectStructure.DomainProject = Path.Combine(root, "Contoso.Domain", "Contoso.Domain.csproj");

        MigrateExecuteCommand.UpdateGlobalUsings(report, "Contoso", dryRun: false, new MigrationSummary());

        var content = File.ReadAllText(domain);
        Assert.Contains("global using Contoso.Api.Generated.Extensions;", content, StringComparison.Ordinal);
        Assert.Contains("global using Contoso.Api.Contracts.Generated.Endpoints;", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Contoso.Api.Contracts.Generated.Extensions", content, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateGlobalUsings_TestProject_GetsNoNamespaceItsCodeDidNotUse()
    {
        // Every segment's namespaces in a test project made a type ambiguous with another project's type of the same name
        var tests = Write("Contoso.Domain.Tests/GlobalUsings.cs", "global using Contoso.Api.Contracts.Generated.Pets.Models;\n");
        var report = Report();
        report.ProjectStructure.DomainTestProject = Path.Combine(root, "Contoso.Domain.Tests", "Contoso.Domain.Tests.csproj");

        MigrateExecuteCommand.UpdateGlobalUsings(report, "Contoso", dryRun: false, new MigrationSummary());

        Assert.DoesNotContain("Owners", File.ReadAllText(tests), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private MigrationValidationReport Report()
    {
        var specPath = Path.Combine(root, "Contoso.yaml");
        if (!File.Exists(specPath))
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(specPath, Spec);
        }

        return new MigrationValidationReport
        {
            DetectedProjectName = "Contoso",
            SpecificationPath = specPath,
        };
    }

    private string Write(
        string relativePath,
        string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}