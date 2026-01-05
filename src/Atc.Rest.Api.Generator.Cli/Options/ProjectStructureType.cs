namespace Atc.Rest.Api.Generator.Cli.Options;

/// <summary>
/// Defines the project structure for code generation.
/// </summary>
public enum ProjectStructureType
{
    /// <summary>
    /// Host + Contracts + Domain-Handlers in 1 project.
    /// </summary>
    SingleProject = 0,

    /// <summary>
    /// Host + Contracts in 1 project, Domain-Handlers in another project.
    /// </summary>
    TwoProjects = 1,

    /// <summary>
    /// Host + Contracts + Domain-Handlers in 3 separate projects.
    /// </summary>
    ThreeProjects = 2,
}