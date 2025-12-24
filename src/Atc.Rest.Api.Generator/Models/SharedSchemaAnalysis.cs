namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Analysis of a schema that's shared across multiple domains.
/// </summary>
public sealed class SharedSchemaAnalysis
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SharedSchemaAnalysis"/> class.
    /// </summary>
    public SharedSchemaAnalysis(
        string name,
        IReadOnlyList<string> usedByDomains)
    {
        Name = name;
        UsedByDomains = usedByDomains;
    }

    /// <summary>
    /// The schema name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Domains (tags or path segments) that use this schema.
    /// </summary>
    public IReadOnlyList<string> UsedByDomains { get; }
}