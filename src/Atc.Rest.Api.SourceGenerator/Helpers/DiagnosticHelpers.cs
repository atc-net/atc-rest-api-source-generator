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
    /// ATC_API_GEN022: The marker's namespace is no C# namespace and was written as one.
    /// </summary>
    public static readonly DiagnosticDescriptor MarkerNamespaceRewritten = new(
        RuleIdentifiers.MarkerNamespaceRewritten,
        "The marker's namespace was rewritten",
        "The marker's namespace '{0}' is no C# namespace: the generated code is in '{1}'. Write that in the marker to say what is generated.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_AOT001: The Request Delegate Generator is on and does not see the generated endpoints.
    /// </summary>
    public static readonly DiagnosticDescriptor RequestDelegateGeneratorDoesNotSeeTheEndpoints = new(
        RuleIdentifiers.RequestDelegateGeneratorDoesNotSeeTheEndpoints,
        "The Request Delegate Generator does not see the generated endpoints",
        "EnableRequestDelegateGenerator is on (PublishAot sets it), but the Request Delegate Generator does not see the endpoints this generator emits - one source generator's output is not another's input. They are bound by reflection at run time (RequestDelegateFactory), which trimming and Native AOT do not support, and the build's trim and AOT analysis does not report it. A Native AOT server is not supported yet.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_AOT002: The root namespace's first segment binds to an ASP.NET Core name inside RDG's generated file.
    /// </summary>
    public static readonly DiagnosticDescriptor RootNamespaceBindsToAnAspNetCoreName = new(
        RuleIdentifiers.RootNamespaceBindsToAnAspNetCoreName,
        "The root namespace names an ASP.NET Core type or namespace",
        "The root namespace '{0}' starts with '{1}', which inside Microsoft.AspNetCore.Http.Generated is {2}. The Request Delegate Generator writes an [AsParameters] type without 'global::' there, so its generated code does not compile (CS0426) for an endpoint with parameters whose types are in this namespace. Give the project another root namespace (the marker's 'namespace', or the name of the specification file).",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
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
    /// ATC_API_GEN011: Contracts namespace cannot be auto-detected.
    /// </summary>
    public static readonly DiagnosticDescriptor ContractsNamespaceAmbiguous = new(
        RuleIdentifiers.ContractsNamespaceAmbiguous,
        "Contracts Namespace Cannot Be Auto-Detected",
        "Cannot auto-detect contracts namespace for Domain project '{0}'. Multiple sibling .atc-rest-api-server markers exist and none match the directory-name convention (<X>.Domain ↔ <X>.Contracts). Set 'contractsNamespace' explicitly in .atc-rest-api-server-handlers.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_GEN012: A hand-written handler shadows a scaffolded stub of the same name.
    /// </summary>
    public static readonly DiagnosticDescriptor HandlerStubShadowed = new(
        RuleIdentifiers.HandlerStubShadowed,
        "Handler Stub Shadowed by User Handler",
        "Handler '{0}' is implemented in '{1}' and also has a leftover scaffolded stub in '{2}'. The user handler is registered; delete the stub to avoid confusion.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_GEN013: A marker file could not be read - no JSON, or a value of the wrong kind; nothing is generated.
    /// </summary>
    public static readonly DiagnosticDescriptor MarkerConfigParseError = new(
        RuleIdentifiers.MarkerConfigParseError,
        "Marker File Could Not Be Read",
        "The marker file '{0}' could not be read: {1}. Nothing is generated until it is fixed.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_GEN020: A project has two marker files of one kind; nothing is generated.
    /// </summary>
    public static readonly DiagnosticDescriptor MultipleMarkerFiles = new(
        RuleIdentifiers.MultipleMarkerFiles,
        "More Than One Marker File",
        "The project has {0} '{1}' marker files ({2}); the generator takes one configuration per project. Nothing is generated until one is left.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_GEN021: A project has two base specifications; nothing is generated.
    /// </summary>
    public static readonly DiagnosticDescriptor MultipleSpecifications = new(
        RuleIdentifiers.MultipleSpecifications,
        "More Than One Specification",
        "The project has {0} specifications ({1}); the generator reads one specification per project, with its parts named '{2}_{{Part}}.yaml'. Nothing is generated until one is left.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_GEN014: A marker file has a setting the generator does not have, or a value a setting does not know.
    /// </summary>
    public static readonly DiagnosticDescriptor MarkerConfigUnknownSetting = new(
        RuleIdentifiers.MarkerConfigUnknownSetting,
        "Unknown Marker File Setting",
        "The marker file '{0}': {1}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_GEN015: An overlay the marker file lists is not an AdditionalFiles item of the project.
    /// </summary>
    public static readonly DiagnosticDescriptor OverlayNotFound = new(
        RuleIdentifiers.OverlayNotFound,
        "Overlay Not Found",
        "The marker file '{0}' lists the overlay '{1}', which is not an AdditionalFiles item of the project (add <AdditionalFiles Include=\"{1}\" /> next to the specification's); nothing is generated",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_GEN016: An overlay is invalid or one of its actions cannot be applied.
    /// </summary>
    public static readonly DiagnosticDescriptor OverlayInvalid = new(
        RuleIdentifiers.OverlayInvalid,
        "Overlay Cannot Be Applied",
        "The overlay '{0}'{1} cannot be applied, so nothing is generated: {2}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_GEN017: An overlay action's target selects nothing.
    /// </summary>
    public static readonly DiagnosticDescriptor OverlayTargetSelectsNothing = new(
        RuleIdentifiers.OverlayTargetSelectsNothing,
        "Overlay Target Selects Nothing",
        "The overlay '{0}'{1} changes nothing - has the specification changed (a renamed path or schema)? {2}",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_SRV002: An operation declares its 400 as a string while the ValidationFilter is on.
    /// </summary>
    public static readonly DiagnosticDescriptor ValidationBadRequestDeclaredAsString = new(
        RuleIdentifiers.ValidationBadRequestDeclaredAsString,
        "Validation 400 Declared As String",
        "'{0}' declares its 400 as a string, but the ValidationFilter answers an invalid request with a ValidationProblemDetails (application/problem+json): the client reads its title as the string, and the field errors are only in the response content. Declare the 400 as ValidationProblemDetails to keep them typed.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_GEN019: A handler scaffold cannot be written to disk.
    /// </summary>
    public static readonly DiagnosticDescriptor HandlerScaffoldNotWritten = new(
        RuleIdentifiers.HandlerScaffoldNotWritten,
        "Handler Scaffold Not Written",
        "The handler scaffold '{0}' cannot be written to '{1}': {2}. It is part of this build, but no file is created; check \"generateHandlersOutput\" and the folder.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_GEN018: An overlay file is an AdditionalFiles item, but the marker file does not list it.
    /// </summary>
    public static readonly DiagnosticDescriptor OverlayNotListed = new(
        RuleIdentifiers.OverlayNotListed,
        "Overlay Not Listed",
        "'{0}' is an overlay, but the marker file '{1}' does not list it in \"overlays\", so it is not applied",
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
    /// ATC_API_DEP011: Client EndpointPerOperation mode requires Atc.Rest.Client 2.4.0 or later.
    /// </summary>
    public static readonly DiagnosticDescriptor AtcRestClientTooOld = new(
        RuleIdentifiers.AtcRestClientTooOld,
        "Atc.Rest.Client Version Too Old",
        "The EndpointPerOperation generation mode requires Atc.Rest.Client {0} or later, and the project references {1}. Update the Atc.Rest.Client package reference.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Error,
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
    /// ATC_API_DEP006: a bearer scheme asks for a handler registered as "Bearer", and no package that provides one is referenced.
    /// </summary>
    public static readonly DiagnosticDescriptor JwtBearerRequiresPackage = new(
        RuleIdentifiers.JwtBearerRequiresPackage,
        "Bearer authentication handler package recommended",
        "The OpenAPI specification has a bearer security scheme, so the generated endpoints ask for an authentication scheme named \"Bearer\". Register one - AddJwtBearer() from Microsoft.AspNetCore.Authentication.JwtBearer or AddMicrosoftIdentityWebApi() from Microsoft.Identity.Web - unless the project registers its own handler under that name.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Info,
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

    /// <summary>
    /// ATC_API_DEP008: InjectLogger enabled but logging abstractions not available.
    /// </summary>
    public static readonly DiagnosticDescriptor LoggingRequiresPackage = new(
        RuleIdentifiers.LoggingRequiresPackage,
        "Microsoft.Extensions.Logging Package Required",
        "The marker file specifies injectLogger: true but Microsoft.Extensions.Logging.Abstractions is not available. Add a FrameworkReference to Microsoft.AspNetCore.App or a PackageReference to Microsoft.Extensions.Logging.Abstractions.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_DEP009: Health check config conflicts with health paths defined in OpenAPI spec.
    /// </summary>
    public static readonly DiagnosticDescriptor HealthCheckPathConflict = new(
        RuleIdentifiers.HealthCheckPathConflict,
        "Health Check Path Conflict",
        "The marker file has healthChecks.enabled but the OpenAPI spec also defines paths under '{0}'. This will produce duplicate endpoint registrations. Remove the health paths from the spec or disable healthChecks in the marker file.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// ATC_API_DEP010: Atc.Rest.HealthChecks package recommended for structured JSON health responses.
    /// </summary>
    public static readonly DiagnosticDescriptor HealthChecksPackageRecommended = new(
        RuleIdentifiers.HealthChecksPackageRecommended,
        "Atc.Rest.HealthChecks Package Recommended",
        "Health checks are enabled but Atc.Rest.HealthChecks package is not referenced. Add a PackageReference to Atc.Rest.HealthChecks for structured JSON health responses with per-check details.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Info,
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
            DescribeException(exception)));
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
            DescribeException(exception)));
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
            DescribeException(exception)));
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
    /// The root namespace of the generated code - the marker's <c>namespace</c>, else an <c>info.title</c> that
    /// already is a namespace, else the specification file's name (<see cref="NamespaceResolver.Resolve"/>): the one
    /// rule of every generator. A marker namespace that had to be rewritten is reported (ATC_API_GEN022).
    /// </summary>
    /// <param name="context">The source-production context the diagnostic is reported to.</param>
    /// <param name="markerNamespace">The marker's <c>namespace</c>, if any.</param>
    /// <param name="document">The specification.</param>
    /// <param name="specificationPath">The path of the specification file (the base file of a multi-part one).</param>
    /// <returns>The root namespace.</returns>
    public static string ResolveRootNamespace(
        SourceProductionContext context,
        string? markerNamespace,
        OpenApiDocument? document,
        string specificationPath)
    {
        var resolved = NamespaceResolver.Resolve(markerNamespace, document?.Info?.Title, specificationPath);
        if (!string.IsNullOrWhiteSpace(markerNamespace) &&
            !string.Equals(markerNamespace!.Trim(), resolved, StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(MarkerNamespaceRewritten, Location.None, markerNamespace.Trim(), resolved));
        }

        return resolved;
    }

    /// <summary>
    /// Reports that the Request Delegate Generator is on and does not see the generated endpoints.
    /// </summary>
    public static void ReportRequestDelegateGeneratorDoesNotSeeTheEndpoints(
        SourceProductionContext context)
        => context.ReportDiagnostic(Diagnostic.Create(RequestDelegateGeneratorDoesNotSeeTheEndpoints, Location.None));

    /// <summary>
    /// Reports that the root namespace's first segment binds to an ASP.NET Core name in RDG's generated file.
    /// </summary>
    public static void ReportRootNamespaceBindsToAnAspNetCoreName(
        SourceProductionContext context,
        string rootNamespace,
        string segment,
        string what)
        => context.ReportDiagnostic(Diagnostic.Create(RootNamespaceBindsToAnAspNetCoreName, Location.None, rootNamespace, segment, what));

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
    /// Reports that the contracts namespace cannot be auto-detected because multiple
    /// sibling .atc-rest-api-server markers exist and none match the directory-name convention.
    /// </summary>
    public static void ReportContractsNamespaceAmbiguous(
        SourceProductionContext context,
        string domainProjectName)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            ContractsNamespaceAmbiguous,
            Location.None,
            domainProjectName));
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
            DescribeException(exception)));
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
    /// Reports that a hand-written handler shadows a leftover scaffolded stub of the same name.
    /// </summary>
    public static void ReportHandlerStubShadowed(
        SourceProductionContext context,
        string handlerName,
        string userNamespace,
        string stubNamespace)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            HandlerStubShadowed,
            Location.None,
            handlerName,
            userNamespace,
            stubNamespace));
    }

    /// <summary>
    /// Reports every setting of a marker file the generator does not have, and every value a setting does not know
    /// (the setting is ignored, the value gives the setting its default).
    /// </summary>
    public static void ReportUnknownMarkerSettings(
        SourceProductionContext context,
        string markerPath,
        IReadOnlyList<string> problems)
    {
        foreach (var problem in problems)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MarkerConfigUnknownSetting,
                Location.None,
                markerPath,
                problem));
        }
    }

    /// <summary>
    /// Reports that a marker file could not be parsed and default configuration was used.
    /// </summary>
    public static void ReportMarkerConfigParseError(
        SourceProductionContext context,
        string markerPath,
        Exception exception,
        Type configType)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            MarkerConfigParseError,
            Location.None,
            markerPath,
            MarkerConfigInspector.DescribeReadError(exception, configType)));
    }

    /// <summary>
    /// Reports that a project has more than one marker file of one kind (ATC_API_GEN020).
    /// </summary>
    public static void ReportMultipleMarkerFiles(
        SourceProductionContext context,
        IReadOnlyList<string> markerPaths)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            MultipleMarkerFiles,
            Location.None,
            markerPaths.Count,
            Path.GetFileName(markerPaths[0]),
            string.Join(", ", markerPaths)));
    }

    /// <summary>
    /// Reports that the project has more than one base specification (ATC_API_GEN021); nothing is generated.
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="specificationPaths">The base specifications, the one the generator found first ahead.</param>
    public static void ReportMultipleSpecifications(
        SourceProductionContext context,
        IReadOnlyList<string> specificationPaths)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            MultipleSpecifications,
            Location.None,
            specificationPaths.Count,
            string.Join(", ", specificationPaths),
            Path.GetFileNameWithoutExtension(specificationPaths[0])));
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
    /// Reports that the client EndpointPerOperation mode requires a newer Atc.Rest.Client than the one referenced.
    /// </summary>
    public static void ReportAtcRestClientTooOld(
        SourceProductionContext context,
        Version minimum,
        Version referenced)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            AtcRestClientTooOld,
            Location.None,
            minimum.ToString(3),
            referenced.ToString(3)));
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
    /// Reports ATC_API_GEN019: a handler scaffold cannot be written to disk.
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="handlerName">The handler.</param>
    /// <param name="filePath">Where the scaffold was to be written.</param>
    /// <param name="exception">Why it could not be.</param>
    public static void ReportHandlerScaffoldNotWritten(
        SourceProductionContext context,
        string handlerName,
        string filePath,
        Exception exception)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            HandlerScaffoldNotWritten,
            Location.None,
            handlerName,
            filePath,
            exception.Message));
    }

    /// <summary>
    /// Reports ATC_API_SRV002: an operation declares its 400 as a string while the ValidationFilter is on.
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="operation">The operation id, or its method and path.</param>
    public static void ReportValidationBadRequestDeclaredAsString(
        SourceProductionContext context,
        string operation)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            ValidationBadRequestDeclaredAsString,
            Location.None,
            operation));
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

    /// <summary>
    /// Reports that Atc.Rest.HealthChecks package is recommended for structured health responses.
    /// </summary>
    public static void ReportHealthChecksPackageRecommended(
        SourceProductionContext context)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            HealthChecksPackageRecommended,
            Location.None));
    }

    /// <summary>
    /// Reports that healthChecks config conflicts with health paths in the OpenAPI spec.
    /// </summary>
    public static void ReportHealthCheckPathConflict(
        SourceProductionContext context,
        string healthPath)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            HealthCheckPathConflict,
            Location.None,
            healthPath));
    }

    /// <summary>
    /// Reports that injectLogger is enabled but Microsoft.Extensions.Logging is not available.
    /// </summary>
    public static void ReportLoggingRequiresPackage(
        SourceProductionContext context)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            LoggingRequiresPackage,
            Location.None));
    }

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
    /// ATCAPI_OPR022: 403 Forbidden response defined on an operation without any security requirement.
    /// </summary>
    public static readonly DiagnosticDescriptor ForbiddenWithoutAuthorization = new(
        RuleIdentifiers.ForbiddenWithoutAuthorization,
        "Forbidden Response On Anonymous Operation",
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
    /// ATCAPI_OPR024: 409 Conflict response on read-only operation (GET).
    /// </summary>
    public static readonly DiagnosticDescriptor ConflictOnNonMutatingOperation = new(
        RuleIdentifiers.ConflictOnNonMutatingOperation,
        "Conflict Response on Read-Only Operation",
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

    // ========== Dropped Feature Diagnostics ==========

    /// <summary>
    /// ATC_API_OPR035: A response declares links, which nothing is generated from.
    /// </summary>
    public static readonly DiagnosticDescriptor ResponseLinksDropped = new(
        RuleIdentifiers.ResponseLinksDropped,
        "Operation Validation",
        "Response links are declared on {0}: nothing is generated from them, a client follows them by hand",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <summary>
    /// Reports the OpenAPI features of the document that the generators drop without a trace: response links, one
    /// information naming the operations that declare them. (readOnly/writeOnly, callbacks and several servers are
    /// supported and report nothing.)
    /// </summary>
    public static List<Diagnostic> DetectUnsupportedFeatures(
        OpenApiDocument openApiDoc)
    {
        var diagnostics = new List<Diagnostic>();

        var operationsWithLinks = OperationsWithResponseLinks(openApiDoc);
        if (operationsWithLinks.Count > 0)
        {
            diagnostics.Add(Diagnostic.Create(ResponseLinksDropped, Location.None, string.Join(", ", operationsWithLinks)));
        }

        return diagnostics;
    }

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

        return Diagnostic.Create(descriptor, CreateLocation(message));
    }

    /// <summary>
    /// Checks if any DiagnosticMessage in the collection has Error severity.
    /// </summary>
    /// <param name="messages">The DiagnosticMessages to check.</param>
    /// <returns>True if any message is an error.</returns>
    public static bool HasErrors(
        IEnumerable<GeneratorDiagnosticMessage> messages)
        => messages.Any(m => m.Severity == GeneratorDiagnosticSeverity.Error);

    /// <summary>
    /// Gets a human-readable title from a rule ID: the name of its category (the letters between <c>ATC_API_</c>
    /// and the number), e.g. <c>ATC_API_NAM001</c> is "Naming Convention" and <c>ATC_API_CACHE001</c> "Caching".
    /// The categories are the ones the wiki's Analyzer Rules page lists.
    /// </summary>
    internal static string GetTitleFromRuleId(string ruleId)
    {
        const string prefix = "ATC_API_";
        if (!ruleId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return ruleId;
        }

        var end = prefix.Length;
        while (end < ruleId.Length && char.IsLetter(ruleId[end]))
        {
            end++;
        }

        return ruleId.Substring(prefix.Length, end - prefix.Length) switch
        {
            "GEN" => "Generation Error",
            "DEP" => "Dependency Error",
            "AOT" => "Native AOT",
            "VAL" => "Validation Error",
            "NAM" => "Naming Convention",
            "SEC" => "Security",
            "SRV" => "Server Configuration",
            "SCH" => "Schema Validation",
            "PTH" => "Path Validation",
            "OPR" => "Operation Validation",
            "MPT" => "Multi-Part Specification",
            "RL" => "Rate Limiting",
            "RTY" => "Retry",
            "CACHE" => "Caching",
            "CLT" => "Client Configuration",
            "STREAM" => "Streaming",
            "VER" => "Specification Version",
            "WBH" => "Webhook",
            _ => ruleId,
        };
    }

    /// <summary>
    /// Describes a failure for a generation-error diagnostic: the exception type and message, the first frames of
    /// its stack (where it was thrown) and the inner exception - on one line, as the error list shows it.
    /// </summary>
    /// <param name="exception">The exception a generator caught.</param>
    /// <returns>For example <c>NullReferenceException: Object reference … (at SchemaExtractor.Extract &lt;- …)</c>.</returns>
    internal static string DescribeException(Exception exception)
    {
        var sb = new StringBuilder();
        sb.Append(exception.GetType().Name).Append(": ").Append(OneLine(exception.Message));

        var frames = (exception.StackTrace ?? string.Empty)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("at ", StringComparison.Ordinal))
            .Select(line =>
            {
                var frame = line.Substring(3);
                var parameters = frame.IndexOf('(');
                return parameters > 0 ? frame.Substring(0, parameters) : frame;
            })
            .Take(3)
            .ToList();
        if (frames.Count > 0)
        {
            sb.Append(" (at ").Append(string.Join(" <- ", frames)).Append(')');
        }

        if (exception.InnerException is { } inner)
        {
            sb.Append(" -> ").Append(inner.GetType().Name).Append(": ").Append(OneLine(inner.Message));
        }

        return sb.ToString();

        static string OneLine(string text)
            => text.Replace("\r", string.Empty).Replace('\n', ' ');
    }

    /// <summary>
    /// The operations (by operationId, or method and path without one) with a response that declares links.
    /// </summary>
    private static List<string> OperationsWithResponseLinks(
        OpenApiDocument openApiDoc)
    {
        var operations = new List<string>();
        foreach (var path in openApiDoc.Paths)
        {
            if (path.Value?.Operations is null)
            {
                continue;
            }

            foreach (var entry in path.Value.Operations)
            {
                var operation = entry.Value;
                if (operation.Responses is not null &&
                    operation.Responses.Values.Any(response => response is { Links.Count: > 0 }))
                {
                    operations.Add(operation.OperationId ?? $"{entry.Key.ToString().ToUpperInvariant()} {path.Key}");
                }
            }
        }

        return operations;
    }

    /// <summary>
    /// Builds a file-based <see cref="Location"/> from a <see cref="GeneratorDiagnosticMessage"/>,
    /// falling back to <see cref="Location.None"/> when no file path is available.
    /// Diagnostics without a file-based location are not subject to `.editorconfig`
    /// severity overrides (e.g. dotnet_diagnostic.ATC_API_OPR001.severity), because
    /// Roslyn resolves those overrides based on the diagnostic's source file path.
    /// </summary>
    private static Location CreateLocation(GeneratorDiagnosticMessage message)
    {
        if (string.IsNullOrEmpty(message.FilePath))
        {
            return Location.None;
        }

        var line = System.Math.Max(0, (message.LineNumber ?? 1) - 1);
        var column = System.Math.Max(0, (message.ColumnNumber ?? 1) - 1);
        var linePosition = new LinePosition(line, column);
        var lineSpan = new LinePositionSpan(linePosition, linePosition);

        return Location.Create(message.FilePath!, new TextSpan(0, 0), lineSpan);
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
}