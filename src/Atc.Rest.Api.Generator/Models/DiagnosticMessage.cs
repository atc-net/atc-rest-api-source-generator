namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Represents a diagnostic message (error, warning, info) with rich context.
/// Platform-agnostic - converted to Roslyn Diagnostic or console output by adapters.
/// </summary>
/// <param name="RuleId">The diagnostic rule ID (e.g., "ATC_API_GEN001").</param>
/// <param name="Message">The diagnostic message text.</param>
/// <param name="Severity">The severity level of the diagnostic.</param>
/// <param name="FilePath">Optional file path where the diagnostic occurred.</param>
/// <param name="LineNumber">Optional line number where the diagnostic occurred (1-based).</param>
/// <param name="ColumnNumber">Optional column number where the diagnostic occurred (1-based).</param>
/// <param name="Context">Optional context identifier (e.g., operation name, schema name).</param>
/// <param name="Suggestions">Optional list of suggestions for fixing the issue.</param>
/// <param name="DocumentationUrl">Optional URL to documentation for this rule.</param>
[SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "String URLs are more convenient for this use case.")]
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "String URLs are more convenient for this use case.")]
public record DiagnosticMessage(
    string RuleId,
    string Message,
    DiagnosticSeverity Severity,
    string? FilePath = null,
    int? LineNumber = null,
    int? ColumnNumber = null,
    string? Context = null,
    IReadOnlyList<string>? Suggestions = null,
    string? DocumentationUrl = null)
{
    /// <summary>
    /// Creates a simple error diagnostic message.
    /// </summary>
    public static DiagnosticMessage Error(
        string ruleId,
        string message,
        string? filePath = null,
        int? lineNumber = null)
        => new(ruleId, message, DiagnosticSeverity.Error, filePath, lineNumber);

    /// <summary>
    /// Creates a simple warning diagnostic message.
    /// </summary>
    public static DiagnosticMessage Warning(
        string ruleId,
        string message,
        string? filePath = null,
        int? lineNumber = null)
        => new(ruleId, message, DiagnosticSeverity.Warning, filePath, lineNumber);

    /// <summary>
    /// Creates a simple info diagnostic message.
    /// </summary>
    public static DiagnosticMessage Info(
        string ruleId,
        string message,
        string? filePath = null)
        => new(ruleId, message, DiagnosticSeverity.Info, filePath);

    /// <summary>
    /// Creates a new diagnostic message with additional context.
    /// </summary>
    public DiagnosticMessage WithContext(string context)
        => this with { Context = context };

    /// <summary>
    /// Creates a new diagnostic message with location information.
    /// </summary>
    public DiagnosticMessage WithLocation(
        string filePath,
        int lineNumber,
        int? columnNumber = null)
        => this with { FilePath = filePath, LineNumber = lineNumber, ColumnNumber = columnNumber };

    /// <summary>
    /// Creates a new diagnostic message with suggestions.
    /// </summary>
    public DiagnosticMessage WithSuggestions(params string[] suggestions)
        => this with { Suggestions = suggestions };

    /// <summary>
    /// Creates a new diagnostic message with documentation URL.
    /// </summary>
    public DiagnosticMessage WithDocumentation(string url)
        => this with { DocumentationUrl = url };

    /// <summary>
    /// Gets whether this diagnostic has rich context (location, suggestions, or documentation).
    /// </summary>
    public bool HasRichContext
        => LineNumber.HasValue || ColumnNumber.HasValue || Context is not null ||
           (Suggestions is not null && Suggestions.Count > 0) || DocumentationUrl is not null;
}