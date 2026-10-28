// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable CommentTypo
// ReSharper disable InvertIf
// ReSharper disable LoopCanBeConvertedToQuery
namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// The path rules (ATC_API_PTH): path templates, their placeholders, and a document without operations.
/// </summary>
public static partial class OpenApiDocumentValidator
{
    /// <summary>
    /// The problems of a path template (<c>ATC_API_PTH001</c>), in the order they are checked: unbalanced braces, an
    /// empty or a nested placeholder, a malformed parameter, then what the route or the generated code could not carry.
    /// A problem that ends the checks is the last; the path's operations are not checked against it.
    /// </summary>
    /// <param name="pathKey">The path template.</param>
    /// <returns>The problems; none for a well-formed template.</returns>
    internal static List<PathTemplateProblem> PathTemplateProblems(
        string pathKey)
    {
        var problems = new List<PathTemplateProblem>();

        var openBraceCount = pathKey.Count(c => c == '{');
        var closeBraceCount = pathKey.Count(c => c == '}');
        if (openBraceCount != closeBraceCount)
        {
            problems.Add(new PathTemplateProblem(
                $"Path '{pathKey}' has unbalanced braces: {openBraceCount} opening '{{', {closeBraceCount} closing '}}'.",
                ["Ensure each '{' has a matching '}'", "Path parameters should be formatted as {parameterName}"],
                EndsChecks: true));
            return problems;
        }

        if (pathKey.IndexOf("{}", StringComparison.Ordinal) >= 0)
        {
            problems.Add(new PathTemplateProblem(
                $"Path '{pathKey}' contains empty parameter placeholder '{{}}'.",
                ["Provide a name for the path parameter (e.g., {id})"],
                EndsChecks: false));
        }

        if (pathKey.IndexOf("{{", StringComparison.Ordinal) >= 0 ||
            pathKey.IndexOf("}}", StringComparison.Ordinal) >= 0)
        {
            problems.Add(new PathTemplateProblem(
                $"Path '{pathKey}' contains nested or escaped braces which are not valid in OpenAPI paths.",
                ["Use single braces for path parameters (e.g., {id} not {{id}})"],
                EndsChecks: false));
        }

        if (!ValidatePathParameterFormat(pathKey, out var errorMessage))
        {
            problems.Add(new PathTemplateProblem(
                $"Path '{pathKey}' has malformed parameters: {errorMessage}",
                ["Path parameters should be formatted as {parameterName}", "Parameter names should be valid identifiers (letters, digits, underscores)"],
                EndsChecks: true));
            return problems;
        }

        if (TemplateProblem(pathKey) is { } problem)
        {
            problems.Add(new PathTemplateProblem(
                $"Path '{pathKey}' {problem}.",
                ["A path starts with '/', has no empty segment, names each placeholder once, and holds no quote, backslash, whitespace, '?' or '#'"],
                EndsChecks: true));
        }

        return problems;
    }

    /// <summary>
    /// Kestrel's default <c>MaxRequestLineSize</c>. A request line longer than this is rejected with
    /// <c>414 URI Too Long</c> before routing, so it never reaches a handler.
    /// </summary>
    private const int RequestLineBudgetInBytes = 8192;

    // How deep a schema walk goes before it gives up: far beyond any real nesting, short of a stack overflow
    private const int MaxSchemaDepth = 64;

    private static readonly string[] CollectionIntentPrefixes = ["list", "search", "find"];

    private static readonly string[] QueryVerbPrefixes = ["get", "list", "find", "search", "fetch", "retrieve"];

    // Suffixes that end in 's' but describe single-item responses (properties of one entity).
    private static readonly string[] SingleItemSuffixes =
    [
        "Details",    // getDeviceDetails - details about ONE device
        "Status",     // getStatus - status of ONE item (also covered by 'us' check)
        "Settings",   // getSettings - settings for ONE user/account
        "Statistics", // getStatistics - statistics for ONE entity
        "Contents",   // getContents - contents of ONE container
        "Metrics",    // getMetrics - metrics for ONE service
        "News",       // getNews - news is uncountable
        "Progress",   // getProgress - progress of ONE operation (doesn't end in 's' but for completeness)
    ];

    /// <summary>
    /// Validates using Microsoft.OpenApi library errors (Standard level).
    /// </summary>
    /// <summary>
    /// ATC_API_PTH001 for a path the reader left out of the document: one that does not start with <c>/</c>, which the
    /// reader reports only as "{name} is not a valid property at #/paths" among its own errors (shown under Standard
    /// and Strict only). The operations under it would otherwise disappear without a word. Every strategy - for a path
    /// the marker's <c>includePaths</c> / <c>excludePaths</c> keep.
    /// </summary>
    private static List<DiagnosticMessage> ValidatePathsTheReaderDropped(
        IList<OpenApiError> diagnosticErrors,
        string sourceFilePath,
        IList<string>? includePaths,
        IList<string>? excludePaths)
    {
        var diagnostics = new List<DiagnosticMessage>();
        foreach (var error in diagnosticErrors)
        {
            if (DroppedPath(error) is not { } path ||
                PathFilterHelper.FilterPaths([path], includePaths, excludePaths).Count == 0)
            {
                continue;
            }

            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.PathParametersNotWellFormatted,
                $"Path '{path}' does not start with '/': the reader leaves it out, with every operation under it.",
                DiagnosticSeverity.Error,
                sourceFilePath,
                LineNumber: null,
                ColumnNumber: null,
                Context: path,
                Suggestions: [$"Write the path as '/{path}'"]));
        }

        return diagnostics;
    }

    // The path the reader dropped, from its error "{name} is not a valid property at #/paths"; null for any other error
    private static string? DroppedPath(OpenApiError error)
    {
        const string suffix = " is not a valid property at #/paths";
        var message = error.Message?.Trim();
        return message is not null && message.EndsWith(suffix, StringComparison.Ordinal) && message.Length > suffix.Length
            ? message.Substring(0, message.Length - suffix.Length).Trim()
            : null;
    }

    /// <summary>
    /// Validates paths (ATC_API_PTH001).
    /// </summary>
    private static List<DiagnosticMessage> ValidatePaths(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.Paths is null || document.Paths.Count == 0)
        {
            return diagnostics;
        }

        foreach (var pathEntry in document.Paths)
        {
            var pathKey = pathEntry.Key;

            // ATC_API_PTH001: a template the route or the generated code could not carry
            var problems = PathTemplateProblems(pathKey);
            diagnostics.AddRange(problems.Select(problem => new DiagnosticMessage(
                RuleIdentifiers.PathParametersNotWellFormatted,
                problem.Message,
                DiagnosticSeverity.Error,
                sourceFilePath,
                LineNumber: null,
                ColumnNumber: null,
                Context: pathKey,
                Suggestions: problem.Suggestions,
                Scope: SpecSuppressions.Path(pathKey))));
            if (problems.Any(problem => problem.EndsChecks))
            {
                continue;
            }

            // ATC_API_OPR012: a placeholder no parameter declares - the generated code would name a variable that
            // does not exist (a client), or bind nothing (a server)
            var placeholders = GetParametersFromPath(pathKey);
            if (placeholders.Count == 0 || pathEntry.Value?.Operations is null)
            {
                continue;
            }

            var pathLevel = pathEntry.Value.Parameters?.Where(p => p.In == ParameterLocation.Path).Select(p => p.Name) ?? [];
            foreach (var operationEntry in pathEntry.Value.Operations)
            {
                var operationLevel = operationEntry.Value?.Parameters?.Where(p => p.In == ParameterLocation.Path).Select(p => p.Name) ?? [];
                var declared = new HashSet<string>(pathLevel.Concat(operationLevel).Where(n => n is not null)!, StringComparer.OrdinalIgnoreCase);
                var operationId = operationEntry.Value?.OperationId ?? $"{operationEntry.Key.ToString().ToUpperInvariant()} {pathKey}";
                foreach (var placeholder in placeholders.Where(p => !declared.Contains(p)))
                {
                    diagnostics.Add(new DiagnosticMessage(
                        RuleIdentifiers.OperationMissingPathParameter,
                        $"Operation '{operationId}' in path '{pathKey}' does not define a parameter named '{placeholder}'.",
                        DiagnosticSeverity.Error,
                        sourceFilePath,
                        Scope: SpecSuppressions.Operation(operationEntry.Key.Method, pathKey)));
                }
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// What a path template has that the route or the generated code could not carry, or <see langword="null"/>.
    /// </summary>
    private static string? TemplateProblem(string pathKey)
    {
        if (!pathKey.StartsWith("/", StringComparison.Ordinal))
        {
            return "does not start with '/'";
        }

        if (pathKey.IndexOf("//", StringComparison.Ordinal) >= 0)
        {
            return "has an empty segment ('//')";
        }

        var bad = pathKey.FirstOrDefault(c => c is '"' or '\\' or '?' or '#' || char.IsWhiteSpace(c) || char.IsControl(c));
        if (bad != default)
        {
            var shown = char.IsControl(bad) ? $"U+{(int)bad:X4}" : bad.ToString();
            return $"contains '{shown}', which the route and the generated code cannot carry";
        }

        var twice = GetParametersFromPath(pathKey)
            .GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        return twice is null ? null : $"has the placeholder '{{{twice.Key}}}' more than once";
    }

    /// <summary>
    /// Validates path parameter format and returns an error message if invalid.
    /// </summary>
    private static bool ValidatePathParameterFormat(
        string path,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        var index = 0;

        while (index < path.Length)
        {
            var openBrace = path.IndexOf('{', index);
            if (openBrace < 0)
            {
                break;
            }

            var closeBrace = path.IndexOf('}', openBrace);
            if (closeBrace < 0)
            {
                errorMessage = $"Unclosed brace starting at position {openBrace}";
                return false;
            }

            // Check for another open brace before the close brace (nested)
            var nextOpen = path.IndexOf('{', openBrace + 1);
            if (nextOpen >= 0 && nextOpen < closeBrace)
            {
                errorMessage = $"Nested brace at position {nextOpen}";
                return false;
            }

            var paramName = path.Substring(openBrace + 1, closeBrace - openBrace - 1);

            // Validate parameter name is a valid identifier
            if (string.IsNullOrWhiteSpace(paramName))
            {
                errorMessage = "Empty parameter name";
                return false;
            }

            // Check for whitespace in parameter name
            if (paramName.Any(char.IsWhiteSpace))
            {
                errorMessage = $"Parameter '{paramName}' contains whitespace";
                return false;
            }

            // A placeholder is a parameter name: ASP.NET route syntax ({*rest}, {id:int}, {id?}) is not OpenAPI
            if (!paramName.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.'))
            {
                errorMessage = $"Parameter '{paramName}' is no parameter name (letters, digits, '_', '-' and '.'); ASP.NET route syntax such as '*', ':' or '?' is not OpenAPI";
                return false;
            }

            index = closeBrace + 1;
        }

        return true;
    }

    /// <summary>
    /// Gets path parameters from a path string.
    /// </summary>
    private static List<string> GetParametersFromPath(string path)
    {
        var parameters = new List<string>();
        var startIndex = 0;

        while ((startIndex = path.IndexOf('{', startIndex)) >= 0)
        {
            var endIndex = path.IndexOf('}', startIndex);
            if (endIndex < 0)
            {
                break;
            }

            var paramName = path.Substring(startIndex + 1, endIndex - startIndex - 1);
            parameters.Add(paramName);
            startIndex = endIndex + 1;
        }

        return parameters;
    }

    /// <summary>
    /// Validates that the document has something to generate from (ATC_API_PTH002): with no operations there are
    /// no endpoints, handlers or client methods - a models-only document, a multi-part base file whose parts are
    /// missing, an overlay that removed 'paths'. A document with webhooks has operations of its own kind.
    /// </summary>
    private static List<DiagnosticMessage> ValidateNoOperations(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var operations = document.Paths?.Sum(p => p.Value?.Operations?.Count ?? 0) ?? 0;
        if (operations > 0 || document.Webhooks is { Count: > 0 })
        {
            return [];
        }

        return
        [
            new DiagnosticMessage(
                RuleIdentifiers.NoOperations,
                "The specification has no operations: no endpoints, handlers or client methods are generated.",
                DiagnosticSeverity.Warning,
                sourceFilePath,
                Suggestions: ["Add the operations under 'paths'", "For a multi-part specification, check that the part files are next to the base file"]),
        ];
    }
}