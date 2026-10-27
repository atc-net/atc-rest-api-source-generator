namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// A referenced package older than the generated code needs: the build stops with <see cref="RuleId"/> until it is updated.
/// </summary>
/// <param name="Package">The package id.</param>
/// <param name="Version">The version the project references.</param>
/// <param name="Minimum">The version the generated code needs at least.</param>
/// <param name="RuleId">The build error an older version gives.</param>
public sealed record PackageUpgrade(
    string Package,
    string Version,
    string Minimum,
    string RuleId);