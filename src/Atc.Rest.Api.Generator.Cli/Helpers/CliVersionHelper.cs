namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// Helper for checking CLI version information.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The update check must never affect the command that runs after it.")]
public static class CliVersionHelper
{
    /// <summary>
    /// The environment variable that turns the start-up update check off (<c>1</c> or <c>true</c>).
    /// </summary>
    public const string NoUpdateCheckVariable = "ATC_REST_API_GEN_NO_UPDATE_CHECK";

    private const string PackageId = "atc-rest-api-gen";

    private static readonly string[] ArgumentsWithoutUpdateCheck =
    [
        CommandConstants.ArgumentShortHelp,
        CommandConstants.ArgumentLongHelp,
        CommandConstants.ArgumentShortVersion,
        CommandConstants.ArgumentLongVersion,
    ];

    /// <summary>
    /// Tells whether the update check should run for the given command line. It is skipped when
    /// no arguments are given and for <c>-h</c>, <c>--help</c>, <c>-v</c> and <c>--version</c>,
    /// which must answer at once, even on a lossy network.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <returns><see langword="true"/> when the check should run.</returns>
    public static bool ShouldCheckForUpdates(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return args.Length > 0 &&
               !args.Any(arg => ArgumentsWithoutUpdateCheck.Contains(arg, StringComparer.Ordinal));
    }

    /// <summary>
    /// Gets the version on NuGet that is newer than <paramref name="currentVersion"/>, within the
    /// given time budget. Returns <see langword="null"/> when the current version is the latest, when
    /// the feed cannot be read or answers nonsense, and when the budget runs out; it never throws.
    /// </summary>
    /// <param name="nugetService">The NuGet version service.</param>
    /// <param name="currentVersion">The version of the running CLI.</param>
    /// <param name="budget">The time the whole check may take.</param>
    /// <returns>The newer version, or <see langword="null"/>.</returns>
    public static async Task<Version?> TryGetNewerVersionAsync(
        INugetPackageVersionService nugetService,
        Version currentVersion,
        TimeSpan budget)
    {
        ArgumentNullException.ThrowIfNull(nugetService);
        ArgumentNullException.ThrowIfNull(currentVersion);

        try
        {
            using var cts = new CancellationTokenSource(budget);
            var latestVersion = await nugetService
                .GetLatestVersionAsync(PackageId, cts.Token)
                .WaitAsync(budget, cts.Token)
                .ConfigureAwait(false);

            return latestVersion is not null && latestVersion.GreaterThan(currentVersion)
                ? latestVersion
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Tells whether the start-up update check is turned off.
    /// </summary>
    /// <param name="getEnvironmentVariable">Reads an environment variable by name.</param>
    /// <returns>True when the check should be skipped.</returns>
    public static bool IsUpdateCheckDisabled(
        Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        // The explicit switch, or a CI system (they set CI=true), where an update notice is noise
        return IsSet(getEnvironmentVariable(NoUpdateCheckVariable)) ||
               IsSet(getEnvironmentVariable("CI"));

        static bool IsSet(string? value)
            => value is not null &&
               (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the latest available version from NuGet.
    /// </summary>
    /// <returns>The latest version, or null if unavailable.</returns>
    public static Task<Version?> GetLatestVersionAsync(
        INugetPackageVersionService nugetService,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nugetService);
        return nugetService.GetLatestVersionAsync(PackageId, cancellationToken);
    }

    /// <summary>
    /// Checks if the current version is the latest available.
    /// </summary>
    /// <returns>True if current version is latest or if check fails.</returns>
    public static async Task<bool> IsLatestVersionAsync(
        INugetPackageVersionService nugetService,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nugetService);

        var currentVersion = CliHelper.GetCurrentVersion();

        // Skip check for development versions
        if (currentVersion == new Version(1, 0, 0, 0))
        {
            return true;
        }

        var latestVersion = await nugetService.GetLatestVersionAsync(PackageId, cancellationToken);
        return latestVersion is null ||
               !latestVersion.GreaterThan(currentVersion);
    }
}