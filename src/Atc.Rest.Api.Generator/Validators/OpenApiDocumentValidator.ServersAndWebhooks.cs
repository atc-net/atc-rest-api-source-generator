// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable CommentTypo
// ReSharper disable InvertIf
// ReSharper disable LoopCanBeConvertedToQuery
namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// The server and webhook rules (ATC_API_SRV, ATC_API_WBH).
/// </summary>
public static partial class OpenApiDocumentValidator
{
    /// <summary>
    /// Validates servers (ATC_API_SRV001).
    /// </summary>
    private static List<DiagnosticMessage> ValidateServers(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.Servers is null || document.Servers.Count == 0)
        {
            return diagnostics;
        }

        foreach (var server in document.Servers)
        {
            var url = server.Url ?? string.Empty;

            // Check for empty or null URL
            if (string.IsNullOrWhiteSpace(url))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.InvalidServerUrl,
                    "Server URL is empty or null.",
                    DiagnosticSeverity.Error,
                    sourceFilePath,
                    LineNumber: null,
                    ColumnNumber: null,
                    Context: "servers",
                    Suggestions:
                    [
                        "Provide a valid server URL (e.g., https://api.example.com)"
                    ]));
                continue;
            }

            // Allow relative URLs starting with /
            if (url.StartsWith("/", StringComparison.Ordinal))
            {
                continue;
            }

            // Allow URLs with variable placeholders (e.g., {protocol}://api.example.com)
            if (url.IndexOf("{", StringComparison.Ordinal) >= 0)
            {
                // Validate that variables used in URL are defined in server.Variables
                var variablesInUrl = ExtractServerVariables(url);
                foreach (var variable in variablesInUrl)
                {
                    if (server.Variables is null ||
                        !server.Variables.ContainsKey(variable))
                    {
                        diagnostics.Add(new DiagnosticMessage(
                            RuleIdentifiers.InvalidServerUrl,
                            $"Server URL '{url}' uses variable '{{{variable}}}' but it is not defined in server variables.",
                            DiagnosticSeverity.Error,
                            sourceFilePath,
                            LineNumber: null,
                            ColumnNumber: null,
                            Context: "servers",
                            Suggestions:
                            [
                                $"Add '{variable}' to server variables with a default value"
                            ]));
                    }
                }

                continue;
            }

            // Validate absolute URL format
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.InvalidServerUrl,
                    $"Server URL '{url}' is not a valid format. Must be an absolute URL (http:// or https://), a relative path (/), or use server variables.",
                    DiagnosticSeverity.Error,
                    sourceFilePath,
                    LineNumber: null,
                    ColumnNumber: null,
                    Context: "servers",
                    Suggestions:
                    [
                        $"Use an absolute URL like 'https://{url}' or a relative path like '/{url}'"
                    ]));
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Extracts variable names from a server URL template.
    /// </summary>
    private static List<string> ExtractServerVariables(string url)
    {
        var variables = new List<string>();
        var startIndex = 0;

        while ((startIndex = url.IndexOf('{', startIndex)) >= 0)
        {
            var endIndex = url.IndexOf('}', startIndex);
            if (endIndex < 0)
            {
                break;
            }

            var variableName = url.Substring(startIndex + 1, endIndex - startIndex - 1);
            if (!string.IsNullOrEmpty(variableName))
            {
                variables.Add(variableName);
            }

            startIndex = endIndex + 1;
        }

        return variables;
    }

    /// <summary>
    /// Validates webhooks (OpenAPI 3.1 feature).
    /// </summary>
    private static List<DiagnosticMessage> ValidateWebhooks(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (!document.HasWebhooks)
        {
            return diagnostics;
        }

        // Info: Webhooks detected
        var webhookCount = document.WebhooksCount;
        diagnostics.Add(new DiagnosticMessage(
            RuleIdentifiers.WebhooksDetected,
            $"OpenAPI 3.1 webhooks detected: {webhookCount} webhook(s) defined. " +
            $"Webhooks allow your API to send data to consumer endpoints.",
            DiagnosticSeverity.Info,
            sourceFilePath,
            LineNumber: null,
            ColumnNumber: null,
            Context: "Webhooks",
            Suggestions:
            [
                "Webhook handlers will be generated for each webhook operation",
                "Implement webhook handlers to process incoming webhook events"
            ]));

        // Validate each webhook
        foreach (var (webhookName, method, operation) in document.GetAllWebhookOperations())
        {
            // ATC_API_WBH001: Missing operationId
            if (string.IsNullOrEmpty(operation.OperationId))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.WebhookMissingOperationId,
                    $"Webhook '{webhookName}' ({method}) is missing an operationId. " +
                    $"An operationId is required for generating handler interfaces.",
                    DiagnosticSeverity.Error,
                    sourceFilePath,
                    LineNumber: null,
                    ColumnNumber: null,
                    Context: $"Webhook: {webhookName}",
                    Suggestions:
                    [
                        $"Add an operationId to the {method} operation in webhook '{webhookName}'",
                        "Use a descriptive name like 'onOrderCreated' or 'handlePaymentWebhook'"
                    ]));
            }

            // ATC_API_WBH002: Missing request body
            if (operation.RequestBody is null || operation.RequestBody.Content is null || operation.RequestBody.Content.Count == 0)
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.WebhookMissingRequestBody,
                    $"Webhook '{webhookName}' ({method}) is missing a request body. " +
                    $"Webhooks typically receive data in the request body.",
                    DiagnosticSeverity.Warning,
                    sourceFilePath,
                    LineNumber: null,
                    ColumnNumber: null,
                    Context: $"Webhook: {webhookName}",
                    Suggestions:
                    [
                        $"Add a requestBody to the {method} operation in webhook '{webhookName}'",
                        "Define the schema for the data your API will receive"
                    ]));
            }
        }

        return diagnostics;
    }
}