namespace Atc.Rest.Api.Generator.Services;

/// <summary>
/// Represents a single generated type (class, record, interface, enum) that can be
/// output individually for tests or combined with others for source generators.
/// </summary>
/// <param name="TypeName">The name of the type (e.g., "Pet", "IListPetsHandler").</param>
/// <param name="Category">The category/group of the type (e.g., "Models", "Handlers", "Parameters", "Results").</param>
/// <param name="Namespace">The namespace for the type (e.g., "PetStoreSimple.Generated.Models").</param>
/// <param name="Content">The type body content without file headers or namespace declarations.</param>
/// <param name="RequiredUsings">The using statements required by this type.</param>
/// <param name="GroupName">The group name for folder organization (e.g., "Pets"). Determined by SubFolderStrategy.</param>
/// <param name="SubFolder">The relative folder path for file organization (e.g., "Contracts/Pets/Models").</param>
public record GeneratedType(
    string TypeName,
    string Category,
    string Namespace,
    string Content,
    IReadOnlyList<string> RequiredUsings,
    string? GroupName = null,
    string? SubFolder = null);