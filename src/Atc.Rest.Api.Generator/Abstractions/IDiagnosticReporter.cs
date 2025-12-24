namespace Atc.Rest.Api.Generator.Abstractions;

/// <summary>
/// Abstraction for reporting diagnostics (errors, warnings, info).
/// Implemented by:
/// - RoslynDiagnosticReporter (source generator - reports to SourceProductionContext)
/// - ConsoleDiagnosticReporter (CLI tool - writes to console with colors)
/// </summary>
public interface IDiagnosticReporter
{
    /// <summary>
    /// Reports a diagnostic message.
    /// </summary>
    /// <param name="message">The diagnostic message to report.</param>
    void Report(DiagnosticMessage message);

    /// <summary>
    /// Reports an error diagnostic.
    /// </summary>
    /// <param name="ruleId">The diagnostic rule ID (e.g., "ATCAPI001").</param>
    /// <param name="message">The error message.</param>
    /// <param name="filePath">Optional file path where the error occurred.</param>
    void ReportError(
        string ruleId,
        string message,
        string? filePath = null);

    /// <summary>
    /// Reports a warning diagnostic.
    /// </summary>
    /// <param name="ruleId">The diagnostic rule ID.</param>
    /// <param name="message">The warning message.</param>
    /// <param name="filePath">Optional file path where the warning occurred.</param>
    void ReportWarning(
        string ruleId,
        string message,
        string? filePath = null);

    /// <summary>
    /// Reports an informational diagnostic.
    /// </summary>
    /// <param name="message">The info message.</param>
    void ReportInfo(string message);
}