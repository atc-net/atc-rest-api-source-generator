// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable CommentTypo
// ReSharper disable InvertIf
// ReSharper disable LoopCanBeConvertedToQuery
namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// The security rules (ATC_API_SEC): security schemes and the access an operation or a path declares.
/// </summary>
public static partial class OpenApiDocumentValidator
{
    /// <summary>
    /// Validates security configuration (ATC_API_SEC001-010).
    /// </summary>
    private static List<DiagnosticMessage> ValidateSecurityConfiguration(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        // Extract global security configuration from document extensions
        var globalAuthorizeRoles = new List<string>();
        var globalAuthenticationSchemes = new List<string>();

        if (document.Extensions is { Count: > 0 })
        {
            globalAuthorizeRoles.AddRange(document.Extensions.ExtractAuthorizeRoles());
            globalAuthenticationSchemes.AddRange(document.Extensions.ExtractAuthenticationSchemes());
        }

        if (document.Paths is null)
        {
            return diagnostics;
        }

        foreach (var pathEntry in document.Paths)
        {
            var pathKey = pathEntry.Key;
            var pathItem = pathEntry.Value;

            // Validate path-level security
            ValidatePathSecurity(
                diagnostics,
                sourceFilePath,
                pathKey,
                pathItem,
                globalAuthorizeRoles,
                globalAuthenticationSchemes);

            // Validate operation-level security
            if (pathItem.Operations is not null)
            {
                foreach (var operationEntry in pathItem.Operations)
                {
                    var operation = operationEntry.Value;
                    ValidateOperationSecurity(
                        diagnostics,
                        sourceFilePath,
                        pathKey,
                        operation,
                        globalAuthorizeRoles,
                        globalAuthenticationSchemes);
                }
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates path-level security configuration.
    /// </summary>
    private static void ValidatePathSecurity(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string pathKey,
        IOpenApiPathItem pathItem,
        List<string> globalAuthorizeRoles,
        List<string> globalAuthenticationSchemes)
    {
        if (pathItem.Extensions is null || pathItem.Extensions.Count == 0)
        {
            return;
        }

        var pathAuthenticationRequired = pathItem.Extensions.ExtractAuthenticationRequired();
        var pathAuthorizeRoles = pathItem.Extensions.ExtractAuthorizeRoles();
        var pathAuthenticationSchemes = pathItem.Extensions.ExtractAuthenticationSchemes();

        // ATC_API_SEC010: Path has authenticationRequired=false but has roles/schemes
        if (pathAuthenticationRequired.HasValue &&
            !pathAuthenticationRequired.Value &&
            (pathAuthorizeRoles.Count > 0 || pathAuthenticationSchemes.Count > 0))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.PathAuthenticationConflict,
                $"Path '{pathKey}' has x-authentication-required set to false but has " +
                $"x-authorize-roles and/or x-authentication-schemes set.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        // ATC_API_SEC001: Path authorize role not defined in global section
        // ATC_API_SEC008: Path authorize role has incorrect casing vs global section
        foreach (var pathRole in pathAuthorizeRoles)
        {
            if (!globalAuthorizeRoles.Contains(pathRole, StringComparer.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PathAuthorizeRoleNotDefined,
                    $"Path '{pathKey}' has the role '{pathRole}' defined which is not " +
                    $"defined in the global x-authorize-roles section.",
                    DiagnosticSeverity.Error,
                    sourceFilePath));
            }
            else if (globalAuthorizeRoles.Contains(pathRole, StringComparer.OrdinalIgnoreCase) &&
                     !globalAuthorizeRoles.Contains(pathRole, StringComparer.Ordinal))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PathAuthorizeRoleCasing,
                    $"Path '{pathKey}' has the role '{pathRole}' defined, but is using " +
                    $"incorrect casing compared to the global x-authorize-roles section.",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }

        // ATC_API_SEC002: Path authentication scheme not defined in global section
        // ATC_API_SEC009: Path authentication scheme has incorrect casing vs global
        foreach (var pathScheme in pathAuthenticationSchemes)
        {
            if (!globalAuthenticationSchemes.Contains(pathScheme, StringComparer.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PathAuthenticationSchemeNotDefined,
                    $"Path '{pathKey}' has the authentication scheme '{pathScheme}' defined " +
                    $"which is not defined in the global x-authentication-schemes section.",
                    DiagnosticSeverity.Error,
                    sourceFilePath));
            }
            else if (globalAuthenticationSchemes.Contains(pathScheme, StringComparer.OrdinalIgnoreCase) &&
                     !globalAuthenticationSchemes.Contains(pathScheme, StringComparer.Ordinal))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PathAuthenticationSchemeCasing,
                    $"Path '{pathKey}' has the authentication scheme '{pathScheme}' defined, " +
                    $"but is using incorrect casing compared to the global x-authentication-schemes section.",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }
    }

    /// <summary>
    /// Validates operation-level security configuration.
    /// </summary>
    private static void ValidateOperationSecurity(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string pathKey,
        OpenApiOperation operation,
        List<string> globalAuthorizeRoles,
        List<string> globalAuthenticationSchemes)
    {
        if (operation.Extensions is null || operation.Extensions.Count == 0)
        {
            return;
        }

        var operationName = operation.OperationId ?? $"operation at {pathKey}";
        var operationAuthenticationRequired = operation.Extensions.ExtractAuthenticationRequired();
        var operationAuthorizeRoles = operation.Extensions.ExtractAuthorizeRoles();
        var operationAuthenticationSchemes = operation.Extensions.ExtractAuthenticationSchemes();

        // ATC_API_SEC005: Operation has authenticationRequired=false but has roles/schemes
        if (operationAuthenticationRequired.HasValue &&
            !operationAuthenticationRequired.Value &&
            (operationAuthorizeRoles.Count > 0 || operationAuthenticationSchemes.Count > 0))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.OperationAuthenticationConflict,
                $"Operation '{operationName}' has x-authentication-required set to false but has " +
                $"x-authorize-roles and/or x-authentication-schemes set.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        // ATC_API_SEC003: Operation authorize role not defined in global section
        // ATC_API_SEC006: Operation authorize role has incorrect casing vs global section
        foreach (var operationRole in operationAuthorizeRoles)
        {
            if (!globalAuthorizeRoles.Contains(operationRole, StringComparer.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.OperationAuthorizeRoleNotDefined,
                    $"Operation '{operationName}' has the role '{operationRole}' defined which is not " +
                    $"defined in the global x-authorize-roles section.",
                    DiagnosticSeverity.Error,
                    sourceFilePath));
            }
            else if (globalAuthorizeRoles.Contains(operationRole, StringComparer.OrdinalIgnoreCase) &&
                     !globalAuthorizeRoles.Contains(operationRole, StringComparer.Ordinal))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.OperationAuthorizeRoleCasing,
                    $"Operation '{operationName}' has the role '{operationRole}' defined, but is using " +
                    $"incorrect casing compared to the global x-authorize-roles section.",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }

        // ATC_API_SEC004: Operation authentication scheme not defined in global section
        // ATC_API_SEC007: Operation authentication scheme has incorrect casing vs global
        foreach (var operationScheme in operationAuthenticationSchemes)
        {
            if (!globalAuthenticationSchemes.Contains(operationScheme, StringComparer.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.OperationAuthenticationSchemeNotDefined,
                    $"Operation '{operationName}' has the authentication scheme '{operationScheme}' defined " +
                    $"which is not defined in the global x-authentication-schemes section.",
                    DiagnosticSeverity.Error,
                    sourceFilePath));
            }
            else if (globalAuthenticationSchemes.Contains(operationScheme, StringComparer.OrdinalIgnoreCase) &&
                     !globalAuthenticationSchemes.Contains(operationScheme, StringComparer.Ordinal))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.OperationAuthenticationSchemeCasing,
                    $"Operation '{operationName}' has the authentication scheme '{operationScheme}' defined, " +
                    $"but is using incorrect casing compared to the global x-authentication-schemes section.",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }
    }

    /// <summary>
    /// Validates that every 'security' requirement, the document's and the operations', names a scheme that
    /// 'components/securitySchemes' declares (ATC_API_SEC012): the generated endpoint requires an authentication
    /// scheme by that name, which no host registers for a typo, so every request on it would fail at run time.
    /// </summary>
    private static List<DiagnosticMessage> ValidateSecuritySchemeReferences(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();
        var declared = document.Components?.SecuritySchemes?.Keys.ToList() ?? [];
        var declaredText = declared.Count == 0 ? "none" : string.Join(", ", declared.Select(n => $"'{n}'"));

        void Check(
            IList<OpenApiSecurityRequirement>? requirements,
            string where)
        {
            foreach (var requirement in requirements ?? [])
            {
                foreach (var scheme in requirement.Keys)
                {
                    var name = scheme.Reference?.Id ?? scheme.Name ?? string.Empty;
                    if (!declared.Contains(name, StringComparer.Ordinal))
                    {
                        diagnostics.Add(new DiagnosticMessage(
                            RuleIdentifiers.SecuritySchemeNotDeclared,
                            $"The security requirement of {where} names the scheme '{name}', which components/securitySchemes does not declare (declared: {declaredText}). The endpoint would require an authentication scheme no host registers.",
                            DiagnosticSeverity.Error,
                            sourceFilePath,
                            Context: $"Security: {name}",
                            Suggestions: ["Name a declared securityScheme, or declare the scheme under components/securitySchemes"]));
                    }
                }
            }
        }

        Check(document.Security, "the document");
        foreach (var path in document.Paths ?? [])
        {
            foreach (var operation in path.Value?.Operations ?? [])
            {
                Check(operation.Value?.Security, $"operation '{operation.Value?.OperationId ?? $"{operation.Key.Method.ToUpperInvariant()} {path.Key}"}'");
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Emits ATC_API_SEC011 (Info) for each mutualTLS security scheme — the generator produces a policy
    /// constant but no HTTP credential injection; certificate must be configured at transport level.
    /// </summary>
    private static void ValidateMutualTlsSchemes(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        if (document.Components?.SecuritySchemes is null)
        {
            return;
        }

        foreach (var kvp in document.Components.SecuritySchemes)
        {
            if (kvp.Value.Type != Microsoft.OpenApi.SecuritySchemeType.MutualTLS)
            {
                continue;
            }

            diagnostics.Add(new DiagnosticMessage(
                RuleId: RuleIdentifiers.MutualTlsSchemeNoCertInjection,
                Message: $"Security scheme '{kvp.Key}' uses mutualTLS — no HTTP credential is injected by the generator. " +
                         "Configure the client certificate at the HttpClient transport level (e.g., via X509Certificate2 on HttpClientHandler).",
                Severity: DiagnosticSeverity.Info,
                FilePath: sourceFilePath));
        }
    }
}