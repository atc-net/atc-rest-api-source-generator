namespace Atc.Rest.Api.Generator.Cli.Tests.Services;

/// <summary>
/// The projects generate server scaffolds, planned without touching the disk.
/// </summary>
public sealed class ServerScaffoldPlanTests
{
    private static readonly string Root = Path.GetFullPath("contoso-repo");

    [Theory]
    [InlineData(ProjectStructureType.SingleProject, "Contoso.Api", "Contoso.Api", "Contoso.Api", "src/Contoso.Api/Contoso.Api.csproj")]
    [InlineData(ProjectStructureType.TwoProjects, "Contoso.Api", "Contoso.Domain", "Contoso.Domain", "src/Contoso.Api/Contoso.Api.csproj,src/Contoso.Domain/Contoso.Domain.csproj")]
    [InlineData(ProjectStructureType.ThreeProjects, "Contoso.Api", "Contoso.Api.Contracts", "Contoso.Domain", "src/Contoso.Api/Contoso.Api.csproj,src/Contoso.Api.Contracts/Contoso.Api.Contracts.csproj,src/Contoso.Domain/Contoso.Domain.csproj")]
    public void Create_NamesTheProjectsByTheStructure_AndListsThemInOrder(
        ProjectStructureType structure,
        string host,
        string contracts,
        string domain,
        string sourceProjects)
    {
        var plan = ServerScaffoldPlan.Create("Contoso", Root, domainOutputPath: null, new ScaffoldingOptions { ProjectStructure = structure });

        Assert.Equal(host, plan.HostProjectName);
        Assert.Equal(contracts, plan.ContractsProjectName);
        Assert.Equal(domain, plan.DomainProjectName);
        Assert.Equal(sourceProjects, string.Join(",", plan.SourceProjects));
        Assert.Equal(Path.Combine(Root, "src", contracts), plan.ContractsOutputPath);
        Assert.Equal("Contoso.Tests", plan.TestProjectName);
    }

    /// <summary>
    /// Two projects are the host and one project with the contracts and the handlers: the host references it, and it
    /// references nothing back - the contracts cannot live in the host, which references the handlers.
    /// </summary>
    [Fact]
    public void Create_TwoProjects_PutsTheContractsInTheDomainProject_AlsoInAnotherFolder()
    {
        var domainFolder = Path.GetFullPath("elsewhere");

        var plan = ServerScaffoldPlan.Create("Contoso", Root, domainFolder, new ScaffoldingOptions { ProjectStructure = ProjectStructureType.TwoProjects });

        Assert.Equal(plan.DomainProjectName, plan.ContractsProjectName);
        Assert.Equal(domainFolder, plan.ContractsOutputPath);
        Assert.Equal(domainFolder, plan.DomainOutputPath);
        Assert.Equal("Contoso.Api", plan.ApiProjectName);
    }

    [Fact]
    public void Create_WithAspire_AddsTheAppHostLast_RunningTheHost()
    {
        var plan = ServerScaffoldPlan.Create("Contoso", Root, domainOutputPath: null, new ScaffoldingOptions { ProjectStructure = ProjectStructureType.ThreeProjects, Aspire = true });

        Assert.Equal("Contoso.Aspire", plan.AspireProjectName);
        Assert.Equal("src/Contoso.Aspire/Contoso.Aspire.csproj", plan.SourceProjects[^1]);
        Assert.Equal("Contoso.Api", plan.ApiProjectName);
    }

    [Fact]
    public void Create_ExplicitNamesAndDomainFolder_AreKept()
    {
        var domainFolder = Path.GetFullPath("elsewhere");
        var scaffolding = new ScaffoldingOptions
        {
            ProjectStructure = ProjectStructureType.ThreeProjects,
            HostProjectName = "Contoso.Host",
            ContractsProjectName = "Contoso.Contracts",
            DomainProjectName = "Contoso.Logic",
        };

        var plan = ServerScaffoldPlan.Create("Contoso", Root, domainFolder, scaffolding);

        Assert.Equal("Contoso.Host", plan.HostProjectName);
        Assert.Equal("Contoso.Contracts", plan.ContractsProjectName);
        Assert.Equal("Contoso.Logic", plan.DomainProjectName);
        Assert.Equal(domainFolder, plan.DomainOutputPath);
    }
}