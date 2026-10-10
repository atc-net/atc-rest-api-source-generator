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
    /// Fallback version for Atc.Rest.Api.SourceGenerator when NuGet cannot be reached: the CLI's own version, since the
    /// CLI and the generator are released together.
    /// </summary>
    public static readonly Version SourceGeneratorFallback = OwnVersion();

    /// <summary>
    /// Fallback minimum version for Atc.Rest.Client.
    /// </summary>
    /// <remarks>
    /// The generated EndpointPerOperation client needs the trim- and Native AOT-ready 2.3.0 (WithUrlEncodedForm,
    /// WithBody with a media type, ConfigureAtcRestClientJsonOptions, URI-escaped query parameter keys,
    /// WithFormJsonField for an object field of a multipart form).
    /// </remarks>
    public static readonly Version RestClientMinFallback = new(2, 3, 0);

    /// <summary>
    /// Fallback minimum version for Microsoft.Extensions.Http.Resilience.
    /// </summary>
    public static readonly Version ResilienceMinFallback = new(10, 7, 0);

    private static Version OwnVersion()
    {
        var version = typeof(PackageVersionDefaults).Assembly.GetName().Version ?? new Version(1, 0, 0);
        return new Version(version.Major, version.Minor, System.Math.Max(version.Build, 0));
    }
}