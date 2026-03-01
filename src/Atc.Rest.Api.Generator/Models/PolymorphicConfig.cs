namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Configuration for a polymorphic schema (oneOf/anyOf).
/// </summary>
public class PolymorphicConfig
{
    /// <summary>
    /// Gets or sets the base type name for the polymorphic hierarchy.
    /// </summary>
    public string BaseTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the discriminator property name.
    /// </summary>
    public string DiscriminatorPropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this is oneOf (true) or anyOf (false).
    /// oneOf = exactly one schema must match (exclusive), anyOf = at least one must match (inclusive).
    /// </summary>
    public bool IsOneOf { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the discriminator was explicitly defined
    /// in the OpenAPI spec (true) or auto-detected from common properties (false).
    /// </summary>
    public bool IsDiscriminatorExplicit { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this type requires a custom
    /// JsonConverter (try-parse) instead of JsonPolymorphic attributes.
    /// True for oneOf/anyOf schemas without a discriminator.
    /// </summary>
    public bool UsesCustomConverter { get; set; }

    /// <summary>
    /// Gets the list of polymorphic variants.
    /// </summary>
    public List<PolymorphicVariant> Variants { get; } = [];
}