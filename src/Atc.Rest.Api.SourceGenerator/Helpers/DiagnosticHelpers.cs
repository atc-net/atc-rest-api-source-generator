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
    public static readonly DiagnosticDescriptor ServerGenerationError = Descriptor(RuleIdentifiers.ServerGenerationError);

    /// <summary>
    /// ATCAPI002: Server parsing error.
    /// </summary>
    public static readonly DiagnosticDescriptor ServerParsingError = Descriptor(RuleIdentifiers.ServerParsingError);

    /// <summary>
    /// ATCAPI003: Client generation error.
    /// </summary>
    public static readonly DiagnosticDescriptor ClientGenerationError = Descriptor(RuleIdentifiers.ClientGenerationError);

    /// <summary>
    /// ATCAPI004: Client parsing error.
    /// </summary>
    public static readonly DiagnosticDescriptor ClientParsingError = Descriptor(RuleIdentifiers.ClientParsingError);

    /// <summary>
    /// ATCAPI005: Handler scaffold generation error.
    /// </summary>
    public static readonly DiagnosticDescriptor HandlerScaffoldGenerationError = Descriptor(RuleIdentifiers.HandlerScaffoldGenerationError);

    /// <summary>
    /// ATCAPI006: Domain parsing error.
    /// </summary>
    public static readonly DiagnosticDescriptor DomainParsingError = Descriptor(RuleIdentifiers.DomainParsingError);

    /// <summary>
    /// ATCAPI007: Output directory not specified.
    /// </summary>
    public static readonly DiagnosticDescriptor OutputDirectoryNotSpecified = Descriptor(RuleIdentifiers.OutputDirectoryNotSpecified);

    /// <summary>
    /// ATCAPI008: Server generator requires ASP.NET Core references.
    /// </summary>
    public static readonly DiagnosticDescriptor ServerRequiresAspNetCore = Descriptor(RuleIdentifiers.ServerRequiresAspNetCore);

    /// <summary>
    /// ATC_API_GEN022: The marker's namespace is no C# namespace and was written as one.
    /// </summary>
    public static readonly DiagnosticDescriptor MarkerNamespaceRewritten = Descriptor(RuleIdentifiers.MarkerNamespaceRewritten);

    /// <summary>
    /// ATC_API_GEN023: The marker sets readEnumMemberNames.
    /// </summary>
    public static readonly DiagnosticDescriptor ReadEnumMemberNamesOn = Descriptor(RuleIdentifiers.ReadEnumMemberNamesOn);

    /// <summary>
    /// ATC_API_AOT001: The Request Delegate Generator is on and does not see the generated endpoints.
    /// </summary>
    public static readonly DiagnosticDescriptor RequestDelegateGeneratorDoesNotSeeTheEndpoints = Descriptor(RuleIdentifiers.RequestDelegateGeneratorDoesNotSeeTheEndpoints);

    /// <summary>
    /// ATC_API_AOT002: The root namespace's first segment binds to an ASP.NET Core name inside RDG's generated file.
    /// </summary>
    public static readonly DiagnosticDescriptor RootNamespaceBindsToAnAspNetCoreName = Descriptor(RuleIdentifiers.RootNamespaceBindsToAnAspNetCoreName);

    /// <summary>
    /// ATCAPI009: Domain generator requires ASP.NET Core references.
    /// </summary>
    public static readonly DiagnosticDescriptor DomainRequiresAspNetCore = Descriptor(RuleIdentifiers.DomainRequiresAspNetCore);

    /// <summary>
    /// ATCAPI010: Endpoint injection generation error.
    /// </summary>
    public static readonly DiagnosticDescriptor EndpointInjectionGenerationError = Descriptor(RuleIdentifiers.EndpointInjectionGenerationError);

    /// <summary>
    /// ATCAPI011: No endpoints found for endpoint injection.
    /// </summary>
    public static readonly DiagnosticDescriptor NoEndpointsFoundForInjection = Descriptor(RuleIdentifiers.NoEndpointsFoundForInjection);

    /// <summary>
    /// ATC_API_GEN010: Generation summary (Info level).
    /// </summary>
    public static readonly DiagnosticDescriptor GenerationSummary = Descriptor(RuleIdentifiers.GenerationSummary);

    /// <summary>
    /// ATC_API_GEN011: Contracts namespace cannot be auto-detected.
    /// </summary>
    public static readonly DiagnosticDescriptor ContractsNamespaceAmbiguous = Descriptor(RuleIdentifiers.ContractsNamespaceAmbiguous);

    /// <summary>
    /// ATC_API_GEN012: A hand-written handler shadows a scaffolded stub of the same name.
    /// </summary>
    public static readonly DiagnosticDescriptor HandlerStubShadowed = Descriptor(RuleIdentifiers.HandlerStubShadowed);

    /// <summary>
    /// ATC_API_GEN013: A marker file could not be read - no JSON, or a value of the wrong kind; nothing is generated.
    /// </summary>
    public static readonly DiagnosticDescriptor MarkerConfigParseError = Descriptor(RuleIdentifiers.MarkerConfigParseError);

    /// <summary>
    /// ATC_API_GEN020: A project has two marker files of one kind; nothing is generated.
    /// </summary>
    public static readonly DiagnosticDescriptor MultipleMarkerFiles = Descriptor(RuleIdentifiers.MultipleMarkerFiles);

    /// <summary>
    /// ATC_API_GEN021: A project has two base specifications; nothing is generated.
    /// </summary>
    public static readonly DiagnosticDescriptor MultipleSpecifications = Descriptor(RuleIdentifiers.MultipleSpecifications);

    /// <summary>
    /// ATC_API_GEN014: A marker file has a setting the generator does not have, or a value a setting does not know.
    /// </summary>
    public static readonly DiagnosticDescriptor MarkerConfigUnknownSetting = Descriptor(RuleIdentifiers.MarkerConfigUnknownSetting);

    /// <summary>
    /// ATC_API_GEN015: An overlay the marker file lists is not an AdditionalFiles item of the project.
    /// </summary>
    public static readonly DiagnosticDescriptor OverlayNotFound = Descriptor(RuleIdentifiers.OverlayNotFound);

    /// <summary>
    /// ATC_API_GEN016: An overlay is invalid or one of its actions cannot be applied.
    /// </summary>
    public static readonly DiagnosticDescriptor OverlayInvalid = Descriptor(RuleIdentifiers.OverlayInvalid);

    /// <summary>
    /// ATC_API_GEN017: An overlay action's target selects nothing.
    /// </summary>
    public static readonly DiagnosticDescriptor OverlayTargetSelectsNothing = Descriptor(RuleIdentifiers.OverlayTargetSelectsNothing);

    /// <summary>
    /// ATC_API_SRV002: An operation declares its 400 as a string while the ValidationFilter is on.
    /// </summary>
    public static readonly DiagnosticDescriptor ValidationBadRequestDeclaredAsString = Descriptor(RuleIdentifiers.ValidationBadRequestDeclaredAsString);

    /// <summary>
    /// ATC_API_GEN019: A handler scaffold cannot be written to disk.
    /// </summary>
    public static readonly DiagnosticDescriptor HandlerScaffoldNotWritten = Descriptor(RuleIdentifiers.HandlerScaffoldNotWritten);

    /// <summary>
    /// ATC_API_GEN024: An operation has no handler, and the scaffold is not written (AtcRestApiScaffoldHandlers is false).
    /// </summary>
    public static readonly DiagnosticDescriptor HandlerNotImplemented = Descriptor(RuleIdentifiers.HandlerNotImplemented);

    /// <summary>
    /// ATC_API_GEN018: An overlay file is an AdditionalFiles item, but the marker file does not list it.
    /// </summary>
    public static readonly DiagnosticDescriptor OverlayNotListed = Descriptor(RuleIdentifiers.OverlayNotListed);

    /// <summary>
    /// ATCAPI012: Client EndpointPerOperation mode requires Atc.Rest.Client reference.
    /// </summary>
    public static readonly DiagnosticDescriptor ClientRequiresAtcRestClient = Descriptor(RuleIdentifiers.ClientRequiresAtcRestClient);

    /// <summary>
    /// ATC_API_DEP011: Client EndpointPerOperation mode (and the typed client's Result style) requires Atc.Rest.Client 2.7.0 or later.
    /// </summary>
    public static readonly DiagnosticDescriptor AtcRestClientTooOld = Descriptor(RuleIdentifiers.AtcRestClientTooOld);

    /// <summary>
    /// ATC_API_DEP013: The server builds on Atc.Rest.MinimalApi and the project references a version older than 3.1.1.
    /// </summary>
    public static readonly DiagnosticDescriptor MinimalApiTooOld = Descriptor(RuleIdentifiers.MinimalApiTooOld);

    /// <summary>
    /// ATC_API_DEP014: A TypedClient uses a type of Atc.Rest.Client and the project does not reference the package.
    /// </summary>
    public static readonly DiagnosticDescriptor TypedClientRequiresAtcRestClient = Descriptor(RuleIdentifiers.TypedClientRequiresAtcRestClient);

    /// <summary>
    /// ATC_API_DEP012: SignalR hubs are declared and the client project has no Microsoft.AspNetCore.SignalR.Client.
    /// </summary>
    public static readonly DiagnosticDescriptor SignalRClientPackageRecommended = Descriptor(RuleIdentifiers.SignalRClientPackageRecommended);

    /// <summary>
    /// ATCAPI014: Resilience extensions require Microsoft.Extensions.Http.Resilience reference.
    /// </summary>
    public static readonly DiagnosticDescriptor ResilienceRequiresPackage = Descriptor(RuleIdentifiers.ResilienceRequiresPackage);

    /// <summary>
    /// ATC_API_DEP006: a bearer scheme asks for a handler registered as "Bearer", and no package that provides one is referenced.
    /// </summary>
    public static readonly DiagnosticDescriptor JwtBearerRequiresPackage = Descriptor(RuleIdentifiers.JwtBearerRequiresPackage);

    /// <summary>
    /// ATC_API_DEP007: useMinimalApiPackage, useValidationFilter or useGlobalErrorHandler enabled but Atc.Rest.MinimalApi
    /// package not referenced.
    /// </summary>
    public static readonly DiagnosticDescriptor MinimalApiPackageRequired = Descriptor(RuleIdentifiers.MinimalApiPackageRequired);

    /// <summary>
    /// ATC_API_DEP008: InjectLogger enabled but logging abstractions not available.
    /// </summary>
    public static readonly DiagnosticDescriptor LoggingRequiresPackage = Descriptor(RuleIdentifiers.LoggingRequiresPackage);

    /// <summary>
    /// ATC_API_DEP009: Health check config conflicts with health paths defined in OpenAPI spec.
    /// </summary>
    public static readonly DiagnosticDescriptor HealthCheckPathConflict = Descriptor(RuleIdentifiers.HealthCheckPathConflict);

    /// <summary>
    /// ATC_API_DEP010: Atc.Rest.HealthChecks package recommended for structured JSON health responses.
    /// </summary>
    public static readonly DiagnosticDescriptor HealthChecksPackageRecommended = Descriptor(RuleIdentifiers.HealthChecksPackageRecommended);

    // ========== OpenAPI Validation Diagnostics ==========

    /// <summary>
    /// ATC_API_VAL001: OpenAPI core parsing error from Microsoft.OpenApi library.
    /// </summary>
    public static readonly DiagnosticDescriptor OpenApiCoreError = Descriptor(RuleIdentifiers.OpenApiCoreError);

    /// <summary>
    /// ATC_API_VAL002: OpenAPI 2.0 not supported - must use OpenAPI 3.0.x.
    /// </summary>
    public static readonly DiagnosticDescriptor OpenApi20NotSupported = Descriptor(RuleIdentifiers.OpenApi20NotSupported);

    /// <summary>
    /// ATC_API_NAM001: OperationId must start with lowercase letter (camelCase).
    /// </summary>
    public static readonly DiagnosticDescriptor OperationIdMustBeCamelCase = Descriptor(RuleIdentifiers.OperationIdMustBeCamelCase);

    /// <summary>
    /// ATC_API_NAM002: Model name must use PascalCase.
    /// </summary>
    public static readonly DiagnosticDescriptor ModelNameMustBePascalCase = Descriptor(RuleIdentifiers.ModelNameMustBePascalCase);

    /// <summary>
    /// ATC_API_NAM003: Property name must use camelCase.
    /// </summary>
    public static readonly DiagnosticDescriptor PropertyNameMustBeCamelCase = Descriptor(RuleIdentifiers.PropertyNameMustBeCamelCase);

    /// <summary>
    /// ATC_API_NAM004: Parameter name must use camelCase.
    /// </summary>
    public static readonly DiagnosticDescriptor ParameterNameMustBeCamelCase = Descriptor(RuleIdentifiers.ParameterNameMustBeCamelCase);

    /// <summary>
    /// ATC_API_NAM005: Enum value must use PascalCase or UPPER_SNAKE_CASE.
    /// </summary>
    public static readonly DiagnosticDescriptor EnumValueCasing = Descriptor(RuleIdentifiers.EnumValueCasing);

    /// <summary>
    /// ATC_API_NAM006: Tag name must use kebab-case.
    /// </summary>
    public static readonly DiagnosticDescriptor TagNameMustBeKebabCase = Descriptor(RuleIdentifiers.TagNameMustBeKebabCase);

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
    /// Reports that the marker sets <c>readEnumMemberNames</c> (ATC_API_GEN023), so a rollout setting is not left on
    /// unnoticed.
    /// </summary>
    /// <param name="context">The source-production context the diagnostic is reported to.</param>
    /// <param name="markerFileName">The marker file's name.</param>
    public static void ReportReadEnumMemberNamesOn(
        SourceProductionContext context,
        string markerFileName)
        => context.ReportDiagnostic(Diagnostic.Create(ReadEnumMemberNamesOn, Location.None, markerFileName));

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
    public static void ReportSignalRClientPackageRecommended(
        SourceProductionContext context,
        IReadOnlyList<string> hubClients)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            SignalRClientPackageRecommended,
            Location.None,
            hubClients.Count,
            string.Join(", ", hubClients)));
    }

    public static void ReportTypedClientRequiresAtcRestClient(
        SourceProductionContext context,
        string reason)
        => context.ReportDiagnostic(Diagnostic.Create(
            TypedClientRequiresAtcRestClient,
            Location.None,
            reason));

    public static void ReportMinimalApiTooOld(
        SourceProductionContext context,
        Version minimum,
        Version referenced)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            MinimalApiTooOld,
            Location.None,
            minimum.ToString(3),
            referenced.ToString(3)));
    }

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
    /// Reports ATC_API_GEN024: an operation has no handler, and the scaffold is not written into the project.
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="handlerName">The handler's class name.</param>
    /// <param name="filePath">The file the scaffold would be written to.</param>
    public static void ReportHandlerNotImplemented(
        SourceProductionContext context,
        string handlerName,
        string filePath)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            HandlerNotImplemented,
            Location.None,
            handlerName,
            filePath));
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
    /// Reports that a setting that builds on Atc.Rest.MinimalApi is enabled but the package is not referenced.
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="setting">The marker setting that asks for the package.</param>
    public static void ReportMinimalApiPackageRequired(
        SourceProductionContext context,
        string setting)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            MinimalApiPackageRequired,
            Location.None,
            setting));
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
    /// ATC_API_SCH001: Missing title on array type.
    /// </summary>
    public static readonly DiagnosticDescriptor ArrayTitleMissing = Descriptor(RuleIdentifiers.ArrayTitleMissing);

    /// <summary>
    /// ATC_API_SCH002: Array type title not starting with uppercase.
    /// </summary>
    public static readonly DiagnosticDescriptor ArrayTitleNotUppercase = Descriptor(RuleIdentifiers.ArrayTitleNotUppercase);

    /// <summary>
    /// ATC_API_SCH003: Missing title on object type.
    /// </summary>
    public static readonly DiagnosticDescriptor ObjectTitleMissing = Descriptor(RuleIdentifiers.ObjectTitleMissing);

    /// <summary>
    /// ATC_API_SCH004: Object type title not starting with uppercase.
    /// </summary>
    public static readonly DiagnosticDescriptor ObjectTitleNotUppercase = Descriptor(RuleIdentifiers.ObjectTitleNotUppercase);

    /// <summary>
    /// ATC_API_SCH005: Implicit object definition in array property not supported.
    /// </summary>
    public static readonly DiagnosticDescriptor ImplicitArrayObjectNotSupported = Descriptor(RuleIdentifiers.ImplicitArrayObjectNotSupported);

    /// <summary>
    /// ATC_API_SCH006: Object name not using correct casing style.
    /// </summary>
    public static readonly DiagnosticDescriptor ObjectNameCasing = Descriptor(RuleIdentifiers.ObjectNameCasing);

    /// <summary>
    /// ATC_API_SCH007: Object property name not using correct casing style.
    /// </summary>
    public static readonly DiagnosticDescriptor PropertyNameCasing = Descriptor(RuleIdentifiers.PropertyNameCasing);

    /// <summary>
    /// ATC_API_SCH008: Enum name not using correct casing style.
    /// </summary>
    public static readonly DiagnosticDescriptor EnumNameCasing = Descriptor(RuleIdentifiers.EnumNameCasing);

    /// <summary>
    /// ATC_API_SCH009: Array property missing data type specification.
    /// </summary>
    public static readonly DiagnosticDescriptor ArrayPropertyMissingType = Descriptor(RuleIdentifiers.ArrayPropertyMissingType);

    /// <summary>
    /// ATC_API_SCH010: Implicit object definition on property not supported.
    /// </summary>
    public static readonly DiagnosticDescriptor ImplicitObjectNotSupported = Descriptor(RuleIdentifiers.ImplicitObjectNotSupported);

    /// <summary>
    /// ATC_API_SCH011: Array property missing items specification.
    /// </summary>
    public static readonly DiagnosticDescriptor ArrayPropertyMissingItems = Descriptor(RuleIdentifiers.ArrayPropertyMissingItems);

    /// <summary>
    /// ATC_API_SCH012: Missing key/name for object property.
    /// </summary>
    public static readonly DiagnosticDescriptor PropertyKeyMissing = Descriptor(RuleIdentifiers.PropertyKeyMissing);

    /// <summary>
    /// ATC_API_SCH013: Schema reference does not exist in components.schemas.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidSchemaReference = Descriptor(RuleIdentifiers.InvalidSchemaReference);

    // ========== Operation Validation Diagnostics ==========

    /// <summary>
    /// ATC_API_OPR001: Missing operationId.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationIdMissing = Descriptor(RuleIdentifiers.OperationIdMissing);

    /// <summary>
    /// ATC_API_OPR003: GET operationId should start with 'Get' or 'List'.
    /// </summary>
    public static readonly DiagnosticDescriptor GetOperationIdPrefix = Descriptor(RuleIdentifiers.GetOperationIdPrefix);

    /// <summary>
    /// ATC_API_OPR004: POST operationId should not start with 'Delete'.
    /// </summary>
    public static readonly DiagnosticDescriptor PostOperationIdPrefix = Descriptor(RuleIdentifiers.PostOperationIdPrefix);

    /// <summary>
    /// ATC_API_OPR005: PUT operationId should start with 'Update'.
    /// </summary>
    public static readonly DiagnosticDescriptor PutOperationIdPrefix = Descriptor(RuleIdentifiers.PutOperationIdPrefix);

    /// <summary>
    /// ATC_API_OPR006: PATCH operationId should start with 'Patch' or 'Update'.
    /// </summary>
    public static readonly DiagnosticDescriptor PatchOperationIdPrefix = Descriptor(RuleIdentifiers.PatchOperationIdPrefix);

    /// <summary>
    /// ATC_API_OPR007: DELETE operationId should start with 'Delete' or 'Remove'.
    /// </summary>
    public static readonly DiagnosticDescriptor DeleteOperationIdPrefix = Descriptor(RuleIdentifiers.DeleteOperationIdPrefix);

    /// <summary>
    /// ATC_API_OPR008: Pluralized operationId but response is single item.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationIdPluralizationMismatch = Descriptor(RuleIdentifiers.OperationIdPluralizationMismatch);

    /// <summary>
    /// ATC_API_OPR009: Singular operationId but response is array.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationIdSingularMismatch = Descriptor(RuleIdentifiers.OperationIdSingularMismatch);

    /// <summary>
    /// ATC_API_OPR010: Has BadRequest response but no parameters.
    /// </summary>
    public static readonly DiagnosticDescriptor BadRequestWithoutParameters = Descriptor(RuleIdentifiers.BadRequestWithoutParameters);

    /// <summary>
    /// ATC_API_OPR011: Global path parameter not present in route.
    /// </summary>
    public static readonly DiagnosticDescriptor GlobalPathParameterNotInRoute = Descriptor(RuleIdentifiers.GlobalPathParameterNotInRoute);

    /// <summary>
    /// ATC_API_OPR012: Operation missing path parameter defined in route.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationMissingPathParameter = Descriptor(RuleIdentifiers.OperationMissingPathParameter);

    /// <summary>
    /// ATC_API_OPR013: Operation path parameter not present in route.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationPathParameterNotInRoute = Descriptor(RuleIdentifiers.OperationPathParameterNotInRoute);

    /// <summary>
    /// ATC_API_OPR014: GET with path parameter missing NotFound response.
    /// </summary>
    public static readonly DiagnosticDescriptor GetMissingNotFoundResponse = Descriptor(RuleIdentifiers.GetMissingNotFoundResponse);

    /// <summary>
    /// ATC_API_OPR015: Path parameter missing required=true.
    /// </summary>
    public static readonly DiagnosticDescriptor PathParameterNotRequired = Descriptor(RuleIdentifiers.PathParameterNotRequired);

    /// <summary>
    /// ATC_API_OPR016: Path parameter must not be nullable.
    /// </summary>
    public static readonly DiagnosticDescriptor PathParameterNullable = Descriptor(RuleIdentifiers.PathParameterNullable);

    /// <summary>
    /// ATC_API_OPR017: RequestBody with inline model not supported.
    /// </summary>
    public static readonly DiagnosticDescriptor RequestBodyInlineModel = Descriptor(RuleIdentifiers.RequestBodyInlineModel);

    /// <summary>
    /// ATC_API_OPR018: Multiple 2xx status codes not supported.
    /// </summary>
    public static readonly DiagnosticDescriptor Multiple2xxStatusCodes = Descriptor(RuleIdentifiers.Multiple2xxStatusCodes);

    // ========== Response Code Consistency Diagnostics (OPR021-OPR025) ==========

    /// <summary>
    /// ATC_API_OPR021: 401 Unauthorized response defined but no security requirements.
    /// </summary>
    public static readonly DiagnosticDescriptor UnauthorizedWithoutSecurity = Descriptor(RuleIdentifiers.UnauthorizedWithoutSecurity);

    /// <summary>
    /// ATC_API_OPR022: 403 Forbidden response defined on an operation without any security requirement.
    /// </summary>
    public static readonly DiagnosticDescriptor ForbiddenWithoutAuthorization = Descriptor(RuleIdentifiers.ForbiddenWithoutAuthorization);

    /// <summary>
    /// ATC_API_OPR023: 404 NotFound response on POST operation (unusual).
    /// </summary>
    public static readonly DiagnosticDescriptor NotFoundOnPostOperation = Descriptor(RuleIdentifiers.NotFoundOnPostOperation);

    /// <summary>
    /// ATC_API_OPR024: 409 Conflict response on read-only operation (GET).
    /// </summary>
    public static readonly DiagnosticDescriptor ConflictOnNonMutatingOperation = Descriptor(RuleIdentifiers.ConflictOnNonMutatingOperation);

    /// <summary>
    /// ATC_API_OPR025: 429 TooManyRequests response defined but no rate limiting configured.
    /// </summary>
    public static readonly DiagnosticDescriptor TooManyRequestsWithoutRateLimiting = Descriptor(RuleIdentifiers.TooManyRequestsWithoutRateLimiting);

    // ========== Security Validation DiagnosticDescriptors ==========

    /// <summary>
    /// ATC_API_SEC001: Path authorize role not defined in global section.
    /// </summary>
    public static readonly DiagnosticDescriptor PathAuthorizeRoleNotDefined = Descriptor(RuleIdentifiers.PathAuthorizeRoleNotDefined);

    /// <summary>
    /// ATC_API_SEC002: Path authentication scheme not defined in global section.
    /// </summary>
    public static readonly DiagnosticDescriptor PathAuthenticationSchemeNotDefined = Descriptor(RuleIdentifiers.PathAuthenticationSchemeNotDefined);

    /// <summary>
    /// ATC_API_SEC003: Operation authorize role not defined in global section.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationAuthorizeRoleNotDefined = Descriptor(RuleIdentifiers.OperationAuthorizeRoleNotDefined);

    /// <summary>
    /// ATC_API_SEC004: Operation authentication scheme not defined in global section.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationAuthenticationSchemeNotDefined = Descriptor(RuleIdentifiers.OperationAuthenticationSchemeNotDefined);

    /// <summary>
    /// ATC_API_SEC005: Operation has authenticationRequired=false but has roles/schemes.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationAuthenticationConflict = Descriptor(RuleIdentifiers.OperationAuthenticationConflict);

    /// <summary>
    /// ATC_API_SEC006: Operation authorize role has incorrect casing vs global section.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationAuthorizeRoleCasing = Descriptor(RuleIdentifiers.OperationAuthorizeRoleCasing);

    /// <summary>
    /// ATC_API_SEC007: Operation authentication scheme has incorrect casing vs global.
    /// </summary>
    public static readonly DiagnosticDescriptor OperationAuthenticationSchemeCasing = Descriptor(RuleIdentifiers.OperationAuthenticationSchemeCasing);

    /// <summary>
    /// ATC_API_SEC008: Path authorize role has incorrect casing vs global section.
    /// </summary>
    public static readonly DiagnosticDescriptor PathAuthorizeRoleCasing = Descriptor(RuleIdentifiers.PathAuthorizeRoleCasing);

    /// <summary>
    /// ATC_API_SEC009: Path authentication scheme has incorrect casing vs global.
    /// </summary>
    public static readonly DiagnosticDescriptor PathAuthenticationSchemeCasing = Descriptor(RuleIdentifiers.PathAuthenticationSchemeCasing);

    /// <summary>
    /// ATC_API_SEC010: Path has authenticationRequired=false but has roles/schemes.
    /// </summary>
    public static readonly DiagnosticDescriptor PathAuthenticationConflict = Descriptor(RuleIdentifiers.PathAuthenticationConflict);

    // ========== Dropped Feature Diagnostics ==========

    /// <summary>
    /// ATC_API_OPR035: A response declares links, which nothing is generated from.
    /// </summary>
    public static readonly DiagnosticDescriptor ResponseLinksDropped = Descriptor(RuleIdentifiers.ResponseLinksDropped);

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
            "HUB" => "SignalR Hub",
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
    /// The location is the specification file, which is no C# file: an <c>.editorconfig</c> section's
    /// <c>dotnet_diagnostic.&lt;id&gt;.severity</c> does not reach it (Roslyn applies it to the syntax trees it matches); a
    /// global config's does, and the marker's <c>ruleSeverities</c>, which the generator applies itself.
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

    /// <summary>
    /// The Roslyn descriptor of a rule of the catalog (<see cref="RuleCatalog"/>): its title, message and severity,
    /// in the generator's category, enabled by default.
    /// </summary>
    /// <param name="ruleId">The rule's id.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Descriptor(string ruleId)
    {
        var rule = RuleCatalog.Rules[ruleId];
        return new DiagnosticDescriptor(
            ruleId,
            rule.Title,
            rule.MessageFormat,
            RuleIdentifiers.Category,
            rule.Severity switch
            {
                GeneratorDiagnosticSeverity.Error => DiagnosticSeverity.Error,
                GeneratorDiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
                _ => DiagnosticSeverity.Info,
            },
            isEnabledByDefault: true);
    }
}