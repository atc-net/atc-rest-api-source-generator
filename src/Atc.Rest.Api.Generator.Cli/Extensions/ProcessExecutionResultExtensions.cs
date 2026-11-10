namespace Atc.Rest.Api.Generator.Cli.Extensions;

/// <summary>
/// Extension methods for the result of <see cref="ProcessHelper"/>.
/// </summary>
internal static class ProcessExecutionResultExtensions
{
    /// <summary>
    /// Tells whether the process could not be started at all (e.g. the tool is not installed). Such a result has no
    /// standard output and no standard error; the reason is in <see cref="ProcessExecutionResult.Output"/>.
    /// A process that ran and wrote nothing has an empty <see cref="ProcessExecutionResult.Output"/> too.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns><see langword="true"/> when the process did not start.</returns>
    public static bool IsStartFailure(this ProcessExecutionResult result)
        => result is { IsTimedOut: false, IsCancelled: false } &&
           result.StandardOutput.Length == 0 &&
           result.StandardError.Length == 0 &&
           result.Output.Length > 0;
}