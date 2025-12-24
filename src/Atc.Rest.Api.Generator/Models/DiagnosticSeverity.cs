namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Severity level for diagnostic messages.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// Informational message, not a problem.
    /// </summary>
    Info,

    /// <summary>
    /// Warning message, may indicate a problem but does not block generation.
    /// </summary>
    Warning,

    /// <summary>
    /// Error message, indicates a problem that blocks generation.
    /// </summary>
    Error,
}