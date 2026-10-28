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
    /// Host in 1 project, Contracts + Domain-Handlers in another project (the host references it).
    /// </summary>
    TwoProjects = 1,

    /// <summary>
    /// Host + Contracts + Domain-Handlers in 3 separate projects.
    /// </summary>
    ThreeProjects = 2,
}