namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Represents a variant in a polymorphic schema.
/// </summary>
public class PolymorphicVariant
{
    /// <summary>
    /// Gets or sets the C# type name for this variant.
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the discriminator value that identifies this variant.
    /// </summary>
    public string DiscriminatorValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original schema reference ID.
    /// </summary>
    public string SchemaRefId { get; set; } = string.Empty;
}