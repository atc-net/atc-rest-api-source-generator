namespace Atc.Rest.Api.Generator.Cli.Services;

/// <summary>
/// The projects <c>generate server</c> scaffolds, decided before anything is written: their names and folders by the
/// project structure, and the solution's projects in order.
/// </summary>
/// <param name="Structure">The project structure.</param>
/// <param name="BaseName">The solution's name, from the project name.</param>
/// <param name="RepositoryRoot">The repository root.</param>
/// <param name="SourcePath">The repository's <c>src</c> folder.</param>
/// <param name="HostProjectName">The API host project (the contracts project itself for a single project).</param>
/// <param name="ContractsProjectName">The contracts project (the domain project itself for two projects).</param>
/// <param name="DomainProjectName">The domain project (the contracts project itself for a single project).</param>
/// <param name="ContractsOutputPath">The contracts project's folder.</param>
/// <param name="DomainOutputPath">The domain project's folder.</param>
/// <param name="AspireProjectName">The Aspire AppHost project, or <see langword="null"/> without one.</param>
/// <param name="TestProjectName">The test project.</param>
public sealed record ServerScaffoldPlan(
    ProjectStructureType Structure,
    string BaseName,
    string RepositoryRoot,
    string SourcePath,
    string HostProjectName,
    string ContractsProjectName,
    string DomainProjectName,
    string ContractsOutputPath,
    string DomainOutputPath,
    string? AspireProjectName,
    string TestProjectName)
{
    /// <summary>
    /// Gets a value indicating whether the server is the host and one project with the contracts and the handlers.
    /// </summary>
    public bool IsTwoProjects
        => Structure == ProjectStructureType.TwoProjects;

    /// <summary>
    /// Gets a value indicating whether the host, the contracts and the domain are three projects.
    /// </summary>
    public bool IsThreeProjects
        => Structure == ProjectStructureType.ThreeProjects;

    /// <summary>
    /// Gets a value indicating whether the domain is a project of its own.
    /// </summary>
    public bool HasSeparateDomain => IsTwoProjects || IsThreeProjects;

    /// <summary>
    /// Gets the project the Aspire AppHost runs: the host.
    /// </summary>
    public string ApiProjectName
        => HasSeparateDomain ? HostProjectName : ContractsProjectName;

    /// <summary>
    /// Gets the solution's source projects, in order: the host, the contracts, the domain, then the Aspire AppHost.
    /// </summary>
    public IReadOnlyList<string> SourceProjects
        => [.. (IsThreeProjects
                   ? new[] { HostProjectName, ContractsProjectName, DomainProjectName }
                   : IsTwoProjects
                       ? new[] { HostProjectName, DomainProjectName }
                       : new[] { ContractsProjectName })
               .Concat(AspireProjectName is null ? [] : [AspireProjectName])
               .Select(name => $"src/{name}/{name}.csproj")];

    /// <summary>
    /// Gets the solution's test projects.
    /// </summary>
    public IReadOnlyList<string> TestProjects
        => [$"test/{TestProjectName}/{TestProjectName}.csproj"];

    /// <summary>
    /// Plans the projects of a server.
    /// </summary>
    /// <param name="projectName">The project name the command was given.</param>
    /// <param name="repositoryRoot">The repository root (full path).</param>
    /// <param name="domainOutputPath">The <c>--domain-output</c> folder, or <see langword="null"/> for the default.</param>
    /// <param name="scaffolding">The scaffolding options, with the command's overrides.</param>
    /// <returns>The plan.</returns>
    public static ServerScaffoldPlan Create(
        string projectName,
        string repositoryRoot,
        string? domainOutputPath,
        ScaffoldingOptions scaffolding)
    {
        ArgumentNullException.ThrowIfNull(scaffolding);

        var baseName = ProjectNameHelper.ExtractSolutionName(projectName);
        var sourcePath = Path.Combine(repositoryRoot, "src");

        // SingleProject: {Name}.Api (all-in-one); TwoProjects: {Name}.Api (host), {Name}.Domain (contracts + handlers);
        // ThreeProjects: {Name}.Api (host), {Name}.Api.Contracts, {Name}.Domain. Two projects keep the contracts in the
        // domain project: in the host they would form a cycle (the host references the handlers' project, which
        // needs the contracts)
        var (contracts, host, domain) = scaffolding.ProjectStructure switch
        {
            ProjectStructureType.ThreeProjects => (
                scaffolding.ContractsProjectName ?? $"{baseName}.Api.Contracts",
                scaffolding.HostProjectName ?? $"{baseName}.Api",
                scaffolding.DomainProjectName ?? $"{baseName}.Domain"),
            ProjectStructureType.TwoProjects => (
                scaffolding.DomainProjectName ?? $"{baseName}.Domain",
                scaffolding.HostProjectName ?? $"{baseName}.Api",
                scaffolding.DomainProjectName ?? $"{baseName}.Domain"),
            _ => (
                scaffolding.ContractsProjectName ?? $"{baseName}.Api",
                scaffolding.ContractsProjectName ?? $"{baseName}.Api",
                scaffolding.ContractsProjectName ?? $"{baseName}.Api"),
        };

        var domainPath = string.IsNullOrWhiteSpace(domainOutputPath) ? Path.Combine(sourcePath, domain) : Path.GetFullPath(domainOutputPath);
        return new ServerScaffoldPlan(
            scaffolding.ProjectStructure,
            baseName,
            repositoryRoot,
            sourcePath,
            host,
            contracts,
            domain,
            scaffolding.ProjectStructure == ProjectStructureType.TwoProjects ? domainPath : Path.Combine(sourcePath, contracts),
            domainPath,
            scaffolding.Aspire ? $"{baseName}.Aspire" : null,
            ProjectNameHelper.ExtractSolutionName(contracts) + ".Tests");
    }
}