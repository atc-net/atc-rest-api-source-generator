namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Formats diagnostic messages for display in various contexts.
/// </summary>
[SuppressMessage("Design", "S1075:URIs should not be hardcoded", Justification = "Documentation URLs are intentionally static.")]
public static class DiagnosticMessageFormatter
{
    private const string DocumentationBaseUrl = "https://github.com/atc-net/atc-rest-api-generator/blob/main/docs/analyzer-rules.md";

    /// <summary>
    /// Formats a diagnostic message for rich console/terminal output.
    /// </summary>
    /// <param name="message">The diagnostic message to format.</param>
    /// <param name="useColors">Whether to include ANSI color codes.</param>
    /// <returns>A formatted string suitable for terminal output.</returns>
    public static string FormatRich(
        DiagnosticMessage message,
        bool useColors = false)
    {
        var sb = new StringBuilder();

        // Severity icon and rule ID
        var severityPrefix = GetSeverityPrefix(message.Severity);
        sb.Append(severityPrefix);
        sb.Append(' ');
        sb.Append(message.RuleId);

        // Context (e.g., operation name)
        if (!string.IsNullOrEmpty(message.Context))
        {
            sb.Append(" in '");
            sb.Append(message.Context);
            sb.Append('\'');
        }

        sb.AppendLine();
        sb.AppendLine();

        // Main message
        sb.Append("  ");
        sb.AppendLine(message.Message);

        // Location information
        if (!string.IsNullOrEmpty(message.FilePath))
        {
            sb.AppendLine();
            sb.Append("  Location: ");
            sb.Append(message.FilePath);

            if (message.LineNumber.HasValue)
            {
                sb.Append(", line ");
                sb.Append(message.LineNumber.Value);

                if (message.ColumnNumber.HasValue)
                {
                    sb.Append(", column ");
                    sb.Append(message.ColumnNumber.Value);
                }
            }

            sb.AppendLine();
        }

        // Suggestions
        if (message.Suggestions is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("  Suggestions:");
            for (var i = 0; i < message.Suggestions.Count; i++)
            {
                sb.Append("    ");
                sb.Append(i + 1);
                sb.Append(". ");
                sb.AppendLine(message.Suggestions[i]);
            }
        }

        // Documentation link
        var docUrl = message.DocumentationUrl ?? GetDefaultDocumentationUrl(message.RuleId);
        if (!string.IsNullOrEmpty(docUrl))
        {
            sb.AppendLine();
            sb.Append("  Documentation: ");
            sb.AppendLine(docUrl);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a diagnostic message as a single-line summary.
    /// </summary>
    /// <param name="message">The diagnostic message to format.</param>
    /// <returns>A single-line summary string.</returns>
    public static string FormatSingleLine(DiagnosticMessage message)
    {
        var sb = new StringBuilder();

        // File location
        if (!string.IsNullOrEmpty(message.FilePath))
        {
            sb.Append(message.FilePath);

            if (message.LineNumber.HasValue)
            {
                sb.Append('(');
                sb.Append(message.LineNumber.Value);

                if (message.ColumnNumber.HasValue)
                {
                    sb.Append(',');
                    sb.Append(message.ColumnNumber.Value);
                }

                sb.Append(')');
            }

            sb.Append(": ");
        }

        // Severity
        sb.Append(message.Severity.ToString().ToLowerInvariant());
        sb.Append(' ');
        sb.Append(message.RuleId);
        sb.Append(": ");

        // Message with context
        if (!string.IsNullOrEmpty(message.Context))
        {
            sb.Append('[');
            sb.Append(message.Context);
            sb.Append("] ");
        }

        sb.Append(message.Message);

        return sb.ToString();
    }

    /// <summary>
    /// Formats a diagnostic message for Spectre.Console markup.
    /// </summary>
    /// <param name="message">The diagnostic message to format.</param>
    /// <returns>A Spectre.Console markup string.</returns>
    public static string FormatSpectreMarkup(DiagnosticMessage message)
    {
        var sb = new StringBuilder();

        // Severity with color
        var (severityColor, severityIcon) = message.Severity switch
        {
            DiagnosticSeverity.Error => ("red", "x"),
            DiagnosticSeverity.Warning => ("yellow", "!"),
            _ => ("blue", "i")
        };

        sb.Append('[');
        sb.Append(severityColor);
        sb.Append(']');
        sb.Append(severityIcon);
        sb.Append("[/] ");

        // Rule ID with bold
        sb.Append("[bold]");
        sb.Append(EscapeSpectreMarkup(message.RuleId));
        sb.Append("[/]");

        // Context
        if (!string.IsNullOrEmpty(message.Context))
        {
            sb.Append(" in [italic]");
            sb.Append(EscapeSpectreMarkup(message.Context!));
            sb.Append("[/]");
        }

        sb.AppendLine();

        // Message
        sb.Append("   ");
        sb.AppendLine(EscapeSpectreMarkup(message.Message));

        // Location
        if (!string.IsNullOrEmpty(message.FilePath))
        {
            sb.Append("   [dim]Location:[/] ");
            sb.Append(EscapeSpectreMarkup(message.FilePath!));

            if (message.LineNumber.HasValue)
            {
                sb.Append(", line ");
                sb.Append(message.LineNumber.Value);

                if (message.ColumnNumber.HasValue)
                {
                    sb.Append(", column ");
                    sb.Append(message.ColumnNumber.Value);
                }
            }

            sb.AppendLine();
        }

        // Suggestions
        if (message.Suggestions is { Count: > 0 })
        {
            sb.AppendLine("   [dim]Suggestions:[/]");
            foreach (var suggestion in message.Suggestions)
            {
                sb.Append("     [green]*[/] ");
                sb.AppendLine(EscapeSpectreMarkup(suggestion));
            }
        }

        // Documentation
        var docUrl = message.DocumentationUrl ?? GetDefaultDocumentationUrl(message.RuleId);
        if (!string.IsNullOrEmpty(docUrl))
        {
            sb.Append("   [dim]Docs:[/] [link]");
            sb.Append(EscapeSpectreMarkup(docUrl!));
            sb.AppendLine("[/]");
        }

        return sb.ToString();
    }

    private static string GetSeverityPrefix(DiagnosticSeverity severity)
        => severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            _ => "info"
        };

    private static string? GetDefaultDocumentationUrl(string ruleId)
    {
        // Only generate URL for known ATC_API rules
        if (ruleId.StartsWith("ATC_API_", StringComparison.Ordinal))
        {
            return $"{DocumentationBaseUrl}#{ruleId.ToLowerInvariant().Replace("_", "-")}";
        }

        return null;
    }

    private static string EscapeSpectreMarkup(string text)
        => text.Replace("[", "[[").Replace("]", "]]");
}