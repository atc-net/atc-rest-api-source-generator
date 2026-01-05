namespace Atc.Rest.Api.Generator.Cli.Options;

/// <summary>
/// General configuration options shared across all generation modes.
/// </summary>
public sealed class GeneralOptions
{
    /// <summary>
    /// OpenAPI specification validation strategy.
    /// Default: Strict.
    /// </summary>
    public ValidateSpecificationStrategy ValidateSpecificationStrategy { get; set; } = ValidateSpecificationStrategy.Strict;

    /// <summary>
    /// Include deprecated operations and schemas in generated code.
    /// Default: false.
    /// </summary>
    public bool IncludeDeprecated { get; set; }
}