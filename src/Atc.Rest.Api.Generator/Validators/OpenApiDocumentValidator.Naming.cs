// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable CommentTypo
// ReSharper disable InvertIf
// ReSharper disable LoopCanBeConvertedToQuery
namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// The naming rules (ATC_API_NAM): the casing of operation ids, schemas, properties and tags.
/// </summary>
public static partial class OpenApiDocumentValidator
{
    /// <summary>
    /// Validates naming conventions (ATC_API_NAM001-006).
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
            var operationStart = diagnostics.Count;

            // ATC_API_NAM001: OperationId must start with lowercase letter (camelCase).
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

            // ATC_API_NAM004: Parameter name must use camelCase
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

            // ATC_API_NAM006: Tag name must use kebab-case
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

            InScope(diagnostics, operationStart, SpecSuppressions.Operation(httpMethod, path));
        }

        // ATC_API_NAM002: Model name must use PascalCase
        // ATC_API_NAM003: Property name must use camelCase
        // ATC_API_NAM005: Enum value must use PascalCase or UPPER_SNAKE_CASE
        diagnostics.AddRange(ValidateSchemaNameConventions(document, sourceFilePath));

        // Also validate global tags
        diagnostics.AddRange(ValidateGlobalTagNamingConventions(document, sourceFilePath));

        return diagnostics;
    }

    /// <summary>
    /// Validates schema naming conventions (ATC_API_NAM002, ATC_API_NAM003, ATC_API_NAM005).
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

        var enumCasing = EnumValueCasing.OfDocument(document.Components.Schemas);

        foreach (var schemaEntry in document.Components.Schemas)
        {
            var schemaName = schemaEntry.Key;
            var schema = schemaEntry.Value;
            var schemaStart = diagnostics.Count;

            // ATC_API_NAM002: Model name must use PascalCase
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

            // ATC_API_NAM003: Property name must use camelCase
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

            // ATC_API_NAM005: Enum values are PascalCase, UPPER_SNAKE_CASE or one other casing throughout the document
            if (actualSchema.Enum is { Count: > 0 } && enumCasing is { } casing)
            {
                foreach (var enumValue in actualSchema.Enum)
                {
                    var enumString = enumValue?.ToString();
                    if (!string.IsNullOrWhiteSpace(enumString) &&
                        !EnumValueCasing.IsCSharpStyle(enumString!) &&
                        !casing.Matches(enumString!))
                    {
                        diagnostics.Add(DiagnosticBuilder.NamingConventionWarning(
                            RuleIdentifiers.EnumValueCasing,
                            "Enum value",
                            enumString!,
                            $"PascalCase, UPPER_SNAKE_CASE or {casing.Name}, the casing of the document's other enum values",
                            casing.Suggest(enumString!),
                            $"#/components/schemas/{schemaName}/enum",
                            sourceFilePath));
                    }
                }
            }

            InScope(diagnostics, schemaStart, SpecSuppressions.Schema(schemaName));
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates global tag naming conventions (ATC_API_NAM006).
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
}