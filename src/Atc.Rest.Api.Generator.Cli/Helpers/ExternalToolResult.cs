namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// What a run gave.
/// </summary>
/// <param name="ExitCode">The exit code; <see langword="null"/> when the tool was stopped for taking too long.</param>
/// <param name="Output">What it wrote to standard output.</param>
/// <param name="Error">What it wrote to standard error.</param>
public sealed record ExternalToolResult(
    int? ExitCode,
    string Output,
    string Error)
{
    /// <summary>
    /// Gets a value indicating whether the tool was stopped because it did not end in time.
    /// </summary>
    public bool TimedOut => ExitCode is null;
}