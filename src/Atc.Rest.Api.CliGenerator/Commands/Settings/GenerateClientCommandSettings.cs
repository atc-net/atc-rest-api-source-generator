namespace Atc.Rest.Api.CliGenerator.Commands.Settings;

/// <summary>
/// Settings for the generate client command.
/// </summary>
public sealed class GenerateClientCommandSettings : BaseGenerateCommandSettings
{
    [CommandOption("--generation-mode <MODE>")]
    [Description("Client generation mode: TypedClient or EndpointPerOperation.")]
    public string? GenerationMode { get; init; }

    [CommandOption("--client-suffix <SUFFIX>")]
    [Description("Suffix for the HTTP client class name (default: 'Client').")]
    public string? ClientSuffix { get; init; }

    [CommandOption("--no-oauth")]
    [Description("Disable OAuth2 token management generation even when OAuth2 security is detected.")]
    [DefaultValue(false)]
    public bool DisableOAuth { get; init; }

    [CommandOption("--client-project <NAME>")]
    [Description("Override the client project name.")]
    public string? ClientProjectName { get; init; }

    [CommandOption("--client-namespace <NAMESPACE>")]
    [Description("Override the client namespace (written to marker file).")]
    public string? ClientNamespace { get; init; }

    [CommandOption("--report")]
    [Description("Generate a .generation-report.md file in the output directory.")]
    [DefaultValue(false)]
    public bool GenerateReport { get; init; }

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