namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Result of running an external process: its exit code and the combined
/// stdout + stderr output.
/// </summary>
internal sealed record ProcessResult(int ExitCode, string Output);