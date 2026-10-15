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
    /// Package ID for the SignalR client the typed hub clients of x-signalr-hubs build on.
    /// </summary>
    public const string SignalRClientPackageId = "Microsoft.AspNetCore.SignalR.Client";

    /// <summary>
    /// Fallback version for Atc.Rest.Api.SourceGenerator when NuGet cannot be reached: the CLI's own version, since the
    /// CLI and the generator are released together.
    /// </summary>
    public static readonly Version SourceGeneratorFallback = OwnVersion();

    /// <summary>
    /// Fallback minimum version for Atc.Rest.Client.
    /// </summary>
    /// <remarks>
    /// The generated EndpointPerOperation client needs the trim- and Native AOT-ready library (WithUrlEncodedForm,
    /// WithBody with a media type, ConfigureAtcRestClientJsonOptions, URI-escaped query parameter keys,
    /// WithFormJsonField for an object field of a multipart form), its readers of streamed responses (2.4.0:
    /// StreamFraming, typed errors and headers on StreamingEndpointResponse&lt;T&gt;) and the default error response
    /// without a type (2.5.0); the typed client's Result style reads an event stream with its ServerSentEventReader
    /// (2.6.0).
    /// </remarks>
    public static readonly Version RestClientMinFallback = new(2, 6, 0);

    /// <summary>
    /// Fallback minimum version for Microsoft.Extensions.Http.Resilience.
    /// </summary>
    public static readonly Version ResilienceMinFallback = new(10, 7, 0);

    /// <summary>
    /// The minimum Atc.Rest.MinimalApi a generated server that builds on it needs (the generator's ATC_API_DEP013).
    /// </summary>
    public static readonly Version MinimalApiMin = new(3, 1, 0);

    /// <summary>
    /// Fallback minimum version for Microsoft.AspNetCore.SignalR.Client.
    /// </summary>
    public static readonly Version SignalRClientMinFallback = new(10, 0, 13);

    private static Version OwnVersion()
    {
        var version = typeof(PackageVersionDefaults).Assembly.GetName().Version ?? new Version(1, 0, 0);
        return new Version(version.Major, version.Minor, System.Math.Max(version.Build, 0));
    }
}