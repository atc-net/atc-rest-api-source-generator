// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable CommentTypo
// ReSharper disable InvertIf
// ReSharper disable LoopCanBeConvertedToQuery
namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// Validates OpenAPI documents according to the configured strategy.
/// Returns DiagnosticMessage objects (Roslyn-free) instead of Roslyn Diagnostic objects.
/// </summary>
public static class OpenApiDocumentValidator
{
    private static readonly string[] PaginationPropertyNames = ["items", "results", "data", "records", "values"];

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
    /// Validates an OpenAPI document according to the specified strategy.
    /// </summary>
    /// <param name="strategy">The validation strategy to use.</param>
    /// <param name="document">The OpenAPI document to validate.</param>
    /// <param name="diagnosticErrors">Diagnostic errors from parsing (used in Standard validation).</param>
    /// <param name="sourceFilePath">Path to the source OpenAPI file for error reporting.</param>
    /// <returns>List of diagnostic messages to report.</returns>
    public static List<DiagnosticMessage> Validate(
        ValidateSpecificationStrategy strategy,
        OpenApiDocument document,
        IList<OpenApiError> diagnosticErrors,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (strategy == ValidateSpecificationStrategy.None)
        {
            return diagnostics; // Skip all validation
        }

        // Standard validation: Report Microsoft.OpenApi parsing errors + schema reference validation
        if (strategy >= ValidateSpecificationStrategy.Standard)
        {
            diagnostics.AddRange(ValidateStandard(strategy, diagnosticErrors, document, sourceFilePath));
        }

        // Strict validation: Standard + custom ATC rules
        if (strategy == ValidateSpecificationStrategy.Strict)
        {
            diagnostics.AddRange(ValidateStrict(document, sourceFilePath));
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates using Microsoft.OpenApi library errors (Standard level).
    /// </summary>
    private static List<DiagnosticMessage> ValidateStandard(
        ValidateSpecificationStrategy strategy,
        IList<OpenApiError> diagnosticErrors,
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (diagnosticErrors.Count > 0)
        {
            var isOpenApi32 = document.GetOpenApiSpecVersion() == OpenApiSpecVersion.OpenApi3_2;

            foreach (var error in diagnosticErrors)
            {
                // In OpenAPI 3.2, discriminator.propertyName is optional.
                // Suppress the parser's 3.0/3.1 rule "discriminator property must be in required"
                // when the discriminator at that path genuinely has no propertyName.
                if (isOpenApi32 &&
                    error.Pointer is not null &&
                    error.Pointer.EndsWith("/discriminator", StringComparison.Ordinal) &&
                    error.Message.Contains("discriminator", StringComparison.OrdinalIgnoreCase) &&
                    error.Message.Contains("required", StringComparison.OrdinalIgnoreCase) &&
                    IsDiscriminatorWithoutPropertyName(document, error.Pointer))
                {
                    continue;
                }

                diagnostics.Add(DiagnosticBuilder.ParsingError(
                    error.Message,
                    error.Pointer,
                    sourceFilePath));
            }
        }

        // ATCAPI_SCH013: Validate schema references (fundamental error that breaks code generation)
        diagnostics.AddRange(ValidateSchemaReferences(document, sourceFilePath));

        // ATC_API_SCH018: Detect schema-name sanitization collisions (also breaks code generation)
        diagnostics.AddRange(ValidateSchemaNameCollisions(document, sourceFilePath));

        // ATC_API_SCH019: Warn on anonymous inline schema in components.mediaTypes
        ValidateComponentsMediaTypes(diagnostics, sourceFilePath, document);

        // ATC_API_SCH020: Warn when discriminator block lacks propertyName and auto-detect fails
        ValidateDiscriminatorPropertyNames(diagnostics, sourceFilePath, document);

        // ATC_API_SEC011: Info when a mutualTLS security scheme is declared
        ValidateMutualTlsSchemes(diagnostics, sourceFilePath, document);

        // ATC_API_STREAM001: Info when a streaming media type has unsupported prefixEncoding
        ValidateStreamingEncodings(diagnostics, sourceFilePath, document);

        // ATC_API_RL001/RL002: Warn on rate-limit partitioning that is silently ignored
        ValidateRateLimitPartitioning(diagnostics, sourceFilePath, document);

        // ATC_API_RL003: Warn when one policy name is declared with conflicting settings
        ValidateRateLimitPolicyConflicts(diagnostics, sourceFilePath, document);

        // ATC_API_RL004: Error when distinct policy names collide on the generated constant
        ValidateRateLimitPolicyNameCollisions(diagnostics, sourceFilePath, document);

        // ATC_API_RL005/RL006/RL008: Warn on values and placements the runtime rejects or ignores
        ValidateRateLimitValuesAndPlacement(diagnostics, sourceFilePath, document);

        // ATC_API_RL007: Info when the algorithm cannot supply a Retry-After value
        ValidateRateLimitRetryAfterSupport(diagnostics, sourceFilePath, document);

        // ATC_API_OPR001: Warn when operationId is missing (Standard only — Strict mode reports it as an Error per-operation)
        if (strategy == ValidateSpecificationStrategy.Standard)
        {
            diagnostics.AddRange(ValidateMissingOperationIds(document, sourceFilePath));
        }

        return diagnostics;
    }

    /// <summary>
    /// Warns (Standard level) when an operation does not declare an <c>operationId</c>.
    /// The generator synthesises a name from the HTTP method and path, which tends to be
    /// long and unreadable. This warning surfaces the issue before the user encounters
    /// cryptic CS0234/CS0246 compilation errors in the generated code.
    /// </summary>
    private static List<DiagnosticMessage> ValidateMissingOperationIds(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.Paths is null)
        {
            return diagnostics;
        }

        foreach (var pathEntry in document.Paths)
        {
            var pathKey = pathEntry.Key;

            if (pathEntry.Value is not IOpenApiPathItem pathItem || pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operationEntry in pathItem.Operations)
            {
                var opValue = operationEntry.Value;
                if (opValue is null || !string.IsNullOrEmpty(opValue.OperationId))
                {
                    continue;
                }

                var httpMethodLower = operationEntry.Key.ToString().ToLowerInvariant();
                var httpMethodUpper = httpMethodLower.ToUpperInvariant();

                // Compute the synthetic class name the generator will use
                // (mirrors EndpointPerOperationExtractor / HttpClientExtractor logic)
                var normalizedPath = pathKey
                    .Replace("/", "_")
                    .Replace("{", string.Empty)
                    .Replace("}", string.Empty);
                var syntheticId = $"{httpMethodLower}{normalizedPath}";
                var syntheticClassName = CasingHelper.ToPascalCase(syntheticId);

                // Build a concise suggested operationId from the last non-parameter path segment
                var lastSegment = pathKey
                    .Split('/')
                    .LastOrDefault(s => s.Length > 0 && !s.StartsWith("{", StringComparison.Ordinal));
                var suggestedId = lastSegment is not null
                    ? $"{httpMethodLower}{CasingHelper.ToPascalCase(lastSegment)}"
                    : syntheticId;

                diagnostics.Add(DiagnosticBuilder.MissingOperationIdWarning(
                    httpMethodUpper,
                    pathKey,
                    syntheticClassName,
                    suggestedId,
                    sourceFilePath));
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates using custom ATC rules (Strict level).
    /// </summary>
    private static List<DiagnosticMessage> ValidateStrict(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        // Check OpenAPI version (must be 3.0.x)
        diagnostics.AddRange(ValidateOpenApiVersion(document, sourceFilePath));

        // Naming convention validations
        diagnostics.AddRange(ValidateNamingConventions(document, sourceFilePath));

        // Security validations
        diagnostics.AddRange(ValidateSecurityConfiguration(document, sourceFilePath));

        // Schema validations
        diagnostics.AddRange(ValidateSchemas(document, sourceFilePath));

        // Path validations
        diagnostics.AddRange(ValidatePaths(document, sourceFilePath));

        // Operation validations
        diagnostics.AddRange(ValidateOperations(document, sourceFilePath));

        // Server validations
        diagnostics.AddRange(ValidateServers(document, sourceFilePath));

        // Webhook validations (OpenAPI 3.1)
        diagnostics.AddRange(ValidateWebhooks(document, sourceFilePath));

        return diagnostics;
    }

    /// <summary>
    /// Validates the OpenAPI specification version (ATCAPI_VAL002: OpenAPI 2.0 not supported).
    /// Recognizes 3.0 / 3.1 / 3.2 as supported and rejects Swagger/OpenAPI 2.0.
    /// The spec version is read from the parsed document's metadata (see
    /// <c>OpenApiDocument.GetOpenApiSpecVersion()</c>), not from <c>info.version</c>
    /// (which is the API's own semantic version).
    /// </summary>
    private static List<DiagnosticMessage> ValidateOpenApiVersion(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.GetOpenApiSpecVersion() == OpenApiSpecVersion.OpenApi2_0)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.OpenApi20NotSupported,
                "OpenAPI 2.0 (Swagger) is not supported. Please use OpenAPI 3.0.x, 3.1.x or 3.2.x.",
                DiagnosticSeverity.Error,
                sourceFilePath));
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates naming conventions (ATCAPI_NAM001-006).
    /// </summary>
    private static List<DiagnosticMessage> ValidateNamingConventions(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        // Get all operations from the document
        var allOperations = document.GetAllOperations();

        foreach (var (path, httpMethod, operation) in allOperations)
        {
            // ATCAPI_NAM001: OperationId must start with lowercase letter (camelCase).
            // Only validate when the user explicitly declared an operationId — do NOT run this
            // check against synthetic names produced by GetOperationId(), because those start
            // with the uppercase HTTP method (e.g. "GET_...") and would fire a spurious NAM001
            // warning that masks the real OPR001 error emitted by ValidateOperation().
            var operationId = operation.OperationId;
            if (!string.IsNullOrWhiteSpace(operationId))
            {
                var firstChar = operationId![0];
                if (char.IsLetter(firstChar) && char.IsUpper(firstChar))
                {
                    var suggestedName = $"{char.ToLowerInvariant(firstChar)}{operationId.Substring(1)}";
                    diagnostics.Add(DiagnosticBuilder.OperationIdCasingWarning(
                        operationId,
                        suggestedName,
                        httpMethod,
                        path,
                        sourceFilePath));
                }
            }

            // ATCAPI_NAM004: Parameter name must use camelCase
            // Note: Header parameters are excluded because HTTP headers traditionally use hyphenated names (e.g., X-Continuation, Content-Type)
            if (operation.Parameters is not null)
            {
                foreach (var parameter in operation.Parameters)
                {
                    // Skip header parameters - they follow HTTP header naming conventions (hyphenated), not camelCase
                    if (parameter.In == ParameterLocation.Header)
                    {
                        continue;
                    }

                    var paramName = parameter.Name;
                    if (!string.IsNullOrWhiteSpace(paramName) && !CasingHelper.IsCamelCase(paramName))
                    {
                        var suggested = CasingHelper.SuggestCamelCase(paramName!);
                        diagnostics.Add(DiagnosticBuilder.NamingConventionWarning(
                            RuleIdentifiers.ParameterNameMustBeCamelCase,
                            "Parameter",
                            paramName!,
                            "camelCase",
                            suggested,
                            $"{httpMethod.ToUpperInvariant()} {path}/parameters/{paramName}",
                            sourceFilePath));
                    }
                }
            }

            // ATCAPI_NAM006: Tag name must use kebab-case
            if (operation.Tags is not null)
            {
                foreach (var tag in operation.Tags)
                {
                    var tagName = tag.Name;
                    if (!string.IsNullOrWhiteSpace(tagName) && !CasingHelper.IsKebabCase(tagName))
                    {
                        var suggested = CasingHelper.SuggestKebabCase(tagName!);
                        diagnostics.Add(DiagnosticBuilder.NamingConventionWarning(
                            RuleIdentifiers.TagNameMustBeKebabCase,
                            "Tag",
                            tagName!,
                            "kebab-case",
                            suggested,
                            $"{httpMethod.ToUpperInvariant()} {path}/tags/{tagName}",
                            sourceFilePath));
                    }
                }
            }
        }

        // ATCAPI_NAM002: Model name must use PascalCase
        // ATCAPI_NAM003: Property name must use camelCase
        // ATCAPI_NAM005: Enum value must use PascalCase or UPPER_SNAKE_CASE
        diagnostics.AddRange(ValidateSchemaNameConventions(document, sourceFilePath));

        // Also validate global tags
        diagnostics.AddRange(ValidateGlobalTagNamingConventions(document, sourceFilePath));

        return diagnostics;
    }

    /// <summary>
    /// Validates schema naming conventions (ATCAPI_NAM002, ATCAPI_NAM003, ATCAPI_NAM005).
    /// </summary>
    private static List<DiagnosticMessage> ValidateSchemaNameConventions(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.Components?.Schemas is null)
        {
            return diagnostics;
        }

        foreach (var schemaEntry in document.Components.Schemas)
        {
            var schemaName = schemaEntry.Key;
            var schema = schemaEntry.Value;

            // ATCAPI_NAM002: Model name must use PascalCase
            if (!string.IsNullOrWhiteSpace(schemaName) && !CasingHelper.IsPascalCase(schemaName))
            {
                var suggested = CasingHelper.SuggestPascalCase(schemaName);
                diagnostics.Add(DiagnosticBuilder.NamingConventionWarning(
                    RuleIdentifiers.ModelNameMustBePascalCase,
                    "Model",
                    schemaName,
                    "PascalCase",
                    suggested,
                    $"#/components/schemas/{schemaName}",
                    sourceFilePath));
            }

            // Get the actual schema (handle references)
            var actualSchema = schema;
            if (schema is OpenApiSchemaReference { Target: not null } schemaRef)
            {
                actualSchema = schemaRef.Target;
            }

            // ATCAPI_NAM003: Property name must use camelCase
            if (actualSchema.Properties is not null)
            {
                foreach (var propertyEntry in actualSchema.Properties)
                {
                    var propertyName = propertyEntry.Key;
                    if (!string.IsNullOrWhiteSpace(propertyName) && !CasingHelper.IsCamelCase(propertyName))
                    {
                        var suggested = CasingHelper.SuggestCamelCase(propertyName);
                        diagnostics.Add(DiagnosticBuilder.NamingConventionWarning(
                            RuleIdentifiers.PropertyNameMustBeCamelCase,
                            "Property",
                            propertyName,
                            "camelCase",
                            suggested,
                            $"#/components/schemas/{schemaName}/properties/{propertyName}",
                            sourceFilePath));
                    }
                }
            }

            // ATCAPI_NAM005: Enum value must use PascalCase or UPPER_SNAKE_CASE
            if (actualSchema.Enum is { Count: > 0 })
            {
                foreach (var enumValue in actualSchema.Enum)
                {
                    var enumString = enumValue?.ToString();
                    if (!string.IsNullOrWhiteSpace(enumString) &&
                        !CasingHelper.IsPascalCase(enumString) &&
                        !CasingHelper.IsUpperSnakeCase(enumString))
                    {
                        var suggestedPascal = CasingHelper.SuggestPascalCase(enumString!);
                        diagnostics.Add(DiagnosticBuilder.NamingConventionWarning(
                            RuleIdentifiers.EnumValueCasing,
                            "Enum value",
                            enumString!,
                            "PascalCase or UPPER_SNAKE_CASE",
                            suggestedPascal,
                            $"#/components/schemas/{schemaName}/enum",
                            sourceFilePath));
                    }
                }
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates global tag naming conventions (ATCAPI_NAM006).
    /// </summary>
    private static List<DiagnosticMessage> ValidateGlobalTagNamingConventions(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.Tags is null)
        {
            return diagnostics;
        }

        foreach (var tag in document.Tags)
        {
            var tagName = tag.Name;
            if (!string.IsNullOrWhiteSpace(tagName) && !CasingHelper.IsKebabCase(tagName))
            {
                var suggested = CasingHelper.SuggestKebabCase(tagName!);
                diagnostics.Add(DiagnosticBuilder.NamingConventionWarning(
                    RuleIdentifiers.TagNameMustBeKebabCase,
                    "Global tag",
                    tagName!,
                    "kebab-case",
                    suggested,
                    "#/tags",
                    sourceFilePath));
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates security configuration (ATCAPI_SEC001-010).
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
            globalAuthorizeRoles.AddRange(ExtractAuthorizationRoles(document.Extensions));
            globalAuthenticationSchemes.AddRange(ExtractAuthenticationSchemes(document.Extensions));
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

        var pathAuthenticationRequired = ExtractAuthenticationRequired(pathItem.Extensions);
        var pathAuthorizeRoles = ExtractAuthorizationRoles(pathItem.Extensions);
        var pathAuthenticationSchemes = ExtractAuthenticationSchemes(pathItem.Extensions);

        // ATCAPI_SEC010: Path has authenticationRequired=false but has roles/schemes
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

        // ATCAPI_SEC001: Path authorize role not defined in global section
        // ATCAPI_SEC008: Path authorize role has incorrect casing vs global section
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

        // ATCAPI_SEC002: Path authentication scheme not defined in global section
        // ATCAPI_SEC009: Path authentication scheme has incorrect casing vs global
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
        var operationAuthenticationRequired = ExtractAuthenticationRequired(operation.Extensions);
        var operationAuthorizeRoles = ExtractAuthorizationRoles(operation.Extensions);
        var operationAuthenticationSchemes = ExtractAuthenticationSchemes(operation.Extensions);

        // ATCAPI_SEC005: Operation has authenticationRequired=false but has roles/schemes
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

        // ATCAPI_SEC003: Operation authorize role not defined in global section
        // ATCAPI_SEC006: Operation authorize role has incorrect casing vs global section
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

        // ATCAPI_SEC004: Operation authentication scheme not defined in global section
        // ATCAPI_SEC007: Operation authentication scheme has incorrect casing vs global
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
    /// Extracts the x-authentication-required boolean value from extensions.
    /// </summary>
    private static bool? ExtractAuthenticationRequired<TExtension>(
        IDictionary<string, TExtension> extensions)
        where TExtension : class
    {
        const string extensionKey = "x-authentication-required";

        if (!extensions.TryGetValue(extensionKey, out var extension) || extension is null)
        {
            return null;
        }

        // Try to get the value using reflection on Node property (JsonNodeExtension)
        var extensionType = extension.GetType();
        var nodeProperty = extensionType.GetProperty("Node");
        if (nodeProperty is not null)
        {
            var node = nodeProperty.GetValue(extension);
            if (node is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var boolValue))
            {
                return boolValue;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts the x-authorize-roles string array from extensions.
    /// </summary>
    private static List<string> ExtractAuthorizationRoles<TExtension>(
        IDictionary<string, TExtension> extensions)
        where TExtension : class
        => ExtractStringArrayExtension(extensions, "x-authorize-roles");

    /// <summary>
    /// Extracts the x-authentication-schemes string array from extensions.
    /// </summary>
    private static List<string> ExtractAuthenticationSchemes<TExtension>(
        IDictionary<string, TExtension> extensions)
        where TExtension : class
        => ExtractStringArrayExtension(extensions, "x-authentication-schemes");

    /// <summary>
    /// Extracts a string array from an OpenAPI extension.
    /// </summary>
    private static List<string> ExtractStringArrayExtension<TExtension>(
        IDictionary<string, TExtension> extensions,
        string extensionKey)
        where TExtension : class
    {
        var result = new List<string>();

        if (!extensions.TryGetValue(extensionKey, out var extension) || extension is null)
        {
            return result;
        }

        // Try to get the value using reflection on Node property (JsonNodeExtension)
        var extensionType = extension.GetType();
        var nodeProperty = extensionType.GetProperty("Node");
        if (nodeProperty is null)
        {
            return result;
        }

        var node = nodeProperty.GetValue(extension);
        if (node is not JsonArray jsonArray)
        {
            return result;
        }

        foreach (var item in jsonArray)
        {
            if (item is JsonValue jsonValue &&
                jsonValue.TryGetValue<string>(out var stringValue) &&
                !result.Contains(stringValue, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(stringValue);
            }
        }

        return result;
    }

    /// <summary>
    /// Validates schemas (ATCAPI_SCH001-012, ATCAPI_SCH014).
    /// </summary>
    private static List<DiagnosticMessage> ValidateSchemas(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.Components?.Schemas is null)
        {
            return diagnostics;
        }

        foreach (var schemaEntry in document.Components.Schemas)
        {
            var schemaName = schemaEntry.Key;
            var schema = schemaEntry.Value;

            // Get the actual schema (handle references)
            var actualSchema = schema;
            if (schema is OpenApiSchemaReference { Target: not null } schemaRef)
            {
                actualSchema = schemaRef.Target;
            }

            // ATCAPI_SCH014: Multiple non-null types (OpenAPI 3.1 type arrays)
            ValidateMultipleNonNullTypes(diagnostics, sourceFilePath, schemaName, actualSchema);

            // ATCAPI_SCH015: $ref with sibling properties (OpenAPI 3.1)
            ValidateRefWithSiblingProperties(diagnostics, sourceFilePath, schemaName, schema);

            // ATCAPI_SCH016: const value (JSON Schema 2020-12)
            ValidateConstValue(diagnostics, sourceFilePath, schemaName, actualSchema);

            // ATCAPI_SCH017: unevaluatedProperties (JSON Schema 2020-12)
            ValidateUnevaluatedProperties(diagnostics, sourceFilePath, schemaName, actualSchema);

            var schemaType = actualSchema.GetSchemaType();

            // Validate based on schema type
            if (schemaType == "array")
            {
                ValidateArraySchema(diagnostics, sourceFilePath, schemaName, actualSchema);
            }
            else if (schemaType == "object")
            {
                ValidateObjectSchema(diagnostics, sourceFilePath, schemaName, schema, actualSchema);
            }

            // ATCAPI_SCH008: Enum name not using correct casing style
            if (actualSchema.Enum is { Count: > 0 } &&
                !CasingHelper.IsPascalCase(schemaName))
            {
                var suggested = CasingHelper.SuggestPascalCase(schemaName);
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.EnumNameCasing,
                    $"Enum '{schemaName}' is not using PascalCase. " +
                    $"Suggestion: '{suggested}'. " +
                    $"Location: #/components/schemas/{schemaName}",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates that schemas with multiple non-null types (OpenAPI 3.1 feature) report a warning.
    /// </summary>
    private static void ValidateMultipleNonNullTypes(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string schemaName,
        IOpenApiSchema schema)
    {
        // Check if schema has multiple non-null types
        if (schema.HasMultipleNonNullTypes())
        {
            var typeNames = schema.GetAllNonNullTypeNames();
            var primaryType = schema.GetPrimaryNonNullType();
            var primaryTypeName = primaryType?.ToString()?.ToLowerInvariant() ?? "unknown";

            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.MultipleNonNullTypes,
                $"Schema '{schemaName}' has multiple non-null types [{string.Join(", ", typeNames)}]. " +
                $"Using primary type '{primaryTypeName}' for C# code generation. " +
                $"Location: #/components/schemas/{schemaName}",
                DiagnosticSeverity.Warning,
                sourceFilePath,
                LineNumber: null,
                ColumnNumber: null,
                Context: $"Schema: {schemaName}",
                Suggestions:
                [
                    "Consider using a single type instead of type array",
                    "Use oneOf/anyOf for polymorphic types"
                ]));
        }

        // Also check properties for multiple types
        if (schema is OpenApiSchema { Properties: not null } actualSchema)
        {
            foreach (var property in actualSchema.Properties)
            {
                var propName = property.Key;
                var propSchema = property.Value;

                // Resolve reference if needed
                var actualPropSchema = propSchema;
                if (propSchema is OpenApiSchemaReference { Target: not null } propRef)
                {
                    actualPropSchema = propRef.Target;
                }

                if (actualPropSchema.HasMultipleNonNullTypes())
                {
                    var typeNames = actualPropSchema.GetAllNonNullTypeNames();
                    var primaryType = actualPropSchema.GetPrimaryNonNullType();
                    var primaryTypeName = primaryType?.ToString()?.ToLowerInvariant() ?? "unknown";

                    diagnostics.Add(new DiagnosticMessage(
                        RuleIdentifiers.MultipleNonNullTypes,
                        $"Property '{propName}' in schema '{schemaName}' has multiple non-null types [{string.Join(", ", typeNames)}]. " +
                        $"Using primary type '{primaryTypeName}' for C# code generation. " +
                        $"Location: #/components/schemas/{schemaName}/properties/{propName}",
                        DiagnosticSeverity.Warning,
                        sourceFilePath,
                        LineNumber: null,
                        ColumnNumber: null,
                        Context: $"Property: {schemaName}.{propName}",
                        Suggestions:
                        [
                            "Consider using a single type instead of type array",
                            "Use oneOf/anyOf for polymorphic types"
                        ]));
                }
            }
        }
    }

    /// <summary>
    /// Validates that $ref schemas with sibling properties are detected (OpenAPI 3.1 info).
    /// </summary>
    private static void ValidateRefWithSiblingProperties(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string schemaName,
        IOpenApiSchema schema)
    {
        // Check if this is a reference with sibling properties
        if (schema.HasRefSiblingProperties())
        {
            var siblingProps = new List<string>();

            if (schema is OpenApiSchemaReference schemaRef)
            {
                if (!string.IsNullOrEmpty(schemaRef.Description))
                {
                    siblingProps.Add("description");
                }

                if (schemaRef.Deprecated)
                {
                    siblingProps.Add("deprecated");
                }

                if (schemaRef.Default is not null)
                {
                    siblingProps.Add("default");
                }
            }

            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.RefWithSiblingProperties,
                $"Schema '{schemaName}' uses $ref with sibling properties [{string.Join(", ", siblingProps)}]. " +
                $"This is an OpenAPI 3.1 feature. The sibling properties will override the referenced schema's properties. " +
                $"Location: #/components/schemas/{schemaName}",
                DiagnosticSeverity.Info,
                sourceFilePath,
                LineNumber: null,
                ColumnNumber: null,
                Context: $"Schema: {schemaName}",
                Suggestions:
                [
                    "This is supported - sibling properties override referenced schema properties",
                    "For OpenAPI 3.0 compatibility, move overrides to a separate schema using allOf"
                ]));
        }

        // Also check properties for $ref with siblings
        if (schema is OpenApiSchema { Properties: not null } actualSchema)
        {
            foreach (var property in actualSchema.Properties)
            {
                var propName = property.Key;
                var propSchema = property.Value;

                if (propSchema.HasRefSiblingProperties())
                {
                    var siblingProps = new List<string>();

                    if (propSchema is OpenApiSchemaReference propRef)
                    {
                        if (!string.IsNullOrEmpty(propRef.Description))
                        {
                            siblingProps.Add("description");
                        }

                        if (propRef.Deprecated)
                        {
                            siblingProps.Add("deprecated");
                        }

                        if (propRef.Default is not null)
                        {
                            siblingProps.Add("default");
                        }
                    }

                    diagnostics.Add(new DiagnosticMessage(
                        RuleIdentifiers.RefWithSiblingProperties,
                        $"Property '{propName}' in schema '{schemaName}' uses $ref with sibling properties [{string.Join(", ", siblingProps)}]. " +
                        $"This is an OpenAPI 3.1 feature. The sibling properties will override the referenced schema's properties. " +
                        $"Location: #/components/schemas/{schemaName}/properties/{propName}",
                        DiagnosticSeverity.Info,
                        sourceFilePath,
                        LineNumber: null,
                        ColumnNumber: null,
                        Context: $"Property: {schemaName}.{propName}",
                        Suggestions:
                        [
                            "This is supported - sibling properties override referenced schema properties",
                            "For OpenAPI 3.0 compatibility, move overrides to a separate schema using allOf"
                        ]));
                }
            }
        }
    }

    /// <summary>
    /// Validates schemas that use const value (JSON Schema 2020-12 feature).
    /// </summary>
    private static void ValidateConstValue(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string schemaName,
        IOpenApiSchema schema)
    {
        if (schema.HasConstValue())
        {
            var constValue = schema.GetConstValue();
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.SchemaUsesConstValue,
                $"Schema '{schemaName}' uses const value '{constValue}' (JSON Schema 2020-12). " +
                $"This value will be used as the default and only valid value for this property. " +
                $"Location: #/components/schemas/{schemaName}",
                DiagnosticSeverity.Info,
                sourceFilePath,
                LineNumber: null,
                ColumnNumber: null,
                Context: $"Schema: {schemaName}",
                Suggestions:
                [
                    $"The const value '{constValue}' will be used as a fixed value",
                    "Consider using enum with a single value for better OpenAPI 3.0 compatibility"
                ]));
        }

        // Also check properties for const values
        if (schema is OpenApiSchema { Properties: not null } actualSchema)
        {
            foreach (var property in actualSchema.Properties)
            {
                if (property.Value.HasConstValue())
                {
                    var constValue = property.Value.GetConstValue();
                    diagnostics.Add(new DiagnosticMessage(
                        RuleIdentifiers.SchemaUsesConstValue,
                        $"Property '{property.Key}' in schema '{schemaName}' uses const value '{constValue}' (JSON Schema 2020-12). " +
                        $"This value will be used as the default and only valid value for this property. " +
                        $"Location: #/components/schemas/{schemaName}/properties/{property.Key}",
                        DiagnosticSeverity.Info,
                        sourceFilePath,
                        LineNumber: null,
                        ColumnNumber: null,
                        Context: $"Property: {schemaName}.{property.Key}",
                        Suggestions:
                        [
                            $"The const value '{constValue}' will be used as a fixed value",
                            "Consider using enum with a single value for better OpenAPI 3.0 compatibility"
                        ]));
                }
            }
        }
    }

    /// <summary>
    /// Validates schemas that use unevaluatedProperties (JSON Schema 2020-12 feature).
    /// </summary>
    private static void ValidateUnevaluatedProperties(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string schemaName,
        IOpenApiSchema schema)
    {
        if (schema.HasUnevaluatedPropertiesRestriction())
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.UnevaluatedPropertiesNotSupported,
                $"Schema '{schemaName}' uses unevaluatedProperties: false (JSON Schema 2020-12). " +
                $"This restricts additional properties in composition but is not fully supported in code generation. " +
                $"Location: #/components/schemas/{schemaName}",
                DiagnosticSeverity.Warning,
                sourceFilePath,
                LineNumber: null,
                ColumnNumber: null,
                Context: $"Schema: {schemaName}",
                Suggestions:
                [
                    "unevaluatedProperties affects allOf/oneOf/anyOf composition validation",
                    "For code generation, additionalProperties: false provides similar behavior",
                    "Manual validation may be needed for strict enforcement"
                ]));
        }
    }

    private static void ValidateArraySchema(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string schemaName,
        IOpenApiSchema actualSchema)
    {
        // ATCAPI_SCH001: Missing title on array type
        if (string.IsNullOrEmpty(actualSchema.Title))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ArrayTitleMissing,
                $"Missing title on array type '#/components/schemas/{schemaName}'. " +
                $"Add a 'title' property to the schema.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
        else if (actualSchema.Title!.Length > 0 && char.IsLower(actualSchema.Title[0]))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ArrayTitleNotUppercase,
                $"Title on array type '{actualSchema.Title}' is not starting with uppercase. " +
                $"Location: #/components/schemas/{schemaName}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        // ATCAPI_SCH006: Object name not using correct casing style (applies to arrays too)
        if (!CasingHelper.IsPascalCase(schemaName))
        {
            var suggested = CasingHelper.SuggestPascalCase(schemaName);
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ObjectNameCasing,
                $"Schema '{schemaName}' is not using PascalCase. " +
                $"Suggestion: '{suggested}'. " +
                $"Location: #/components/schemas/{schemaName}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    private static void ValidateObjectSchema(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string schemaName,
        IOpenApiSchema schema,
        IOpenApiSchema actualSchema)
    {
        // ATCAPI_SCH003: Missing title on object type
        if (string.IsNullOrEmpty(actualSchema.Title))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ObjectTitleMissing,
                $"Missing title on object type '#/components/schemas/{schemaName}'. " +
                $"Add a 'title' property to the schema.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
        else if (actualSchema.Title!.Length > 0 && char.IsLower(actualSchema.Title[0]))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ObjectTitleNotUppercase,
                $"Title on object type '{actualSchema.Title}' is not starting with uppercase. " +
                $"Location: #/components/schemas/{schemaName}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        // ATCAPI_SCH006: Object name not using correct casing style
        if (!CasingHelper.IsPascalCase(schemaName))
        {
            var suggested = CasingHelper.SuggestPascalCase(schemaName);
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ObjectNameCasing,
                $"Schema '{schemaName}' is not using PascalCase. " +
                $"Suggestion: '{suggested}'. " +
                $"Location: #/components/schemas/{schemaName}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        // Validate properties
        if (actualSchema.Properties is not null)
        {
            foreach (var propertyEntry in actualSchema.Properties)
            {
                var propertyKey = propertyEntry.Key;
                var propertySchema = propertyEntry.Value;

                ValidateObjectProperty(diagnostics, sourceFilePath, schemaName, schema, propertyKey, propertySchema);
            }
        }
    }

    private static void ValidateObjectProperty(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string schemaName,
        IOpenApiSchema schema,
        string propertyKey,
        IOpenApiSchema propertySchema)
    {
        // ATCAPI_SCH012: Missing key/name for object property
        if (string.IsNullOrEmpty(propertyKey))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.PropertyKeyMissing,
                $"Missing key/name for one or more properties on object type '#/components/schemas/{schemaName}'.",
                DiagnosticSeverity.Error,
                sourceFilePath));
            return;
        }

        // ATCAPI_SCH007: Object property name not using correct casing style
        if (!CasingHelper.IsCamelCase(propertyKey))
        {
            var suggested = CasingHelper.SuggestCamelCase(propertyKey);
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.PropertyNameCasing,
                $"Property '{propertyKey}' in schema '{schemaName}' is not using camelCase. " +
                $"Suggestion: '{suggested}'. " +
                $"Location: #/components/schemas/{schemaName}/properties/{propertyKey}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        // Get actual property schema
        var actualPropertySchema = propertySchema;
        if (propertySchema is OpenApiSchemaReference { Target: not null } propRef)
        {
            actualPropertySchema = propRef.Target;
        }

        var propertyType = actualPropertySchema.GetSchemaType();

        // ATCAPI_SCH010: Implicit object definition on property not supported
        if (propertyType == "object")
        {
            if (!propertySchema.IsSchemaReference() &&
                actualPropertySchema.AdditionalProperties is null)
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.ImplicitObjectNotSupported,
                    $"Implicit object definition on property '{propertyKey}' in type '#/components/schemas/{schemaName}' is not supported. " +
                    $"Use a $ref to a named schema instead.",
                    DiagnosticSeverity.Error,
                    sourceFilePath));
            }
        }
        else if (propertyType == "array")
        {
            ValidateArrayProperty(diagnostics, sourceFilePath, schemaName, propertyKey, actualPropertySchema);
        }
    }

    private static void ValidateArrayProperty(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string schemaName,
        string propertyKey,
        IOpenApiSchema actualPropertySchema)
    {
        var items = actualPropertySchema.Items;

        // ATCAPI_SCH011: Array property missing items specification
        if (items is null)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ArrayPropertyMissingItems,
                $"Not specifying items for array property '{propertyKey}' in type '#/components/schemas/{schemaName}' is not supported. " +
                $"Add an 'items' specification.",
                DiagnosticSeverity.Error,
                sourceFilePath));
            return;
        }

        // Get actual items schema
        var actualItems = items;
        if (items is OpenApiSchemaReference { Target: not null } itemsRef)
        {
            actualItems = itemsRef.Target;
        }

        var itemsType = actualItems.GetSchemaType();

        // ATCAPI_SCH009: Array property missing data type specification
        if (string.IsNullOrEmpty(itemsType) && !IsSpecialPropertyName(propertyKey))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ArrayPropertyMissingType,
                $"Not specifying a data type for array property '{propertyKey}' in type '#/components/schemas/{schemaName}' is not supported. " +
                $"Add a type or $ref to the items specification.",
                DiagnosticSeverity.Error,
                sourceFilePath));
        }

        // ATCAPI_SCH005: Implicit object definition in array property not supported
        if (!string.IsNullOrEmpty(itemsType) &&
            itemsType == "object" &&
            !items.IsSchemaReference() &&
            !IsSimpleDataType(itemsType))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ImplicitArrayObjectNotSupported,
                $"Implicit object definition on property '{propertyKey}' in array type '#/components/schemas/{schemaName}' is not supported. " +
                $"Use a $ref to a named schema instead.",
                DiagnosticSeverity.Error,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Checks if a property name is a special name that doesn't require strict type checking.
    /// </summary>
    private static bool IsSpecialPropertyName(string propertyName)
        => string.Equals(propertyName, "items", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(propertyName, "result", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(propertyName, "results", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if a type is a simple data type.
    /// </summary>
    private static bool IsSimpleDataType(string? type)
    {
        if (string.IsNullOrEmpty(type))
        {
            return false;
        }

        return type is
            "string" or
            "integer" or
            "number" or
            "boolean";
    }

    /// <summary>
    /// Validates paths (ATCAPI_PTH001).
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

            // Check for balanced braces
            var openBraceCount = pathKey.Count(c => c == '{');
            var closeBraceCount = pathKey.Count(c => c == '}');

            if (openBraceCount != closeBraceCount)
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PathParametersNotWellFormatted,
                    $"Path '{pathKey}' has unbalanced braces: {openBraceCount} opening '{{', {closeBraceCount} closing '}}'.",
                    DiagnosticSeverity.Error,
                    sourceFilePath,
                    LineNumber: null,
                    ColumnNumber: null,
                    Context: pathKey,
                    Suggestions:
                    [
                        "Ensure each '{' has a matching '}'",
                        "Path parameters should be formatted as {parameterName}"
                    ]));
                continue;
            }

            if (pathKey.IndexOf("{}", StringComparison.Ordinal) >= 0)
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PathParametersNotWellFormatted,
                    $"Path '{pathKey}' contains empty parameter placeholder '{{}}'.",
                    DiagnosticSeverity.Error,
                    sourceFilePath,
                    LineNumber: null,
                    ColumnNumber: null,
                    Context: pathKey,
                    Suggestions:
                    [
                        "Provide a name for the path parameter (e.g., {id})"
                    ]));
            }

            // Check for nested braces {{param}}
            if (pathKey.IndexOf("{{", StringComparison.Ordinal) >= 0 ||
                pathKey.IndexOf("}}", StringComparison.Ordinal) >= 0)
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PathParametersNotWellFormatted,
                    $"Path '{pathKey}' contains nested or escaped braces which are not valid in OpenAPI paths.",
                    DiagnosticSeverity.Error,
                    sourceFilePath,
                    LineNumber: null,
                    ColumnNumber: null,
                    Context: pathKey,
                    Suggestions:
                    [
                        "Use single braces for path parameters (e.g., {id} not {{id}})"
                    ]));
            }

            // Check for proper parameter format using detailed validation
            if (!ValidatePathParameterFormat(pathKey, out var errorMessage))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PathParametersNotWellFormatted,
                    $"Path '{pathKey}' has malformed parameters: {errorMessage}",
                    DiagnosticSeverity.Error,
                    sourceFilePath,
                    LineNumber: null,
                    ColumnNumber: null,
                    Context: pathKey,
                    Suggestions:
                    [
                        "Path parameters should be formatted as {parameterName}",
                        "Parameter names should be valid identifiers (letters, digits, underscores)"
                    ]));
            }
        }

        return diagnostics;
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

            index = closeBrace + 1;
        }

        return true;
    }

    /// <summary>
    /// Validates operations (ATCAPI_OPR001-018).
    /// </summary>
    private static List<DiagnosticMessage> ValidateOperations(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.Paths is null)
        {
            return diagnostics;
        }

        foreach (var pathEntry in document.Paths)
        {
            var pathKey = pathEntry.Key;
            var pathItem = pathEntry.Value;

            // Get global path parameters
            var globalPathParameters = pathItem.Parameters?
                .Where(p => p.In == ParameterLocation.Path)
                .Select(p => p.Name)
                .ToList() ?? [];

            // ATCAPI_OPR011: Global path parameter not present in route
            foreach (var globalParam in globalPathParameters)
            {
                if (pathKey.IndexOf($"{{{globalParam}}}", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    diagnostics.Add(new DiagnosticMessage(
                        RuleIdentifiers.GlobalPathParameterNotInRoute,
                        $"Defined global path parameter '{globalParam}' does not exist in route '{pathKey}'.",
                        DiagnosticSeverity.Error,
                        sourceFilePath));
                }
            }

            // Get parameters from path
            var pathParametersFromRoute = GetParametersFromPath(pathKey);

            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operationEntry in pathItem.Operations)
            {
                var httpMethod = operationEntry
                    .Key
                    .ToString()
                    .ToLowerInvariant();

                var operation = operationEntry.Value;

                ValidateOperation(
                    diagnostics,
                    sourceFilePath,
                    document,
                    pathKey,
                    pathItem,
                    httpMethod,
                    operation,
                    globalPathParameters,
                    pathParametersFromRoute);
            }
        }

        return diagnostics;
    }

    private static void ValidateOperation(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document,
        string pathKey,
        IOpenApiPathItem pathItem,
        string httpMethod,
        OpenApiOperation operation,
        List<string?> globalPathParameters,
        List<string> pathParametersFromRoute)
    {
        var httpMethodUpper = httpMethod.ToUpperInvariant();

        // ATCAPI_OPR001: Missing operationId
        if (string.IsNullOrEmpty(operation.OperationId))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.OperationIdMissing,
                $"Missing operationId in path '{httpMethodUpper} {pathKey}'.",
                DiagnosticSeverity.Error,
                sourceFilePath));
            return;
        }

        var operationId = operation.OperationId!;

        // ATCAPI_OPR002: OperationId not using correct casing style
        if (!CasingHelper.IsValidOperationIdCasing(operationId))
        {
            var detectedStyle = CasingHelper.GetDetectedCasingStyle(operationId);
            var suggestedCamelCase = CasingHelper.SuggestCamelCase(operationId);

            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.OperationIdCasing,
                $"OperationId '{operationId}' is not using a valid casing style. " +
                $"Detected: {detectedStyle}. " +
                $"Expected: camelCase or kebab-case. " +
                $"Suggestion: '{suggestedCamelCase}'. " +
                $"Location: {httpMethodUpper} {pathKey}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        // ATCAPI_OPR003-007: HTTP method prefix validations
        ValidateOperationIdPrefix(diagnostics, sourceFilePath, httpMethod, operationId, pathKey);

        // ATCAPI_OPR008/009: Pluralization validation
        ValidateOperationIdPluralization(diagnostics, sourceFilePath, document, operation, operationId, pathKey, httpMethod);

        // ATCAPI_OPR010: BadRequest without parameters
        ValidateBadRequestResponse(diagnostics, sourceFilePath, pathItem, operation, operationId);

        // ATCAPI_OPR021: Unauthorized without security
        ValidateUnauthorizedResponse(diagnostics, sourceFilePath, document, pathItem, operation, operationId);

        // ATCAPI_OPR022: Forbidden without authorization
        ValidateForbiddenResponse(diagnostics, sourceFilePath, document, pathItem, operation, operationId);

        // ATCAPI_OPR023: NotFound on POST operation
        ValidateNotFoundResponse(diagnostics, sourceFilePath, httpMethod, pathKey, operation, operationId);

        // ATCAPI_OPR024: Conflict on non-mutating operation
        ValidateConflictResponse(diagnostics, sourceFilePath, httpMethod, operation, operationId);

        // ATCAPI_OPR025: TooManyRequests without rate limiting
        ValidateTooManyRequestsResponse(diagnostics, sourceFilePath, document, pathItem, operation, operationId);

        // ATCAPI_OPR012: Operation missing path parameter defined in route
        if (globalPathParameters.Count == 0 && pathParametersFromRoute.Count > 0)
        {
            var operationPathParams = operation.Parameters?
                .Where(p => p.In == ParameterLocation.Path)
                .Select(p => p.Name)
                .ToList() ?? [];

            foreach (var routeParam in pathParametersFromRoute)
            {
                if (!operationPathParams.Any(p => string.Equals(p, routeParam, StringComparison.OrdinalIgnoreCase)))
                {
                    diagnostics.Add(new DiagnosticMessage(
                        RuleIdentifiers.OperationMissingPathParameter,
                        $"Operation '{operationId}' in path '{pathKey}' does not define a parameter named '{routeParam}'.",
                        DiagnosticSeverity.Error,
                        sourceFilePath));
                }
            }
        }

        // ATCAPI_OPR013: Operation path parameter not present in route
        var opPathParams = operation.Parameters?
            .Where(p => p.In == ParameterLocation.Path)
            .ToList() ?? [];

        foreach (var pathParam in opPathParams)
        {
            if (pathKey.IndexOf($"{{{pathParam.Name}}}", StringComparison.OrdinalIgnoreCase) < 0)
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.OperationPathParameterNotInRoute,
                    $"Defined path parameter '{pathParam.Name}' does not exist in route '{pathKey}' for operation '{operationId}'.",
                    DiagnosticSeverity.Error,
                    sourceFilePath));
            }
        }

        // ATCAPI_OPR014: GET with path parameter missing NotFound response
        if (string.Equals(httpMethod, "get", StringComparison.OrdinalIgnoreCase))
        {
            var hasPathParam = (pathItem.Parameters?.Any(p => p.In == ParameterLocation.Path) ?? false) ||
                               (operation.Parameters?.Any(p => p.In == ParameterLocation.Path) ?? false);

            if (hasPathParam && !operation.HasNotFoundResponse())
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.GetMissingNotFoundResponse,
                    $"Missing NotFound (404) response type for operation '{operationId}', required by path parameter.",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }

        // ATCAPI_OPR015/016: Path parameter validation
        ValidatePathParameters(diagnostics, sourceFilePath, operation);

        // ATCAPI_OPR017: RequestBody with inline model not supported
        ValidateRequestBody(diagnostics, sourceFilePath, operation, operationId);

        // ATCAPI_OPR018: Multiple 2xx status codes not supported
        ValidateResponseStatusCodes(diagnostics, sourceFilePath, operation, operationId);

        // ATCAPI_OPR026: Parameter serialization not supported
        if (operation.Parameters is not null)
        {
            foreach (var parameter in operation.Parameters)
            {
                if (parameter is OpenApiParameter p)
                {
                    var serialization = p.GetParameterSerialization();
                    if (!serialization.IsSupported)
                    {
                        diagnostics.Add(DiagnosticBuilder.ParameterSerializationNotSupportedWarning(
                            p.Name ?? "(unnamed)",
                            $"style '{serialization.Style}' explode={serialization.Explode.ToString().ToLowerInvariant()} on {serialization.ValueKind}",
                            sourceFilePath));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates operationId prefix based on HTTP method.
    /// </summary>
    private static void ValidateOperationIdPrefix(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string httpMethod,
        string operationId,
        string pathKey)
    {
        var httpMethodUpper = httpMethod.ToUpperInvariant();

        if (string.Equals(httpMethod, "get", StringComparison.OrdinalIgnoreCase))
        {
            // ATCAPI_OPR003: GET operationId should start with 'Get' or 'List'
            if (!operationId.StartsWith("get", StringComparison.OrdinalIgnoreCase) &&
                !operationId.StartsWith("list", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.GetOperationIdPrefix,
                    $"OperationId '{operationId}' should start with 'get' or 'list' for GET operation. Location: {httpMethodUpper} {pathKey}",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }
        else if (string.Equals(httpMethod, "post", StringComparison.OrdinalIgnoreCase))
        {
            // ATCAPI_OPR004: POST operationId should not start with 'Delete'
            if (operationId.StartsWith("delete", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PostOperationIdPrefix,
                    $"OperationId '{operationId}' should not start with 'delete' for POST operation. Location: {httpMethodUpper} {pathKey}",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }
        else if (string.Equals(httpMethod, "put", StringComparison.OrdinalIgnoreCase))
        {
            // ATCAPI_OPR005: PUT operationId should start with 'Update'
            if (!operationId.StartsWith("update", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PutOperationIdPrefix,
                    $"OperationId '{operationId}' should start with 'update' for PUT operation. Location: {httpMethodUpper} {pathKey}",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }
        else if (string.Equals(httpMethod, "patch", StringComparison.OrdinalIgnoreCase))
        {
            // ATCAPI_OPR006: PATCH operationId should start with 'Patch' or 'Update'
            if (!operationId.StartsWith("patch", StringComparison.OrdinalIgnoreCase) &&
                !operationId.StartsWith("update", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PatchOperationIdPrefix,
                    $"OperationId '{operationId}' should start with 'patch' or 'update' for PATCH operation. Location: {httpMethodUpper} {pathKey}",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }
        else if (string.Equals(httpMethod, "delete", StringComparison.OrdinalIgnoreCase) &&
            !operationId.StartsWith("delete", StringComparison.OrdinalIgnoreCase) &&
            !operationId.StartsWith("remove", StringComparison.OrdinalIgnoreCase))
        {
            // ATCAPI_OPR007: DELETE operationId should start with 'Delete' or 'Remove'
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.DeleteOperationIdPrefix,
                $"OperationId '{operationId}' should start with 'delete' or 'remove' for DELETE operation. Location: {httpMethodUpper} {pathKey}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates operationId pluralization matches response type.
    /// </summary>
    private static void ValidateOperationIdPluralization(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document,
        OpenApiOperation operation,
        string operationId,
        string pathKey,
        string httpMethod)
    {
        var responseSchema = GetSuccessResponseSchema(operation);
        if (responseSchema is null)
        {
            return;
        }

        // Author assertion via `x-operation-response-cardinality: single|array` trumps the
        // name-based heuristic. The response-shape cross-check still runs, so an annotation that
        // disagrees with the actual response still surfaces a warning.
        var cardinalityAnnotation = operation.GetResponseCardinalityAnnotation();
        var isPluralized = cardinalityAnnotation switch
        {
            "single" => false,
            "array" => true,
            _ => IsOperationIdPluralized(operationId),
        };

        // Direct array or paginated response (applies to both OPR008 and OPR009)
        var isDirectArrayResponse = IsArraySchema(responseSchema, document) ||
                                    IsPaginatedSchema(responseSchema, document);

        // Wrapper object containing an array (only applies to OPR008)
        var isWrapperWithArray = IsObjectContainingArray(responseSchema, document);

        // ATCAPI_OPR008: Pluralized operationId but response is single item
        // Allow if response is direct array, paginated, OR wrapper containing array
        if (isPluralized && !isDirectArrayResponse && !isWrapperWithArray)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.OperationIdPluralizationMismatch,
                $"OperationId '{operationId}' is pluralized but response is a single item. " +
                $"Location: {httpMethod.ToUpperInvariant()} {pathKey}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        // ATCAPI_OPR009: Singular operationId but response is array
        // Only trigger for direct arrays or paginated responses, NOT for wrappers with arrays
        if (!isPluralized && isDirectArrayResponse && !isWrapperWithArray)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.OperationIdSingularMismatch,
                $"OperationId '{operationId}' is singular but response is an array. " +
                $"Location: {httpMethod.ToUpperInvariant()} {pathKey}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates BadRequest response has parameters.
    /// </summary>
    private static void ValidateBadRequestResponse(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        IOpenApiPathItem pathItem,
        OpenApiOperation operation,
        string operationId)
    {
        var hasBadRequest = operation.Responses?.ContainsKey("400") ?? false;
        if (!hasBadRequest)
        {
            return;
        }

        var hasParameters = (operation.Parameters?.Count > 0) ||
                           (operation.RequestBody is not null) ||
                           (pathItem.Parameters?.Count > 0);

        // ATCAPI_OPR010: Has BadRequest response but no parameters
        if (!hasParameters)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.BadRequestWithoutParameters,
                $"Operation '{operationId}' contains BadRequest (400) response but has no parameters or request body.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates 401 Unauthorized response has security requirements.
    /// </summary>
    private static void ValidateUnauthorizedResponse(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document,
        IOpenApiPathItem pathItem,
        OpenApiOperation operation,
        string operationId)
    {
        var hasUnauthorized = operation.Responses?.ContainsKey("401") ?? false;
        if (!hasUnauthorized)
        {
            return;
        }

        var securityConfig = operation.ExtractUnifiedSecurityConfiguration(pathItem, document);
        var hasSecurity = securityConfig is { AuthenticationRequired: true };

        // ATCAPI_OPR021: Has 401 Unauthorized but no security requirements
        if (!hasSecurity)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.UnauthorizedWithoutSecurity,
                $"Operation '{operationId}' defines 401 Unauthorized response but has no security requirements.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates 403 Forbidden response has authorization requirements (roles/policies/scopes).
    /// </summary>
    private static void ValidateForbiddenResponse(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document,
        IOpenApiPathItem pathItem,
        OpenApiOperation operation,
        string operationId)
    {
        var hasForbidden = operation.Responses?.ContainsKey("403") ?? false;
        if (!hasForbidden)
        {
            return;
        }

        var securityConfig = operation.ExtractUnifiedSecurityConfiguration(pathItem, document);
        var hasRolesOrPolicies = securityConfig is not null &&
            (securityConfig.Roles.Count > 0 ||
             securityConfig.Policies.Count > 0 ||
             securityConfig.Scopes.Count > 0);

        // ATCAPI_OPR022: Has 403 Forbidden but no authorization requirements
        if (!hasRolesOrPolicies)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ForbiddenWithoutAuthorization,
                $"Operation '{operationId}' defines 403 Forbidden response but has no authorization requirements (roles/policies/scopes).",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates 404 NotFound response is not on POST operation (unless it has path parameters).
    /// </summary>
    private static void ValidateNotFoundResponse(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string httpMethod,
        string pathKey,
        OpenApiOperation operation,
        string operationId)
    {
        var hasNotFound = operation.Responses?.ContainsKey("404") ?? false;
        if (!hasNotFound)
        {
            return;
        }

        // ATCAPI_OPR023: Has 404 NotFound on POST operation
        if (string.Equals(httpMethod, "post", StringComparison.OrdinalIgnoreCase))
        {
            // Skip warning if POST has path parameters - the referenced resource might not exist
            // Example: POST /devices/{deviceId}/scan - the device might not exist, so 404 is valid
            var hasPathParameters = pathKey.Contains('{') ||
                                    (operation.Parameters?.Any(p => p.In == ParameterLocation.Path) ?? false);
            if (hasPathParameters)
            {
                return;
            }

            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.NotFoundOnPostOperation,
                $"Operation '{operationId}' defines 404 NotFound response on POST operation - POST creates resources, so 'not found' is unusual.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates 409 Conflict response is on mutating operation (POST/PUT/PATCH/DELETE).
    /// </summary>
    private static void ValidateConflictResponse(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string httpMethod,
        OpenApiOperation operation,
        string operationId)
    {
        var hasConflict = operation.Responses?.ContainsKey("409") ?? false;
        if (!hasConflict)
        {
            return;
        }

        // ATCAPI_OPR024: Has 409 Conflict on read-only operation (GET)
        var isReadOnly = string.Equals(httpMethod, "get", StringComparison.OrdinalIgnoreCase);

        if (isReadOnly)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ConflictOnNonMutatingOperation,
                $"Operation '{operationId}' defines 409 Conflict response but operation is {httpMethod.ToUpperInvariant()} - conflicts typically occur during POST/PUT/PATCH/DELETE operations.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates 429 TooManyRequests response has rate limiting configured.
    /// </summary>
    private static void ValidateTooManyRequestsResponse(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document,
        IOpenApiPathItem pathItem,
        OpenApiOperation operation,
        string operationId)
    {
        var hasTooManyRequests = operation.Responses?.ContainsKey("429") ?? false;
        if (!hasTooManyRequests)
        {
            return;
        }

        var rateLimitConfig = operation.ExtractRateLimitConfiguration(pathItem, document);
        var hasRateLimiting = rateLimitConfig is not null;

        // ATCAPI_OPR025: Has 429 TooManyRequests but no rate limiting configured
        if (!hasRateLimiting)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.TooManyRequestsWithoutRateLimiting,
                $"Operation '{operationId}' defines 429 TooManyRequests response but no rate limiting is configured (x-ratelimit-* extensions).",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates path parameters have required=true and are not nullable.
    /// </summary>
    private static void ValidatePathParameters(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiOperation operation)
    {
        if (operation.Parameters is null)
        {
            return;
        }

        foreach (var parameter in operation.Parameters.Where(p => p.In == ParameterLocation.Path))
        {
            // ATCAPI_OPR015: Path parameter missing required=true
            if (!parameter.Required)
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PathParameterNotRequired,
                    $"Path parameter '{parameter.Name}' for operation '{operation.OperationId}' is missing required=true.",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }

            // ATCAPI_OPR016: Path parameter must not be nullable
            if (parameter.Schema is OpenApiSchema schema && schema.IsNullable())
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PathParameterNullable,
                    $"Path parameter '{parameter.Name}' for operation '{operation.OperationId}' must not be nullable.",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }
    }

    /// <summary>
    /// Validates request body is not inline model.
    /// </summary>
    private static void ValidateRequestBody(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiOperation operation,
        string operationId)
    {
        if (operation.RequestBody?.Content is null)
        {
            return;
        }

        foreach (var contentEntry in operation.RequestBody.Content)
        {
            var schema = contentEntry.Value.Schema;
            if (schema is null)
            {
                continue;
            }

            // Skip binary/file uploads
            if (schema is OpenApiSchema openApiSchema &&
                string.Equals(openApiSchema.Format, "binary", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // ATCAPI_OPR017: RequestBody with inline model not supported
            if (schema is not OpenApiSchemaReference &&
                schema is OpenApiSchema { Properties.Count: > 0 })
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.RequestBodyInlineModel,
                    $"RequestBody is defined with inline model for operation '{operationId}' - only reference to component schemas are supported.",
                    DiagnosticSeverity.Error,
                    sourceFilePath));
            }
        }
    }

    /// <summary>
    /// Validates operation does not have multiple 2xx response codes.
    /// </summary>
    private static void ValidateResponseStatusCodes(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiOperation operation,
        string operationId)
    {
        if (operation.Responses is null)
        {
            return;
        }

        var successResponses = operation.Responses.Keys
            .Where(k => k.StartsWith("2", StringComparison.Ordinal))
            .ToList();

        // ATCAPI_OPR018: Multiple 2xx status codes not supported
        if (successResponses.Count > 1)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.Multiple2xxStatusCodes,
                $"Operation '{operationId}' contains multiple 2xx status codes ({string.Join(", ", successResponses)}), which is not supported.",
                DiagnosticSeverity.Error,
                sourceFilePath));
        }
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
    /// Gets the success response schema (2xx) from an operation.
    /// </summary>
    private static IOpenApiSchema? GetSuccessResponseSchema(
        OpenApiOperation operation)
    {
        if (operation.Responses is null)
        {
            return null;
        }

        // Look for 200, 201, or any 2xx response
        foreach (var key in new[] { "200", "201", "202", "204" })
        {
            if (operation.Responses.TryGetValue(key, out var response))
            {
                var content = response.Content?.FirstOrDefault();
                return content?.Value?.Schema;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if an operationId signals a collection (plural) response. Considers all PascalCase
    /// words after the verb prefix, so connector patterns like `getJobsForDevice` or
    /// `listPathsByRepositoryName` are recognized as plural via the subject noun (`Jobs` / `Paths`),
    /// not the trailing qualifier (`Device` / `Name`).
    /// </summary>
    private static bool IsOperationIdPluralized(string operationId)
    {
        // Collection-intent prefixes (list/search/find) signal a collection response regardless of
        // the trailing noun — aligning with OPR003 which blesses `List` as the "GET array" prefix.
        // Also ensures `listUser` (collection prefix, singular body) is still caught by OPR008.
        foreach (var prefix in CollectionIntentPrefixes)
        {
            if (operationId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Remove common verb prefixes
        var name = operationId;
        var prefixStripped = false;

        foreach (var prefix in QueryVerbPrefixes)
        {
            if (operationId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                name = operationId.Substring(prefix.Length);
                prefixStripped = true;
                break;
            }
        }

        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (prefixStripped)
        {
            // Query-intent prefix stripped — analyse subject noun(s) to catch connector shapes
            // like `getJobsForDevice` (subject `Jobs`) or `getDeviceDetailsHistory` (suffix `Details`).
            var words = SplitPascalCaseWords(name);

            foreach (var word in words)
            {
                foreach (var suffix in SingleItemSuffixes)
                {
                    if (word.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            foreach (var word in words)
            {
                if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
                    !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
                    !word.EndsWith("us", StringComparison.OrdinalIgnoreCase) &&
                    !word.EndsWith("is", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        // No query-intent prefix recognized (e.g. `scanConfigurationsByDevice`, `createFoo`).
        // Keep the original trailing-noun heuristic to avoid flagging action operations whose
        // body contains an object noun in plural form.
        foreach (var suffix in SingleItemSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return name.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
               !name.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
               !name.EndsWith("us", StringComparison.OrdinalIgnoreCase) &&
               !name.EndsWith("is", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Splits a PascalCase identifier into its component words. Each uppercase letter starts a new
    /// word; leading lowercase characters form the first word.
    /// </summary>
    private static List<string> SplitPascalCaseWords(string input)
    {
        var words = new List<string>();
        if (string.IsNullOrEmpty(input))
        {
            return words;
        }

        var start = 0;
        for (var i = 1; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]))
            {
                words.Add(input.Substring(start, i - start));
                start = i;
            }
        }

        words.Add(input.Substring(start));
        return words;
    }

    /// <summary>
    /// Checks if a schema is an array type.
    /// </summary>
    private static bool IsArraySchema(
        IOpenApiSchema schema,
        OpenApiDocument document)
    {
        var actualSchema = schema;

        // Resolve reference
        if (schema is OpenApiSchemaReference schemaRef)
        {
            actualSchema = schemaRef.Target ?? schema;
        }

        if (actualSchema is OpenApiSchema openApiSchema)
        {
            return openApiSchema.Type?.ToString()?.Equals("array", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        return false;
    }

    /// <summary>
    /// Checks if a schema represents a paginated response (object with items/results array).
    /// </summary>
    private static bool IsPaginatedSchema(
        IOpenApiSchema schema,
        OpenApiDocument document)
    {
        // Check explicit x-pagination annotation first (authoritative when present)
        var annotation = schema.GetPaginationAnnotation();
        if (annotation.HasValue)
        {
            return annotation.Value;
        }

        var actualSchema = schema;

        // Resolve reference
        if (schema is OpenApiSchemaReference schemaRef)
        {
            actualSchema = schemaRef.Target ?? schema;

            // Also check annotation on the resolved target
            var targetAnnotation = actualSchema.GetPaginationAnnotation();
            if (targetAnnotation.HasValue)
            {
                return targetAnnotation.Value;
            }
        }

        if (actualSchema is not OpenApiSchema openApiSchema)
        {
            return false;
        }

        // Heuristic fallback: check direct properties if it's an object type
        if (openApiSchema.Type.HasValue &&
            openApiSchema.Type.Value.HasFlag(JsonSchemaType.Object) &&
            openApiSchema.Properties is not null)
        {
            foreach (var prop in openApiSchema.Properties)
            {
                if (PaginationPropertyNames.Contains(prop.Key, StringComparer.OrdinalIgnoreCase) &&
                    IsArraySchema(prop.Value, document))
                {
                    return true;
                }
            }
        }

        // Check allOf compositions (common in pagination patterns like PaginatedResult<T>)
        if (openApiSchema.AllOf is { Count: > 0 })
        {
            foreach (var allOfSchema in openApiSchema.AllOf)
            {
                if (IsPaginatedSchema(allOfSchema, document))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a schema is an object containing any array property.
    /// This handles wrapper response objects like ResendEventsResponse that contain arrays.
    /// </summary>
    private static bool IsObjectContainingArray(
        IOpenApiSchema schema,
        OpenApiDocument document)
    {
        var actualSchema = schema;

        if (schema is OpenApiSchemaReference schemaRef)
        {
            actualSchema = schemaRef.Target ?? schema;
        }

        if (actualSchema is not OpenApiSchema openApiSchema)
        {
            return false;
        }

        if (openApiSchema.Type.HasValue &&
            openApiSchema.Type.Value.HasFlag(JsonSchemaType.Object) &&
            openApiSchema.Properties is not null)
        {
            foreach (var prop in openApiSchema.Properties)
            {
                if (IsArraySchema(prop.Value, document))
                {
                    return true;
                }
            }
        }

        if (openApiSchema.AllOf is { Count: > 0 })
        {
            foreach (var allOfSchema in openApiSchema.AllOf)
            {
                if (IsObjectContainingArray(allOfSchema, document))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Validates servers (ATCAPI_SRV001).
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

        if (!document.HasWebhooks())
        {
            return diagnostics;
        }

        // Info: Webhooks detected
        var webhookCount = document.GetWebhooksCount();
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

    /// <summary>
    /// Validates that no two distinct schema names sanitize to the same C# identifier (ATC_API_SCH018).
    /// Such collisions would produce duplicate type definitions in the generated code.
    /// </summary>
    private static List<DiagnosticMessage> ValidateSchemaNameCollisions(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.Components?.Schemas is null)
        {
            return diagnostics;
        }

        // Group the original schema names by the C# identifier they sanitize to. Use the same
        // sanitization the generator applies so detection matches real generation behavior.
        var groupsByIdentifier = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var schemaName in document.Components.Schemas.Keys)
        {
            var sanitized = OpenApiSchemaExtensions.SanitizeSchemaName(schemaName);
            if (!groupsByIdentifier.TryGetValue(sanitized, out var names))
            {
                names = new List<string>();
                groupsByIdentifier[sanitized] = names;
            }

            names.Add(schemaName);
        }

        foreach (var group in groupsByIdentifier)
        {
            if (group.Value.Count <= 1)
            {
                continue;
            }

            var collidingNames = string.Join("', '", group.Value);
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.SchemaNameCollision,
                $"Schema names '{collidingNames}' all sanitize to the C# identifier '{group.Key}', which would produce duplicate type definitions. Rename all but one to avoid the collision.",
                DiagnosticSeverity.Error,
                sourceFilePath));
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates that components.mediaTypes entries do not wrap anonymous inline schemas (ATC_API_SCH019).
    /// An array whose items is a named $ref is acceptable and does NOT trigger the warning.
    /// </summary>
    private static void ValidateComponentsMediaTypes(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        if (document.Components?.MediaTypes is null)
        {
            return;
        }

        foreach (var mediaType in document.Components.MediaTypes)
        {
            var schema = mediaType.Value?.Schema;
            if (schema is null)
            {
                continue;
            }

            // Array with named-ref items is OK (e.g., type: array, items: {$ref: '#/components/schemas/Foo'}).
            // Only flag schemas that are truly anonymous (no $ref, no title).
            if (schema.GetSchemaType() == "array" && schema.Items is OpenApiSchemaReference)
            {
                continue;
            }

            if (schema is not OpenApiSchemaReference && string.IsNullOrEmpty(schema.Title))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleId: RuleIdentifiers.AnonymousInlineMediaTypeSchema,
                    Message: $"Reusable media type '{mediaType.Key}' in components.mediaTypes wraps an anonymous inline schema. " +
                             "Reference a named components.schemas entry instead to ensure a stable type name in generated code.",
                    Severity: DiagnosticSeverity.Warning,
                    FilePath: sourceFilePath));
            }
        }
    }

    /// <summary>
    /// Warns when a polymorphic schema has a discriminator block without 'propertyName' and
    /// no common string property can be auto-detected across variants (ATC_API_SCH020).
    /// </summary>
    /// <summary>
    /// Returns true when the discriminator at the given JSON pointer path
    /// has no propertyName — used to suppress parser errors that are only
    /// valid for OAS 3.0/3.1 but not 3.2 (where propertyName is optional).
    /// </summary>
    private static bool IsDiscriminatorWithoutPropertyName(
        OpenApiDocument document,
        string pointer)
    {
        // pointer format: #/components/schemas/{Name}/discriminator
        var parts = pointer.TrimStart('#').TrimStart('/').Split('/');
        if (parts.Length < 3 ||
            parts[0] != "components" ||
            parts[1] != "schemas")
        {
            return false;
        }

        var schemaName = parts[2];
        if (document.Components?.Schemas is null ||
            !document.Components.Schemas.TryGetValue(schemaName, out var schema))
        {
            return false;
        }

        return string.IsNullOrEmpty(schema.GetDiscriminatorPropertyName());
    }

    private static void ValidateDiscriminatorPropertyNames(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        if (document.Components?.Schemas is null)
        {
            return;
        }

        foreach (var schema in document.Components.Schemas)
        {
            var schemaValue = schema.Value;

            // Skip references — they are validated at their target
            if (schemaValue is OpenApiSchemaReference)
            {
                continue;
            }

            // Only check schemas with polymorphic composition and a discriminator block
            if (!schemaValue.HasPolymorphicComposition() || !schemaValue.HasDiscriminatorBlock())
            {
                continue;
            }

            // If propertyName is set, there is nothing to warn about
            if (!string.IsNullOrEmpty(schemaValue.GetDiscriminatorPropertyName()))
            {
                continue;
            }

            // Try auto-detect — if it succeeds, generation will proceed normally
            if (!string.IsNullOrEmpty(schemaValue.DetectDiscriminatorProperty(document)))
            {
                continue;
            }

            diagnostics.Add(new DiagnosticMessage(
                RuleId: RuleIdentifiers.DiscriminatorMissingPropertyName,
                Message: $"Schema '{schema.Key}' has a discriminator block without 'propertyName', " +
                         "and no common string property could be auto-detected across all polymorphic variants. " +
                         "Add 'propertyName' to the discriminator block for reliable polymorphic code generation.",
                Severity: DiagnosticSeverity.Warning,
                FilePath: sourceFilePath));
        }
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

    /// <summary>
    /// Emits ATC_API_RL004 (Error) when two or more distinct policy names sanitize to the same C#
    /// identifier. <c>RateLimitPoliciesExtractor</c> emits one constant per policy name with no
    /// de-duplication, so a collision produces duplicate members and the generated code does not compile.
    /// </summary>
    private static void ValidateRateLimitPolicyNameCollisions(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        var namesByIdentifier = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        void Collect(string? policyName)
        {
            if (string.IsNullOrEmpty(policyName))
            {
                return;
            }

            var identifier = PolicyNamingHelper.ToConstantName(policyName!);
            if (string.IsNullOrEmpty(identifier))
            {
                return;
            }

            if (!namesByIdentifier.TryGetValue(identifier, out var names))
            {
                names = new SortedSet<string>(StringComparer.Ordinal);
                namesByIdentifier[identifier] = names;
            }

            names.Add(policyName!);
        }

        Collect(document.Extensions.ExtractRateLimitPolicy());

        if (document.Paths is not null)
        {
            foreach (var pathEntry in document.Paths)
            {
                var pathItem = pathEntry.Value;
                Collect(pathItem.Extensions.ExtractRateLimitPolicy());

                if (pathItem.Operations is null)
                {
                    continue;
                }

                foreach (var operationEntry in pathItem.Operations)
                {
                    Collect(operationEntry.Value?.Extensions.ExtractRateLimitPolicy());
                }
            }
        }

        foreach (var group in namesByIdentifier.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (group.Value.Count <= 1)
            {
                continue;
            }

            var collidingNames = string.Join("', '", group.Value);
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.RateLimitPolicyNameCollision,
                $"Rate limit policy names '{collidingNames}' all sanitize to the C# identifier '{group.Key}', " +
                "which emits duplicate constants in the generated RateLimitPolicies class and breaks compilation. " +
                "Rename all but one to avoid the collision.",
                DiagnosticSeverity.Error,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Emits ATC_API_RL005 (Warning) for values the limiter constructor rejects, ATC_API_RL006 (Warning)
    /// when <c>x-ratelimit-enabled</c> is placed where it is not read, and ATC_API_RL008 (Info) when a
    /// window is declared on a concurrency policy that has no time component.
    /// </summary>
    private static void ValidateRateLimitValuesAndPlacement(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        var documentPolicy = document.Extensions.ExtractRateLimitPolicy();
        var documentAlgorithm = document.Extensions.ExtractRateLimitAlgorithm();

        ValidateRateLimitSite(diagnostics, sourceFilePath, document.Extensions, "document", documentAlgorithm);
        ValidateRateLimitEnabledPlacement(diagnostics, sourceFilePath, document.Extensions, "document", documentPolicy);

        if (document.Paths is null)
        {
            return;
        }

        foreach (var pathEntry in document.Paths)
        {
            var pathItem = pathEntry.Value;
            var pathLocation = $"path '{pathEntry.Key}'";
            var pathPolicy = pathItem.Extensions.ExtractRateLimitPolicy() ?? documentPolicy;
            var pathAlgorithm = pathItem.Extensions.ExtractRateLimitAlgorithm() ?? documentAlgorithm;

            ValidateRateLimitSite(diagnostics, sourceFilePath, pathItem.Extensions, pathLocation, pathAlgorithm);
            ValidateRateLimitEnabledPlacement(diagnostics, sourceFilePath, pathItem.Extensions, pathLocation, pathPolicy);

            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operationEntry in pathItem.Operations)
            {
                var operation = operationEntry.Value;
                if (operation is null)
                {
                    continue;
                }

                var location = $"operation '{operation.OperationId ?? operationEntry.Key.ToString()}'";
                var operationAlgorithm = operation.Extensions.ExtractRateLimitAlgorithm() ?? pathAlgorithm;

                ValidateRateLimitSite(diagnostics, sourceFilePath, operation.Extensions, location, operationAlgorithm);
            }
        }
    }

    /// <summary>
    /// Validates the numeric rate limit values declared at a single site, and reports a window declared
    /// on a concurrency policy.
    /// </summary>
    private static void ValidateRateLimitSite(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        IDictionary<string, IOpenApiExtension>? extensions,
        string location,
        string? effectiveAlgorithm)
    {
        if (extensions is null)
        {
            return;
        }

        var isConcurrency = OpenApiRateLimitExtensions.ParseAlgorithm(effectiveAlgorithm) == RateLimitAlgorithm.Concurrency;

        var permitLimit = extensions.ExtractPermitLimit();
        if (permitLimit is <= 0)
        {
            var hint = permitLimit == 0
                ? $" To switch rate limiting off, use '{RateLimitExtensionNameConstants.Enabled}: false' on the operation instead."
                : string.Empty;

            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.RateLimitValueOutOfRange,
                $"'{RateLimitExtensionNameConstants.PermitLimit}' is {permitLimit} on {location}, but the limiter requires " +
                $"a value greater than 0. The limiter constructor throws during AddApiRateLimiting, so the application " +
                $"fails to start.{hint}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        var queueLimit = extensions.ExtractQueueLimit();
        if (queueLimit is < 0)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.RateLimitValueOutOfRange,
                $"'{RateLimitExtensionNameConstants.QueueLimit}' is {queueLimit} on {location}, but the limiter requires " +
                "a value greater than or equal to 0. The limiter constructor throws during AddApiRateLimiting, so the " +
                "application fails to start.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        var windowSeconds = extensions.ExtractWindowSeconds();
        if (windowSeconds is null)
        {
            return;
        }

        if (isConcurrency)
        {
            // ConcurrencyLimiterOptions has no time component, so the value is dropped entirely -
            // it cannot be out of range because it is never used.
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.RateLimitWindowIgnoredForConcurrency,
                $"'{RateLimitExtensionNameConstants.WindowSeconds}' is declared on {location} but the effective algorithm " +
                "is 'concurrency', which limits simultaneous requests and has no time window. The value is ignored - " +
                "remove it, or switch to 'fixed', 'sliding' or 'token-bucket' if a time window is intended.",
                DiagnosticSeverity.Info,
                sourceFilePath));
            return;
        }

        if (windowSeconds <= 0)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.RateLimitValueOutOfRange,
                $"'{RateLimitExtensionNameConstants.WindowSeconds}' is {windowSeconds} on {location}, but the limiter " +
                "requires a value greater than 0. The limiter constructor throws during AddApiRateLimiting, so the " +
                "application fails to start.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Emits ATC_API_RL006 when <c>x-ratelimit-enabled: false</c> is declared somewhere the generator
    /// never reads it. Only an operation-level declaration produces <c>.DisableRateLimiting()</c>.
    /// </summary>
    private static void ValidateRateLimitEnabledPlacement(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        IDictionary<string, IOpenApiExtension>? extensions,
        string location,
        string? effectivePolicy)
    {
        if (extensions.ExtractRateLimitEnabled() != false || string.IsNullOrEmpty(effectivePolicy))
        {
            return;
        }

        diagnostics.Add(new DiagnosticMessage(
            RuleIdentifiers.RateLimitEnabledIgnoredOutsideOperation,
            $"'{RateLimitExtensionNameConstants.Enabled}: false' is declared on {location}, but the generator only " +
            $"honours it at operation level, so the endpoints covered by policy '{effectivePolicy}' remain rate limited. " +
            $"Declare '{RateLimitExtensionNameConstants.Enabled}: false' on each operation that should be exempt.",
            DiagnosticSeverity.Warning,
            sourceFilePath));
    }

    /// <summary>
    /// Emits ATC_API_RL007 (Info) once per policy whose algorithm cannot supply a Retry-After value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Verified against the runtime: a rejected <c>SlidingWindowRateLimiter</c> lease advertises the
    /// <c>RETRY_AFTER</c> metadata name but <c>TryGetMetadata</c> returns <c>false</c>, and
    /// <c>ConcurrencyLimiter</c> never lists it. The generated <c>OnRejected</c> is still emitted; its
    /// <c>if</c> simply never succeeds, so a 429 goes out with no header.
    /// </para>
    /// <para>
    /// This walks <c>CollectPolicies</c> rather than resolving per operation, because that is the exact
    /// first-wins set the generator turns into limiter registrations. Resolving per operation would
    /// describe an algorithm the generator never emits whenever a policy name is declared at several
    /// sites with differing algorithms (which ATC_API_RL003 reports separately).
    /// </para>
    /// </remarks>
    private static void ValidateRateLimitRetryAfterSupport(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        var policies = RateLimitPoliciesExtractor.CollectPolicies(document, includeDeprecated: false);

        foreach (var policyEntry in policies.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var config = policyEntry.Value;
            if (!config.Enabled || !config.EmitRetryAfter)
            {
                continue;
            }

            var algorithmName = config.Algorithm switch
            {
                RateLimitAlgorithm.Sliding => "sliding",
                RateLimitAlgorithm.Concurrency => "concurrency",
                _ => null,
            };

            if (algorithmName is null)
            {
                continue;
            }

            var reason = config.Algorithm == RateLimitAlgorithm.Concurrency
                ? "a concurrency limiter has no time component, so there is nothing to wait for"
                : "the sliding window limiter advertises the Retry-After metadata name but never attaches a value";

            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.RateLimitRetryAfterUnsupportedByAlgorithm,
                $"Policy '{policyEntry.Key}' uses the '{algorithmName}' algorithm, so no Retry-After header is sent on " +
                $"429 responses even though '{RateLimitExtensionNameConstants.EmitRetryAfter}' is enabled - {reason}. " +
                "Use 'fixed' or 'token-bucket' if clients depend on Retry-After, or supply a fallback through the " +
                "AddApiRateLimiting configure callback.",
                DiagnosticSeverity.Info,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Emits ATC_API_RL003 (Warning) when a single rate limit policy name is declared with conflicting
    /// settings at more than one site.
    /// </summary>
    /// <remarks>
    /// A policy name is the unit of limiter registration in <c>RateLimiterOptions</c>, so one name maps
    /// to exactly one limiter. <c>RateLimitPoliciesExtractor.CollectPolicies</c> is first-wins, so the
    /// losing declarations are silently discarded. Only settings that are <b>explicitly declared</b> at
    /// a site are compared - re-declaring a policy name on a sub-path without repeating every setting is
    /// idiomatic and contradicts nothing, so it must not warn.
    /// </remarks>
    private static void ValidateRateLimitPolicyConflicts(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        var sitesByPolicy = new Dictionary<string, List<(string Location, Dictionary<string, string> Declared)>>(StringComparer.Ordinal);

        var documentPolicy = document.Extensions.ExtractRateLimitPolicy();
        AddRateLimitDeclarationSite(sitesByPolicy, documentPolicy, "document", document.Extensions);

        if (document.Paths is not null)
        {
            foreach (var pathEntry in document.Paths)
            {
                var pathItem = pathEntry.Value;
                var pathPolicy = pathItem.Extensions.ExtractRateLimitPolicy() ?? documentPolicy;

                AddRateLimitDeclarationSite(sitesByPolicy, pathPolicy, $"path '{pathEntry.Key}'", pathItem.Extensions);

                if (pathItem.Operations is null)
                {
                    continue;
                }

                foreach (var operationEntry in pathItem.Operations)
                {
                    var operation = operationEntry.Value;
                    if (operation is null || operation.Extensions.ExtractRateLimitEnabled() == false)
                    {
                        continue;
                    }

                    var operationPolicy = operation.Extensions.ExtractRateLimitPolicy() ?? pathPolicy;
                    var location = $"operation '{operation.OperationId ?? operationEntry.Key.ToString()}'";

                    AddRateLimitDeclarationSite(sitesByPolicy, operationPolicy, location, operation.Extensions);
                }
            }
        }

        foreach (var policyEntry in sitesByPolicy.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (policyEntry.Value.Count < 2)
            {
                continue;
            }

            var conflictingKeys = policyEntry.Value
                .SelectMany(site => site.Declared.Select(setting => setting.Key))
                .Distinct(StringComparer.Ordinal)
                .Where(key => policyEntry.Value
                    .Where(site => site.Declared.ContainsKey(key))
                    .Select(site => site.Declared[key])
                    .Distinct(StringComparer.Ordinal)
                    .Count() > 1)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();

            foreach (var key in conflictingKeys)
            {
                var declarations = policyEntry.Value
                    .Where(site => site.Declared.ContainsKey(key))
                    .Select(site => $"'{site.Declared[key]}' at {site.Location}")
                    .ToList();

                diagnostics.Add(new DiagnosticMessage(
                    RuleId: RuleIdentifiers.RateLimitPolicyConflictingSettings,
                    Message: $"Rate limit policy '{policyEntry.Key}' is declared with conflicting '{key}' values: " +
                             $"{string.Join(", ", declarations)}. A policy name maps to a single limiter " +
                             "registration, so only the first declaration is used and the rest are ignored. " +
                             "Use identical values at every site, or split into separate policy names if the " +
                             "endpoints genuinely need different settings.",
                    Severity: DiagnosticSeverity.Warning,
                    FilePath: sourceFilePath));
            }
        }
    }

    /// <summary>
    /// Records the rate limit settings explicitly declared at a single site, keyed by effective policy name.
    /// Sites that declare no settings contribute nothing and cannot conflict.
    /// </summary>
    private static void AddRateLimitDeclarationSite(
        Dictionary<string, List<(string Location, Dictionary<string, string> Declared)>> sitesByPolicy,
        string? policyName,
        string location,
        IDictionary<string, IOpenApiExtension>? extensions)
    {
        if (string.IsNullOrEmpty(policyName))
        {
            return;
        }

        var declared = GetDeclaredRateLimitSettings(extensions);
        if (declared.Count == 0)
        {
            return;
        }

        if (!sitesByPolicy.TryGetValue(policyName!, out var sites))
        {
            sites = [];
            sitesByPolicy[policyName!] = sites;
        }

        sites.Add((location, declared));
    }

    /// <summary>
    /// Extracts the rate limit settings explicitly declared in the given extensions, normalized for
    /// comparison. Values parsed case-insensitively are lower-cased so that, for example,
    /// <c>user</c> and <c>User</c> do not read as a conflict.
    /// </summary>
    private static Dictionary<string, string> GetDeclaredRateLimitSettings(
        IDictionary<string, IOpenApiExtension>? extensions)
    {
        var declared = new Dictionary<string, string>(StringComparer.Ordinal);
        if (extensions is null)
        {
            return declared;
        }

        var permitLimit = extensions.ExtractPermitLimit();
        if (permitLimit.HasValue)
        {
            declared[RateLimitExtensionNameConstants.PermitLimit] = permitLimit.Value.ToString(NumberFormatInfo.InvariantInfo);
        }

        var windowSeconds = extensions.ExtractWindowSeconds();
        if (windowSeconds.HasValue)
        {
            declared[RateLimitExtensionNameConstants.WindowSeconds] = windowSeconds.Value.ToString(NumberFormatInfo.InvariantInfo);
        }

        var queueLimit = extensions.ExtractQueueLimit();
        if (queueLimit.HasValue)
        {
            declared[RateLimitExtensionNameConstants.QueueLimit] = queueLimit.Value.ToString(NumberFormatInfo.InvariantInfo);
        }

        var algorithm = extensions.ExtractRateLimitAlgorithm();
        if (!string.IsNullOrEmpty(algorithm))
        {
            declared[RateLimitExtensionNameConstants.Algorithm] = algorithm!.ToLowerInvariant();
        }

        var partition = extensions.ExtractRateLimitPartition();
        if (!string.IsNullOrEmpty(partition))
        {
            declared[RateLimitExtensionNameConstants.Partition] = partition!.ToLowerInvariant();
        }

        // Claim names are case-sensitive, so this one is compared verbatim.
        var partitionClaim = extensions.ExtractRateLimitPartitionClaim();
        if (!string.IsNullOrEmpty(partitionClaim))
        {
            declared[RateLimitExtensionNameConstants.PartitionClaim] = partitionClaim!;
        }

        var emitRetryAfter = extensions.ExtractRateLimitEmitRetryAfter();
        if (emitRetryAfter.HasValue)
        {
            declared[RateLimitExtensionNameConstants.EmitRetryAfter] = emitRetryAfter.Value
                ? "true"
                : "false";
        }

        return declared;
    }

    /// <summary>
    /// Emits ATC_API_RL001 (Warning) for every <c>x-ratelimit-partition</c> declaration whose value is
    /// not <c>global</c>, <c>ip</c> or <c>user</c>, and ATC_API_RL002 (Warning) for every
    /// <c>x-ratelimit-partition-claim</c> declaration that no operation in its scope can actually use.
    /// </summary>
    /// <remarks>
    /// Both fall back silently today: an unrecognized partition value degrades to <c>global</c> - one
    /// shared bucket for every caller, which is the exact failure mode partitioning exists to prevent -
    /// and a claim is only read when the effective partition is <c>user</c>.
    /// </remarks>
    private static void ValidateRateLimitPartitioning(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        ValidatePartitionValue(diagnostics, sourceFilePath, document.Extensions, "document");

        var documentPartition = document.Extensions.ExtractRateLimitPartition();
        var documentClaim = document.Extensions.ExtractRateLimitPartitionClaim();

        // Collects whether any operation anywhere resolves to user partitioning, which decides
        // whether a document-level claim is actually reachable.
        var anyOperationUsesUserPartition = false;

        if (document.Paths is not null)
        {
            foreach (var pathEntry in document.Paths)
            {
                var pathItem = pathEntry.Value;

                ValidatePartitionValue(diagnostics, sourceFilePath, pathItem.Extensions, $"path '{pathEntry.Key}'");

                var pathPartition = pathItem.Extensions.ExtractRateLimitPartition() ?? documentPartition;
                var pathClaim = pathItem.Extensions.ExtractRateLimitPartitionClaim();

                var anyOperationInPathUsesUserPartition = false;

                if (pathItem.Operations is not null)
                {
                    foreach (var operationEntry in pathItem.Operations)
                    {
                        var operation = operationEntry.Value;
                        if (operation is null)
                        {
                            continue;
                        }

                        var location = $"operation '{operation.OperationId ?? operationEntry.Key.ToString()}'";

                        ValidatePartitionValue(diagnostics, sourceFilePath, operation.Extensions, location);

                        var effectivePartition = operation.Extensions.ExtractRateLimitPartition() ?? pathPartition;
                        var usesUserPartition = OpenApiRateLimitExtensions.ParsePartitionStrategy(effectivePartition)
                                                == RateLimitPartitionStrategy.User;

                        anyOperationInPathUsesUserPartition |= usesUserPartition;
                        anyOperationUsesUserPartition |= usesUserPartition;

                        // An operation-level claim is scoped to exactly this operation.
                        if (!string.IsNullOrEmpty(operation.Extensions.ExtractRateLimitPartitionClaim()) && !usesUserPartition)
                        {
                            AddPartitionClaimIgnoredDiagnostic(diagnostics, sourceFilePath, location, effectivePartition);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(pathClaim) && !anyOperationInPathUsesUserPartition)
                {
                    AddPartitionClaimIgnoredDiagnostic(diagnostics, sourceFilePath, $"path '{pathEntry.Key}'", pathPartition);
                }
            }
        }

        if (!string.IsNullOrEmpty(documentClaim) && !anyOperationUsesUserPartition)
        {
            AddPartitionClaimIgnoredDiagnostic(diagnostics, sourceFilePath, "document", documentPartition);
        }
    }

    /// <summary>
    /// Emits ATC_API_RL001 when the extensions declare an <c>x-ratelimit-partition</c> value that the
    /// generator does not recognize.
    /// </summary>
    private static void ValidatePartitionValue(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        IDictionary<string, IOpenApiExtension>? extensions,
        string location)
    {
        var declaredValue = extensions.ExtractRateLimitPartition();
        if (string.IsNullOrEmpty(declaredValue))
        {
            return;
        }

        var normalized = declaredValue!.ToLowerInvariant();
        if (normalized is "global" or "ip" or "user")
        {
            return;
        }

        diagnostics.Add(new DiagnosticMessage(
            RuleId: RuleIdentifiers.RateLimitPartitionValueUnrecognized,
            Message: $"Unrecognized '{RateLimitExtensionNameConstants.Partition}' value '{declaredValue}' on {location}. " +
                     "Expected 'global', 'ip' or 'user'. The value falls back to 'global', which means one shared " +
                     "rate-limit bucket for all callers.",
            Severity: DiagnosticSeverity.Warning,
            FilePath: sourceFilePath));
    }

    /// <summary>
    /// Emits ATC_API_RL002 for a <c>x-ratelimit-partition-claim</c> declaration that cannot take effect.
    /// </summary>
    private static void AddPartitionClaimIgnoredDiagnostic(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string location,
        string? effectivePartition)
    {
        var effective = string.IsNullOrEmpty(effectivePartition)
            ? "global"
            : effectivePartition;

        diagnostics.Add(new DiagnosticMessage(
            RuleId: RuleIdentifiers.RateLimitPartitionClaimWithoutUserPartition,
            Message: $"'{RateLimitExtensionNameConstants.PartitionClaim}' is declared on {location} but the effective " +
                     $"'{RateLimitExtensionNameConstants.Partition}' is '{effective}', so the claim is ignored. " +
                     $"Set '{RateLimitExtensionNameConstants.Partition}: user' to partition by claim.",
            Severity: DiagnosticSeverity.Warning,
            FilePath: sourceFilePath));
    }

    /// <summary>
    /// Emits ATC_API_STREAM001 (Info) when any response media type declares <c>prefixEncoding</c>.
    /// The generator does not yet emit per-prefix encoding headers; the field is silently ignored.
    /// </summary>
    private static void ValidateStreamingEncodings(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        if (document.Paths is null)
        {
            return;
        }

        foreach (var pathEntry in document.Paths)
        {
            var pathItem = pathEntry.Value;
            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operationEntry in pathItem.Operations)
            {
                var operation = operationEntry.Value;
                if (operation.Responses is null)
                {
                    continue;
                }

                foreach (var response in operation.Responses.Values)
                {
                    if (response.Content is null)
                    {
                        continue;
                    }

                    foreach (var contentEntry in response.Content)
                    {
                        var mediaTypeKey = contentEntry.Key;
                        var mediaTypeValue = contentEntry.Value;

                        if (mediaTypeValue.PrefixEncoding is not { Count: > 0 })
                        {
                            continue;
                        }

                        diagnostics.Add(new DiagnosticMessage(
                            RuleId: RuleIdentifiers.StreamingPrefixEncodingUnsupported,
                            Message: $"Media type '{mediaTypeKey}' on operation '{operation.OperationId}' declares 'prefixEncoding' " +
                                     "which is not yet supported by the generator. The field will be ignored.",
                            Severity: DiagnosticSeverity.Info,
                            FilePath: sourceFilePath));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates schema references point to existing schemas (ATCAPI_SCH013).
    /// </summary>
    private static List<DiagnosticMessage> ValidateSchemaReferences(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.Paths is null)
        {
            return diagnostics;
        }

        // Check operation response schemas, request body schemas, and parameter schemas
        foreach (var pathEntry in document.Paths)
        {
            var pathKey = pathEntry.Key;
            var pathItem = pathEntry.Value;

            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operationEntry in pathItem.Operations)
            {
                var operationType = operationEntry
                    .Key
                    .ToString()
                    .ToLowerInvariant();

                var operation = operationEntry.Value;

                // Check response schemas
                if (operation.Responses is not null)
                {
                    foreach (var responseEntry in operation.Responses)
                    {
                        var statusCode = responseEntry.Key;
                        var response = responseEntry.Value;

                        if (response.Content is null)
                        {
                            continue;
                        }

                        foreach (var contentEntry in response.Content)
                        {
                            var contentType = contentEntry.Key;
                            var mediaType = contentEntry.Value;

                            ValidateSingleSchemaReference(
                                mediaType.Schema,
                                diagnostics,
                                sourceFilePath,
                                $"{pathKey}/{operationType}/responses/{statusCode}/content/{contentType}/schema");
                        }
                    }
                }

                // Check request body schemas
                if (operation.RequestBody?.Content is not null)
                {
                    foreach (var contentEntry in operation.RequestBody.Content)
                    {
                        var contentType = contentEntry.Key;
                        var mediaType = contentEntry.Value;

                        ValidateSingleSchemaReference(
                            mediaType.Schema,
                            diagnostics,
                            sourceFilePath,
                            $"{pathKey}/{operationType}/requestBody/content/{contentType}/schema");
                    }
                }

                // Check parameter schemas
                if (operation.Parameters is not null)
                {
                    foreach (var parameter in operation.Parameters)
                    {
                        ValidateSingleSchemaReference(
                            parameter.Schema,
                            diagnostics,
                            sourceFilePath,
                            $"{pathKey}/{operationType}/parameters/{parameter.Name}/schema");
                    }
                }
            }
        }

        // Check component schema properties for invalid references
        if (document.Components?.Schemas is not null)
        {
            foreach (var schemaEntry in document.Components.Schemas)
            {
                var schemaName = schemaEntry.Key;
                var schema = schemaEntry.Value;

                ValidateSchemaPropertiesReferences(
                    schema,
                    diagnostics,
                    sourceFilePath,
                    $"components/schemas/{schemaName}");
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates a single schema reference points to an existing schema.
    /// </summary>
    private static void ValidateSingleSchemaReference(
        IOpenApiSchema? schema,
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string path)
    {
        if (schema is null)
        {
            return;
        }

        if (schema is OpenApiSchemaReference { Target: null } schemaRef)
        {
            var referenceId = schemaRef.Reference.Id ?? "unknown";
            diagnostics.Add(DiagnosticBuilder.SchemaReferenceError(
                referenceId,
                path,
                sourceFilePath));
        }
        else if (schema is OpenApiSchema { Items: OpenApiSchemaReference { Target: null } itemRef })
        {
            // Check array items
            var referenceId = itemRef.Reference.Id ?? "unknown";
            diagnostics.Add(DiagnosticBuilder.SchemaReferenceError(
                referenceId,
                $"{path}/items",
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates schema properties recursively for invalid references.
    /// </summary>
    private static void ValidateSchemaPropertiesReferences(
        IOpenApiSchema schema,
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string path)
    {
        var actualSchema = schema is OpenApiSchemaReference { Target: not null } schemaRef
            ? schemaRef.Target
            : schema as OpenApiSchema;

        if (actualSchema?.Properties is null)
        {
            return;
        }

        foreach (var propertyEntry in actualSchema.Properties)
        {
            var propName = propertyEntry.Key;
            var propSchema = propertyEntry.Value;

            ValidateSingleSchemaReference(
                propSchema,
                diagnostics,
                sourceFilePath,
                $"{path}/properties/{propName}");
        }
    }
}