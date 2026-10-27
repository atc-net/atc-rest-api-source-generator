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

    /// <summary>
    /// Remove blank lines between namespace groups in GlobalUsings.cs.
    /// Default: false (blank lines are preserved between groups).
    /// </summary>
    public bool RemoveNamespaceGroupSeparatorInGlobalUsings { get; set; }

    /// <summary>
    /// OpenAPI Overlay files (1.0 to 1.2) applied to the specification first, in the order listed and before the --overlay files of the command line. Paths are relative to the options file.
    /// Default: none.
    /// </summary>
    public IList<string> Overlays { get; set; } = [];
}