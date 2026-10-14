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
    /// <summary>
    /// Registers the pipeline: the marker file and the specifications as additional texts, the project's package references, and the server code and its diagnostics as the output.
    /// </summary>
    /// <param name="context">The initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find marker files - marker file presence IS the trigger for this generator
        var markerFiles = context.AdditionalTextsProvider
            .Where(static file => Path.GetFileName(file.Path).Equals(Constants.MarkerFile.Server, StringComparison.OrdinalIgnoreCase) ||
                                  Path.GetFileName(file.Path).Equals(Constants.MarkerFile.ServerJson, StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) =>
                new MarkerFileInfo(file.Path, file.GetText(cancellationToken)?.ToString() ?? "{}"))
            .Collect()
            .Select(static (array, _) => new EquatableArray<MarkerFileInfo>(array));

        // Register a pipeline to collect ALL OpenAPI YAML files (for multi-parts support)
        var yamlFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                                 file.Path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                                 file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) =>
                new YamlFileInfo(file.Path, file.GetText(cancellationToken)?.ToString() ?? string.Empty))
            .Where(static file => OverlayFileHelper.IsCandidate(file))
            .Collect()
            .Select(static (array, _) => new EquatableArray<YamlFileInfo>(array));

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
                HasAspNetCore = project.Compilation.ReferencedAssemblyNames
                    .Any(a => a.Name.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase)),
                HasMinimalApi = project.Compilation.ReferencedAssemblyNames
                    .Any(a => a.Name.Equals("Atc.Rest.MinimalApi", StringComparison.OrdinalIgnoreCase)),
                HasHealthChecks = project.Compilation.ReferencedAssemblyNames
                    .Any(a => a.Name.Equals("Atc.Rest.HealthChecks", StringComparison.OrdinalIgnoreCase)),
                HasBearerHandler = project.Compilation.ReferencedAssemblyNames
                    .Any(a => a.Name.Equals("Microsoft.AspNetCore.Authentication.JwtBearer", StringComparison.OrdinalIgnoreCase) ||
                              a.Name.Equals("Microsoft.Identity.Web", StringComparison.OrdinalIgnoreCase)),
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

        public bool HasMinimalApi { get; init; }

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

        // Skip if no marker file found - marker file presence IS the trigger
        if (markers.IsEmpty)
        {
            return;
        }

        // Skip if no YAML files found
        if (yamlFiles.IsEmpty)
        {
            return;
        }

        // One configuration per project: two marker files are a mistake, not a choice
        if (markers.Values.Length > 1)
        {
            DiagnosticHelpers.ReportMultipleMarkerFiles(productionContext, markers.Values.Select(m => m.Path).ToList());
            return;
        }

        var marker = markers.Values[0];

        ServerConfig config;
        try
        {
            config = MarkerConfigInspector.Read<ServerConfig>(marker.Content, out var markerProblems);
            DiagnosticHelpers.ReportUnknownMarkerSettings(productionContext, marker.Path, markerProblems);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A marker that cannot be read is no configuration at all: generating from the defaults would be a
            // different project
            DiagnosticHelpers.ReportMarkerConfigParseError(productionContext, marker.Path, ex, typeof(ServerConfig));
            return;
        }

        // Skip if generation is disabled
        if (!config.Generate)
        {
            return;
        }

        // Validate ASP.NET Core references - generated code requires them
        if (!hasAspNetCore)
        {
            DiagnosticHelpers.ReportServerRequiresAspNetCore(productionContext);
            return;
        }

        // Validate MinimalApiPackage mode - report error if Enabled but package not referenced
        if (config.UseMinimalApiPackage == MinimalApiPackageMode.Enabled && !hasMinimalApi)
        {
            DiagnosticHelpers.ReportMinimalApiPackageRequired(productionContext);
            return;
        }

        // Validate ValidationFilter mode - report error if Enabled but package not referenced
        if (config.UseValidationFilter == MinimalApiPackageMode.Enabled && !hasMinimalApi)
        {
            DiagnosticHelpers.ReportMinimalApiPackageRequired(productionContext);
            return;
        }

        // Resolve effective useMinimalApiPackage mode:
        // - Enabled: always use package interface (validated above)
        // - Disabled: always generate interface
        // - Auto: use package interface if package is referenced
        var useMinimalApiPackage = config.UseMinimalApiPackage switch
        {
            MinimalApiPackageMode.Enabled => true,
            MinimalApiPackageMode.Disabled => false,
            _ => hasMinimalApi,
        };

        // Resolve effective useValidationFilter mode:
        // - Enabled: always add ValidationFilter (validated above)
        // - Disabled: never add ValidationFilter
        // - Auto: add ValidationFilter if package is referenced
        var useValidationFilter = config.UseValidationFilter switch
        {
            MinimalApiPackageMode.Enabled => true,
            MinimalApiPackageMode.Disabled => false,
            _ => hasMinimalApi,
        };

        // Resolve effective useGlobalErrorHandler mode:
        // - Enabled: always generate (validated above similar to ValidationFilter)
        // - Disabled: never generate
        // - Auto: generate if package is referenced
        var useGlobalErrorHandler = config.UseGlobalErrorHandler switch
        {
            MinimalApiPackageMode.Enabled => true,
            MinimalApiPackageMode.Disabled => false,
            _ => hasMinimalApi,
        };

        // Everything from here runs under the catch: a failure before the base file is known is reported on the marker
        var filePath = marker.Path;
        try
        {
            // Identify the base file (non-part file or the first file that is not a part file)
            // Part files follow the naming convention: {BaseName}_{PartName}.yaml
            // Overlay files are no specifications: the ones the marker lists are applied to it
            var (specificationFiles, overlayFiles) = OverlayFileHelper.Split(yamlFiles.Values);
            var overlays = OverlayFileHelper.Resolve(productionContext, marker.Path, config.Overlays, overlayFiles);
            if (overlays is null)
            {
                return;
            }

            var baseFile = YamlFileHelper.IdentifyBaseFile(specificationFiles);
            if (baseFile is null)
            {
                return;
            }

            filePath = baseFile.Value.Path;

            // A second base specification: the generator reads one per project
            var otherBases = YamlFileHelper.OtherBaseFiles(specificationFiles, baseFile.Value);
            if (otherBases.Count > 0)
            {
                DiagnosticHelpers.ReportMultipleSpecifications(productionContext, [baseFile.Value.Path, .. otherBases.Select(f => f.Path)]);
                return;
            }

            // Check if multi-parts specification
            var baseName = Path.GetFileNameWithoutExtension(baseFile.Value.Path);
            var partFiles = specificationFiles
                .Where(f => YamlFileHelper.IsPartFile(f.Path, baseName))
                .ToList();

            if (partFiles.Count > 0)
            {
                // Multi-part mode: merge all files
                GenerateApiServerMultiPart(
                    productionContext,
                    baseFile.Value,
                    partFiles,
                    config,
                    useMinimalApiPackage,
                    useValidationFilter,
                    useGlobalErrorHandler,
                    packages,
                    overlays);
            }
            else
            {
                // Single file mode
                GenerateApiServer(
                    productionContext,
                    baseFile.Value.Path,
                    baseFile.Value.Content,
                    config,
                    useMinimalApiPackage,
                    useValidationFilter,
                    useGlobalErrorHandler,
                    packages,
                    overlays);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            DiagnosticHelpers.ReportServerGenerationError(productionContext, filePath, ex);
        }
    }

    /// <summary>
    /// Generates API server code from a multi-parts specification.
    /// Merges all part files with the base file before generation.
    /// </summary>
    private static void GenerateApiServerMultiPart(
        SourceProductionContext context,
        YamlFileInfo baseFile,
        List<YamlFileInfo> partFiles,
        ServerConfig config,
        bool useMinimalApiPackage,
        bool useValidationFilter,
        bool useGlobalErrorHandler,
        ServerPackageReferences packages,
        IReadOnlyList<OverlayFile> overlays)
    {
        var openApiDoc = SpecificationLoader.MergeAndValidate(context, baseFile, partFiles, config.MultiPartConfiguration, config.ValidateSpecificationStrategy, overlays, parametersMustBind: true);
        if (openApiDoc is null)
        {
            return;
        }

        // The root namespace: marker namespace, qualifying info.title, then base file name - as for a client
        var projectName = DiagnosticHelpers.ResolveRootNamespace(context, config.Namespace, openApiDoc, baseFile.Path);

        GenerateApiServerFromDocument(
            context,
            baseFile.Path,
            openApiDoc,
            config,
            projectName,
            useMinimalApiPackage,
            useValidationFilter,
            useGlobalErrorHandler,
            packages);
    }

    private static void GenerateApiServer(
        SourceProductionContext context,
        string yamlPath,
        string yamlContent,
        ServerConfig config,
        bool useMinimalApiPackage,
        bool useValidationFilter,
        bool useGlobalErrorHandler,
        ServerPackageReferences packages,
        IReadOnlyList<OverlayFile> overlays)
    {
        var openApiDoc = SpecificationLoader.ParseAndValidate(context, yamlPath, yamlContent, config.ValidateSpecificationStrategy, DiagnosticHelpers.ReportServerParsingError, overlays, parametersMustBind: true);
        if (openApiDoc is null)
        {
            return;
        }

        // The root namespace: marker namespace, qualifying info.title, then file name - as for a client
        var projectName = DiagnosticHelpers.ResolveRootNamespace(context, config.Namespace, openApiDoc, yamlPath);

        GenerateApiServerFromDocument(
            context,
            yamlPath,
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

        // An inline allOf over a plain base becomes one inline object, so it gets a model of its own
        InlineAllOfFlattener.Flatten(openApiDoc);

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

        // The ValidationFilter writes a ValidationProblemDetails: a 400 declared as a string cannot carry its field
        // errors (information only - the specification is valid, and a handler may answer with the string)
        if (useValidationFilter)
        {
            foreach (var operation in ErrorResponseShapeResolver.GetOperationsWithStringBadRequest(openApiDoc))
            {
                DiagnosticHelpers.ReportValidationBadRequestDeclaredAsString(context, operation);
            }
        }

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
        var generatedContext = new GeneratedSourceContext(context, config.ExcludeFromCodeCoverage, DeprecationDetector.HasDeprecatedMembers(openApiDoc));

        // Detect shared schemas (used by multiple path segments) for deduplication
        var sharedSchemas = PathSegmentHelper.GetSharedSchemas(openApiDoc);

        // The models an XML body can carry get an XML mapping (only when the specification has an XML body)
        var xmlModels = XmlBodyReachability.Collect(openApiDoc);
        XmlModelEmitter.EmitSupport(generatedContext, projectName, xmlModels);
        XmlModelEmitter.EmitProblemDetailsWriter(generatedContext, projectName, xmlModels);

        // The models bound by Content-Type (JSON and/or XML request bodies): a partial with BindAsync
        var boundRequestModels = XmlBodyHelper.ServerBoundRequestModels(openApiDoc);

        // A list body offered as XML is bound into the generated RequestBodyList<T>
        if (boundRequestModels.Values.Any(m => m.BoundInList))
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.{XmlModelMapperExtractor.RequestBodyListTypeName}.g.cs",
                SourceText.From(XmlModelMapperExtractor.GenerateRequestBodyList(projectName).NormalizeForSourceOutput(), Encoding.UTF8));
        }

        // The models of an XML request body name their XML names where they differ from the JSON ones
        // ([XmlElement] / [XmlAttribute]): a validation filter maps an XML request's error keys through them
        var xmlRequestModels = XmlBodyReachability.CollectRequestBodies(openApiDoc);

        // A spec-defined ProblemDetails / ValidationProblemDetails is not materialized: the server binds
        // those names to Microsoft.AspNetCore.Mvc (every generated Results file imports it), and a second,
        // generated type of the same name would make every reference ambiguous (CS0104). Unless another
        // schema extends or holds it: then it is a model like any other, and the files that import both
        // namespaces alias the name to the framework type.
        var referencedReservedSchemas = BuiltInErrorContractNames.ReferencedByOtherSchemas(openApiDoc);
        sharedSchemas.RemoveWhere(name => BuiltInErrorContractNames.IsReserved(name) && !referencedReservedSchemas.Contains(name));

        // Generate shared models/enums first (under common namespace without segment suffix)
        if (sharedSchemas.Count > 0)
        {
            var sharedRegistry = TypeConflictRegistry.ForSegment(conflicts, projectName, componentTypeNames: componentTypeNames);
            GenerateModelsForSchemas(
                generatedContext,
                openApiDoc,
                projectName,
                sharedSchemas,
                pathSegment: null,
                sharedRegistry,
                config.IncludeDeprecated,
                config.GeneratePartialModels,
                xmlModels: xmlModels,
                boundRequestModels: boundRequestModels,
                xmlRequestModels: xmlRequestModels,
                requireJsonProperties: !useValidationFilter);

            GenerateEnumsForSchemas(
                generatedContext,
                openApiDoc,
                projectName,
                sharedSchemas,
                pathSegment: null);

            GenerateTuplesForSchemas(
                generatedContext,
                openApiDoc,
                projectName,
                sharedSchemas,
                pathSegment: null);

            PolymorphicTypeEmitter.Emit(
                generatedContext,
                openApiDoc,
                projectName,
                sharedSchemas,
                pathSegment: null);
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

            // Resolve the segment used for namespaces and file names. It collapses to an empty
            // string when the segment is redundant (single segment, or it duplicates the root
            // namespace tail), while the raw pathSegment is still used to filter operations.
            var effectiveSegment = PathSegmentHelper.ResolveEffectivePathSegment(openApiDoc, projectName, pathSegment) ?? string.Empty;
            effectiveSegments.Add(effectiveSegment);

            // Create conflict registry for this path segment (lightweight: O(1)).
            // Uses the effective segment - the raw pathSegment would qualify conflicting types
            // against a namespace that is never generated when the segment collapses away.
            var registry = TypeConflictRegistry.ForSegment(conflicts, projectName, effectiveSegment, sharedSchemas, componentTypeNames: componentTypeNames);

            // Get segment-specific schemas (excluding shared ones) — uses pre-computed sharedSchemas to avoid O(S²)
            var segmentSchemas = PathSegmentHelper.GetSegmentSpecificSchemas(openApiDoc, pathSegment, sharedSchemas);

            // Same as for the shared set: ProblemDetails / ValidationProblemDetails bind to the framework types.
            segmentSchemas.RemoveWhere(name => BuiltInErrorContractNames.IsReserved(name) && !referencedReservedSchemas.Contains(name));

            // Generate segment-specific models (excluding shared ones)
            // Include shared models using directive so segment types can reference shared types
            if (segmentSchemas.Count > 0)
            {
                GenerateModelsForSchemas(
                    generatedContext,
                    openApiDoc,
                    projectName,
                    segmentSchemas,
                    effectiveSegment,
                    registry,
                    config.IncludeDeprecated,
                    config.GeneratePartialModels,
                    includeSharedModelsUsing: sharedSchemas.Count > 0,
                    xmlModels: xmlModels,
                    boundRequestModels: boundRequestModels,
                    xmlRequestModels: xmlRequestModels,
                    requireJsonProperties: !useValidationFilter);

                GenerateEnumsForSchemas(
                    generatedContext,
                    openApiDoc,
                    projectName,
                    segmentSchemas,
                    effectiveSegment);

                GenerateTuplesForSchemas(
                    generatedContext,
                    openApiDoc,
                    projectName,
                    segmentSchemas,
                    effectiveSegment);

                PolymorphicTypeEmitter.Emit(
                    generatedContext,
                    openApiDoc,
                    projectName,
                    segmentSchemas,
                    effectiveSegment);
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
                SourceText.From(WireBindingHelper.GenerateContent(projectName).NormalizeForSourceOutput(), Encoding.UTF8));
        }

        // The trim- and AOT-safe length-limit attributes the models and parameters are written with
        if (LengthLimitAttributesExtractor.IsNeeded(openApiDoc))
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.Generated.LengthLimitAttributes.g.cs",
                SourceText.From(LengthLimitAttributesExtractor.Generate(NamespaceBuilder.BuildBase(projectName)).NormalizeForSourceOutput(), Encoding.UTF8));
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
                SourceText.From(sequentialResults.Content.NormalizeForSourceOutput(), Encoding.UTF8));
        }

        // Generate combined endpoint mapping extension (calls all path segment endpoint methods)
        GenerateCombinedEndpointMapping(generatedContext, projectName, effectiveSegments);

        // Generate DI registration (not segmented - registers all handlers)
        GenerateDependencyInjection(generatedContext, openApiDoc, projectName, effectiveSegments, config.IncludeDeprecated);

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
            GenerateOpenIdConnectAuthentication(generatedContext, openApiDoc, projectName);
        }

        // Generate rate limiting policies and DI extensions (only if rate limiting is configured)
        // Note: Package availability checks are not performed here as Contracts projects
        // don't need the packages directly - consuming API projects will need them
        if (openApiDoc.HasRateLimiting())
        {
            GenerateRateLimitPolicies(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateRateLimitDependencyInjection(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
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
        if (config.GenerateWebhooks && openApiDoc.HasWebhooks())
        {
            GenerateWebhookHandlerInterfaces(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateWebhookParameterClasses(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateWebhookResultClasses(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateWebhookEndpoints(generatedContext, openApiDoc, projectName, config);
            GenerateWebhookDependencyInjection(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
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

        // Generate WebApplication extensions (GlobalErrorHandler middleware setup)
        if (useGlobalErrorHandler)
        {
            GenerateWebApplicationExtensions(generatedContext, projectName, useGlobalErrorHandler);
        }

        var operationCount = openApiDoc.Paths?.Sum(p => p.Value.Operations?.Count ?? 0) ?? 0;

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
            SourceText.From(GeneratedJsonOptionsExtractor.Generate(NamespaceBuilder.BuildBase(projectName)).NormalizeForSourceOutput(), Encoding.UTF8));

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
            var enumContent = InlineEnumExtractor.GenerateEnumContent(inlineEnum.EnumParameters, stringOnTheWire: true);
            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(modelsNamespace, inlineEnum.TypeName),
                SourceText.From(enumContent.NormalizeForSourceOutput(), Encoding.UTF8));
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
            .ToList();
        var queryObjectModels = QueryObjectHelper.Models(openApiDoc);
        var boundTypeNames = new HashSet<string>(boundSources.Select(x => x.TypeName), StringComparer.Ordinal);
        boundTypeNames.UnionWith(queryObjectModels.Keys);

        // Generate each model as a separate file
        foreach (var modelRecord in recordsParameters.Parameters)
        {
            var named = WithXmlNames(modelRecord, xmlSources, xmlModels, xmlRequestModels);
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
                SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));

            // An object in the query: the model reads the value the wire-binding step collected from its keys
            if (queryObjectModels.TryGetValue(record.Name.Split(' ')[0], out var queryObject))
            {
                context.AddGeneratedSource(
                    NamespaceBuilder.ToFileName(modelsNamespace, sanitizedName + "QueryObject"),
                    SourceText.From(QueryObjectModelExtractor.Generate(record, queryObject, modelsNamespace).NormalizeForSourceOutput(), Encoding.UTF8));
            }
        }

        if (xmlModels is not null)
        {
            XmlModelEmitter.EmitCompanions(context, xmlSources, recordsParameters.HeaderContent, modelsNamespace, projectName, xmlModels);
        }

        foreach (var bound in boundSources)
        {
            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(modelsNamespace, bound.TypeName + "Binding"),
                SourceText.From(XmlModelMapperExtractor.GenerateBinding(bound.TypeName, modelsNamespace, projectName, boundRequestModels![bound.SchemaKey]).NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// The record of a model an XML request body carries, with <c>[XmlElement]</c> / <c>[XmlAttribute]</c> on the
    /// properties whose XML name differs from their JSON name.
    /// </summary>
    private static RecordParameters WithXmlNames(
        RecordParameters record,
        List<XmlModelSource> xmlSources,
        ISet<string>? xmlModels,
        ISet<string>? xmlRequestModels)
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

        return record with
        {
            Parameters = record.Parameters?
                .Select(p => overrides.TryGetValue(p.Name, out var xml)
                    ? p with
                    {
                        Attributes = (p.Attributes ?? [])
                            .Append(new AttributeParameters(xml.IsAttribute ? "global::System.Xml.Serialization.XmlAttribute" : "global::System.Xml.Serialization.XmlElement", $"\"{xml.Name}\""))
                            .ToList(),
                    }
                    : p)
                .ToList(),
        };
    }

    /// <summary>
    /// Generates enums for a specific set of schemas.
    /// Used for both shared types (pathSegment = null) and segment-specific types.
    /// </summary>
    private static void GenerateEnumsForSchemas(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemaNames,
        string? pathSegment)
    {
        // Use EnumExtractor.ExtractForSchemas to extract only specific enums
        var enumParametersList = EnumExtractor.ExtractForSchemas(openApiDoc, projectName, schemaNames, pathSegment);

        if (enumParametersList is null || enumParametersList.Count == 0)
        {
            return;
        }

        var enumsNamespace = NamespaceBuilder.ForModels(projectName, pathSegment);

        // Generate each enum as a separate file
        foreach (var enumParams in enumParametersList)
        {
            var generatedContent = EnumExtractor.GenerateEnumContent(enumParams, stringOnTheWire: true);

            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(enumsNamespace, enumParams.EnumTypeName),
                SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Generates tuple records for a specific set of schemas.
    /// Tuples are schemas with prefixItems (JSON Schema 2020-12 / OpenAPI 3.1).
    /// </summary>
    private static void GenerateTuplesForSchemas(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemaNames,
        string? pathSegment)
    {
        // Use TupleExtractor.ExtractForSchemas to extract only specific tuples
        var tupleParametersList = TupleExtractor.ExtractForSchemas(
            openApiDoc,
            projectName,
            schemaNames,
            pathSegment);

        if (tupleParametersList is null || tupleParametersList.Count == 0)
        {
            return;
        }

        var ns = NamespaceBuilder.ForModels(projectName, pathSegment);

        // Generate each tuple as a separate file
        foreach (var tupleParams in tupleParametersList)
        {
            var generatedContent = TupleExtractor.GenerateTupleContent(tupleParams, ns);

            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(ns, tupleParams.Name),
                SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }
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
            var lines = generatedInterface.SplitIntoLinesPreserveEmpty();
            var namespaceFound = false;
            foreach (var line in lines)
            {
                if (namespaceFound)
                {
                    allGeneratedCode.AppendLine(line);
                }
                else if (line.StartsWith("namespace ", StringComparison.Ordinal))
                {
                    namespaceFound = true;
                }
            }
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

        // IEndpointDefinition is now generated once in a shared file — no per-segment emission needed

        // Appends the generated type body, skipping the generator's own header/namespace lines.
        static void AppendTypeBody(
            StringBuilder sb,
            string generatedCode)
        {
            var lines = generatedCode.SplitIntoLinesPreserveEmpty();
            var namespaceFound = false;
            foreach (var line in lines)
            {
                if (namespaceFound)
                {
                    sb.AppendLine(line);
                }
                else if (line.StartsWith("namespace ", StringComparison.Ordinal))
                {
                    namespaceFound = true;
                }
            }
        }

        void AddTypeSource(
            string typeName,
            string generatedCode)
        {
            var sb = CreateFileBuilder();
            AppendTypeBody(sb, generatedCode);

            context.AddGeneratedSource(
                $"{endpointsNamespace}.{typeName}.g.cs",
                SourceText.From(sb.ToString().NormalizeForSourceOutput(), Encoding.UTF8));
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

        var namespaceFound = false;
        foreach (var line in generatedClass.SplitIntoLinesPreserveEmpty())
        {
            if (namespaceFound)
            {
                builder.AppendLine(line);
            }
            else if (line.StartsWith("namespace ", StringComparison.Ordinal))
            {
                namespaceFound = true;
            }
        }

        context.AddGeneratedSource(
            $"{rootEndpointsNamespace}.{extensionParams.ClassTypeName}.g.cs",
            SourceText.From(builder.ToString().NormalizeForSourceOutput(), Encoding.UTF8));
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

    private static void GenerateDependencyInjection(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        List<string> pathSegments,
        bool includeDeprecated)
    {
        // Use ServerDependencyInjectionExtractor to extract class parameters with path segments
        var classParameters = ServerDependencyInjectionExtractor.Extract(openApiDoc, projectName, pathSegments, includeDeprecated);

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
            $"{projectName}.DependencyInjection.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    private static void GenerateWebApplicationExtensions(
        GeneratedSourceContext context,
        string projectName,
        bool useGlobalErrorHandler)
    {
        // Use WebApplicationExtensionsExtractor to generate middleware setup helpers
        var classParameters = WebApplicationExtensionsExtractor.Extract(projectName, useGlobalErrorHandler);

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
            $"{projectName}.WebApplicationExtensions.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
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
            var enumContent = InlineEnumExtractor.GenerateEnumContent(inlineEnum.EnumParameters, stringOnTheWire: true);
            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(parametersNamespace, inlineEnum.TypeName),
                SourceText.From(enumContent.NormalizeForSourceOutput(), Encoding.UTF8));
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
        var queryObjectModels = new HashSet<string>(QueryObjectHelper.Models(openApiDoc).Keys, StringComparer.Ordinal);
        var generatedContent = RecordContentGenerator.GenerateParameterRecords(codeDocGenerator, recordsParams, queryObjectModels);

        context.AddGeneratedSource(
            $"{parametersNamespace}.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));

        // What each record binds, for the endpoint's wire-binding step - described here, not found by reflection
        // (only where the step is generated: a record can bind a header no endpoint step reads)
        foreach (var record in WireBindingHelper.IsNeeded(openApiDoc) ? recordsParams.Parameters : [])
        {
            queryObjects.TryGetValue(record.Name, out var recordQueryObjects);
            if (WireBindingHelper.ParameterDescriptionClass(record, recordsParams.Namespace, recordsParams.HeaderContent, recordQueryObjects, registry) is not { } description)
            {
                continue;
            }

            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(parametersNamespace, description.ClassTypeName),
                SourceText.From(new GenerateContentForClass(codeDocGenerator, description).Generate().NormalizeForSourceOutput(), Encoding.UTF8));
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

        // Generate inline model files for any inline schemas discovered
        if (inlineSchemas.Count > 0)
        {
            var inlineTypes = CodeGenerationService.GenerateInlineModels(inlineSchemas, projectName);
            foreach (var inlineType in inlineTypes)
            {
                var inlineContent = CodeGenerationService.FormatAsFile(inlineType);
                context.AddGeneratedSource(
                    NamespaceBuilder.ToFileName(modelsNamespace, inlineType.TypeName),
                    SourceText.From(inlineContent.NormalizeForSourceOutput(), Encoding.UTF8));
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
            var lines = generatedClass.SplitIntoLinesPreserveEmpty();
            var namespaceFound = false;
            foreach (var line in lines)
            {
                if (namespaceFound)
                {
                    allGeneratedCode.AppendLine(line);
                }
                else if (line.StartsWith("namespace ", StringComparison.Ordinal))
                {
                    namespaceFound = true;
                }
            }
        }

        // 429 factories wrap their result to add Retry-After; emit the wrapper once per Results namespace.
        if (allGeneratedCode.ToString().Contains("new RetryAfterHttpResult("))
        {
            allGeneratedCode.AppendLine(ResultClassExtractor.RetryAfterHttpResultSource());
        }

        // A problem factory passes the handler's problem through with the response status
        if (allGeneratedCode.ToString().Contains("ProblemWithStatus.Of("))
        {
            allGeneratedCode.AppendLine(ResultClassExtractor.ProblemWithStatusSource());
        }

        if (allGeneratedCode.ToString().Contains("new LocationHttpResult("))
        {
            allGeneratedCode.AppendLine(ResultClassExtractor.LocationHttpResultSource());
        }

        // A factory writes the headers its response declares
        if (allGeneratedCode.ToString().Contains(ResponseHeaderFactoryHelper.WriterCall))
        {
            allGeneratedCode.AppendLine(ResponseHeaderFactoryHelper.ResponseHeadersSource());
        }

        // An XML body's factories write the model's generated XML
        if (allGeneratedCode.ToString().Contains("new XmlHttpResult("))
        {
            allGeneratedCode.AppendLine(ResultClassExtractor.XmlHttpResultSource());
        }

        // A body offered as JSON and XML is negotiated by Accept
        if (allGeneratedCode.ToString().Contains("new NegotiatedHttpResult("))
        {
            allGeneratedCode.AppendLine(ResultClassExtractor.NegotiatedHttpResultSource());
        }

        // A JSON body is written through the JsonTypeInfo the app's JSON options resolve (trim- and AOT-safe)
        if (allGeneratedCode.ToString().Contains("JsonTypeInfoHttpResult.Of("))
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
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
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
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
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
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
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
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
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
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
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
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
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
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
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
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    private static void GenerateOpenIdConnectAuthentication(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName)
    {
        // Extract OpenID Connect configuration
        var oidcConfig = OpenIdConnectConfigExtractor.Extract(openApiDoc);

        if (oidcConfig is null)
        {
            return;
        }

        // Generate OpenID Connect DI extension
        var generatedContent = OpenIdConnectDependencyInjectionExtractor.Extract(oidcConfig, projectName);

        if (generatedContent is null)
        {
            return;
        }

        context.AddGeneratedSource(
            $"{projectName}.Authentication.OpenIdConnect.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
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
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
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
            SourceText.From(serviceOptionsContent.NormalizeForSourceOutput(), Encoding.UTF8));

        // Generate ApiMiddlewareOptions
        var middlewareOptionsContent = ApiOptionsExtractor.ExtractMiddlewareOptions(openApiDoc, projectName, useGlobalErrorHandler);
        context.AddGeneratedSource(
            $"{projectName}.ApiMiddlewareOptions.g.cs",
            SourceText.From(middlewareOptionsContent.NormalizeForSourceOutput(), Encoding.UTF8));
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
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));

        // The converter the Add…Api() above puts first in the JSON options, when there is an enum to write
        if (EnumWireConverterExtractor.HasEnums(openApiDoc))
        {
            context.AddGeneratedSource(
                $"{projectName}.Generated.{EnumWireConverterExtractor.ClassName}.g.cs",
                SourceText.From(EnumWireConverterExtractor.Generate(NamespaceBuilder.BuildBase(projectName), context.Enums).NormalizeForSourceOutput(), Encoding.UTF8));
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
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
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
                SourceText.From(formattedCode.NormalizeForSourceOutput(), Encoding.UTF8));
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
                SourceText.From(formattedCode.NormalizeForSourceOutput(), Encoding.UTF8));
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
                SourceText.From(formattedCode.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Generates webhook endpoint registration from OpenAPI webhooks.
    /// </summary>
    private static void GenerateWebhookEndpoints(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config)
    {
        var generatedType = CodeGenerationService.GenerateWebhookEndpoints(openApiDoc, projectName, config);
        if (generatedType is not null)
        {
            var formattedCode = CodeGenerationService.FormatAsFile(generatedType);
            context.AddGeneratedSource(
                $"Webhooks.Endpoints.{generatedType.TypeName}.g.cs",
                SourceText.From(formattedCode.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Generates webhook DI registration from OpenAPI webhooks.
    /// </summary>
    private static void GenerateWebhookDependencyInjection(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        var generatedType = CodeGenerationService.GenerateWebhookDependencyInjection(openApiDoc, projectName, includeDeprecated);
        if (generatedType is not null)
        {
            var formattedCode = CodeGenerationService.FormatAsFile(generatedType);
            context.AddGeneratedSource(
                $"Webhooks.{generatedType.TypeName}.g.cs",
                SourceText.From(formattedCode.NormalizeForSourceOutput(), Encoding.UTF8));
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
            /// specification, kept on the member as [EnumMember]) or by its member name.
            /// </summary>
            [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
            public sealed class ParsableEnumList<T> : List<T>, IParsable<ParsableEnumList<T>>
                where T : struct, Enum
            {
                private static readonly Dictionary<string, T> WireValues = BuildWireValues();

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

                private static bool TryParseItem(string item, out T value)
                    => WireValues.TryGetValue(item, out value) ||
                       (Enum.TryParse(item, ignoreCase: true, out value) && Enum.IsDefined(value));

                private static Dictionary<string, T> BuildWireValues()
                {
                    var values = new Dictionary<string, T>(StringComparer.Ordinal);
                    foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (field.GetCustomAttribute<EnumMemberAttribute>()?.Value is { } wireValue)
                        {
                            values[wireValue] = (T)field.GetValue(null)!;
                        }
                    }

                    return values;
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
            SourceText.From(content.NormalizeForSourceOutput(), Encoding.UTF8));
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
            SourceText.From(sb.ToString().NormalizeForSourceOutput(), Encoding.UTF8));
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
            SourceText.From(content.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    private static void GenerateHealthCheckServiceExtensions(
        GeneratedSourceContext context,
        string projectName)
    {
        var content = HealthCheckExtractor.GenerateServiceExtensions(projectName);

        context.AddGeneratedSource(
            $"{projectName}.Health.HealthCheckServiceExtensions.g.cs",
            SourceText.From(content.NormalizeForSourceOutput(), Encoding.UTF8));
    }
}