// ReSharper disable CommentTypo
namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Helper class for reporting diagnostics during source generation.
/// Contains all diagnostic descriptors and helper methods.
/// </summary>
internal static class DiagnosticHelpers
{
    /// <summary>
    /// ATC_API_GEN001: Server generation error.
    /// </summary>
    public static readonly DiagnosticDescriptor ServerGenerationError = new(
        RuleIdentifiers.ServerGenerationError,
        "OpenAPI Generation Error",
        "Failed to generate API from {0}: {1}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI002: Server parsing error.
    /// </summary>
    public static readonly DiagnosticDescriptor ServerParsingError = new(
        RuleIdentifiers.ServerParsingError,
        "OpenAPI Parsing Error",
        "Failed to parse OpenAPI document from {0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI003: Client generation error.
    /// </summary>
    public static readonly DiagnosticDescriptor ClientGenerationError = new(
        RuleIdentifiers.ClientGenerationError,
        "OpenAPI Client Generation Error",
        "Failed to generate API client from {0}: {1}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI004: Client parsing error.
    /// </summary>
    public static readonly DiagnosticDescriptor ClientParsingError = new(
        RuleIdentifiers.ClientParsingError,
        "OpenAPI Client Parsing Error",
        "Failed to parse OpenAPI document for client from {0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI005: Handler scaffold generation error.
    /// </summary>
    public static readonly DiagnosticDescriptor HandlerScaffoldGenerationError = new(
        RuleIdentifiers.HandlerScaffoldGenerationError,
        "Handler Scaffold Generation Error",
        "Failed to generate handler scaffolds from {0}: {1}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI006: Domain parsing error.
    /// </summary>
    public static readonly DiagnosticDescriptor DomainParsingError = new(
        RuleIdentifiers.DomainParsingError,
        "OpenAPI Parsing Error for Domain Generator",
        "Failed to parse OpenAPI document from {0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI007: Output directory not specified.
    /// </summary>
    public static readonly DiagnosticDescriptor OutputDirectoryNotSpecified = new(
        RuleIdentifiers.OutputDirectoryNotSpecified,
        "Output Directory Not Specified",
        "Cannot generate handler scaffolds: no marker file found and no outputPath configured",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI008: Server generator requires ASP.NET Core references.
    /// </summary>
    public static readonly DiagnosticDescriptor ServerRequiresAspNetCore = new(
        RuleIdentifiers.ServerRequiresAspNetCore,
        "ASP.NET Core References Required",
        "The .atc-rest-api-server marker file requires ASP.NET Core references. Add a reference to Microsoft.AspNetCore.App or remove the marker file.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI009: Domain generator requires ASP.NET Core references.
    /// </summary>
    public static readonly DiagnosticDescriptor DomainRequiresAspNetCore = new(
        RuleIdentifiers.DomainRequiresAspNetCore,
        "ASP.NET Core References Required",
        "The .atc-rest-api-server-handlers marker file requires ASP.NET Core references. Add a reference to Microsoft.AspNetCore.App or remove the marker file.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI010: Endpoint injection generation error.
    /// </summary>
    public static readonly DiagnosticDescriptor EndpointInjectionGenerationError = new(
        RuleIdentifiers.EndpointInjectionGenerationError,
        "Endpoint Injection Generation Error",
        "Failed to generate endpoint injection for {0}: {1}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI011: No endpoints found for endpoint injection.
    /// </summary>
    public static readonly DiagnosticDescriptor NoEndpointsFoundForInjection = new(
        RuleIdentifiers.NoEndpointsFoundForInjection,
        "No Endpoints Found for Injection",
        "No endpoint interfaces found matching project name '{0}'. Ensure the client generator has run and generated endpoints in namespace '{0}.Generated.*.Endpoints.Interfaces'.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_GEN010: Generation summary (Info level).
    /// </summary>
    public static readonly DiagnosticDescriptor GenerationSummary = new(
        RuleIdentifiers.GenerationSummary,
        "ATC API Generation Summary",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI012: Client EndpointPerOperation mode requires Atc.Rest.Client reference.
    /// </summary>
    public static readonly DiagnosticDescriptor ClientRequiresAtcRestClient = new(
        RuleIdentifiers.ClientRequiresAtcRestClient,
        "Atc.Rest.Client Reference Required",
        "The EndpointPerOperation generation mode requires a reference to the Atc.Rest.Client NuGet package. Add a reference to Atc.Rest.Client or change generationMode to TypedClient.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI013: Rate limiting extensions require Microsoft.AspNetCore.RateLimiting reference.
    /// </summary>
    public static readonly DiagnosticDescriptor RateLimitingRequiresPackage = new(
        RuleIdentifiers.RateLimitingRequiresPackage,
        "Rate Limiting Package Recommended",
        "The OpenAPI specification contains x-ratelimit-* extensions. Ensure the Microsoft.AspNetCore.RateLimiting package is installed in the consuming API project.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI014: Resilience extensions require Microsoft.Extensions.Http.Resilience reference.
    /// </summary>
    public static readonly DiagnosticDescriptor ResilienceRequiresPackage = new(
        RuleIdentifiers.ResilienceRequiresPackage,
        "HTTP Resilience Package Recommended",
        "The OpenAPI specification contains x-retry-* extensions. Ensure the Microsoft.Extensions.Http.Resilience package is installed in the consuming client project.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI015: JWT Bearer security requires Microsoft.AspNetCore.Authentication.JwtBearer reference.
    /// </summary>
    public static readonly DiagnosticDescriptor JwtBearerRequiresPackage = new(
        RuleIdentifiers.JwtBearerRequiresPackage,
        "JWT Bearer Authentication Package Recommended",
        "The OpenAPI specification contains JWT Bearer security scheme. Ensure the Microsoft.AspNetCore.Authentication.JwtBearer package is installed in the consuming API project.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_DEP007: UseMinimalApiPackage enabled but Atc.Rest.MinimalApi package not referenced.
    /// </summary>
    public static readonly DiagnosticDescriptor MinimalApiPackageRequired = new(
        RuleIdentifiers.MinimalApiPackageRequired,
        "Atc.Rest.MinimalApi Package Required",
        "The marker file specifies useMinimalApiPackage: true but Atc.Rest.MinimalApi package is not referenced. Add a reference to Atc.Rest.MinimalApi or set useMinimalApiPackage to false or auto.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // ========== OpenAPI Validation Diagnostics ==========

    /// <summary>
    /// ATCAPI_VAL001: OpenAPI core parsing error from Microsoft.OpenApi library.
    /// </summary>
    public static readonly DiagnosticDescriptor OpenApiCoreError = new(
        RuleIdentifiers.OpenApiCoreError,
        "OpenAPI Core Parsing Error",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_VAL002: OpenAPI 2.0 not supported - must use OpenAPI 3.0.x.
    /// </summary>
    public static readonly DiagnosticDescriptor OpenApi20NotSupported = new(
        RuleIdentifiers.OpenApi20NotSupported,
        "OpenAPI 2.0 Not Supported",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_NAM001: OperationId must start with lowercase letter (camelCase).
    /// </summary>
    public static readonly DiagnosticDescriptor OperationIdMustBeCamelCase = new(
        RuleIdentifiers.OperationIdMustBeCamelCase,
        "OperationId Must Use camelCase",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR002: OperationId not using correct casing style.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationIdInvalidCasing = new(
        RuleIdentifiers.OperationIdCasing,
        "OperationId Invalid Casing Style",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_NAM002: Model name must use PascalCase.
    /// </summary>
    public static readonly DiagnosticDescriptor ModelNameMustBePascalCase = new(
        RuleIdentifiers.ModelNameMustBePascalCase,
        "Model Name Must Use PascalCase",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_NAM003: Property name must use camelCase.
    /// </summary>
    public static readonly DiagnosticDescriptor PropertyNameMustBeCamelCase = new(
        RuleIdentifiers.PropertyNameMustBeCamelCase,
        "Property Name Must Use camelCase",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_NAM004: Parameter name must use camelCase.
    /// </summary>
    public static readonly DiagnosticDescriptor ParameterNameMustBeCamelCase = new(
        RuleIdentifiers.ParameterNameMustBeCamelCase,
        "Parameter Name Must Use camelCase",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_NAM005: Enum value must use PascalCase or UPPER_SNAKE_CASE.
    /// </summary>
    public static readonly DiagnosticDescriptor EnumValueCasing = new(
        RuleIdentifiers.EnumValueCasing,
        "Enum Value Must Use PascalCase or UPPER_SNAKE_CASE",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_NAM006: Tag name must use kebab-case.
    /// </summary>
    public static readonly DiagnosticDescriptor TagNameMustBeKebabCase = new(
        RuleIdentifiers.TagNameMustBeKebabCase,
        "Tag Name Must Use kebab-case",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Reports a server generation error.
    /// </summary>
    public static void ReportServerGenerationError(
        SourceProductionContext context,
        string filePath,
        Exception exception)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            ServerGenerationError,
            Location.None,
            filePath,
            exception.Message));
    }

    /// <summary>
    /// Reports a server parsing error.
    /// </summary>
    public static void ReportServerParsingError(
        SourceProductionContext context,
        string filePath)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            ServerParsingError,
            Location.None,
            filePath));
    }

    /// <summary>
    /// Reports a client generation error.
    /// </summary>
    public static void ReportClientGenerationError(
        SourceProductionContext context,
        string filePath,
        Exception exception)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            ClientGenerationError,
            Location.None,
            filePath,
            exception.Message));
    }

    /// <summary>
    /// Reports a client parsing error.
    /// </summary>
    public static void ReportClientParsingError(
        SourceProductionContext context,
        string filePath)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            ClientParsingError,
            Location.None,
            filePath));
    }

    /// <summary>
    /// Reports a handler scaffold generation error.
    /// </summary>
    public static void ReportHandlerScaffoldGenerationError(
        SourceProductionContext context,
        string filePath,
        Exception exception)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            HandlerScaffoldGenerationError,
            Location.None,
            filePath,
            exception.Message));
    }

    /// <summary>
    /// Reports a domain parsing error.
    /// </summary>
    public static void ReportDomainParsingError(
        SourceProductionContext context,
        string filePath)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            DomainParsingError,
            Location.None,
            filePath));
    }

    /// <summary>
    /// Reports an output directory not specified warning.
    /// </summary>
    public static void ReportOutputDirectoryNotSpecified(
        SourceProductionContext context)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            OutputDirectoryNotSpecified,
            Location.None));
    }

    /// <summary>
    /// Reports that the server generator requires ASP.NET Core references.
    /// </summary>
    public static void ReportServerRequiresAspNetCore(
        SourceProductionContext context)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            ServerRequiresAspNetCore,
            Location.None));
    }

    /// <summary>
    /// Reports that the domain generator requires ASP.NET Core references.
    /// </summary>
    public static void ReportDomainRequiresAspNetCore(
        SourceProductionContext context)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            DomainRequiresAspNetCore,
            Location.None));
    }

    /// <summary>
    /// Reports an endpoint injection generation error.
    /// </summary>
    public static void ReportEndpointInjectionGenerationError(
        SourceProductionContext context,
        string className,
        Exception exception)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            EndpointInjectionGenerationError,
            Location.None,
            className,
            exception.Message));
    }

    /// <summary>
    /// Reports that no endpoints were found for endpoint injection.
    /// </summary>
    public static void ReportNoEndpointsFoundForInjection(
        SourceProductionContext context,
        string projectName)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            NoEndpointsFoundForInjection,
            Location.None,
            projectName));
    }

    /// <summary>
    /// Reports a generation summary (Info level diagnostic).
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="specName">Name of the OpenAPI specification file.</param>
    /// <param name="generatorType">The type of generator ("Server" or "Client").</param>
    /// <param name="models">Number of models generated.</param>
    /// <param name="handlers">Number of handlers generated.</param>
    /// <param name="endpoints">Number of endpoints generated.</param>
    /// <param name="warnings">Number of validation warnings.</param>
    /// <param name="errors">Number of validation errors.</param>
    public static void ReportGenerationSummary(
        SourceProductionContext context,
        string specName,
        string generatorType,
        int models,
        int handlers,
        int endpoints,
        int warnings,
        int errors)
    {
        var message = $"[{generatorType}] Generated from {specName}: {models} models, {handlers} handlers, {endpoints} endpoints. Validation: {errors} errors, {warnings} warnings.";
        context.ReportDiagnostic(Diagnostic.Create(
            GenerationSummary,
            Location.None,
            message));
    }

    /// <summary>
    /// Reports that the client EndpointPerOperation mode requires Atc.Rest.Client reference.
    /// </summary>
    public static void ReportClientRequiresAtcRestClient(
        SourceProductionContext context)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            ClientRequiresAtcRestClient,
            Location.None));
    }

    /// <summary>
    /// Reports that rate limiting extensions require Microsoft.AspNetCore.RateLimiting package.
    /// </summary>
    public static void ReportRateLimitingRequiresPackage(
        SourceProductionContext context)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            RateLimitingRequiresPackage,
            Location.None));
    }

    /// <summary>
    /// Reports that resilience extensions require Microsoft.Extensions.Http.Resilience package.
    /// </summary>
    public static void ReportResilienceRequiresPackage(
        SourceProductionContext context)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            ResilienceRequiresPackage,
            Location.None));
    }

    /// <summary>
    /// Reports that JWT Bearer security scheme requires Microsoft.AspNetCore.Authentication.JwtBearer package.
    /// </summary>
    public static void ReportJwtBearerRequiresPackage(
        SourceProductionContext context)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            JwtBearerRequiresPackage,
            Location.None));
    }

    /// <summary>
    /// Reports that useMinimalApiPackage is enabled but Atc.Rest.MinimalApi package is not referenced.
    /// </summary>
    public static void ReportMinimalApiPackageRequired(
        SourceProductionContext context)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            MinimalApiPackageRequired,
            Location.None));
    }

    // ========== OpenAPI Validation Reporting Methods ==========

    /// <summary>
    /// Reports an OpenAPI core parsing error (ATCAPI_VAL001).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Error message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportOpenApiCoreError(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            OpenApiCoreError,
            Location.None,
            message);

    /// <summary>
    /// Reports OpenAPI 2.0 not supported error (ATCAPI_VAL002).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Error message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportOpenApi20NotSupported(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            OpenApi20NotSupported,
            Location.None,
            message);

    /// <summary>
    /// Reports operationId must use camelCase warning (ATCAPI_NAM001).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Warning message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportOperationIdMustBeCamelCase(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            OperationIdMustBeCamelCase,
            Location.None,
            message);

    /// <summary>
    /// Reports operationId invalid casing style warning (ATCAPI_OPR002).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Warning message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportOperationIdInvalidCasing(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            OperationIdInvalidCasing,
            Location.None,
            message);

    /// <summary>
    /// Reports model name must use PascalCase warning (ATCAPI_NAM002).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Warning message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportModelNameMustBePascalCase(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            ModelNameMustBePascalCase,
            Location.None,
            message);

    /// <summary>
    /// Reports property name must use camelCase warning (ATCAPI_NAM003).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Warning message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportPropertyNameMustBeCamelCase(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            PropertyNameMustBeCamelCase,
            Location.None,
            message);

    /// <summary>
    /// Reports parameter name must use camelCase warning (ATCAPI_NAM004).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Warning message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportParameterNameMustBeCamelCase(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            ParameterNameMustBeCamelCase,
            Location.None,
            message);

    /// <summary>
    /// Reports enum value casing warning (ATCAPI_NAM005).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Warning message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportEnumValueCasing(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            EnumValueCasing,
            Location.None,
            message);

    /// <summary>
    /// Reports tag name must use kebab-case warning (ATCAPI_NAM006).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Warning message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportTagNameMustBeKebabCase(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            TagNameMustBeKebabCase,
            Location.None,
            message);

    // ========== Schema Validation Diagnostics ==========

    /// <summary>
    /// ATCAPI_SCH001: Missing title on array type.
    /// </summary>
    public static readonly DiagnosticDescriptor ArrayTitleMissing = new(
        RuleIdentifiers.ArrayTitleMissing,
        "Missing Title on Array Type",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SCH002: Array type title not starting with uppercase.
    /// </summary>
    public static readonly DiagnosticDescriptor ArrayTitleNotUppercase = new(
        RuleIdentifiers.ArrayTitleNotUppercase,
        "Array Title Not Starting With Uppercase",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SCH003: Missing title on object type.
    /// </summary>
    public static readonly DiagnosticDescriptor ObjectTitleMissing = new(
        RuleIdentifiers.ObjectTitleMissing,
        "Missing Title on Object Type",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SCH004: Object type title not starting with uppercase.
    /// </summary>
    public static readonly DiagnosticDescriptor ObjectTitleNotUppercase = new(
        RuleIdentifiers.ObjectTitleNotUppercase,
        "Object Title Not Starting With Uppercase",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SCH005: Implicit object definition in array property not supported.
    /// </summary>
    public static readonly DiagnosticDescriptor ImplicitArrayObjectNotSupported = new(
        RuleIdentifiers.ImplicitArrayObjectNotSupported,
        "Implicit Object Definition in Array Not Supported",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SCH006: Object name not using correct casing style.
    /// </summary>
    public static readonly DiagnosticDescriptor ObjectNameCasing = new(
        RuleIdentifiers.ObjectNameCasing,
        "Object Name Must Use PascalCase",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SCH007: Object property name not using correct casing style.
    /// </summary>
    public static readonly DiagnosticDescriptor PropertyNameCasing = new(
        RuleIdentifiers.PropertyNameCasing,
        "Property Name Must Use camelCase",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SCH008: Enum name not using correct casing style.
    /// </summary>
    public static readonly DiagnosticDescriptor EnumNameCasing = new(
        RuleIdentifiers.EnumNameCasing,
        "Enum Name Must Use PascalCase",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SCH009: Array property missing data type specification.
    /// </summary>
    public static readonly DiagnosticDescriptor ArrayPropertyMissingType = new(
        RuleIdentifiers.ArrayPropertyMissingType,
        "Array Property Missing Data Type",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SCH010: Implicit object definition on property not supported.
    /// </summary>
    public static readonly DiagnosticDescriptor ImplicitObjectNotSupported = new(
        RuleIdentifiers.ImplicitObjectNotSupported,
        "Implicit Object Definition Not Supported",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SCH011: Array property missing items specification.
    /// </summary>
    public static readonly DiagnosticDescriptor ArrayPropertyMissingItems = new(
        RuleIdentifiers.ArrayPropertyMissingItems,
        "Array Property Missing Items Specification",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SCH012: Missing key/name for object property.
    /// </summary>
    public static readonly DiagnosticDescriptor PropertyKeyMissing = new(
        RuleIdentifiers.PropertyKeyMissing,
        "Missing Property Key/Name",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SCH013: Schema reference does not exist in components.schemas.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidSchemaReference = new(
        RuleIdentifiers.InvalidSchemaReference,
        "Invalid Schema Reference",
        "Schema reference '{0}' at path '{1}' does not exist in components.schemas",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // ========== Schema Validation Reporting Methods ==========

    /// <summary>
    /// Reports missing title on array type warning (ATCAPI_SCH001).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Warning message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportArrayTitleMissing(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            ArrayTitleMissing,
            Location.None,
            message);

    /// <summary>
    /// Reports array title not starting with uppercase warning (ATCAPI_SCH002).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Warning message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportArrayTitleNotUppercase(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            ArrayTitleNotUppercase,
            Location.None,
            message);

    /// <summary>
    /// Reports missing title on object type warning (ATCAPI_SCH003).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Warning message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportObjectTitleMissing(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            ObjectTitleMissing,
            Location.None,
            message);

    /// <summary>
    /// Reports object title not starting with uppercase warning (ATCAPI_SCH004).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Warning message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportObjectTitleNotUppercase(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            ObjectTitleNotUppercase,
            Location.None,
            message);

    /// <summary>
    /// Reports implicit object definition in array not supported error (ATCAPI_SCH005).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Error message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportImplicitArrayObjectNotSupported(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            ImplicitArrayObjectNotSupported,
            Location.None,
            message);

    /// <summary>
    /// Reports object name casing warning (ATCAPI_SCH006).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Warning message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportObjectNameCasing(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            ObjectNameCasing,
            Location.None,
            message);

    /// <summary>
    /// Reports property name casing warning (ATCAPI_SCH007).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Warning message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportPropertyNameCasing(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            PropertyNameCasing,
            Location.None,
            message);

    /// <summary>
    /// Reports enum name casing warning (ATCAPI_SCH008).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Warning message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportEnumNameCasing(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            EnumNameCasing,
            Location.None,
            message);

    /// <summary>
    /// Reports array property missing data type error (ATCAPI_SCH009).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Error message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportArrayPropertyMissingType(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            ArrayPropertyMissingType,
            Location.None,
            message);

    /// <summary>
    /// Reports implicit object definition not supported error (ATCAPI_SCH010).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Error message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportImplicitObjectNotSupported(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            ImplicitObjectNotSupported,
            Location.None,
            message);

    /// <summary>
    /// Reports array property missing items specification error (ATCAPI_SCH011).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Error message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportArrayPropertyMissingItems(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            ArrayPropertyMissingItems,
            Location.None,
            message);

    /// <summary>
    /// Reports missing property key/name error (ATCAPI_SCH012).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="message">Error message.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportPropertyKeyMissing(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            PropertyKeyMissing,
            Location.None,
            message);

    /// <summary>
    /// Reports invalid schema reference error (ATCAPI_SCH013).
    /// </summary>
    /// <param name="sourceFilePath">Path to the OpenAPI file.</param>
    /// <param name="referenceId">The invalid schema reference ID.</param>
    /// <param name="path">The path where the invalid reference was found.</param>
    /// <returns>Diagnostic to report.</returns>
    public static Diagnostic ReportInvalidSchemaReference(
        string sourceFilePath,
        string referenceId,
        string path)
        => Diagnostic.Create(
            InvalidSchemaReference,
            Location.None,
            referenceId,
            path);

    // ========== Operation Validation Diagnostics ==========

    /// <summary>
    /// ATCAPI_OPR001: Missing operationId.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationIdMissing = new(
        RuleIdentifiers.OperationIdMissing,
        "Missing OperationId",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR003: GET operationId should start with 'Get' or 'List'.
    /// </summary>
    public static readonly DiagnosticDescriptor GetOperationIdPrefix = new(
        RuleIdentifiers.GetOperationIdPrefix,
        "GET OperationId Should Start With 'Get' or 'List'",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR004: POST operationId should not start with 'Delete'.
    /// </summary>
    public static readonly DiagnosticDescriptor PostOperationIdPrefix = new(
        RuleIdentifiers.PostOperationIdPrefix,
        "POST OperationId Should Not Start With 'Delete'",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR005: PUT operationId should start with 'Update'.
    /// </summary>
    public static readonly DiagnosticDescriptor PutOperationIdPrefix = new(
        RuleIdentifiers.PutOperationIdPrefix,
        "PUT OperationId Should Start With 'Update'",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR006: PATCH operationId should start with 'Patch' or 'Update'.
    /// </summary>
    public static readonly DiagnosticDescriptor PatchOperationIdPrefix = new(
        RuleIdentifiers.PatchOperationIdPrefix,
        "PATCH OperationId Should Start With 'Patch' or 'Update'",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR007: DELETE operationId should start with 'Delete' or 'Remove'.
    /// </summary>
    public static readonly DiagnosticDescriptor DeleteOperationIdPrefix = new(
        RuleIdentifiers.DeleteOperationIdPrefix,
        "DELETE OperationId Should Start With 'Delete' or 'Remove'",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR008: Pluralized operationId but response is single item.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationIdPluralizationMismatch = new(
        RuleIdentifiers.OperationIdPluralizationMismatch,
        "Pluralized OperationId But Response Is Single Item",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR009: Singular operationId but response is array.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationIdSingularMismatch = new(
        RuleIdentifiers.OperationIdSingularMismatch,
        "Singular OperationId But Response Is Array",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR010: Has BadRequest response but no parameters.
    /// </summary>
    public static readonly DiagnosticDescriptor BadRequestWithoutParameters = new(
        RuleIdentifiers.BadRequestWithoutParameters,
        "BadRequest Response Without Parameters",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR011: Global path parameter not present in route.
    /// </summary>
    public static readonly DiagnosticDescriptor GlobalPathParameterNotInRoute = new(
        RuleIdentifiers.GlobalPathParameterNotInRoute,
        "Global Path Parameter Not In Route",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR012: Operation missing path parameter defined in route.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationMissingPathParameter = new(
        RuleIdentifiers.OperationMissingPathParameter,
        "Operation Missing Path Parameter",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR013: Operation path parameter not present in route.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationPathParameterNotInRoute = new(
        RuleIdentifiers.OperationPathParameterNotInRoute,
        "Operation Path Parameter Not In Route",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR014: GET with path parameter missing NotFound response.
    /// </summary>
    public static readonly DiagnosticDescriptor GetMissingNotFoundResponse = new(
        RuleIdentifiers.GetMissingNotFoundResponse,
        "GET Missing NotFound Response",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR015: Path parameter missing required=true.
    /// </summary>
    public static readonly DiagnosticDescriptor PathParameterNotRequired = new(
        RuleIdentifiers.PathParameterNotRequired,
        "Path Parameter Missing required=true",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR016: Path parameter must not be nullable.
    /// </summary>
    public static readonly DiagnosticDescriptor PathParameterNullable = new(
        RuleIdentifiers.PathParameterNullable,
        "Path Parameter Must Not Be Nullable",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR017: RequestBody with inline model not supported.
    /// </summary>
    public static readonly DiagnosticDescriptor RequestBodyInlineModel = new(
        RuleIdentifiers.RequestBodyInlineModel,
        "RequestBody With Inline Model Not Supported",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR018: Multiple 2xx status codes not supported.
    /// </summary>
    public static readonly DiagnosticDescriptor Multiple2xxStatusCodes = new(
        RuleIdentifiers.Multiple2xxStatusCodes,
        "Multiple 2xx Status Codes Not Supported",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // ========== Response Code Consistency Diagnostics (OPR021-OPR025) ==========

    /// <summary>
    /// ATCAPI_OPR021: 401 Unauthorized response defined but no security requirements.
    /// </summary>
    public static readonly DiagnosticDescriptor UnauthorizedWithoutSecurity = new(
        RuleIdentifiers.UnauthorizedWithoutSecurity,
        "Unauthorized Response Without Security",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR022: 403 Forbidden response defined but no authorization (roles/policies).
    /// </summary>
    public static readonly DiagnosticDescriptor ForbiddenWithoutAuthorization = new(
        RuleIdentifiers.ForbiddenWithoutAuthorization,
        "Forbidden Response Without Authorization",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR023: 404 NotFound response on POST operation (unusual).
    /// </summary>
    public static readonly DiagnosticDescriptor NotFoundOnPostOperation = new(
        RuleIdentifiers.NotFoundOnPostOperation,
        "NotFound Response on POST Operation",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR024: 409 Conflict response on non-mutating operation (GET/DELETE).
    /// </summary>
    public static readonly DiagnosticDescriptor ConflictOnNonMutatingOperation = new(
        RuleIdentifiers.ConflictOnNonMutatingOperation,
        "Conflict Response on Non-Mutating Operation",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_OPR025: 429 TooManyRequests response defined but no rate limiting configured.
    /// </summary>
    public static readonly DiagnosticDescriptor TooManyRequestsWithoutRateLimiting = new(
        RuleIdentifiers.TooManyRequestsWithoutRateLimiting,
        "TooManyRequests Response Without Rate Limiting",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // ========== Security Validation DiagnosticDescriptors ==========

    /// <summary>
    /// ATCAPI_SEC001: Path authorize role not defined in global section.
    /// </summary>
    public static readonly DiagnosticDescriptor PathAuthorizeRoleNotDefined = new(
        RuleIdentifiers.PathAuthorizeRoleNotDefined,
        "Path Authorize Role Not Defined",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SEC002: Path authentication scheme not defined in global section.
    /// </summary>
    public static readonly DiagnosticDescriptor PathAuthenticationSchemeNotDefined = new(
        RuleIdentifiers.PathAuthenticationSchemeNotDefined,
        "Path Authentication Scheme Not Defined",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SEC003: Operation authorize role not defined in global section.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationAuthorizeRoleNotDefined = new(
        RuleIdentifiers.OperationAuthorizeRoleNotDefined,
        "Operation Authorize Role Not Defined",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SEC004: Operation authentication scheme not defined in global section.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationAuthenticationSchemeNotDefined = new(
        RuleIdentifiers.OperationAuthenticationSchemeNotDefined,
        "Operation Authentication Scheme Not Defined",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SEC005: Operation has authenticationRequired=false but has roles/schemes.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationAuthenticationConflict = new(
        RuleIdentifiers.OperationAuthenticationConflict,
        "Operation Authentication Conflict",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SEC006: Operation authorize role has incorrect casing vs global section.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationAuthorizeRoleCasing = new(
        RuleIdentifiers.OperationAuthorizeRoleCasing,
        "Operation Authorize Role Casing Mismatch",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SEC007: Operation authentication scheme has incorrect casing vs global.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationAuthenticationSchemeCasing = new(
        RuleIdentifiers.OperationAuthenticationSchemeCasing,
        "Operation Authentication Scheme Casing Mismatch",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SEC008: Path authorize role has incorrect casing vs global section.
    /// </summary>
    public static readonly DiagnosticDescriptor PathAuthorizeRoleCasing = new(
        RuleIdentifiers.PathAuthorizeRoleCasing,
        "Path Authorize Role Casing Mismatch",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SEC009: Path authentication scheme has incorrect casing vs global.
    /// </summary>
    public static readonly DiagnosticDescriptor PathAuthenticationSchemeCasing = new(
        RuleIdentifiers.PathAuthenticationSchemeCasing,
        "Path Authentication Scheme Casing Mismatch",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATCAPI_SEC010: Path has authenticationRequired=false but has roles/schemes.
    /// </summary>
    public static readonly DiagnosticDescriptor PathAuthenticationConflict = new(
        RuleIdentifiers.PathAuthenticationConflict,
        "Path Authentication Conflict",
        "{0}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // ========== Operation Validation Reporting Methods ==========

    /// <summary>
    /// Reports missing operationId error (ATCAPI_OPR001).
    /// </summary>
    public static Diagnostic ReportOperationIdMissing(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            OperationIdMissing,
            Location.None,
            message);

    /// <summary>
    /// Reports GET operationId prefix warning (ATCAPI_OPR003).
    /// </summary>
    public static Diagnostic ReportGetOperationIdPrefix(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            GetOperationIdPrefix,
            Location.None,
            message);

    /// <summary>
    /// Reports POST operationId prefix warning (ATCAPI_OPR004).
    /// </summary>
    public static Diagnostic ReportPostOperationIdPrefix(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            PostOperationIdPrefix,
            Location.None,
            message);

    /// <summary>
    /// Reports PUT operationId prefix warning (ATCAPI_OPR005).
    /// </summary>
    public static Diagnostic ReportPutOperationIdPrefix(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            PutOperationIdPrefix,
            Location.None,
            message);

    /// <summary>
    /// Reports PATCH operationId prefix warning (ATCAPI_OPR006).
    /// </summary>
    public static Diagnostic ReportPatchOperationIdPrefix(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            PatchOperationIdPrefix,
            Location.None,
            message);

    /// <summary>
    /// Reports DELETE operationId prefix warning (ATCAPI_OPR007).
    /// </summary>
    public static Diagnostic ReportDeleteOperationIdPrefix(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            DeleteOperationIdPrefix,
            Location.None,
            message);

    /// <summary>
    /// Reports pluralized operationId mismatch warning (ATCAPI_OPR008).
    /// </summary>
    public static Diagnostic ReportOperationIdPluralizationMismatch(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            OperationIdPluralizationMismatch,
            Location.None,
            message);

    /// <summary>
    /// Reports singular operationId mismatch warning (ATCAPI_OPR009).
    /// </summary>
    public static Diagnostic ReportOperationIdSingularMismatch(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            OperationIdSingularMismatch,
            Location.None,
            message);

    /// <summary>
    /// Reports BadRequest without parameters warning (ATCAPI_OPR010).
    /// </summary>
    public static Diagnostic ReportBadRequestWithoutParameters(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            BadRequestWithoutParameters,
            Location.None,
            message);

    /// <summary>
    /// Reports global path parameter not in route error (ATCAPI_OPR011).
    /// </summary>
    public static Diagnostic ReportGlobalPathParameterNotInRoute(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            GlobalPathParameterNotInRoute,
            Location.None,
            message);

    /// <summary>
    /// Reports operation missing path parameter error (ATCAPI_OPR012).
    /// </summary>
    public static Diagnostic ReportOperationMissingPathParameter(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            OperationMissingPathParameter,
            Location.None,
            message);

    /// <summary>
    /// Reports operation path parameter not in route error (ATCAPI_OPR013).
    /// </summary>
    public static Diagnostic ReportOperationPathParameterNotInRoute(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            OperationPathParameterNotInRoute,
            Location.None,
            message);

    /// <summary>
    /// Reports GET missing NotFound response warning (ATCAPI_OPR014).
    /// </summary>
    public static Diagnostic ReportGetMissingNotFoundResponse(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            GetMissingNotFoundResponse,
            Location.None,
            message);

    /// <summary>
    /// Reports path parameter not required warning (ATCAPI_OPR015).
    /// </summary>
    public static Diagnostic ReportPathParameterNotRequired(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            PathParameterNotRequired,
            Location.None,
            message);

    /// <summary>
    /// Reports path parameter nullable warning (ATCAPI_OPR016).
    /// </summary>
    public static Diagnostic ReportPathParameterNullable(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            PathParameterNullable,
            Location.None,
            message);

    /// <summary>
    /// Reports requestBody inline model error (ATCAPI_OPR017).
    /// </summary>
    public static Diagnostic ReportRequestBodyInlineModel(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            RequestBodyInlineModel,
            Location.None,
            message);

    /// <summary>
    /// Reports multiple 2xx status codes error (ATCAPI_OPR018).
    /// </summary>
    public static Diagnostic ReportMultiple2xxStatusCodes(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            Multiple2xxStatusCodes,
            Location.None,
            message);

    // ========== Security Validation Reporting Methods ==========

    /// <summary>
    /// Reports path authorize role not defined error (ATCAPI_SEC001).
    /// </summary>
    public static Diagnostic ReportPathAuthorizeRoleNotDefined(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            PathAuthorizeRoleNotDefined,
            Location.None,
            message);

    /// <summary>
    /// Reports path authentication scheme not defined error (ATCAPI_SEC002).
    /// </summary>
    public static Diagnostic ReportPathAuthenticationSchemeNotDefined(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            PathAuthenticationSchemeNotDefined,
            Location.None,
            message);

    /// <summary>
    /// Reports operation authorize role not defined error (ATCAPI_SEC003).
    /// </summary>
    public static Diagnostic ReportOperationAuthorizeRoleNotDefined(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            OperationAuthorizeRoleNotDefined,
            Location.None,
            message);

    /// <summary>
    /// Reports operation authentication scheme not defined error (ATCAPI_SEC004).
    /// </summary>
    public static Diagnostic ReportOperationAuthenticationSchemeNotDefined(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            OperationAuthenticationSchemeNotDefined,
            Location.None,
            message);

    /// <summary>
    /// Reports operation authentication conflict warning (ATCAPI_SEC005).
    /// </summary>
    public static Diagnostic ReportOperationAuthenticationConflict(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            OperationAuthenticationConflict,
            Location.None,
            message);

    /// <summary>
    /// Reports operation authorize role casing mismatch warning (ATCAPI_SEC006).
    /// </summary>
    public static Diagnostic ReportOperationAuthorizeRoleCasing(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            OperationAuthorizeRoleCasing,
            Location.None,
            message);

    /// <summary>
    /// Reports operation authentication scheme casing mismatch warning (ATCAPI_SEC007).
    /// </summary>
    public static Diagnostic ReportOperationAuthenticationSchemeCasing(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            OperationAuthenticationSchemeCasing,
            Location.None,
            message);

    /// <summary>
    /// Reports path authorize role casing mismatch warning (ATCAPI_SEC008).
    /// </summary>
    public static Diagnostic ReportPathAuthorizeRoleCasing(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            PathAuthorizeRoleCasing,
            Location.None,
            message);

    /// <summary>
    /// Reports path authentication scheme casing mismatch warning (ATCAPI_SEC009).
    /// </summary>
    public static Diagnostic ReportPathAuthenticationSchemeCasing(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            PathAuthenticationSchemeCasing,
            Location.None,
            message);

    /// <summary>
    /// Reports path authentication conflict warning (ATCAPI_SEC010).
    /// </summary>
    public static Diagnostic ReportPathAuthenticationConflict(
        string sourceFilePath,
        string message)
        => Diagnostic.Create(
            PathAuthenticationConflict,
            Location.None,
            message);

    // ========== DiagnosticMessage Conversion ==========

    /// <summary>
    /// Converts a platform-agnostic DiagnosticMessage from the Generator library
    /// to a Roslyn Diagnostic for source generator reporting.
    /// </summary>
    /// <param name="message">The DiagnosticMessage to convert.</param>
    /// <returns>A Roslyn Diagnostic that can be reported via SourceProductionContext.</returns>
    public static Diagnostic ToRoslynDiagnostic(
        GeneratorDiagnosticMessage message)
    {
        var severity = message.Severity switch
        {
            GeneratorDiagnosticSeverity.Error => DiagnosticSeverity.Error,
            GeneratorDiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
            GeneratorDiagnosticSeverity.Info => DiagnosticSeverity.Info,
            _ => DiagnosticSeverity.Hidden
        };

        // Build rich message text with context and suggestions
        var messageText = BuildRichMessageText(message);

        var descriptor = new DiagnosticDescriptor(
            message.RuleId,
            GetTitleFromRuleId(message.RuleId),
            messageText,
            RuleIdentifiers.Category,
            severity,
            isEnabledByDefault: true,
            helpLinkUri: message.DocumentationUrl);

        return Diagnostic.Create(descriptor, Location.None);
    }

    /// <summary>
    /// Builds a rich message text including context and suggestions.
    /// </summary>
    private static string BuildRichMessageText(
        GeneratorDiagnosticMessage message)
    {
        var sb = new StringBuilder();

        // Add context prefix if available
        if (!string.IsNullOrEmpty(message.Context))
        {
            sb.Append('[');
            sb.Append(message.Context);
            sb.Append("] ");
        }

        // Main message
        sb.Append(message.Message);

        // Add first suggestion if available (keep output concise)
        if (message.Suggestions is { Count: > 0 })
        {
            sb.Append(" Suggestion: ");
            sb.Append(message.Suggestions[0]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets a human-readable title from a rule ID.
    /// </summary>
    private static string GetTitleFromRuleId(string ruleId)
    {
        // Extract category and convert to readable format
        // e.g., "ATC_API_NAM001" -> "Naming Convention"
        if (ruleId.StartsWith("ATC_API_", StringComparison.Ordinal) && ruleId.Length >= 11)
        {
            var category = ruleId.Substring(8, 3);
            return category switch
            {
                "GEN" => "Generation Error",
                "DEP" => "Dependency Error",
                "VAL" => "Validation Error",
                "NAM" => "Naming Convention",
                "SEC" => "Security",
                "SRV" => "Server Configuration",
                "SCH" => "Schema Validation",
                "PTH" => "Path Validation",
                "OPR" => "Operation Validation",
                _ => ruleId
            };
        }

        return ruleId;
    }

    /// <summary>
    /// Converts a list of platform-agnostic DiagnosticMessages to Roslyn Diagnostics.
    /// </summary>
    /// <param name="messages">The DiagnosticMessages to convert.</param>
    /// <returns>A list of Roslyn Diagnostics.</returns>
    public static List<Diagnostic> ToRoslynDiagnostics(
        IEnumerable<GeneratorDiagnosticMessage> messages)
        => messages
            .Select(ToRoslynDiagnostic)
            .ToList();

    /// <summary>
    /// Checks if any DiagnosticMessage in the collection has Error severity.
    /// </summary>
    /// <param name="messages">The DiagnosticMessages to check.</param>
    /// <returns>True if any message is an error.</returns>
    public static bool HasErrors(
        IEnumerable<GeneratorDiagnosticMessage> messages)
        => messages.Any(m => m.Severity == GeneratorDiagnosticSeverity.Error);
}