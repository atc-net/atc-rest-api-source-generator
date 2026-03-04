namespace Atc.Rest.Api.Generator.Cli.Defaults;

/// <summary>
/// Default fallback versions for NuGet packages when API lookup fails.
/// </summary>
internal static class PackageVersionDefaults
{
    /// <summary>
    /// Package ID for the source generator.
    /// </summary>
    public const string SourceGeneratorPackageId = "Atc.Rest.Api.SourceGenerator";

    /// <summary>
    /// Package ID for the REST client.
    /// </summary>
    public const string RestClientPackageId = "Atc.Rest.Client";

    /// <summary>
    /// Package ID for the HTTP resilience package.
    /// </summary>
    public const string ResiliencePackageId = "Microsoft.Extensions.Http.Resilience";

    /// <summary>
    /// Fallback version for Atc.Rest.Api.SourceGenerator.
    /// </summary>
    public static readonly Version SourceGeneratorFallback = new(1, 0, 115);

    /// <summary>
    /// Fallback minimum version for Atc.Rest.Client.
    /// </summary>
    public static readonly Version RestClientMinFallback = new(2, 0, 31);

    /// <summary>
    /// Fallback minimum version for Microsoft.Extensions.Http.Resilience.
    /// </summary>
    public static readonly Version ResilienceMinFallback = new(10, 3, 0);
}