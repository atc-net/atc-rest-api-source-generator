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
            diagnostics.AddRange(ValidateStandard(diagnosticErrors, document, sourceFilePath));
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
        IList<OpenApiError> diagnosticErrors,
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (diagnosticErrors.Count > 0)
        {
            foreach (var error in diagnosticErrors)
            {
                diagnostics.Add(DiagnosticBuilder.ParsingError(
                    error.Message,
                    error.Pointer,
                    sourceFilePath));
            }
        }

        // ATCAPI_SCH013: Validate schema references (fundamental error that breaks code generation)
        diagnostics.AddRange(ValidateSchemaReferences(document, sourceFilePath));

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
    /// Validates OpenAPI version (ATCAPI_VAL002: OpenAPI 2.0 not supported).
    /// </summary>
    private static List<DiagnosticMessage> ValidateOpenApiVersion(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.Info.Version != null)
        {
            // Check if version starts with "2." (OpenAPI/Swagger 2.0)
            var specVersion = document.Info.Version;
            if (specVersion.StartsWith("2.", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.OpenApi20NotSupported,
                    $"OpenAPI 2.0 (Swagger) is not supported. Please use OpenAPI 3.0.x. Current version: {specVersion}",
                    DiagnosticSeverity.Error,
                    sourceFilePath));
            }
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
            // ATCAPI_NAM001: OperationId must start with lowercase letter (camelCase)
            var operationId = operation.GetOperationId(path, httpMethod);
            if (!string.IsNullOrWhiteSpace(operationId))
            {
                var firstChar = operationId[0];
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
            if (operation.Parameters != null)
            {
                foreach (var parameter in operation.Parameters)
                {
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
            if (operation.Tags != null)
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

        if (document.Components?.Schemas == null)
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
            if (actualSchema.Properties != null)
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

        if (document.Tags == null)
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

        if (document.Paths == null)
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
            if (pathItem.Operations != null)
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
        if (pathItem.Extensions == null || pathItem.Extensions.Count == 0)
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
        if (operation.Extensions == null || operation.Extensions.Count == 0)
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

        if (!extensions.TryGetValue(extensionKey, out var extension) || extension == null)
        {
            return null;
        }

        // Try to get the value using reflection on Node property (JsonNodeExtension)
        var extensionType = extension.GetType();
        var nodeProperty = extensionType.GetProperty("Node");
        if (nodeProperty != null)
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

        if (!extensions.TryGetValue(extensionKey, out var extension) || extension == null)
        {
            return result;
        }

        // Try to get the value using reflection on Node property (JsonNodeExtension)
        var extensionType = extension.GetType();
        var nodeProperty = extensionType.GetProperty("Node");
        if (nodeProperty == null)
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

        if (document.Components?.Schemas == null)
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

                if (schemaRef.Default != null)
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

                        if (propRef.Default != null)
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
        if (actualSchema.Properties != null)
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
                actualPropertySchema.AdditionalProperties == null)
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
        if (items == null)
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

        return type == "string" ||
               type == "integer" ||
               type == "number" ||
               type == "boolean";
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

        if (document.Paths == null)
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

            if (pathItem.Operations == null)
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
        ValidateNotFoundResponse(diagnostics, sourceFilePath, httpMethod, operation, operationId);

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
        if (responseSchema == null)
        {
            return;
        }

        var isPluralized = IsOperationIdPluralized(operationId);
        var isArrayResponse = IsArraySchema(responseSchema, document) ||
                              IsPaginatedSchema(responseSchema, document);

        // ATCAPI_OPR008: Pluralized operationId but response is single item
        if (isPluralized && !isArrayResponse)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.OperationIdPluralizationMismatch,
                $"OperationId '{operationId}' is pluralized but response is a single item. " +
                $"Location: {httpMethod.ToUpperInvariant()} {pathKey}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        // ATCAPI_OPR009: Singular operationId but response is array
        if (!isPluralized && isArrayResponse)
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
                           (operation.RequestBody != null) ||
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

        if (pathItem is not OpenApiPathItem pathItemCast)
        {
            return;
        }

        var securityConfig = operation.ExtractUnifiedSecurityConfiguration(pathItemCast, document);
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

        if (pathItem is not OpenApiPathItem pathItemCast)
        {
            return;
        }

        var securityConfig = operation.ExtractUnifiedSecurityConfiguration(pathItemCast, document);
        var hasRolesOrPolicies = securityConfig != null &&
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
    /// Validates 404 NotFound response is not on POST operation.
    /// </summary>
    private static void ValidateNotFoundResponse(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string httpMethod,
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
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.NotFoundOnPostOperation,
                $"Operation '{operationId}' defines 404 NotFound response on POST operation - POST creates resources, so 'not found' is unusual.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates 409 Conflict response is on mutating operation (POST/PUT/PATCH).
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

        // ATCAPI_OPR024: Has 409 Conflict on non-mutating operation (GET/DELETE)
        var isNonMutating = string.Equals(httpMethod, "get", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(httpMethod, "delete", StringComparison.OrdinalIgnoreCase);

        if (isNonMutating)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ConflictOnNonMutatingOperation,
                $"Operation '{operationId}' defines 409 Conflict response but operation is {httpMethod.ToUpperInvariant()} - conflicts typically occur during POST/PUT/PATCH operations.",
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

        if (pathItem is not OpenApiPathItem pathItemCast)
        {
            return;
        }

        var rateLimitConfig = operation.ExtractRateLimitConfiguration(pathItemCast, document);
        var hasRateLimiting = rateLimitConfig != null;

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
        if (operation.Parameters == null)
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
        if (operation.RequestBody?.Content == null)
        {
            return;
        }

        foreach (var contentEntry in operation.RequestBody.Content)
        {
            var schema = contentEntry.Value.Schema;
            if (schema == null)
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
                schema is OpenApiSchema inlineSchema &&
                inlineSchema.Properties != null &&
                inlineSchema.Properties.Count > 0)
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
        if (operation.Responses == null)
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
        if (operation.Responses == null)
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
    /// Checks if an operationId is pluralized (ends with 's' after typical verb prefixes).
    /// </summary>
    private static bool IsOperationIdPluralized(string operationId)
    {
        // Remove common prefixes
        var prefixes = new[] { "get", "list", "find", "search", "fetch", "retrieve" };
        var name = operationId;

        foreach (var prefix in prefixes)
        {
            if (operationId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                name = operationId.Substring(prefix.Length);
                break;
            }
        }

        // Common plural endings
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        // Check if name ends with 's' but not 'ss' (like 'address')
        return name.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
               !name.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
               !name.EndsWith("us", StringComparison.OrdinalIgnoreCase) &&
               !name.EndsWith("is", StringComparison.OrdinalIgnoreCase);
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
        var actualSchema = schema;

        // Resolve reference
        if (schema is OpenApiSchemaReference schemaRef)
        {
            actualSchema = schemaRef.Target ?? schema;
        }

        if (actualSchema is not OpenApiSchema openApiSchema)
        {
            return false;
        }

        // Check direct properties if it's an object type
        if (openApiSchema.Type.HasValue &&
            openApiSchema.Type.Value.HasFlag(JsonSchemaType.Object) &&
            openApiSchema.Properties != null)
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
            if (operation.RequestBody == null || operation.RequestBody.Content == null || operation.RequestBody.Content.Count == 0)
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
    /// Validates schema references point to existing schemas (ATCAPI_SCH013).
    /// </summary>
    private static List<DiagnosticMessage> ValidateSchemaReferences(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.Paths == null)
        {
            return diagnostics;
        }

        // Check operation response schemas, request body schemas, and parameter schemas
        foreach (var pathEntry in document.Paths)
        {
            var pathKey = pathEntry.Key;
            var pathItem = pathEntry.Value;

            if (pathItem.Operations == null)
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
                if (operation.Responses != null)
                {
                    foreach (var responseEntry in operation.Responses)
                    {
                        var statusCode = responseEntry.Key;
                        var response = responseEntry.Value;

                        if (response.Content == null)
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
                if (operation.RequestBody?.Content != null)
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
                if (operation.Parameters != null)
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
        if (document.Components?.Schemas != null)
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
        if (schema == null)
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

        if (actualSchema?.Properties == null)
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