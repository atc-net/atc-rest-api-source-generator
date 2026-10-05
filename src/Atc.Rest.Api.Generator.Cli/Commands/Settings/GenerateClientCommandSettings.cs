namespace Atc.Rest.Api.Generator.Cli.Commands.Settings;

/// <summary>
/// Settings for the generate client command.
/// </summary>
public sealed class GenerateClientCommandSettings : BaseGenerateCommandSettings
{
    /// <summary>
    /// Gets the client generation mode: <c>TypedClient</c> or <c>EndpointPerOperation</c>.
    /// </summary>
    [CommandOption("--generation-mode <MODE>")]
    [Description("Client generation mode: TypedClient or EndpointPerOperation.")]
    public string? GenerationMode { get; init; }

    /// <summary>
    /// Gets the suffix appended to the generated HTTP client class names (default <c>Client</c>).
    /// </summary>
    [CommandOption("--client-suffix <SUFFIX>")]
    [Description("Suffix for the HTTP client class name (default: 'Client').")]
    public string? ClientSuffix { get; init; }

    /// <summary>
    /// Gets the client granularity: one client per path segment (<c>PerArea</c>, default) or a <c>Single</c> client.
    /// </summary>
    [CommandOption("--client-granularity <GRANULARITY>")]
    [Description("Client granularity: PerArea (default) or Single.")]
    public string? ClientGranularity { get; init; }

    /// <summary>
    /// Gets the explicit type name of the single client; only used with <c>--client-granularity Single</c>.
    /// </summary>
    [CommandOption("--client-name <NAME>")]
    [Description("Explicit client type name, used verbatim. Only applies with --client-granularity Single.")]
    public string? ClientName { get; init; }

    /// <summary>
    /// Gets a value indicating whether OAuth2 token management is left out even when the specification declares OAuth2 security.
    /// </summary>
    [CommandOption("--no-oauth")]
    [Description("Disable OAuth2 token management generation even when OAuth2 security is detected.")]
    [DefaultValue(false)]
    public bool DisableOAuth { get; init; }

    /// <summary>
    /// Gets a value indicating whether string enums are generated as C# enums instead of extensible enums.
    /// </summary>
    [CommandOption("--no-extensible-enums")]
    [Description("Generate string enums as C# enums instead of extensible enums (a value the specification does not list then fails the response). Writes \"extensibleEnums\": false to the marker.")]
    [DefaultValue(false)]
    public bool NoExtensibleEnums { get; init; }

    /// <summary>
    /// Gets the client project name override; derived from the base project name when not set.
    /// </summary>
    [CommandOption("--client-project <NAME>")]
    [Description("Override the client project name.")]
    public string? ClientProjectName { get; init; }

    /// <summary>
    /// Gets the client namespace override written to the client marker file.
    /// </summary>
    [CommandOption("--client-namespace <NAMESPACE>")]
    [Description("Override the client namespace (written to marker file).")]
    public string? ClientNamespace { get; init; }

    /// <summary>
    /// Gets a value indicating whether a <c>.generation-report.md</c> is written to the output directory.
    /// </summary>
    [CommandOption("--report")]
    [Description("Generate a .generation-report.md file in the output directory.")]
    [DefaultValue(false)]
    public bool GenerateReport { get; init; }

    /// <summary>
    /// Runs the base validation, then checks the generation mode and the client project name override.
    /// </summary>
    /// <returns>A success result, or an error naming the first option that is invalid.</returns>
    public override ValidationResult Validate()
    {
        var baseResult = base.Validate();
        if (!baseResult.Successful)
        {
            return baseResult;
        }

        // Validate generation mode if provided
        if (!string.IsNullOrWhiteSpace(GenerationMode) &&
            !Enum.TryParse<GenerationModeType>(GenerationMode, ignoreCase: true, out _))
        {
            return ValidationResult.Error($"Invalid generation mode: '{GenerationMode}'. Valid values: TypedClient, EndpointPerOperation.");
        }

        // Validate client project name override doesn't contain spaces
        if (!string.IsNullOrWhiteSpace(ClientProjectName) && ClientProjectName.Contains(' ', StringComparison.Ordinal))
        {
            return ValidationResult.Error("Client project name cannot contain spaces.");
        }

        return ValidationResult.Success();
    }
}