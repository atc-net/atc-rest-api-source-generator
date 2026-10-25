// ReSharper disable UnusedVariable
// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
namespace Atc.Rest.Api.SourceGenerator;

/// <summary>
/// The source generator behind the <c>.atc-rest-api-server</c> marker file: the models, parameters, results, handler interfaces, endpoint definitions and dependency-injection extensions of every OpenAPI specification in the project.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "OK.")]
[Generator(LanguageNames.CSharp)]
public class ApiServerGenerator : IIncrementalGenerator
{
    // The Atc.Rest.MinimalApi a server that builds on it needs: 3.0 keys validation errors by the names the client sent,
    // keeps a handler's ValidationException field errors and writes every error as a problem; 3.1 validates the request a
    // form endpoint builds (RequestValidation)
    private static readonly Version MinimumMinimalApiVersion = new(3, 1, 1);

    /// <summary>
    /// Registers the pipeline: the marker file and the specifications as additional texts, the project's package references, and the server code and its diagnostics as the output.
    /// </summary>
    /// <param name="context">The initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find marker files - marker file presence IS the trigger for this generator
        // A sibling project's marker that the package's targets add to a domain project (AtcRestApiSiblingMarker) is that
        // project's, not this one's
        var markerFiles = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Where(static pair => MarkerFileHelper.IsServerMarker(pair.Left.Path) &&
                                  !BuildSettings.IsSiblingMarker(pair.Right.GetOptions(pair.Left)))
            .Select(static (pair, cancellationToken) =>
                new MarkerFileInfo(pair.Left.Path, pair.Left.GetText(cancellationToken)?.ToString() ?? "{}"))
            .Collect()
            .Select(static (array, _) => new EquatableArray<MarkerFileInfo>(array));

        // Register a pipeline to collect ALL OpenAPI YAML files (for multi-parts support)
        var yamlFiles = SpecificationFileProvider.Create(context);

        // The packages the generated code needs or recommends, read from the project's references
        // EnableRequestDelegateGenerator: what a Native AOT project turns on (PublishAot sets it)
        var requestDelegateGenerator = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
                options.GlobalOptions.TryGetValue("build_property.EnableRequestDelegateGenerator", out var value) &&
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));

        var packagesProvider = context.CompilationProvider
            .Combine(requestDelegateGenerator)
            .Select(static (pair, _) => (Compilation: pair.Left, RequestDelegateGenerator: pair.Right))
            .Select(static (project, _) => new ServerPackageReferences
            {
                RequestDelegateGenerator = project.RequestDelegateGenerator,
                NamesFoundFirst = project.RequestDelegateGenerator ? NamesBesideRequestDelegates(project.Compilation) : string.Empty,
                HasAspNetCore = project.Compilation.ReferencesAnyStartingWith("Microsoft.AspNetCore"),
                MinimalApiVersion = project.Compilation.VersionOf("Atc.Rest.MinimalApi"),
                HasHealthChecks = project.Compilation.References("Atc.Rest.HealthChecks"),
                HasBearerHandler = project.Compilation.References("Microsoft.AspNetCore.Authentication.JwtBearer") ||
                                   project.Compilation.References("Microsoft.Identity.Web"),
            });

        // Combine ALL YAML files (as collection) with marker files and compilation info
        var combined = yamlFiles
            .Combine(markerFiles)
            .Combine(packagesProvider);

        // Register source output - processes all YAML files together for multi-parts support
        context.RegisterSourceOutput(combined, RegisterSourceOutputAction);
    }

    /// <summary>
    /// The packages a server project references, as far as the generated code cares: ASP.NET Core (required),
    /// Atc.Rest.MinimalApi (the IEndpointDefinition and filters), Atc.Rest.HealthChecks (structured health
    /// responses), and a bearer handler package - JwtBearer or Microsoft.Identity.Web - for a bearer scheme.
    /// </summary>
    private sealed record ServerPackageReferences
    {
        public bool HasAspNetCore { get; init; }

        public bool HasMinimalApi => MinimalApiVersion is not null;

        /// <summary>
        /// The version of the referenced Atc.Rest.MinimalApi, or <see langword="null"/> when there is none.
        /// </summary>
        public Version? MinimalApiVersion { get; init; }

        public bool HasHealthChecks { get; init; }

        public bool HasBearerHandler { get; init; }

        /// <summary>
        /// The project turns the Request Delegate Generator on (<c>EnableRequestDelegateGenerator</c>).
        /// </summary>
        public bool RequestDelegateGenerator { get; init; }

        /// <summary>
        /// With that generator on: the names its generated file (namespace
        /// <c>Microsoft.AspNetCore.Http.Generated</c>) finds before the global namespace, each as
        /// <c>name=what it is</c>, one per line. Text, so the record compares by value.
        /// </summary>
        public string NamesFoundFirst { get; init; } = string.Empty;
    }

    // The public types and the namespaces of Microsoft.AspNetCore.Http, Microsoft.AspNetCore and Microsoft: a name
    // written without global:: inside Microsoft.AspNetCore.Http.Generated binds to the innermost of them
    private static string NamesBesideRequestDelegates(Compilation compilation)
    {
        var names = new SortedDictionary<string, string>(StringComparer.Ordinal);
        INamespaceSymbol? current = compilation.GlobalNamespace;
        foreach (var part in (string[])["Microsoft", "AspNetCore", "Http"])
        {
            current = current?.GetNamespaceMembers().FirstOrDefault(n => string.Equals(n.Name, part, StringComparison.Ordinal));
            if (current is null)
            {
                break;
            }

            foreach (var member in current.GetMembers())
            {
                if (member is INamespaceSymbol)
                {
                    names[member.Name] = $"the namespace {member.ToDisplayString()}";
                }
                else if (member.DeclaredAccessibility == Accessibility.Public)
                {
                    names[member.Name] = $"the type {member.ToDisplayString()}";
                }
            }
        }

        return string.Join("\n", names.Select(n => n.Key + "=" + n.Value));
    }

    private static void RegisterSourceOutputAction(
        SourceProductionContext productionContext,
        ((EquatableArray<YamlFileInfo> Left,
            EquatableArray<MarkerFileInfo> Right) Left,
            ServerPackageReferences Right) combinedData)
    {
        var ((yamlFiles, markers), packages) = combinedData;
        var hasAspNetCore = packages.HasAspNetCore;
        var hasMinimalApi = packages.HasMinimalApi;

        if (GeneratorStartup.ReadMarker<ServerConfig>(productionContext, yamlFiles, markers) is not { } started)
        {
            return;
        }

        var (marker, config) = started;

        // Validate ASP.NET Core references - generated code requires them
        if (!hasAspNetCore)
        {
            DiagnosticHelpers.ReportServerRequiresAspNetCore(productionContext);
            return;
        }

        // A setting enabled without Atc.Rest.MinimalApi: the generated code would not build, so say which setting asks for it
        var missingFor = new[]
            {
                ("useMinimalApiPackage", config.UseMinimalApiPackage),
                ("useValidationFilter", config.UseValidationFilter),
                ("useGlobalErrorHandler", config.UseGlobalErrorHandler),
            }
            .FirstOrDefault(s => s.Item2.MissesPackage(hasMinimalApi))
            .Item1;
        if (missingFor is not null)
        {
            DiagnosticHelpers.ReportMinimalApiPackageRequired(productionContext, missingFor);
            return;
        }

        // Each setting: always when enabled, never when disabled, and when the package is referenced for auto
        var useMinimalApiPackage = config.UseMinimalApiPackage.UsesPackage(hasMinimalApi);
        var useValidationFilter = config.UseValidationFilter.UsesPackage(hasMinimalApi);
        var useGlobalErrorHandler = config.UseGlobalErrorHandler.UsesPackage(hasMinimalApi);

        // ... in the version the generated server is written for
        if ((useMinimalApiPackage || useValidationFilter || useGlobalErrorHandler) &&
            packages.MinimalApiVersion is { } minimalApiVersion &&
            minimalApiVersion < MinimumMinimalApiVersion)
        {
            DiagnosticHelpers.ReportMinimalApiTooOld(productionContext, MinimumMinimalApiVersion, minimalApiVersion);
            return;
        }

        // Everything from here runs under the catch: a failure before the base file is known is reported on the marker
        var filePath = marker.Path;
        try
        {
            GeneratorFaultInjection.ThrowIfRequested(RuleIdentifiers.ServerGenerationError);

            if (GeneratorStartup.FindSpecification(productionContext, marker.Path, config.Overlays, yamlFiles) is not { } files)
            {
                return;
            }

            filePath = files.BaseFile.Path;
            GenerateApiServer(
                productionContext,
                files,
                config,
                useMinimalApiPackage,
                useValidationFilter,
                useGlobalErrorHandler,
                packages);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            DiagnosticHelpers.ReportServerGenerationError(productionContext, filePath, ex);
        }
    }

    /// <summary>
    /// Generates the API server from the project's specification (its parts merged).
    /// </summary>
    private static void GenerateApiServer(
        SourceProductionContext context,
        SpecificationFiles files,
        ServerConfig config,
        bool useMinimalApiPackage,
        bool useValidationFilter,
        bool useGlobalErrorHandler,
        ServerPackageReferences packages)
    {
        var openApiDoc = GeneratorStartup.Load(context, files, config, config.MultiPartConfiguration, parametersMustBind: true, DiagnosticHelpers.ReportServerParsingError);
        if (openApiDoc is null)
        {
            return;
        }

        // A rule about the specification only a server with the ValidationFilter has
        ReportValidationBadRequestsDeclaredAsString(context, openApiDoc, files, config, useValidationFilter);

        // The root namespace: marker namespace, qualifying info.title, then base file name - as for a client
        var projectName = DiagnosticHelpers.ResolveRootNamespace(context, config.Namespace, openApiDoc, files.BaseFile.Path);

        GenerateApiServerFromDocument(
            context,
            files.BaseFile.Path,
            openApiDoc,
            config,
            projectName,
            useMinimalApiPackage,
            useValidationFilter,
            useGlobalErrorHandler,
            packages);
    }

    /// <summary>
    /// Generates API server code from a pre-parsed OpenAPI document.
    /// Used by both single-file and multi-parts generation flows.
    /// </summary>
    private static void GenerateApiServerFromDocument(
        SourceProductionContext context,
        string yamlPath,
        OpenApiDocument openApiDoc,
        ServerConfig config,
        string projectName,
        bool useMinimalApiPackage,
        bool useValidationFilter,
        bool useGlobalErrorHandler,
        ServerPackageReferences packages)
    {
        // An inline allOf over a plain base becomes one inline object, so it gets a model of its own
        InlineAllOfFlattener.Flatten(openApiDoc);

        // What the project and the specification need to know before anything is generated
        ReportServerPreamble(context, openApiDoc, projectName, packages, useValidationFilter);

        // Filter paths based on include/exclude patterns before segmentation
        // (on a copy: the parsed document is cached and shared)
        openApiDoc = PathFilterHelper.WithFilteredPaths(openApiDoc, config.IncludePaths, config.ExcludePaths);

        // Get unique path segments for grouping generated files
        var pathSegments = PathSegmentHelper.GetUniquePathSegments(openApiDoc);

        // Scan for type conflicts once (optimization: O(schemas) instead of O(pathSegments × schemas))
        var conflicts = TypeConflictRegistry.ScanForConflicts(
            openApiDoc,
            projectName,
            pathSegments.Select(s => PathSegmentHelper.ResolveEffectivePathSegment(openApiDoc, projectName, s) ?? string.Empty));

        // The component schemas' C# names, which an inline enum's generated name must not take
        var componentTypeNames = TypeConflictRegistry.ComponentTypeNamesOf(openApiDoc);

        // Create system type conflict resolver for conditional Task qualification
        var modelNames = openApiDoc.Components?.Schemas?.Keys ?? [];
        var systemTypeResolver = new SystemTypeConflictResolver(modelNames);

        // Proxy over `context` that adds [ExcludeFromCodeCoverage] alongside [GeneratedCode] when
        // configured. Used by every sub-generation method below that only ever calls AddSource.
        var generatedContext = new GeneratedSourceContext(
            context,
            config.ExcludeFromCodeCoverage,
            DeprecationDetector.HasDeprecatedMembers(openApiDoc),
            $"global::{NamespaceBuilder.BuildBase(projectName)}.{EnumWireConverterExtractor.ClassName}",
            config.ExtensibleEnums,
            config.ReadEnumMemberNames);
        if (config.ReadEnumMemberNames)
        {
            DiagnosticHelpers.ReportReadEnumMemberNamesOn(context, ".atc-rest-api-server");
        }

        // Detect shared schemas (used by multiple path segments) for deduplication
        var sharedSchemas = PathSegmentHelper.GetSharedSchemas(openApiDoc);

        // The models an XML body can carry get an XML mapping (only when the specification has an XML body)
        var xmlModels = XmlBodyReachability.Collect(openApiDoc);
        XmlModelEmitter.EmitSupport(generatedContext, projectName, xmlModels);
        XmlModelEmitter.EmitProblemDetailsWriter(generatedContext, projectName, xmlModels);

        // The models bound by Content-Type (JSON and/or XML request bodies): a partial with BindAsync
        var boundRequestModels = XmlBodyHelper.ServerBoundRequestModels(openApiDoc);

        // The readers a request body is bound by (a stream of records, a list, the body problems)
        EmitRequestBodySupport(generatedContext, openApiDoc, projectName, boundRequestModels);

        // The models of an XML request body name their XML names where they differ from the JSON ones
        // ([XmlElement] / [XmlAttribute]): a validation filter maps an XML request's error keys through them
        var xmlRequestModels = XmlBodyReachability.CollectRequestBodies(openApiDoc);

        // A model only an XML request carries is validated as XML only: its messages name it by its XML names too
        var xmlOnlyRequestModels = new HashSet<string>(xmlRequestModels, StringComparer.Ordinal);
        xmlOnlyRequestModels.ExceptWith(XmlBodyReachability.CollectOtherRequests(openApiDoc));

        // A spec-defined ProblemDetails / ValidationProblemDetails is not materialized: the server binds
        // those names to Microsoft.AspNetCore.Mvc (every generated Results file imports it), and a second,
        // generated type of the same name would make every reference ambiguous (CS0104). Unless another
        // schema extends or holds it: then it is a model like any other, and the files that import both
        // namespaces alias the name to the framework type.
        var referencedReservedSchemas = BuiltInErrorContractNames.ReferencedByOtherSchemas(openApiDoc);
        sharedSchemas.RemoveWhere(name => BuiltInErrorContractNames.IsReserved(name) && !referencedReservedSchemas.Contains(name));

        var bodies = new ServerBodyModels(xmlModels, boundRequestModels, xmlRequestModels, xmlOnlyRequestModels, RequireJsonProperties: !useValidationFilter);

        // Shared models and enums first (under the common namespace without a segment suffix)
        if (sharedSchemas.Count > 0)
        {
            var sharedRegistry = TypeConflictRegistry.ForSegment(conflicts, projectName, extensibleEnums: config.ExtensibleEnums, componentTypeNames: componentTypeNames);
            EmitSchemaTypes(generatedContext, openApiDoc, projectName, sharedSchemas, pathSegment: null, sharedRegistry, config, includeSharedModelsUsing: false, bodies);
        }

        // Generate shared IEndpointDefinition interface once (unless using Atc.Rest.MinimalApi package)
        if (!useMinimalApiPackage)
        {
            GenerateSharedEndpointDefinitionInterface(generatedContext, projectName);
        }

        // Generate files per path segment (excluding shared schemas)
        // Tracks the namespace segment actually used per path segment, so the cross-segment
        // aggregation below emits usings and Map*/Add* calls that match what was generated.
        var effectiveSegments = new List<string>();
        var endpointDefinitionsBySegment = new List<(string Segment, List<string> EndpointDefinitionClassNames)>();
        foreach (var pathSegment in pathSegments)
        {
            // A superseded IDE run stops here instead of emitting the rest
            context.CancellationToken.ThrowIfCancellationRequested();

            var (effectiveSegment, segmentEndpointDefinitionClassNames) = EmitSegmentContracts(
                generatedContext,
                openApiDoc,
                projectName,
                pathSegment,
                config,
                conflicts,
                sharedSchemas,
                componentTypeNames,
                referencedReservedSchemas,
                systemTypeResolver,
                useMinimalApiPackage,
                useValidationFilter,
                bodies);
            effectiveSegments.Add(effectiveSegment);

            if (segmentEndpointDefinitionClassNames.Count > 0)
            {
                endpointDefinitionsBySegment.Add((effectiveSegment, segmentEndpointDefinitionClassNames));
            }
        }

        // Generate the single endpoint definition mapping extension containing one
        // Map{Segment}Endpoints method per path segment
        GenerateConsolidatedEndpointDefinitionExtensions(
            generatedContext,
            projectName,
            endpointDefinitionsBySegment);

        // The runtime helpers the generated code uses where the specification needs them
        EmitServerRuntimeHelpers(generatedContext, openApiDoc, projectName);

        // Generate combined endpoint mapping extension (calls all path segment endpoint methods)
        GenerateCombinedEndpointMapping(generatedContext, projectName, effectiveSegments);

        // The cross-cutting features the specification declares: versioning, security, rate limiting, SignalR hubs,
        // caching, webhooks, health checks
        EmitServerFeatures(context, generatedContext, openApiDoc, projectName, config, useValidationFilter, packages);

        var operationCount = openApiDoc.Paths?.Sum(p => p.Value.Operations?.Count ?? 0) ?? 0;

        // The host surface: Add...Api() and Map...Api() with their options, and the JSON options
        EmitHostSurface(generatedContext, openApiDoc, projectName, config, useGlobalErrorHandler, effectiveSegments, operationCount);

        // Report generation summary
        var schemaCount = openApiDoc.Components?.Schemas?.Count ?? 0;

        ReportServerGenerationSummary(
            context,
            yamlPath,
            warnings: 0, // Validation diagnostics already reported earlier
            errors: 0,
            modelCount: schemaCount,
            handlerCount: operationCount,
            endpointCount: pathSegments.Count);
    }

    // The diagnostics before generation: the Request Delegate Generator does not see the endpoints, a bearer scheme
    // needs a handler package, a 400 declared as a string cannot carry the validation filter's field errors
    private static void ReportServerPreamble(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        ServerPackageReferences packages,
        bool useValidationFilter)
    {
        // What a project aiming at Native AOT must know: its endpoints are not the Request Delegate Generator's, and
        // a root namespace that generator's file binds elsewhere does not compile
        if (packages.RequestDelegateGenerator)
        {
            DiagnosticHelpers.ReportRequestDelegateGeneratorDoesNotSeeTheEndpoints(context);

            var segment = projectName.Split('.')[0];
            var found = packages.NamesFoundFirst
                .Split('\n')
                .FirstOrDefault(n => n.StartsWith(segment + "=", StringComparison.Ordinal));
            if (found is not null)
            {
                DiagnosticHelpers.ReportRootNamespaceBindsToAnAspNetCoreName(context, projectName, segment, found.Substring(segment.Length + 1));
            }
        }

        // Report diagnostics for unsupported OpenAPI features
        foreach (var diagnostic in DiagnosticHelpers.DetectUnsupportedFeatures(openApiDoc))
        {
            context.ReportDiagnostic(diagnostic);
        }

        // A bearer scheme asks the host for a handler registered as "Bearer": say so when no package that
        // provides one is referenced (a hand-written handler is a valid choice, hence information only)
        if (openApiDoc.HasJwtBearerSecurity() && !packages.HasBearerHandler)
        {
            DiagnosticHelpers.ReportJwtBearerRequiresPackage(context);
        }
    }

    /// <summary>
    /// ATC_API_SRV002: the ValidationFilter writes a ValidationProblemDetails, and a 400 declared as a string cannot carry
    /// its field errors. A rule about the specification like the validator's: on the operation's line, switched off by
    /// <c>x-atc-suppress</c> on the operation or its path item, its severity the marker's <c>ruleSeverities</c>.
    /// </summary>
    private static void ReportValidationBadRequestsDeclaredAsString(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        SpecificationFiles files,
        ServerConfig config,
        bool useValidationFilter)
    {
        if (!useValidationFilter)
        {
            return;
        }

        var found = ErrorResponseShapeResolver.GetOperationsWithStringBadRequest(openApiDoc)
            .Select(operation => new GeneratorDiagnosticMessage(
                RuleIdentifiers.ValidationBadRequestDeclaredAsString,
                string.Format(System.Globalization.CultureInfo.InvariantCulture, RuleCatalog.Rules[RuleIdentifiers.ValidationBadRequestDeclaredAsString].MessageFormat, operation.Operation),
                Generator.Models.DiagnosticSeverity.Warning,
                files.BaseFile.Path,
                Scope: SpecSuppressions.Operation(operation.Method, operation.Path)))
            .ToList();
        if (found.Count == 0)
        {
            return;
        }

        var reported = SpecSuppressions.Apply(openApiDoc, found);
        SpecLocations.Apply(reported, SpecificationLoader.SourcesOf(files.BaseFile, files.PartFiles));
        var severities = RuleSeverityOverrides.From(config.RuleSeverities, config.SuppressRules);
        foreach (var message in reported)
        {
            if (severities.Apply(message) is { } shown)
            {
                context.ReportDiagnostic(DiagnosticHelpers.ToRoslynDiagnostic(shown));
            }
        }
    }

    // The readers of a request body the generated code binds: a stream of records, the body problems, a JSON-only list
    // and an XML list
    private static void EmitRequestBodySupport(
        GeneratedSourceContext generatedContext,
        OpenApiDocument openApiDoc,
        string projectName,
        Dictionary<string, ServerBoundModel> boundRequestModels)
    {
        // A body sent as a stream of records (JSON Lines, JSON Text Sequences) is read by the generated
        // StreamedRequestBody<T>, one record at a time
        var hasStreamedRequestBody = StreamedRequestBodyExtractor.DocumentHasStreamedRequestBody(openApiDoc);
        if (hasStreamedRequestBody)
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.{StreamedRequestBodyExtractor.TypeName}.g.cs",
                StreamedRequestBodyExtractor.GenerateContent(projectName));
        }

        // A request body that is text, read by its charset
        var hasTextRequestBody = TextRequestBodyExtractor.DocumentHasTextRequestBody(openApiDoc);
        if (hasTextRequestBody)
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.{TextRequestBodyExtractor.TypeName}.g.cs",
                TextRequestBodyExtractor.GenerateContent(projectName));
        }

        // A body the generated reader cannot read is a problem with its status on every host
        if (boundRequestModels.Count > 0 || XmlBodyHelper.HasServerBoundBody(openApiDoc) || hasStreamedRequestBody || hasTextRequestBody)
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.{XmlModelMapperExtractor.RequestBodyProblemsTypeName}.g.cs",
                XmlModelMapperExtractor.GenerateRequestBodyProblems(projectName));
        }

        // A list body offered as JSON only is read into the generated JsonRequestBodyList<T>
        if (XmlBodyHelper.HasJsonOnlyListBody(openApiDoc))
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.{XmlBodyHelper.JsonRequestBodyListTypeName}.g.cs",
                XmlModelMapperExtractor.GenerateJsonRequestBodyList(projectName));
        }

        // A list body offered as XML is bound into the generated RequestBodyList<T>
        if (boundRequestModels.Values.Any(m => m.BoundInList))
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.{XmlModelMapperExtractor.RequestBodyListTypeName}.g.cs",
                XmlModelMapperExtractor.GenerateRequestBodyList(projectName));
        }
    }

    // A set of schemas as types: the models (with their XML mapping and Content-Type binding), the enums, the tuples
    // and the polymorphic bases
    private static void EmitSchemaTypes(
        GeneratedSourceContext generatedContext,
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemas,
        string? pathSegment,
        TypeConflictRegistry registry,
        ServerConfig config,
        bool includeSharedModelsUsing,
        ServerBodyModels bodies)
    {
        GenerateModelsForSchemas(
            generatedContext,
            openApiDoc,
            projectName,
            schemas,
            pathSegment,
            registry,
            config.IncludeDeprecated,
            config.GeneratePartialModels,
            includeSharedModelsUsing,
            xmlModels: bodies.XmlModels,
            boundRequestModels: bodies.BoundRequestModels,
            xmlRequestModels: bodies.XmlRequestModels,
            xmlOnlyRequestModels: bodies.XmlOnlyRequestModels,
            requireJsonProperties: bodies.RequireJsonProperties);

        SchemaTypeEmitter.EmitEnums(
            generatedContext,
            openApiDoc,
            projectName,
            schemas,
            pathSegment,
            generatedContext.ExtensibleEnums,
            stringOnTheWire: true);

        SchemaTypeEmitter.EmitTuples(
            generatedContext,
            openApiDoc,
            projectName,
            schemas,
            pathSegment);

        PolymorphicTypeEmitter.Emit(
            generatedContext,
            openApiDoc,
            projectName,
            schemas,
            pathSegment,
            bodies.BoundRequestModels);
    }

    // One path segment's contracts: its models, parameters, results, handler interfaces and endpoint definitions; the
    // segment as its namespace uses it, and the endpoint definition classes for the mapping extension
    private static (string EffectiveSegment, List<string> EndpointDefinitionClassNames) EmitSegmentContracts(
        GeneratedSourceContext generatedContext,
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        ServerConfig config,
        ISet<string> conflicts,
        HashSet<string> sharedSchemas,
        ISet<string> componentTypeNames,
        ISet<string> referencedReservedSchemas,
        SystemTypeConflictResolver systemTypeResolver,
        bool useMinimalApiPackage,
        bool useValidationFilter,
        ServerBodyModels bodies)
    {
        // Resolve the segment used for namespaces and file names. It collapses to an empty
        // string when the segment is redundant (single segment, or it duplicates the root
        // namespace tail), while the raw pathSegment is still used to filter operations.
        var effectiveSegment = PathSegmentHelper.ResolveEffectivePathSegment(openApiDoc, projectName, pathSegment) ?? string.Empty;

        // Create conflict registry for this path segment (lightweight: O(1)).
        // Uses the effective segment - the raw pathSegment would qualify conflicting types
        // against a namespace that is never generated when the segment collapses away.
        var registry = TypeConflictRegistry.ForSegment(conflicts, projectName, effectiveSegment, sharedSchemas, config.ExtensibleEnums, componentTypeNames);

        // Get segment-specific schemas (excluding shared ones) — uses pre-computed sharedSchemas to avoid O(S²)
        var segmentSchemas = PathSegmentHelper.GetSegmentSpecificSchemas(openApiDoc, pathSegment, sharedSchemas);

        // Same as for the shared set: ProblemDetails / ValidationProblemDetails bind to the framework types.
        segmentSchemas.RemoveWhere(name => BuiltInErrorContractNames.IsReserved(name) && !referencedReservedSchemas.Contains(name));

        // Segment-specific models (excluding shared ones), with the shared models' using
        if (segmentSchemas.Count > 0)
        {
            EmitSchemaTypes(generatedContext, openApiDoc, projectName, segmentSchemas, effectiveSegment, registry, config, includeSharedModelsUsing: sharedSchemas.Count > 0, bodies);
        }

        // Determine if this segment has its own models namespace. Inline (anonymous) request/response
        // schemas - error responses included - are emitted into it too, so they must count here.
        var hasSegmentModels = segmentSchemas.Count > 0 ||
                               PathSegmentHelper.PathSegmentHasInlineModels(openApiDoc, pathSegment);

        // Generate parameter classes
        GenerateParameterClasses(
            generatedContext,
            openApiDoc,
            projectName,
            pathSegment,
            registry,
            config.IncludeDeprecated,
            includeSharedModelsUsing: sharedSchemas.Count > 0,
            includeSegmentModelsUsing: hasSegmentModels,
            emitInlineParameterEnums: config.InlineParameterEnums == InlineParameterEnumMode.Enum,
            validateStrategy: config.ValidateSpecificationStrategy,
            namespaceSegment: effectiveSegment);

        // Generate result classes
        GenerateResultClasses(
            generatedContext,
            openApiDoc,
            projectName,
            pathSegment,
            registry,
            systemTypeResolver,
            config.IncludeDeprecated,
            includeSharedModelsUsing: sharedSchemas.Count > 0,
            includeSegmentModelsUsing: hasSegmentModels,
            namespaceSegment: effectiveSegment);

        // Generate handler interfaces
        GenerateHandlerInterfaces(
            generatedContext,
            openApiDoc,
            projectName,
            pathSegment,
            systemTypeResolver,
            config.IncludeDeprecated,
            namespaceSegment: effectiveSegment);

        // Generate endpoint definition classes (the mapping extension is emitted once, below)
        var segmentEndpointDefinitionClassNames = GenerateEndpointRegistrations(
            generatedContext,
            openApiDoc,
            projectName,
            pathSegment,
            registry,
            systemTypeResolver,
            config.SubFolderStrategy,
            config.IncludeDeprecated,
            useMinimalApiPackage,
            useValidationFilter,
            config.VersioningStrategy,
            config.DefaultApiVersion,
            config.UseServersBasePath,
            includeSharedModelsUsing: sharedSchemas.Count > 0,
            namespaceSegment: effectiveSegment,
            useAntiforgery: config.UseAntiforgery);

        return (effectiveSegment, segmentEndpointDefinitionClassNames);
    }

    // The runtime helpers: the parsable list of an array query parameter, the wire-binding step, the length-limit and
    // value attributes, the sequential-results writers
    private static void EmitServerRuntimeHelpers(
        GeneratedSourceContext generatedContext,
        OpenApiDocument openApiDoc,
        string projectName)
    {
        // Generate ParsableList<T> helper type when any query parameter uses array types
        if (CodeGenerationService.HasQueryArrayParameters(openApiDoc))
        {
            GenerateParsableListHelper(generatedContext, projectName);
        }

        // Generate the pre-binding step for enum wire values and cookie parameters when an operation needs it
        if (WireBindingHelper.IsNeeded(openApiDoc))
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.WireBinding.g.cs",
                WireBindingHelper.GenerateContent(projectName));
        }

        // The trim- and AOT-safe length-limit attributes the models and parameters are written with
        if (LengthLimitAttributesExtractor.IsNeeded(openApiDoc))
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.Generated.LengthLimitAttributes.g.cs",
                LengthLimitAttributesExtractor.Generate(NamespaceBuilder.BuildBase(projectName), TextRequestBodyExtractor.DocumentHasTextRequestBody(openApiDoc)));
        }

        // The attributes a pattern (on a value or on the items of an array) and a format: uri are written with
        if (LengthLimitAttributesExtractor.NeededValueAttributes(openApiDoc) is { Count: > 0 } valueAttributes)
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.Generated.ValueAttributes.g.cs",
                LengthLimitAttributesExtractor.GenerateValueAttributes(NamespaceBuilder.BuildBase(projectName), valueAttributes, TextRequestBodyExtractor.DocumentHasTextRequestBody(openApiDoc)));
        }

        // Generate the server-side SequentialResults helper (the writer-based sequential-streaming
        // writers + their IResult wrappers for JSON Lines, JSON Text Sequence and multipart/mixed)
        // when an operation uses one of those framings. Uses the shared CodeGenerationService
        // producer (parallel to the client-side StreamReaders emission).
        var sequentialResults = CodeGenerationService.GenerateSequentialResults(openApiDoc, projectName);
        if (sequentialResults is not null)
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.Streaming.SequentialResults.g.cs",
                sequentialResults.Content);
        }
    }

    // The cross-cutting features the specification declares, each only when declared
    private static void EmitServerFeatures(
        SourceProductionContext context,
        GeneratedSourceContext generatedContext,
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config,
        bool useValidationFilter,
        ServerPackageReferences packages)
    {
        // Generate versioning DI registration (only if versioning is enabled)
        if (config.VersioningStrategy != VersioningStrategyType.None)
        {
            GenerateVersioningDependencyInjection(generatedContext, projectName, config);
        }

        // Generate security policies and DI extensions (only if OAuth2 scopes exist)
        // Note: Package availability checks are not performed here as Contracts projects
        // don't need the packages directly - consuming API projects will need them
        if (openApiDoc.HasSecuritySchemes())
        {
            GenerateSecurityPolicies(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateSecurityDependencyInjection(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
        }

        // Generate OpenID Connect authentication extension (only if OpenID Connect is configured)
        if (openApiDoc.HasOpenIdConnectSecurity())
        {
            GenerateOpenIdConnectAuthentication(generatedContext, openApiDoc, projectName, config.ApiName);
        }

        // Generate rate limiting policies and DI extensions (only if rate limiting is configured)
        // Note: Package availability checks are not performed here as Contracts projects
        // don't need the packages directly - consuming API projects will need them
        if (openApiDoc.HasRateLimiting())
        {
            GenerateRateLimitPolicies(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateRateLimitDependencyInjection(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
        }

        // The SignalR hubs of the specification (x-signalr-hubs): the client interface of a typed hub, the hub's own
        // methods, and its names and path
        foreach (var (hintName, content) in SignalRHubExtractor.ExtractServer(openApiDoc, projectName))
        {
            generatedContext.AddGeneratedSource(hintName, content);
        }

        // The name of each operation's endpoint, as constants (what .WithName(...) uses)
        var operationNames = OperationNamesExtractor.Extract(
            openApiDoc,
            projectName,
            config.IncludeDeprecated,
            includeWebhooks: config.GenerateWebhooks);
        if (operationNames is not null)
        {
            generatedContext.AddGeneratedSource($"{projectName}.ApiOperations.g.cs", operationNames);
        }

        // Generate Output Caching policies and DI extensions (only if output caching is configured)
        if (openApiDoc.HasOutputCaching())
        {
            GenerateOutputCachePolicies(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateOutputCachingDependencyInjection(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
        }

        // Generate HybridCache policies and DI extensions (only if hybrid caching is configured)
        if (openApiDoc.HasHybridCaching())
        {
            GenerateHybridCachePolicies(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateHybridCachingDependencyInjection(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
        }

        // Generate webhook handlers, parameters, results, endpoints, and DI (only if webhooks exist and enabled)
        if (config.GenerateWebhooks && openApiDoc.HasWebhooks)
        {
            GenerateWebhookHandlerInterfaces(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateWebhookParameterClasses(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateWebhookResultClasses(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateWebhookEndpoints(generatedContext, openApiDoc, projectName, config, useValidationFilter);
        }

        // Generate health check endpoints (if configured in marker file)
        if (config.HealthChecks is { Enabled: true })
        {
            // Check for conflict: health paths in both config and OpenAPI spec
            var healthPath = config.HealthChecks.Path.TrimEnd('/');
            if (openApiDoc.Paths is not null &&
                openApiDoc.Paths.Keys.Any(p => p.StartsWith(healthPath, StringComparison.OrdinalIgnoreCase)))
            {
                DiagnosticHelpers.ReportHealthCheckPathConflict(context, healthPath);
            }

            if (!packages.HasHealthChecks)
            {
                DiagnosticHelpers.ReportHealthChecksPackageRecommended(context);
            }

            GenerateHealthCheckEndpoints(generatedContext, projectName, config.HealthChecks, packages.HasHealthChecks);
            GenerateHealthCheckServiceExtensions(generatedContext, projectName);
        }
    }

    // Add...Api() and Map...Api() with their options (without Atc.Rest.MinimalApi too - then the framework's exception
    // handler stands where the package's error handler would), and the JSON options the results write through
    private static void EmitHostSurface(
        GeneratedSourceContext generatedContext,
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config,
        bool useGlobalErrorHandler,
        List<string> effectiveSegments,
        int operationCount)
    {
        // Generate the simplified API surface: Add…Api() and Map…Api() with their options. Without
        // Atc.Rest.MinimalApi too - then the framework's exception handler stands where the package's error handler
        // would, and everything else the host wiring does (ProblemDetails, the enum converter, the output cache, the
        // order of authentication and the rate limiter) is the same.
        GenerateApiOptions(generatedContext, openApiDoc, projectName, config, useGlobalErrorHandler);

        // The unified extensions register the handlers and map the endpoints: with no operations there are
        // none of either (ATC_API_PTH002 says so), and the extensions would name namespaces that do not exist
        if (operationCount > 0)
        {
            GenerateUnifiedServiceCollection(generatedContext, openApiDoc, projectName, config, effectiveSegments);
            GenerateUnifiedWebApplicationExtensions(generatedContext, openApiDoc, projectName, config, useGlobalErrorHandler);
        }

        // The JSON results and stream writers write through the metadata the app's JSON options resolve (trim- and AOT-safe)
        generatedContext.AddGeneratedSource(
            $"{projectName}.Generated.{GeneratedJsonOptionsExtractor.ClassName}.g.cs",
            GeneratedJsonOptionsExtractor.Generate(NamespaceBuilder.BuildBase(projectName)));
    }

    /// <summary>
    /// Generates models for a specific set of schemas.
    /// Used for both shared types (pathSegment = null) and segment-specific types.
    /// Also generates inline enum types discovered during schema extraction.
    /// </summary>
    private static void GenerateModelsForSchemas(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemaNames,
        string? pathSegment,
        TypeConflictRegistry registry,
        bool includeDeprecated,
        bool generatePartialModels,
        bool includeSharedModelsUsing = false,
        ISet<string>? xmlModels = null,
        Dictionary<string, ServerBoundModel>? boundRequestModels = null,
        ISet<string>? xmlRequestModels = null,
        ISet<string>? xmlOnlyRequestModels = null,
        bool requireJsonProperties = false)
    {
        // Use SchemaExtractor.ExtractForSchemasWithInlineEnums to extract schemas and inline enums
        var xmlSources = new List<XmlModelSource>();
        var (recordsParameters, inlineEnums) = SchemaExtractor.ExtractForSchemasWithInlineEnums(
            openApiDoc,
            projectName,
            schemaNames,
            pathSegment,
            registry,
            includeDeprecated,
            generatePartialModels,
            includeSharedModelsUsing,
            xmlModels,
            xmlSources);

        // File names mirror the namespace the types are emitted into, so a segment that
        // resolved away never leaves an empty part behind in the file name.
        var modelsNamespace = NamespaceBuilder.ForModels(projectName, pathSegment);

        // Generate inline enum files first (before records that use them)
        foreach (var inlineEnum in inlineEnums)
        {
            var enumContent = InlineEnumExtractor.GenerateEnumContent(inlineEnum.EnumParameters, context.ExtensibleEnums, stringOnTheWire: true, enumWireConverter: context.EnumWireConverter, readMemberNames: context.ReadEnumMemberNames);
            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(modelsNamespace, inlineEnum.TypeName),
                enumContent);
        }

        if (recordsParameters is null || recordsParameters.Parameters.Count == 0)
        {
            return;
        }

        // Without a validation filter nothing checks a body's required properties: the JSON reader then requires them,
        // so a body that leaves one out is rejected (a 400) instead of reaching the handler without it
        if (requireJsonProperties)
        {
            recordsParameters = JsonRequiredHelper.AddJsonRequired(recordsParameters);
        }

        // A validation message names a property as the caller sent it (its JSON name), as the message's key does
        recordsParameters = WireDisplayNameHelper.ForModels(recordsParameters);

        var codeDocGenerator = new CodeDocumentationTagsGenerator();

        // A model bound by Content-Type gets a partial with BindAsync, and an object in the query one with TryParse,
        // so its record is partial
        var boundSources = xmlSources
            .Where(x => boundRequestModels?.ContainsKey(x.SchemaKey) == true)
            .Select(x => (x.SchemaKey, x.TypeName))
            .ToList();

        // A model only JSON bodies carry has no XML mapping, but its record reads the body too
        foreach (var jsonOnly in (boundRequestModels ?? []).Where(m => !m.Value.AcceptsXml && boundSources.All(b => b.SchemaKey != m.Key)))
        {
            var typeName = OpenApiSchemaExtensions.SanitizeSchemaName(jsonOnly.Key);
            if (recordsParameters.Parameters.Any(r => string.Equals(r.Name.Split(' ')[0], typeName, StringComparison.Ordinal)))
            {
                boundSources.Add((jsonOnly.Key, typeName));
            }
        }

        var queryObjectModels = QueryObjectHelper.Models(openApiDoc);
        var boundTypeNames = new HashSet<string>(boundSources.Select(x => x.TypeName), StringComparer.Ordinal);
        boundTypeNames.UnionWith(queryObjectModels.Keys);

        // Generate each model as a separate file
        foreach (var modelRecord in recordsParameters.Parameters)
        {
            var named = WithXmlNames(modelRecord, xmlSources, xmlModels, xmlRequestModels, xmlOnlyRequestModels);
            var record = boundTypeNames.Contains(named.Name.Split(' ')[0])
                ? named with
                {
                    DeclarationModifier = named.DeclarationModifier switch
                    {
                        DeclarationModifiers.PublicSealedRecord => DeclarationModifiers.PublicSealedPartialRecord,
                        DeclarationModifiers.PublicRecord => DeclarationModifiers.PublicPartialRecord,
                        _ => named.DeclarationModifier,
                    },
                }
                : named;

            // Create a single-record container with the same header and namespace
            var singleRecordParams = recordsParameters with { Parameters = [record] };

            var generatedContent = RecordContentGenerator.Generate(codeDocGenerator, singleRecordParams);

            // Sanitize record name for file system (generic types have <> which are invalid)
            var sanitizedName = record.Name.SanitizeForFileName();

            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(modelsNamespace, sanitizedName),
                generatedContent);

            // An object in the query: the model reads the value the wire-binding step collected from its keys
            if (queryObjectModels.TryGetValue(record.Name.Split(' ')[0], out var queryObject))
            {
                context.AddGeneratedSource(
                    NamespaceBuilder.ToFileName(modelsNamespace, sanitizedName + "QueryObject"),
                    QueryObjectModelExtractor.Generate(record, queryObject, modelsNamespace, registry));
            }
        }

        if (xmlModels is not null)
        {
            XmlModelEmitter.EmitCompanions(context, xmlSources, recordsParameters.HeaderContent, modelsNamespace, projectName, xmlModels, context.ExtensibleEnums);
        }

        foreach (var bound in boundSources)
        {
            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(modelsNamespace, bound.TypeName + "Binding"),
                XmlModelMapperExtractor.GenerateBinding(bound.TypeName, modelsNamespace, projectName, boundRequestModels![bound.SchemaKey]));
        }
    }

    /// <summary>
    /// The record of a model an XML request body carries, with <c>[XmlElement]</c> / <c>[XmlAttribute]</c> on the
    /// properties whose XML name differs from their JSON name - and, for a model only an XML request carries, the XML
    /// name as the name its validation messages use.
    /// </summary>
    private static RecordParameters WithXmlNames(
        RecordParameters record,
        List<XmlModelSource> xmlSources,
        ISet<string>? xmlModels,
        ISet<string>? xmlRequestModels,
        ISet<string>? xmlOnlyRequestModels)
    {
        // By name: the record may have been rebuilt since (required JSON properties, display names)
        var source = xmlSources.FirstOrDefault(x => string.Equals(x.Record.Name, record.Name, StringComparison.Ordinal));
        if (source is null || xmlModels is null || xmlRequestModels?.Contains(source.SchemaKey) != true)
        {
            return record;
        }

        var overrides = XmlModelMapperExtractor.XmlNameOverrides(source, xmlModels);
        if (overrides.Count == 0)
        {
            return record;
        }

        var xmlOnly = xmlOnlyRequestModels?.Contains(source.SchemaKey) == true;
        return record with
        {
            Parameters = record.Parameters?
                .Select(p =>
                {
                    if (!overrides.TryGetValue(p.Name, out var xml))
                    {
                        return p;
                    }

                    var named = xmlOnly ? WireDisplayNameHelper.Renamed(p, xml.Name) : p;
                    return named with
                    {
                        Attributes = (named.Attributes ?? [])
                            .Append(new AttributeParameters(xml.IsAttribute ? "global::System.Xml.Serialization.XmlAttribute" : "global::System.Xml.Serialization.XmlElement", $"\"{xml.Name}\""))
                            .ToList(),
                    };
                })
                .ToList(),
        };
    }

    private static void GenerateHandlerInterfaces(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated,
        string? namespaceSegment = null)
    {
        // The raw pathSegment still filters operations, while namespaceSegment drives namespaces
        // and file names so a redundant segment collapses away.
        var effectiveSegment = namespaceSegment ?? pathSegment;

        // Use HandlerExtractor to extract operations into InterfaceParameters list filtered by path segment
        var interfacesList = HandlerExtractor.Extract(openApiDoc, projectName, pathSegment, systemTypeResolver, includeDeprecated, effectiveSegment);

        if (interfacesList is null || interfacesList.Count == 0)
        {
            return;
        }

        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var allGeneratedCode = new StringBuilder();

        // Get namespace availability for this segment
        var namespaces = PathSegmentHelper.GetPathSegmentNamespaces(openApiDoc, pathSegment);

        // Generate header and usings once
        allGeneratedCode.AppendLine("// <auto-generated />");
        allGeneratedCode.AppendLine("#nullable enable");
        allGeneratedCode.AppendLine();
        allGeneratedCode.AppendLine("using System.CodeDom.Compiler;");
        allGeneratedCode.AppendLine("using System.Threading;");
        allGeneratedCode.AppendLine("using System.Threading.Tasks;");

        // Add conditional segment namespace usings (exclude Handlers since this IS the Handlers namespace)
        allGeneratedCode.AppendSegmentUsings(projectName, effectiveSegment, namespaces, includeHandlers: false);

        var handlersNamespace = NamespaceBuilder.Build(projectName, NamespaceBuilder.Categories.Handlers, effectiveSegment);

        allGeneratedCode.AppendLine();
        allGeneratedCode.AppendLine($"namespace {handlersNamespace};");
        allGeneratedCode.AppendLine();

        // Generate each interface
        foreach (var interfaceParams in interfacesList)
        {
            var contentGenerator = new GenerateContentForInterface(
                codeDocGenerator,
                interfaceParams);

            // Get generated content (without header/namespace since we added it above)
            var generatedInterface = contentGenerator.Generate();

            // Skip the header part and append just the interface
            AppendTypeBody(allGeneratedCode, generatedInterface);
        }

        var normalizeForSourceOutput = allGeneratedCode
            .ToString()
            .NormalizeForSourceOutput();

        context.AddGeneratedSource(
            $"{handlersNamespace}.g.cs",
            SourceText.From(normalizeForSourceOutput, Encoding.UTF8));
    }

    private static List<string> GenerateEndpointRegistrations(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        TypeConflictRegistry registry,
        SystemTypeConflictResolver systemTypeResolver,
        SubFolderStrategyType subFolderStrategy,
        bool includeDeprecated,
        bool useMinimalApiPackage,
        bool useValidationFilter,
        VersioningStrategyType versioningStrategy,
        string defaultApiVersion,
        bool useServersBasePath,
        bool includeSharedModelsUsing = false,
        string? namespaceSegment = null,
        bool useAntiforgery = false)
    {
        // The raw pathSegment still filters operations, while namespaceSegment drives namespaces
        // and file names so a redundant segment collapses away.
        var effectiveSegment = namespaceSegment ?? pathSegment;

        // Use EndpointDefinitionExtractor to extract interface and class parameters filtered by path segment
        var (interfaceParams, classParameters) = EndpointDefinitionExtractor.Extract(openApiDoc, projectName, pathSegment, registry, systemTypeResolver, subFolderStrategy, includeDeprecated, useMinimalApiPackage, useValidationFilter, versioningStrategy, defaultApiVersion, useServersBasePath, effectiveSegment, useAntiforgery);

        if (interfaceParams is null && (classParameters is null || classParameters.Count == 0))
        {
            return new List<string>();
        }

        // Get namespace availability for this segment
        var namespaces = PathSegmentHelper.GetPathSegmentNamespaces(openApiDoc, pathSegment);

        var codeDocGenerator = new CodeDocumentationTagsGenerator();

        // Endpoint definition classes are already segment-prefixed (e.g. EmployeesEndpointDefinition),
        // so they all live in the flat root Endpoints namespace/folder instead of one folder per segment.
        var endpointsNamespace = NamespaceBuilder.ForEndpoints(projectName);

        // Every generated type gets its own file, so the shared header/usings block is rebuilt per file.
        StringBuilder CreateFileBuilder()
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using System.CodeDom.Compiler;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");

            // Add namespace for IEndpointDefinition
            if (useMinimalApiPackage)
            {
                sb.AppendLine("using Atc.Rest.MinimalApi.Abstractions;");
            }
            else if (!string.Equals(endpointsNamespace, $"{projectName}.Generated.Endpoints", StringComparison.Ordinal))
            {
                sb.AppendLine($"using {projectName}.Generated.Endpoints;");
            }

            // Add ValidationFilter namespace when using validation
            if (useValidationFilter)
            {
                sb.AppendLine("using Atc.Rest.MinimalApi.Filters.Endpoints;");
            }

            // Add versioning namespace when versioning is enabled
            if (versioningStrategy != VersioningStrategyType.None)
            {
                sb.AppendLine("using Asp.Versioning;");
            }

            // Add output caching namespace when output caching is used
            if (openApiDoc.HasOutputCaching())
            {
                sb.AppendLine("using Microsoft.AspNetCore.OutputCaching;");
                sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            }

            sb.AppendLine("using Microsoft.AspNetCore.Builder;");
            sb.AppendLine("using Microsoft.AspNetCore.Http;");
            sb.AppendLine("using Microsoft.AspNetCore.Mvc;");

            // Only include shared models using if there are shared schemas (and the segment usings below do not
            // name the same namespace already: a segment that resolved away has its models in the shared one)
            if (includeSharedModelsUsing &&
                !(string.IsNullOrEmpty(effectiveSegment) && namespaces.HasModels))
            {
                sb.AppendLine($"using {projectName}.Generated.Models;");
            }

            // Add conditional segment namespace usings
            sb.AppendSegmentUsings(projectName, effectiveSegment, namespaces);
            BuiltInErrorContractNames.AppendFrameworkAliases(sb, BuiltInErrorContractNames.ReferencedByOtherSchemas(openApiDoc));

            // Add caching namespace when output caching is used
            if (openApiDoc.HasOutputCaching())
            {
                sb.AppendLine($"using {projectName}.Generated.Caching;");
            }

            // Add RateLimiting namespace when rate limiting is used (for the RateLimitPolicies
            // const class referenced by .RequireRateLimiting(...) calls)
            if (openApiDoc.HasRateLimiting())
            {
                sb.AppendLine($"using {projectName}.Generated.RateLimiting;");
            }

            // Add Security namespace when an operation requires scopes (for the SecurityPolicies
            // const class referenced by .RequireAuthorization(SecurityPolicies.X) calls)
            if (SecurityPoliciesExtractor.HasPolicies(openApiDoc, includeDeprecated))
            {
                sb.AppendLine($"using {projectName}.Generated.Security;");
            }

            sb.AppendLine();
            sb.AppendLine($"namespace {endpointsNamespace};");
            sb.AppendLine();

            return sb;
        }

        void AddTypeSource(
            string typeName,
            string generatedCode)
        {
            var sb = CreateFileBuilder();
            AppendTypeBody(sb, generatedCode);

            context.AddGeneratedSource(
                $"{endpointsNamespace}.{typeName}.g.cs",
                sb.ToString());
        }

        // Generate each endpoint definition class in its own file and collect class names for the extension method
        var endpointDefinitionClassNames = new List<string>();
        if (classParameters is not null)
        {
            foreach (var classParams in classParameters)
            {
                endpointDefinitionClassNames.Add(classParams.ClassTypeName);

                var contentGenerator = new GenerateContentForClass(
                    codeDocGenerator,
                    classParams);

                AddTypeSource(classParams.ClassTypeName, contentGenerator.Generate());
            }
        }

        // The mapping extension is emitted once for all segments, so only the class names are
        // collected here (see ExtractConsolidatedEndpointMappingExtension).
        return endpointDefinitionClassNames;
    }

    /// <summary>
    /// Generates the single EndpointDefinitionExtensions class in the root Endpoints namespace,
    /// containing one Map{Segment}Endpoints method per path segment.
    /// </summary>
    private static void GenerateConsolidatedEndpointDefinitionExtensions(
        GeneratedSourceContext context,
        string projectName,
        List<(string Segment, List<string> EndpointDefinitionClassNames)> endpointDefinitionsBySegment)
    {
        if (endpointDefinitionsBySegment.Count == 0)
        {
            return;
        }

        var extensionParams = EndpointRegistrationExtractor.ExtractConsolidatedEndpointMappingExtension(
            projectName,
            endpointDefinitionsBySegment);

        if (extensionParams is null)
        {
            return;
        }

        var rootEndpointsNamespace = NamespaceBuilder.ForEndpoints(projectName);

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System.CodeDom.Compiler;");
        builder.AppendLine("using Microsoft.AspNetCore.Builder;");

        // The endpoint definition classes live in the same root Endpoints namespace, so no
        // additional usings are required here.
        builder.AppendLine();
        builder.AppendLine($"namespace {rootEndpointsNamespace};");
        builder.AppendLine();

        var generatedClass = new GenerateContentForClass(
            new CodeDocumentationTagsGenerator(),
            extensionParams).Generate();

        AppendTypeBody(builder, generatedClass);

        context.AddGeneratedSource(
            $"{rootEndpointsNamespace}.{extensionParams.ClassTypeName}.g.cs",
            builder.ToString());
    }

    private static void GenerateCombinedEndpointMapping(
        GeneratedSourceContext context,
        string projectName,
        List<string> pathSegments)
    {
        if (pathSegments.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System.CodeDom.Compiler;");
        builder.AppendLine("using Microsoft.AspNetCore.Builder;");
        builder.AppendLine();

        // The Map{Segment}Endpoints methods live on EndpointDefinitionExtensions in this same
        // root Endpoints namespace, so no additional usings are required here.
        builder.AppendLine($"namespace {projectName}.Generated.Endpoints;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Extension methods for mapping all API endpoints.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class EndpointMappingExtensions");
        builder.AppendLine("{");
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Maps all API endpoints from all path segments.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"app\">The web application.</param>");
        builder.AppendLine(4, "/// <returns>The web application for method chaining.</returns>");
        builder.AppendLine(4, "public static WebApplication MapEndpoints(this WebApplication app)");
        builder.AppendLine(4, "{");

        // Call each path segment's endpoint mapping method. A segment that resolved away emits
        // "MapApiEndpoints" (see EndpointRegistrationExtractor), so mirror that naming here.
        var mappingMethodNames = pathSegments
            .Select(segment => string.IsNullOrEmpty(segment) ? "MapApiEndpoints" : $"Map{segment}Endpoints")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

        foreach (var mappingMethodName in mappingMethodNames)
        {
            builder.AppendLine(8, $"app.{mappingMethodName}();");
        }

        builder.AppendLine();
        builder.AppendLine(8, "return app;");
        builder.AppendLine(4, "}");
        builder.AppendLine("}");

        var normalizeForSourceOutput = builder
            .ToString()
            .NormalizeForSourceOutput();

        context.AddGeneratedSource(
            $"{projectName}.EndpointMapping.g.cs",
            SourceText.From(normalizeForSourceOutput, Encoding.UTF8));
    }

    private static void GenerateParameterClasses(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        TypeConflictRegistry registry,
        bool includeDeprecated,
        bool includeSharedModelsUsing = false,
        bool includeSegmentModelsUsing = true,
        bool emitInlineParameterEnums = true,
        ValidateSpecificationStrategy validateStrategy = ValidateSpecificationStrategy.Strict,
        string? namespaceSegment = null)
    {
        // The raw pathSegment still filters operations, while namespaceSegment drives namespaces
        // and file names so a redundant segment collapses away.
        var effectiveSegment = namespaceSegment ?? pathSegment;

        // Use OperationParameterExtractor inline-enum-aware variant so inline enums on
        // parameter schemas produce dedicated enum files alongside the parameter record.
        // emitInlineParameterEnums = false opts back into the pre-1.0.252 string contract.
        var (recordsParams, inlineEnums) = OperationParameterExtractor.ExtractWithInlineEnums(
            openApiDoc,
            projectName,
            pathSegment,
            registry,
            includeDeprecated,
            includeSharedModelsUsing,
            includeSegmentModelsUsing,
            emitInlineEnums: emitInlineParameterEnums,
            validateStrategy: validateStrategy,
            namespaceSegment: effectiveSegment);

        var parametersNamespace = NamespaceBuilder.ForParameters(projectName, effectiveSegment);

        // Emit inline enum files first so the parameter record's type references resolve.
        foreach (var inlineEnum in inlineEnums)
        {
            var enumContent = InlineEnumExtractor.GenerateEnumContent(inlineEnum.EnumParameters, context.ExtensibleEnums, stringOnTheWire: true, enumWireConverter: context.EnumWireConverter, readMemberNames: context.ReadEnumMemberNames);
            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(parametersNamespace, inlineEnum.TypeName),
                enumContent);
        }

        if (recordsParams is null || recordsParams.Parameters.Count == 0)
        {
            return;
        }

        // A validation message names a parameter as the specification does, as the message's key does
        recordsParams = WireDisplayNameHelper.ForParameters(recordsParams);

        // Shaped so the Request Delegate Generator (a Native AOT build) can bind them
        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var queryObjects = QueryObjectHelper.ByParametersRecord(openApiDoc);
        var extensibleEnumParameters = ExtensibleEnumHelper.ParametersByRecord(openApiDoc, registry);
        var queryObjectModels = new HashSet<string>(QueryObjectHelper.Models(openApiDoc).Keys, StringComparer.Ordinal);
        var generatedContent = RecordContentGenerator.GenerateParameterRecords(codeDocGenerator, recordsParams, queryObjectModels);

        context.AddGeneratedSource(
            $"{parametersNamespace}.g.cs",
            generatedContent);

        // What each record binds, for the endpoint's wire-binding step - described here, not found by reflection
        // (only where the step is generated: a record can bind a header no endpoint step reads)
        foreach (var record in WireBindingHelper.IsNeeded(openApiDoc) ? recordsParams.Parameters : [])
        {
            queryObjects.TryGetValue(record.Name, out var recordQueryObjects);
            extensibleEnumParameters.TryGetValue(record.Name, out var recordExtensibleEnums);
            if (WireBindingHelper.ParameterDescriptionClass(record, recordsParams.Namespace, recordsParams.HeaderContent, recordQueryObjects, registry, recordExtensibleEnums) is not { } description)
            {
                continue;
            }

            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(parametersNamespace, description.ClassTypeName),
                new GenerateContentForClass(codeDocGenerator, description).Generate());
        }
    }

    private static void GenerateResultClasses(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        TypeConflictRegistry registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated,
        bool includeSharedModelsUsing = false,
        bool includeSegmentModelsUsing = true,
        string? namespaceSegment = null)
    {
        // The raw pathSegment still filters operations, while namespaceSegment drives namespaces
        // and file names so a redundant segment collapses away.
        var effectiveSegment = namespaceSegment ?? pathSegment;

        // Use ResultClassExtractor to extract class parameters filtered by path segment
        // This also extracts inline schemas for type generation
        var (classesList, inlineSchemas) = ResultClassExtractor.ExtractWithInlineSchemas(openApiDoc, projectName, pathSegment, registry, systemTypeResolver, includeDeprecated, effectiveSegment);

        if (classesList is null || classesList.Count == 0)
        {
            return;
        }

        var modelsNamespace = NamespaceBuilder.ForModels(projectName, effectiveSegment);
        var resultsNamespace = NamespaceBuilder.ForResults(projectName, effectiveSegment);

        // An enum a response header declares inline is a type next to the results whose factories take it
        foreach (var (typeName, schema) in ResultClassExtractor.HeaderInlineEnums(openApiDoc, pathSegment, includeDeprecated))
        {
            if (InlineEnumExtractor.ExtractEnumFromInlineSchema(schema, typeName, resultsNamespace) is { } enumParameters)
            {
                var enumContent = InlineEnumExtractor.GenerateEnumContent(enumParameters, context.ExtensibleEnums, stringOnTheWire: true, enumWireConverter: context.EnumWireConverter, readMemberNames: context.ReadEnumMemberNames);
                context.AddGeneratedSource(
                    NamespaceBuilder.ToFileName(resultsNamespace, typeName),
                    enumContent);
            }
        }

        // Generate inline model files for any inline schemas discovered
        if (inlineSchemas.Count > 0)
        {
            var inlineTypes = CodeGenerationService.GenerateInlineModels(inlineSchemas, projectName, CodeGenerationService.GeneratorType.Server);
            foreach (var inlineType in inlineTypes)
            {
                var inlineContent = CodeGenerationService.FormatAsFile(inlineType);
                context.AddGeneratedSource(
                    NamespaceBuilder.ToFileName(modelsNamespace, inlineType.TypeName),
                    inlineContent);
            }
        }

        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var allGeneratedCode = new StringBuilder();

        // Generate header and usings once
        allGeneratedCode.AppendLine("// <auto-generated />");
        allGeneratedCode.AppendLine("#nullable enable");
        allGeneratedCode.AppendLine();
        allGeneratedCode.AppendLine("using System.CodeDom.Compiler;");
        allGeneratedCode.AppendLine("using System.Collections.Generic;");
        allGeneratedCode.AppendLine("using Microsoft.AspNetCore.Http;");
        allGeneratedCode.AppendLine("using Microsoft.AspNetCore.Mvc;");

        // Only include shared models using if there are shared schemas
        if (includeSharedModelsUsing)
        {
            allGeneratedCode.AppendLine($"using {projectName}.Generated.Models;");
        }

        // Only include segment-specific models using if there are segment-specific models (unless it is the
        // shared namespace, already imported above: a segment that resolved away)
        if (includeSegmentModelsUsing &&
            !(includeSharedModelsUsing && modelsNamespace == $"{projectName}.Generated.Models"))
        {
            allGeneratedCode.AppendLine($"using {modelsNamespace};");
        }

        // A spec ProblemDetails / ValidationProblemDetails that other models use is generated too: keep meaning the framework type.
        BuiltInErrorContractNames.AppendFrameworkAliases(allGeneratedCode, BuiltInErrorContractNames.ReferencedByOtherSchemas(openApiDoc));

        allGeneratedCode.AppendLine();
        allGeneratedCode.AppendLine($"namespace {resultsNamespace};");
        allGeneratedCode.AppendLine();

        // Generate each class
        foreach (var classParams in classesList)
        {
            var contentGenerator = new GenerateContentForClass(
                codeDocGenerator,
                classParams);

            // Get generated content (without header/namespace since we added it above)
            var generatedClass = contentGenerator.Generate();

            // Skip the header part and append just the class
            AppendTypeBody(allGeneratedCode, generatedClass);
        }

        // The helper types the result classes use, each emitted once per Results namespace - decided on the classes' text
        var classesCode = allGeneratedCode.ToString();

        // 429 factories wrap their result to add Retry-After
        if (classesCode.Contains("new RetryAfterHttpResult("))
        {
            allGeneratedCode.AppendLine(ResultClassExtractor.RetryAfterHttpResultSource());
        }

        // A problem factory passes the handler's problem through with the response status
        if (classesCode.Contains("ProblemWithStatus.Of("))
        {
            allGeneratedCode.AppendLine(ResultClassExtractor.ProblemWithStatusSource());
        }

        if (classesCode.Contains("new LocationHttpResult("))
        {
            allGeneratedCode.AppendLine(ResultClassExtractor.LocationHttpResultSource());
        }

        // A factory writes the headers its response declares
        if (classesCode.Contains(ResponseHeaderFactoryHelper.WriterCall))
        {
            allGeneratedCode.AppendLine(ResponseHeaderFactoryHelper.ResponseHeadersSource());
        }

        // An XML body's factories write the model's generated XML
        if (classesCode.Contains("new XmlHttpResult("))
        {
            allGeneratedCode.AppendLine(ResultClassExtractor.XmlHttpResultSource());
        }

        // A body offered as JSON and XML is negotiated by Accept
        if (classesCode.Contains("new NegotiatedHttpResult("))
        {
            allGeneratedCode.AppendLine(ResultClassExtractor.NegotiatedHttpResultSource());
        }

        // A JSON body is written through the JsonTypeInfo the app's JSON options resolve (trim- and AOT-safe)
        if (classesCode.Contains("JsonTypeInfoHttpResult.Of("))
        {
            allGeneratedCode.AppendLine(ResultClassExtractor.JsonTypeInfoHttpResultSource(NamespaceBuilder.BuildBase(projectName)));
        }

        var normalizeForSourceOutput = allGeneratedCode
            .ToString()
            .NormalizeForSourceOutput();

        context.AddGeneratedSource(
            $"{resultsNamespace}.g.cs",
            SourceText.From(normalizeForSourceOutput, Encoding.UTF8));
    }

    private static void GenerateSecurityPolicies(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        // Use SecurityPoliciesExtractor to extract security policy constants
        var generatedContent = SecurityPoliciesExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (generatedContent is null)
        {
            return;
        }

        context.AddGeneratedSource(
            $"{projectName}.Security.Policies.g.cs",
            generatedContent);
    }

    private static void GenerateSecurityDependencyInjection(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        // Use SecurityDependencyInjectionExtractor to extract security DI extension
        var generatedContent = SecurityDependencyInjectionExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (generatedContent is null)
        {
            return;
        }

        context.AddGeneratedSource(
            $"{projectName}.Security.DependencyInjection.g.cs",
            generatedContent);
    }

    private static void GenerateRateLimitPolicies(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        // Use RateLimitPoliciesExtractor to extract rate limit policy constants
        var generatedContent = RateLimitPoliciesExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (generatedContent is null)
        {
            return;
        }

        context.AddGeneratedSource(
            $"{projectName}.RateLimiting.Policies.g.cs",
            generatedContent);
    }

    private static void GenerateRateLimitDependencyInjection(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        // Use RateLimitDependencyInjectionExtractor to extract rate limit DI extension
        var generatedContent = RateLimitDependencyInjectionExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (generatedContent is null)
        {
            return;
        }

        context.AddGeneratedSource(
            $"{projectName}.RateLimiting.DependencyInjection.g.cs",
            generatedContent);
    }

    private static void GenerateOutputCachePolicies(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        // Use OutputCachePoliciesExtractor to extract output cache policy constants
        var generatedContent = OutputCachePoliciesExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (generatedContent is null)
        {
            return;
        }

        context.AddGeneratedSource(
            $"{projectName}.OutputCaching.Policies.g.cs",
            generatedContent);
    }

    private static void GenerateOutputCachingDependencyInjection(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        // Use OutputCacheDependencyInjectionExtractor to extract output caching DI extension
        var generatedContent = OutputCacheDependencyInjectionExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (generatedContent is null)
        {
            return;
        }

        context.AddGeneratedSource(
            $"{projectName}.OutputCaching.DependencyInjection.g.cs",
            generatedContent);
    }

    private static void GenerateHybridCachePolicies(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        // Use HybridCachePoliciesExtractor to extract HybridCache policy constants
        var generatedContent = HybridCachePoliciesExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (generatedContent is null)
        {
            return;
        }

        context.AddGeneratedSource(
            $"{projectName}.Caching.Policies.g.cs",
            generatedContent);
    }

    private static void GenerateHybridCachingDependencyInjection(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        // Use HybridCacheDependencyInjectionExtractor to extract HybridCache DI extension
        var generatedContent = HybridCacheDependencyInjectionExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (generatedContent is null)
        {
            return;
        }

        context.AddGeneratedSource(
            $"{projectName}.Caching.DependencyInjection.g.cs",
            generatedContent);
    }

    private static void GenerateOpenIdConnectAuthentication(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        string? apiName)
    {
        // Extract OpenID Connect configuration
        var oidcConfig = OpenIdConnectConfigExtractor.Extract(openApiDoc);

        if (oidcConfig is null)
        {
            return;
        }

        // Generate OpenID Connect DI extension
        var generatedContent = OpenIdConnectDependencyInjectionExtractor.Extract(oidcConfig, projectName, apiName);

        if (generatedContent is null)
        {
            return;
        }

        context.AddGeneratedSource(
            $"{projectName}.Authentication.OpenIdConnect.g.cs",
            generatedContent);
    }

    private static void GenerateVersioningDependencyInjection(
        GeneratedSourceContext context,
        string projectName,
        ServerConfig config)
    {
        // Use VersioningDependencyInjectionExtractor to extract versioning DI extension
        var classParameters = VersioningDependencyInjectionExtractor.Extract(projectName, config);

        if (classParameters is null)
        {
            return;
        }

        // Use GenerateContentForClass to generate the code
        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var contentGenerator = new GenerateContentForClass(
            codeDocGenerator,
            classParameters);

        var generatedContent = contentGenerator.Generate();

        context.AddGeneratedSource(
            $"{projectName}.Versioning.DependencyInjection.g.cs",
            generatedContent);
    }

    /// <summary>
    /// Generates ApiServiceOptions and ApiMiddlewareOptions classes for simplified API configuration.
    /// </summary>
    private static void GenerateApiOptions(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config,
        bool useGlobalErrorHandler)
    {
        // Generate ApiServiceOptions
        var serviceOptionsContent = ApiOptionsExtractor.ExtractServiceOptions(openApiDoc, projectName, config);
        context.AddGeneratedSource(
            $"{projectName}.ApiServiceOptions.g.cs",
            serviceOptionsContent);

        // Generate ApiMiddlewareOptions
        var middlewareOptionsContent = ApiOptionsExtractor.ExtractMiddlewareOptions(openApiDoc, projectName, useGlobalErrorHandler);
        context.AddGeneratedSource(
            $"{projectName}.ApiMiddlewareOptions.g.cs",
            middlewareOptionsContent);
    }

    /// <summary>
    /// Generates the unified Add{ProjectName}Api() service collection extension method.
    /// </summary>
    private static void GenerateUnifiedServiceCollection(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config,
        List<string> pathSegments)
    {
        var generatedContent = UnifiedServiceCollectionExtractor.Extract(openApiDoc, projectName, config, pathSegments);

        context.AddGeneratedSource(
            $"{projectName}.UnifiedServiceCollectionExtensions.g.cs",
            generatedContent);

        // The converter the Add…Api() above puts first in the JSON options, when there is an enum to write
        if (EnumWireConverterExtractor.HasEnums(openApiDoc))
        {
            context.AddGeneratedSource(
                $"{projectName}.Generated.{EnumWireConverterExtractor.ClassName}.g.cs",
                EnumWireConverterExtractor.Generate(NamespaceBuilder.BuildBase(projectName), context.Enums, context.ReadEnumMemberNames));
        }
    }

    /// <summary>
    /// Generates the unified Map{ProjectName}Api() WebApplication extension method.
    /// </summary>
    private static void GenerateUnifiedWebApplicationExtensions(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config,
        bool useGlobalErrorHandler)
    {
        var generatedContent = WebApplicationExtensionsExtractor.ExtractUnified(openApiDoc, projectName, config, useGlobalErrorHandler);

        context.AddGeneratedSource(
            $"{projectName}.UnifiedWebApplicationExtensions.g.cs",
            generatedContent);
    }

    /// <summary>
    /// Generates webhook handler interfaces from OpenAPI webhooks.
    /// </summary>
    private static void GenerateWebhookHandlerInterfaces(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        foreach (var generatedType in CodeGenerationService.GenerateWebhookHandlerInterfaces(openApiDoc, projectName, includeDeprecated))
        {
            var formattedCode = CodeGenerationService.FormatAsFile(generatedType);
            context.AddGeneratedSource(
                $"Webhooks.Handlers.{generatedType.TypeName}.g.cs",
                formattedCode);
        }
    }

    /// <summary>
    /// Generates webhook parameter classes from OpenAPI webhooks.
    /// </summary>
    private static void GenerateWebhookParameterClasses(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        foreach (var generatedType in CodeGenerationService.GenerateWebhookParameters(openApiDoc, projectName, includeDeprecated))
        {
            var formattedCode = CodeGenerationService.FormatAsFile(generatedType);
            context.AddGeneratedSource(
                $"Webhooks.Parameters.{generatedType.TypeName}.g.cs",
                formattedCode);
        }
    }

    /// <summary>
    /// Generates webhook result classes from OpenAPI webhooks.
    /// </summary>
    private static void GenerateWebhookResultClasses(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        foreach (var generatedType in CodeGenerationService.GenerateWebhookResults(openApiDoc, projectName, includeDeprecated))
        {
            var formattedCode = CodeGenerationService.FormatAsFile(generatedType);
            context.AddGeneratedSource(
                $"Webhooks.Results.{generatedType.TypeName}.g.cs",
                formattedCode);
        }
    }

    /// <summary>
    /// Generates webhook endpoint registration from OpenAPI webhooks.
    /// </summary>
    private static void GenerateWebhookEndpoints(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config,
        bool useValidationFilter)
    {
        var generatedType = CodeGenerationService.GenerateWebhookEndpoints(openApiDoc, projectName, config, useValidationFilter);
        if (generatedType is not null)
        {
            var formattedCode = CodeGenerationService.FormatAsFile(generatedType);
            context.AddGeneratedSource(
                $"Webhooks.Endpoints.{generatedType.TypeName}.g.cs",
                formattedCode);
        }
    }

    /// <summary>
    /// Reports a generation summary at the end of server code generation.
    /// </summary>
    private static void ReportServerGenerationSummary(
        SourceProductionContext context,
        string yamlPath,
        int warnings,
        int errors,
        int modelCount,
        int handlerCount,
        int endpointCount)
    {
        var specName = Path.GetFileName(yamlPath);

        DiagnosticHelpers.ReportGenerationSummary(
            context,
            specName,
            "Server",
            modelCount,
            handlerCount,
            endpointCount,
            warnings,
            errors);
    }

    /// <summary>
    /// Generates the ParsableList&lt;T&gt; and ParsableEnumList&lt;T&gt; helper classes for array query parameter binding.
    /// </summary>
    private static void GenerateParsableListHelper(
        GeneratedSourceContext context,
        string projectName)
    {
        var content = $$"""
            // <auto-generated />
            #nullable enable

            using System;
            using System.CodeDom.Compiler;
            using System.Collections.Generic;
            using System.Linq;
            using System.Reflection;
            using System.Runtime.Serialization;

            namespace {{projectName}}.Generated;

            /// <summary>
            /// An array query parameter bound via [AsParameters] in minimal APIs. The endpoint's WithWireBinding step hands
            /// the values over as one string with the unit separator between the items (a repeated key is one item each,
            /// a declared delimiter is honored); a plain comma-separated value (tags=a,b) is read too.
            /// </summary>
            [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
            public sealed class ParsableList<T> : List<T>, IParsable<ParsableList<T>>
                where T : IParsable<T>
            {
                /// <inheritdoc />
                public static ParsableList<T> Parse(string s, IFormatProvider? provider)
                    => TryParse(s, provider, out var result)
                        ? result
                        : throw new FormatException($"'{s}' is not a list of {typeof(T).Name} values.");

                /// <inheritdoc />
                public static bool TryParse(string? s, IFormatProvider? provider, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out ParsableList<T> result)
                {
                    // No value is no list: a binder that parses before it looks (the Request Delegate Generator) then
                    // binds null, as one that looks first (RequestDelegateFactory) does
                    if (string.IsNullOrEmpty(s))
                    {
                        result = null;
                        return false;
                    }

                    result = new ParsableList<T>();
                    return ParsableListBinding.TryAddAll(result, ParsableListBinding.Split(s), TryParseItem);
                }

                private static bool TryParseItem(string item, out T value)
                    => T.TryParse(item, global::System.Globalization.CultureInfo.InvariantCulture, out value!);
            }

            /// <summary>
            /// The same for a list of enum values: each value is read by its wire name (the value in the
            /// specification, kept on the member as [EnumMember]) or by its member name - in any casing only one
            /// member has (see EnumMembers).
            /// </summary>
            [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
            public sealed class ParsableEnumList<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields)] T> : List<T>, IParsable<ParsableEnumList<T>>
                where T : struct, Enum
            {
                private static readonly EnumMembers Members = EnumMembers.Of<T>();

                /// <inheritdoc />
                public static ParsableEnumList<T> Parse(string s, IFormatProvider? provider)
                    => TryParse(s, provider, out var result)
                        ? result
                        : throw new FormatException($"'{s}' is not a list of {typeof(T).Name} values.");

                /// <inheritdoc />
                public static bool TryParse(string? s, IFormatProvider? provider, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out ParsableEnumList<T> result)
                {
                    // No value is no list: a binder that parses before it looks (the Request Delegate Generator) then
                    // binds null, as one that looks first (RequestDelegateFactory) does
                    if (string.IsNullOrEmpty(s))
                    {
                        result = null;
                        return false;
                    }

                    result = new ParsableEnumList<T>();
                    return ParsableListBinding.TryAddAll(result, ParsableListBinding.Split(s), TryParseItem);
                }

                // The member the item names; the wire-binding step has already put each item as its member name
                private static bool TryParseItem(string item, out T value)
                {
                    value = default;
                    return Members.TryGetValue(item, out var member) &&
                           Enum.TryParse(member, ignoreCase: false, out value);
                }
            }

            /// <summary>
            /// What the two list types share: the items of the value, and the parse of each.
            /// </summary>
            [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
            public static class ParsableListBinding
            {
                /// <summary>
                /// The separator WithWireBinding puts between the items.
                /// </summary>
                public const char ItemSeparator = '\u001F';

                public delegate bool TryParseItem<T>(string item, out T value);

                /// <summary>
                /// The items: the value as WithWireBinding wrote it - each item after an item separator - is split on
                /// that separator only, the items kept as they are. A value that did not go through WithWireBinding is
                /// split on commas.
                /// </summary>
                public static string[] Split(string? value)
                    => string.IsNullOrEmpty(value)
                        ? []
                        : value![0] == ItemSeparator
                            ? value.Split(ItemSeparator, StringSplitOptions.RemoveEmptyEntries)
                            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                /// <summary>
                /// Parses every item into the list; false at the first item that does not parse.
                /// </summary>
                public static bool TryAddAll<T>(List<T> list, IEnumerable<string> items, TryParseItem<T> tryParseItem)
                {
                    foreach (var item in items)
                    {
                        if (!tryParseItem(item, out var value))
                        {
                            return false;
                        }

                        list.Add(value);
                    }

                    return true;
                }
            }
            """;

        context.AddGeneratedSource(
            $"{projectName}.ParsableList.g.cs",
            content);
    }

    /// <summary>
    /// Generates a shared IEndpointDefinition interface in the root Endpoints namespace.
    /// Emitted once per project, referenced by all per-segment endpoint definition classes.
    /// </summary>
    private static void GenerateSharedEndpointDefinitionInterface(
        GeneratedSourceContext context,
        string projectName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using Microsoft.AspNetCore.Builder;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Generated.Endpoints;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Defines a contract for endpoint definitions.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine("public interface IEndpointDefinition");
        sb.AppendLine("{");
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Defines the endpoints for this definition.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "void DefineEndpoints(WebApplication app);");
        sb.Append('}');

        context.AddGeneratedSource(
            $"{projectName}.IEndpointDefinition.g.cs",
            sb.ToString());
    }

    private static void GenerateHealthCheckEndpoints(
        GeneratedSourceContext context,
        string projectName,
        HealthCheckConfig config,
        bool hasHealthChecksPackage)
    {
        var content = HealthCheckExtractor.GenerateEndpoints(projectName, config, hasHealthChecksPackage);

        context.AddGeneratedSource(
            $"{projectName}.Health.HealthCheckEndpoints.g.cs",
            content);
    }

    private static void GenerateHealthCheckServiceExtensions(
        GeneratedSourceContext context,
        string projectName)
    {
        var content = HealthCheckExtractor.GenerateServiceExtensions(projectName);

        context.AddGeneratedSource(
            $"{projectName}.Health.HealthCheckServiceExtensions.g.cs",
            content);
    }

    /// <summary>
    /// Appends a generated type's body - the lines after its <c>namespace</c> line - to a file that writes its own header
    /// and namespace.
    /// </summary>
    private static void AppendTypeBody(
        StringBuilder target,
        string generatedCode)
    {
        var namespaceFound = false;
        foreach (var line in generatedCode.SplitIntoLinesPreserveEmpty())
        {
            if (namespaceFound)
            {
                target.AppendLine(line);
            }
            else if (line.StartsWith("namespace ", StringComparison.Ordinal))
            {
                namespaceFound = true;
            }
        }
    }
}