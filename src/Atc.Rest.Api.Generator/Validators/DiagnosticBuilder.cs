namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// Helper for building rich diagnostic messages with context, suggestions, and documentation.
/// </summary>
[SuppressMessage("Design", "S1075:URIs should not be hardcoded", Justification = "Documentation URLs are intentionally static.")]
public static class DiagnosticBuilder
{
    private const string DocsBaseUrl = "https://github.com/atc-net/atc-rest-api-generator/blob/main/docs/analyzer-rules.md";

    /// <summary>
    /// Creates a schema reference error diagnostic.
    /// </summary>
    public static DiagnosticMessage SchemaReferenceError(
        string referenceId,
        string jsonPath,
        string filePath)
        => new(
            RuleId: RuleIdentifiers.InvalidSchemaReference,
            Message: $"Schema reference '{referenceId}' does not exist in components.schemas",
            Severity: DiagnosticSeverity.Error,
            FilePath: filePath,
            Context: jsonPath,
            Suggestions:
            [
                $"Add the missing schema '{referenceId}' to components.schemas",
                "Check for typos in the $ref path",
                "Ensure the schema name matches exactly (case-sensitive)"
            ],
            DocumentationUrl: GetDocUrl(RuleIdentifiers.InvalidSchemaReference));

    /// <summary>
    /// Creates a naming convention warning diagnostic.
    /// </summary>
    public static DiagnosticMessage NamingConventionWarning(
        string ruleId,
        string itemType,
        string itemName,
        string expectedCasing,
        string suggestedName,
        string jsonPath,
        string filePath)
        => new DiagnosticMessage(
            RuleId: ruleId,
            Message: $"{itemType} '{itemName}' must use {expectedCasing}",
            Severity: DiagnosticSeverity.Warning,
            FilePath: filePath,
            Context: itemName,
            Suggestions:
            [
                $"Rename to '{suggestedName}'"
            ],
            DocumentationUrl: GetDocUrl(ruleId))
            .WithLocation(filePath, 0) with
        {
            Context = jsonPath,
        };

    /// <summary>
    /// Creates an operationId naming convention warning.
    /// </summary>
    public static DiagnosticMessage OperationIdCasingWarning(
        string operationId,
        string suggestedName,
        string httpMethod,
        string path,
        string filePath)
        => new(
            RuleId: RuleIdentifiers.OperationIdMustBeCamelCase,
            Message: $"operationId '{operationId}' must use camelCase",
            Severity: DiagnosticSeverity.Warning,
            FilePath: filePath,
            Context: $"{httpMethod.ToUpperInvariant()} {path}",
            Suggestions:
            [
                $"Rename operationId to '{suggestedName}'",
                "Use camelCase for operationIds (e.g., 'getPetById', 'createUser')"
            ],
            DocumentationUrl: GetDocUrl(RuleIdentifiers.OperationIdMustBeCamelCase));

    /// <summary>
    /// Creates a response code warning diagnostic for missing security.
    /// </summary>
    public static DiagnosticMessage ResponseCodeSecurityWarning(
        string ruleId,
        string statusCode,
        string message,
        string operationId,
        string httpMethod,
        string path,
        string filePath,
        params string[] suggestions)
        => new(
            RuleId: ruleId,
            Message: message,
            Severity: DiagnosticSeverity.Warning,
            FilePath: filePath,
            Context: $"{httpMethod.ToUpperInvariant()} {path} ({operationId})",
            Suggestions: suggestions,
            DocumentationUrl: GetDocUrl(ruleId));

    /// <summary>
    /// Creates an OpenAPI parsing error diagnostic.
    /// </summary>
    public static DiagnosticMessage ParsingError(
        string errorMessage,
        string? jsonPointer,
        string filePath)
    {
        var suggestions = new List<string>
        {
            "Validate your OpenAPI spec using a tool like Swagger Editor",
            "Check YAML/JSON syntax for formatting errors",
        };

        if (!string.IsNullOrEmpty(jsonPointer))
        {
            suggestions.Insert(0, $"Check the element at JSON path: {jsonPointer}");
        }

        return new DiagnosticMessage(
            RuleId: RuleIdentifiers.OpenApiCoreError,
            Message: $"OpenAPI parsing error: {errorMessage}",
            Severity: DiagnosticSeverity.Error,
            FilePath: filePath,
            Context: jsonPointer,
            Suggestions: suggestions,
            DocumentationUrl: GetDocUrl(RuleIdentifiers.OpenApiCoreError));
    }

    /// <summary>
    /// Creates a missing required field error.
    /// </summary>
    public static DiagnosticMessage MissingRequiredField(
        string ruleId,
        string fieldName,
        string parentContext,
        string jsonPath,
        string filePath,
        string? suggestion = null)
    {
        var suggestions = new List<string>
        {
            $"Add the required '{fieldName}' field to {parentContext}",
        };

        if (suggestion is not null)
        {
            suggestions.Add(suggestion);
        }

        return new DiagnosticMessage(
            RuleId: ruleId,
            Message: $"Missing required '{fieldName}' in {parentContext}",
            Severity: DiagnosticSeverity.Warning,
            FilePath: filePath,
            Context: jsonPath,
            Suggestions: suggestions,
            DocumentationUrl: GetDocUrl(ruleId));
    }

    private static string GetDocUrl(string ruleId)
    {
        var lowerRuleId = ruleId
            .ToLowerInvariant()
            .Replace("_", "-");

        return $"{DocsBaseUrl}#{lowerRuleId}";
    }
}