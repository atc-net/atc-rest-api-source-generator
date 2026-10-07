// ReSharper disable InvertIf
// ReSharper disable UseWithExpressionToCopyRecord
// ReSharper disable UnusedVariable
// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
namespace Atc.Rest.Api.SourceGenerator;

/// <summary>
/// The source generator behind the <c>.atc-rest-api-client</c> marker file: the models and the HTTP clients (typed, or one endpoint per operation) of every OpenAPI specification in the project.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "OK.")]
[Generator(LanguageNames.CSharp)]
public class ApiClientGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Registers the pipeline: the marker file and the specifications as additional texts, the project's package references, and the client code as the output.
    /// </summary>
    /// <param name="context">The initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find marker files - marker file presence IS the trigger for this generator
        var markerFiles = context.AdditionalTextsProvider
            .Where(static file => Path.GetFileName(file.Path).Equals(Constants.MarkerFile.Client, StringComparison.OrdinalIgnoreCase) ||
                                  Path.GetFileName(file.Path).Equals(Constants.MarkerFile.ClientJson, StringComparison.OrdinalIgnoreCase))
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

        // Check for required package references
        var packageReferencesProvider = context.CompilationProvider
            .Select(static (compilation, _) => new ClientPackageReferences
            {
                HasAtcRestClient = compilation.ReferencedAssemblyNames
                    .Any(a => a.Name.Equals("Atc.Rest.Client", StringComparison.OrdinalIgnoreCase)),
                HasResilience = compilation.ReferencedAssemblyNames
                    .Any(a => a.Name.Equals("Microsoft.Extensions.Http.Resilience", StringComparison.OrdinalIgnoreCase)),
                HasHttpClientFactory = compilation.ReferencedAssemblyNames
                    .Any(a => a.Name.Equals("Microsoft.Extensions.Http", StringComparison.OrdinalIgnoreCase)),
            });

        // Combine ALL YAML files (as collection) with marker files and compilation info
        var combined = yamlFiles
            .Combine(markerFiles)
            .Combine(packageReferencesProvider);

        // Register source output - processes all YAML files together for multi-parts support
        context.RegisterSourceOutput(combined, RegisterSourceOutputAction);
    }

    private static void RegisterSourceOutputAction(
        SourceProductionContext productionContext,
        ((EquatableArray<YamlFileInfo> Left,
            EquatableArray<MarkerFileInfo> Right) Left,
            ClientPackageReferences Right) combinedData)
    {
        var ((yamlFiles, markers), packages) = combinedData;

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

        ClientConfig config;
        try
        {
            config = MarkerConfigInspector.Read<ClientConfig>(marker.Content, out var markerProblems);
            DiagnosticHelpers.ReportUnknownMarkerSettings(productionContext, marker.Path, markerProblems);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A marker that cannot be read is no configuration at all
            DiagnosticHelpers.ReportMarkerConfigParseError(productionContext, marker.Path, ex, typeof(ClientConfig));
            return;
        }

        // Skip if generation is disabled in config
        if (!config.Generate)
        {
            return;
        }

        // Check for Atc.Rest.Client reference when using EndpointPerOperation mode
        if (config.GenerationMode == GenerationModeType.EndpointPerOperation && !packages.HasAtcRestClient)
        {
            DiagnosticHelpers.ReportClientRequiresAtcRestClient(productionContext);
            return;
        }

        // Everything from here runs under the catch: a failure before the base file is known is reported on the marker
        var filePath = marker.Path;
        try
        {
            // Identify the base file (non-part file or the first file that is not a part file)
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
                GenerateApiClientMultiPart(productionContext, baseFile.Value, partFiles, config, packages, overlays);
            }
            else
            {
                // Single file mode
                GenerateApiClient(productionContext, baseFile.Value.Path, baseFile.Value.Content, config, packages, overlays);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            DiagnosticHelpers.ReportClientGenerationError(productionContext, filePath, ex);
        }
    }

    /// <summary>
    /// Generates API client code from a multi-parts specification.
    /// </summary>
    private static void GenerateApiClientMultiPart(
        SourceProductionContext context,
        YamlFileInfo baseFile,
        List<YamlFileInfo> partFiles,
        ClientConfig config,
        ClientPackageReferences packages,
        IReadOnlyList<OverlayFile> overlays)
    {
        var openApiDoc = SpecificationLoader.MergeAndValidate(context, baseFile, partFiles, config.MultiPartConfiguration, config.ValidateSpecificationStrategy, overlays);
        if (openApiDoc is null)
        {
            return;
        }

        // Resolve the root namespace: marker namespace, qualifying info.title, then base file name.
        var projectName = NamespaceResolver.Resolve(
            config.Namespace,
            openApiDoc.Info?.Title,
            baseFile.Path);

        GenerateApiClientFromDocument(context, baseFile.Path, openApiDoc, config, packages, projectName);
    }

    /// <summary>
    /// Record to hold client package reference information.
    /// </summary>
    private sealed record ClientPackageReferences
    {
        public bool HasAtcRestClient { get; init; }

        public bool HasResilience { get; init; }

        /// <summary>
        /// True when Microsoft.Extensions.Http is referenced. The typed-client DI extension calls
        /// AddHttpClient&lt;TInterface, TClient&gt;(), so it can only be emitted when present.
        /// </summary>
        public bool HasHttpClientFactory { get; init; }
    }

    private static void GenerateApiClient(
        SourceProductionContext context,
        string yamlPath,
        string yamlContent,
        ClientConfig config,
        ClientPackageReferences packages,
        IReadOnlyList<OverlayFile> overlays)
    {
        var openApiDoc = SpecificationLoader.ParseAndValidate(context, yamlPath, yamlContent, config.ValidateSpecificationStrategy, DiagnosticHelpers.ReportClientParsingError, overlays);
        if (openApiDoc is null)
        {
            return;
        }

        // Resolve the root namespace: marker namespace, qualifying info.title, then file name.
        var projectName = NamespaceResolver.Resolve(
            config.Namespace,
            openApiDoc.Info?.Title,
            yamlPath);

        GenerateApiClientFromDocument(context, yamlPath, openApiDoc, config, packages, projectName);
    }

    /// <summary>
    /// Generates API client code from a pre-parsed OpenAPI document.
    /// Used by both single-file and multi-parts generation flows.
    /// </summary>
    private static void GenerateApiClientFromDocument(
        SourceProductionContext context,
        string yamlPath,
        OpenApiDocument openApiDoc,
        ClientConfig config,
        ClientPackageReferences packages,
        string projectName)
    {
        // An inline allOf over a plain base becomes one inline object, so it gets a model of its own
        InlineAllOfFlattener.Flatten(openApiDoc);

        // Filter paths based on include/exclude patterns before segmentation
        // (on a copy: the parsed document is cached and shared)
        openApiDoc = PathFilterHelper.WithFilteredPaths(openApiDoc, config.IncludePaths, config.ExcludePaths);

        // Report diagnostics for unsupported OpenAPI features
        foreach (var diagnostic in DiagnosticHelpers.DetectUnsupportedFeatures(openApiDoc))
        {
            context.ReportDiagnostic(diagnostic);
        }

        // Get unique path segments for grouping generated files
        var pathSegments = PathSegmentHelper.GetUniquePathSegments(openApiDoc);

        // Scan for type conflicts once (optimization: O(schemas) instead of O(pathSegments × schemas))
        var conflicts = TypeConflictRegistry.ScanForConflicts(
            openApiDoc,
            projectName,
            pathSegments.Select(s => PathSegmentHelper.ResolveEffectivePathSegment(openApiDoc, projectName, s) ?? string.Empty));

        // Create system type conflict resolver for conditional Task qualification
        var modelNames = openApiDoc.Components?.Schemas?.Keys ?? [];
        var systemTypeResolver = new SystemTypeConflictResolver(modelNames);

        // Proxy over `context` that adds [ExcludeFromCodeCoverage] alongside [GeneratedCode] when
        // configured. Used by every sub-generation method below that only ever calls AddSource.
        var generatedContext = new GeneratedSourceContext(context, config.ExcludeFromCodeCoverage, DeprecationDetector.HasDeprecatedMembers(openApiDoc));

        // The custom error model's type name as the generated code refers to it (fully qualified when it collides).
        var customErrorTypeName = config.CustomErrorResponseModel?.Name;

        // A TypedClient download returns a DownloadedFile (without the Atc.Rest.Client-based FromResponse), and a
        // model or a file a ContentOrFile<T>
        if (config.GenerationMode != GenerationModeType.EndpointPerOperation && HasBufferedFileDownload(openApiDoc, streamedDownloadsAreBuffered: true))
        {
            GenerateDownloadedFile(generatedContext, projectName, includeEndpointResponse: false);
            if (HasModelOrFileResponse(openApiDoc))
            {
                GenerateContentOrFile(generatedContext, projectName);
            }
        }

        // The serializer options the typed clients' DI registrations read (only with Microsoft.Extensions.Http, as the
        // registrations themselves)
        if (config.GenerationMode != GenerationModeType.EndpointPerOperation && packages.HasHttpClientFactory)
        {
            GenerateApiClientJsonOptions(generatedContext, openApiDoc, projectName);
        }

        // Generate shared ProblemDetails and factory once for EndpointPerOperation mode
        if (config.GenerationMode == GenerationModeType.EndpointPerOperation)
        {
            GenerateProblemDetails(generatedContext, projectName);
            GenerateProblemDetailsFactory(generatedContext, projectName, hasXml: XmlBodyReachability.Collect(openApiDoc).Count > 0);
            GenerateConstants(generatedContext, projectName, config.HttpClientName);

            // A file download's OkContent (only when the specification has one)
            if (HasBufferedFileDownload(openApiDoc))
            {
                GenerateDownloadedFile(generatedContext, projectName, includeEndpointResponse: true);
            }

            // Generate custom error response model if configured. Named like a spec schema, it would shadow that
            // model in every generated file (the root namespace encloses them all), so it then gets its own
            // namespace and is referred to by its full name.
            if (config.CustomErrorResponseModel is not null)
            {
                var customName = config.CustomErrorResponseModel.Name;
                var collides = openApiDoc.Components?.Schemas?.Keys.Any(k =>
                    string.Equals(k, customName, StringComparison.Ordinal) ||
                    string.Equals(k.ToPascalCaseForDotNet(), customName, StringComparison.Ordinal)) == true;
                var customNamespace = collides ? $"{projectName}.Generated.CustomErrors" : $"{projectName}.Generated";
                GenerateCustomErrorResponse(generatedContext, config.CustomErrorResponseModel, customNamespace, projectName);
                if (collides)
                {
                    customErrorTypeName = $"global::{customNamespace}.{customName}";
                }
            }
        }

        // Detect shared schemas (used by multiple path segments) for deduplication
        var sharedSchemas = PathSegmentHelper.GetSharedSchemas(openApiDoc);

        // The models an XML body can carry get an XML mapping (only when the specification has an XML body)
        var xmlModels = XmlBodyReachability.Collect(openApiDoc);
        XmlModelEmitter.EmitSupport(generatedContext, projectName, xmlModels);

        // In EndpointPerOperation mode the generator owns ProblemDetails/ValidationProblemDetails
        // (emitted above into "{projectName}.Generated"). A same-named schema in the specification
        // can never be referenced - every consumer sits under "{projectName}.Generated.*", so C#
        // binds the unqualified name to the enclosing-namespace built-in - so drop it and tell the
        // user their declaration is being ignored rather than emitting silent dead code.
        if (config.GenerationMode == GenerationModeType.EndpointPerOperation)
        {
            ReportAndRemoveShadowedErrorContracts(context, openApiDoc, sharedSchemas, yamlPath);
        }

        // The converter both client modes put first in their JSON options, when there is an enum to write
        if (EnumWireConverterExtractor.HasEnums(openApiDoc))
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.Generated.{EnumWireConverterExtractor.ClassName}.g.cs",
                SourceText.From(EnumWireConverterExtractor.Generate(NamespaceBuilder.BuildBase(projectName)).NormalizeForSourceOutput(), Encoding.UTF8));
        }

        // The trim- and AOT-safe length-limit attributes the models and parameters are written with
        if (LengthLimitAttributesExtractor.IsNeeded(openApiDoc))
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.Generated.LengthLimitAttributes.g.cs",
                SourceText.From(LengthLimitAttributesExtractor.Generate(NamespaceBuilder.BuildBase(projectName)).NormalizeForSourceOutput(), Encoding.UTF8));
        }

        // Single-client mode: emit one client covering every operation, with all models flattened
        // into "{projectName}.Generated.Models". Handled before the per-area loop because the two
        // layouts are mutually exclusive.
        if (config.GenerationMode != GenerationModeType.EndpointPerOperation &&
            config.ClientGranularity == ClientGranularityType.Single)
        {
            GenerateSingleTypedClient(context, generatedContext, openApiDoc, projectName, config, conflicts, systemTypeResolver, yamlPath, packages.HasHttpClientFactory, xmlModels);
            return;
        }

        // 'clientName' only names the one client produced by Single mode; under PerArea each area
        // names its own client, so the value cannot be applied and is ignored.
        if (!string.IsNullOrWhiteSpace(config.ClientName))
        {
            context.ReportDiagnostic(
                DiagnosticHelpers.ToRoslynDiagnostic(
                    GeneratorDiagnosticMessage.Warning(
                        RuleIdentifiers.ClientNameIgnoredForPerArea,
                        $"'clientName' ('{config.ClientName}') is ignored because 'clientGranularity' is 'PerArea', " +
                        "which generates one client per API area. Set \"clientGranularity\": \"Single\" to use it.",
                        yamlPath)));
        }

        // Generate shared models/enums first (under common namespace without segment suffix)
        if (sharedSchemas.Count > 0)
        {
            // Create conflict registry for shared types (no segment)
            var sharedRegistry = TypeConflictRegistry.ForSegment(conflicts, projectName, extensibleEnums: config.ExtensibleEnums);

            GenerateModelsForSchemas(generatedContext, openApiDoc, projectName, sharedSchemas, pathSegment: null, sharedRegistry, config.IncludeDeprecated, config.GeneratePartialModels, strictRequiredProperties: config.StrictRequiredProperties, xmlModels: xmlModels, extensibleEnums: config.ExtensibleEnums);
            GenerateEnumsForSchemas(generatedContext, openApiDoc, projectName, sharedSchemas, pathSegment: null, config.ExtensibleEnums);
            GenerateTuplesForSchemas(generatedContext, openApiDoc, projectName, sharedSchemas, pathSegment: null);
            PolymorphicTypeEmitter.Emit(generatedContext, openApiDoc, projectName, sharedSchemas, pathSegment: null);
        }

        // Track generated path segments for consolidated DI extension
        var generatedPathSegments = new List<string>();

        // Generate files per path segment (excluding shared schemas)
        foreach (var pathSegment in pathSegments)
        {
            // A superseded IDE run stops here instead of emitting the rest
            context.CancellationToken.ThrowIfCancellationRequested();

            // Resolve the segment used for namespaces/file names. Redundant segments (single-segment
            // documents, or segments that merely echo the root namespace tail) resolve away so
            // generated types land directly under "{projectName}.Generated.{Category}".
            // Normalize to string.Empty (never null) so the extractors treat this as an explicitly
            // resolved value instead of "not specified" and never fall back to the raw segment.
            var effectiveSegment = PathSegmentHelper.ResolveEffectivePathSegment(openApiDoc, projectName, pathSegment) ?? string.Empty;

            // Create conflict registry for this path segment (lightweight: O(1))
            var registry = TypeConflictRegistry.ForSegment(conflicts, projectName, effectiveSegment, sharedSchemas, config.ExtensibleEnums);

            // Get segment-specific schemas (excluding shared ones) — uses pre-computed sharedSchemas to avoid O(S²)
            var segmentSchemas = PathSegmentHelper.GetSegmentSpecificSchemas(openApiDoc, pathSegment, sharedSchemas);

            // Built-in error contracts are emitted once under "{projectName}.Generated"; drop any
            // spec-defined namesake so it is not re-emitted per segment. Already reported above.
            if (config.GenerationMode == GenerationModeType.EndpointPerOperation)
            {
                segmentSchemas.RemoveWhere(BuiltInErrorContractNames.IsReserved);
            }

            // Generate segment-specific models (excluding shared schemas)
            // Include shared models using directive so segment types can reference shared types
            if (segmentSchemas.Count > 0)
            {
                GenerateModelsForSchemas(generatedContext, openApiDoc, projectName, segmentSchemas, effectiveSegment, registry, config.IncludeDeprecated, config.GeneratePartialModels, includeSharedModelsUsing: sharedSchemas.Count > 0, strictRequiredProperties: config.StrictRequiredProperties, xmlModels: xmlModels, extensibleEnums: config.ExtensibleEnums);
                GenerateEnumsForSchemas(generatedContext, openApiDoc, projectName, segmentSchemas, effectiveSegment, config.ExtensibleEnums);
                GenerateTuplesForSchemas(generatedContext, openApiDoc, projectName, segmentSchemas, effectiveSegment);
                PolymorphicTypeEmitter.Emit(generatedContext, openApiDoc, projectName, segmentSchemas, effectiveSegment);
            }

            // Generate client code based on generation mode
            if (config.GenerationMode == GenerationModeType.EndpointPerOperation)
            {
                // Inline (anonymous) response/request schemas are emitted into the segment's Models
                // namespace too, so they must count here - otherwise the generated endpoint and
                // result files omit the Models 'using' and fail to compile with CS0246.
                var hasSegmentModels = segmentSchemas.Count > 0 ||
                                       PathSegmentHelper.PathSegmentHasInlineModels(openApiDoc, pathSegment);
                var hasSharedModels = sharedSchemas.Count > 0;

                // If customErrorResponseModel is configured and errorResponseFormat is default (ProblemDetails),
                // automatically use Custom format for backward compatibility
                var effectiveErrorFormat = config.CustomErrorResponseModel is not null &&
                                           config.ErrorResponseFormat == ErrorResponseFormatType.ProblemDetails
                    ? ErrorResponseFormatType.Custom
                    : config.ErrorResponseFormat;

                var hasEndpoints = GenerateEndpointPerOperation(generatedContext, openApiDoc, projectName, pathSegment, effectiveSegment, registry, config.IncludeDeprecated, hasSegmentModels, hasSharedModels, config.UseServersBasePath, config.HttpClientName, effectiveErrorFormat, customErrorTypeName, config.ValidateSpecificationStrategy, config.StrictRequiredProperties, config.PreferXml, config.ExtensibleEnums);
                if (hasEndpoints)
                {
                    generatedPathSegments.Add(effectiveSegment);
                }
            }
            else
            {
                var hasSegmentModelsTyped = segmentSchemas.Count > 0;
                var hasSharedModelsTyped = sharedSchemas.Count > 0;
                GenerateTypedClient(generatedContext, openApiDoc, projectName, pathSegment, effectiveSegment, registry, systemTypeResolver, config.IncludeDeprecated, hasSegmentModelsTyped, hasSharedModelsTyped, config.UseServersBasePath, config.ValidateSpecificationStrategy, config.ClientSuffix, clientName: null, packages.HasHttpClientFactory, config.TypedClientResultStyle, config.StrictRequiredProperties, config.PreferXml, config.ExtensibleEnums);
            }
        }

        // Emit the shared StreamReaders helper once when an operation references it (Server-Sent
        // Events today). The per-operation variant deserializes each event via the DI-configured
        // contract serializer, whereas the typed variant uses the client serializer options. A
        // generated client is entirely one mode or the other, so only one variant is ever emitted.
        if (StreamReadersExtractor.DocumentRequiresStreamReaders(openApiDoc))
        {
            var perOperation = config.GenerationMode == GenerationModeType.EndpointPerOperation;
            var streamReadersContent = StreamReadersExtractor.GenerateContent(projectName, perOperation);
            generatedContext.AddGeneratedSource(
                $"{projectName}.Streaming.StreamReaders.g.cs",
                SourceText.From(streamReadersContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }

        // Generate consolidated DI extension method for all path segments (EndpointPerOperation mode only).
        // When the only segment resolved away, the per-segment extension is already project-named and
        // lives in "{projectName}.Generated", so a consolidated wrapper would be a duplicate.
        if (config.GenerationMode == GenerationModeType.EndpointPerOperation &&
            generatedPathSegments.Count > 0 &&
            !generatedPathSegments.TrueForAll(string.IsNullOrEmpty))
        {
            var consolidatedDiContent = GenerateConsolidatedDiExtension(projectName, generatedPathSegments);
            generatedContext.AddGeneratedSource(
                $"{projectName}.Endpoints.DependencyInjection.g.cs",
                SourceText.From(consolidatedDiContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }

        // Generate resilience policies and DI extensions (only if resilience is configured)
        if (openApiDoc.HasRetryConfiguration())
        {
            // Check for resilience package
            if (!packages.HasResilience)
            {
                DiagnosticHelpers.ReportResilienceRequiresPackage(context);

                // Continue with generation - user may have a custom setup
            }

            GenerateResiliencePolicies(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateResilienceDependencyInjection(generatedContext, openApiDoc, projectName, config.IncludeDeprecated);
        }

        // Generate OAuth token management infrastructure (only if OAuth2 is configured and enabled)
        if (config.GenerateOAuthTokenManagement && OAuthConfigExtractor.HasOAuth2Security(openApiDoc))
        {
            GenerateOAuthInfrastructure(generatedContext, openApiDoc, projectName);
        }

        // Report generation summary
        var operationCount = openApiDoc.Paths?.Sum(p => p.Value.Operations?.Count ?? 0) ?? 0;
        var schemaCount = openApiDoc.Components?.Schemas?.Count ?? 0;
        ReportClientGenerationSummary(
            context,
            yamlPath,
            warnings: 0, // Validation diagnostics already reported earlier
            errors: 0,
            modelCount: schemaCount,
            clientMethodCount: operationCount,
            endpointCount: generatedPathSegments.Count);
    }

    /// <summary>
    /// Reports a generation summary at the end of client code generation.
    /// </summary>
    private static void ReportClientGenerationSummary(
        SourceProductionContext context,
        string yamlPath,
        int warnings,
        int errors,
        int modelCount,
        int clientMethodCount,
        int endpointCount)
    {
        var specName = Path.GetFileName(yamlPath);

        DiagnosticHelpers.ReportGenerationSummary(
            context,
            specName,
            "Client",
            modelCount,
            clientMethodCount,
            endpointCount,
            warnings,
            errors);
    }

    private static void GenerateResiliencePolicies(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        var content = ResiliencePoliciesExtractor.Extract(openApiDoc, projectName, includeDeprecated);
        if (content is not null)
        {
            context.AddGeneratedSource(
                $"{projectName}.Resilience.Policies.g.cs",
                SourceText.From(content.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    private static void GenerateResilienceDependencyInjection(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        var content = ResilienceDependencyInjectionExtractor.Extract(openApiDoc, projectName, includeDeprecated);
        if (content is not null)
        {
            context.AddGeneratedSource(
                $"{projectName}.Resilience.DependencyInjection.g.cs",
                SourceText.From(content.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    private static void GenerateOAuthInfrastructure(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName)
    {
        // Extract OAuth configuration from OpenAPI document
        var oauthConfig = OAuthConfigExtractor.Extract(openApiDoc);
        if (oauthConfig is null)
        {
            return;
        }

        // Generate OAuthClientOptions (IOptions<T> configuration)
        var optionsContent = OAuthOptionsExtractor.Extract(oauthConfig, projectName);
        if (optionsContent is not null)
        {
            context.AddGeneratedSource(
                $"{projectName}.OAuth.OAuthClientOptions.g.cs",
                SourceText.From(optionsContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }

        // Generate OAuthTokenResponse model
        var tokenResponseContent = OAuthTokenProviderExtractor.ExtractTokenResponse(projectName);
        context.AddGeneratedSource(
            $"{projectName}.OAuth.OAuthTokenResponse.g.cs",
            SourceText.From(tokenResponseContent.NormalizeForSourceOutput(), Encoding.UTF8));

        // Generate IOAuthTokenProvider interface
        var interfaceContent = OAuthTokenProviderExtractor.ExtractInterface(projectName);
        context.AddGeneratedSource(
            $"{projectName}.OAuth.IOAuthTokenProvider.g.cs",
            SourceText.From(interfaceContent.NormalizeForSourceOutput(), Encoding.UTF8));

        // Generate OAuthTokenProvider implementation
        var implementationContent = OAuthTokenProviderExtractor.ExtractImplementation(oauthConfig, projectName);
        if (implementationContent is not null)
        {
            context.AddGeneratedSource(
                $"{projectName}.OAuth.OAuthTokenProvider.g.cs",
                SourceText.From(implementationContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }

        // Generate OAuthAuthenticationHandler
        var handlerContent = OAuthHandlerExtractor.Extract(projectName);
        context.AddGeneratedSource(
            $"{projectName}.OAuth.OAuthAuthenticationHandler.g.cs",
            SourceText.From(handlerContent.NormalizeForSourceOutput(), Encoding.UTF8));

        // Generate OAuth DI extensions
        var diContent = OAuthDependencyInjectionExtractor.Extract(oauthConfig, projectName);
        if (diContent is not null)
        {
            context.AddGeneratedSource(
                $"{projectName}.OAuth.DependencyInjection.g.cs",
                SourceText.From(diContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Generates models for specific schemas (used for shared or segment-specific types).
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
        bool strictRequiredProperties = false,
        ISet<string>? xmlModels = null,
        bool extensibleEnums = false)
    {
        // Use SchemaExtractor to extract specific schemas with inline enum support
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

        // File names mirror the namespace the types are emitted into
        var modelsNamespace = NamespaceBuilder.ForModels(projectName, pathSegment);

        // Generate inline enum files first (before records that use them)
        foreach (var inlineEnum in inlineEnums)
        {
            var enumContent = InlineEnumExtractor.GenerateEnumContent(inlineEnum.EnumParameters, extensibleEnums);
            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(modelsNamespace, inlineEnum.TypeName),
                SourceText.From(enumContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }

        if (recordsParameters is null || recordsParameters.Parameters.Count == 0)
        {
            return;
        }

        // For client-side, convert IFormFile types to IFileContent (from Atc.Rest.Client)
        recordsParameters = ConvertToClientTypes(recordsParameters);

        // strictRequiredProperties: a response that leaves out a required property fails when it is read
        if (strictRequiredProperties)
        {
            recordsParameters = AddJsonRequired(recordsParameters);
        }

        var codeDocGenerator = new CodeDocumentationTagsGenerator();

        // Generate each model as a separate file
        foreach (var record in recordsParameters.Parameters)
        {
            // Create a single-record container with the same header and namespace
            var singleRecordParams = new RecordsParameters(
                HeaderContent: recordsParameters.HeaderContent,
                Namespace: recordsParameters.Namespace,
                DocumentationTags: recordsParameters.DocumentationTags,
                Attributes: recordsParameters.Attributes,
                DeclarationModifier: recordsParameters.DeclarationModifier,
                Parameters: [record]);

            var generatedContent = RecordContentGenerator.Generate(codeDocGenerator, singleRecordParams);

            // Sanitize record name for file system (generic types have <> which are invalid)
            var sanitizedName = record.Name.SanitizeForFileName();

            // File name mirrors the namespace, e.g. My.Api.Generated.Models.AuthorizationDto.g.cs
            var fileName = NamespaceBuilder.ToFileName(modelsNamespace, sanitizedName);

            context.AddGeneratedSource(
                fileName,
                SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }

        if (xmlModels is not null)
        {
            XmlModelEmitter.EmitCompanions(context, xmlSources, recordsParameters.HeaderContent, modelsNamespace, projectName, xmlModels, extensibleEnums);
        }
    }

    /// <summary>
    /// Generates enums for specific schemas (used for shared or segment-specific types).
    /// </summary>
    private static void GenerateEnumsForSchemas(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemaNames,
        string? pathSegment,
        bool extensibleEnums)
    {
        // Use EnumExtractor to extract specific enum schemas
        var enumParametersList = EnumExtractor.ExtractForSchemas(
            openApiDoc,
            projectName,
            schemaNames,
            pathSegment);

        if (enumParametersList is null || enumParametersList.Count == 0)
        {
            return;
        }

        var enumsNamespace = NamespaceBuilder.ForModels(projectName, pathSegment);

        // Generate each enum as a separate file
        foreach (var enumParams in enumParametersList)
        {
            var generatedContent = EnumExtractor.GenerateEnumContent(enumParams, extensibleEnums);

            // File name mirrors the namespace the enum is emitted into
            var fileName = NamespaceBuilder.ToFileName(enumsNamespace, enumParams.EnumTypeName);

            context.AddGeneratedSource(
                fileName,
                SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Generates tuple records for specific schemas (used for shared or segment-specific types).
    /// Tuples are schemas with prefixItems (JSON Schema 2020-12 / OpenAPI 3.1).
    /// </summary>
    private static void GenerateTuplesForSchemas(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemaNames,
        string? pathSegment)
    {
        // Use TupleExtractor to extract specific tuple schemas
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

            // File name mirrors the namespace the tuple is emitted into
            var fileName = NamespaceBuilder.ToFileName(ns, tupleParams.Name);

            context.AddGeneratedSource(
                fileName,
                SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Converts server-side types (IFormFile) to client-side types (IFileContent from Atc.Rest.Client) in the records parameters.
    /// </summary>
    /// <summary>
    /// Adds <c>[JsonRequired]</c> next to every <c>[Required]</c> of the client models (<c>strictRequiredProperties</c>),
    /// so System.Text.Json rejects JSON that leaves a required property out. The server keeps DataAnnotations only:
    /// its ValidationFilter answers a missing required property with a ValidationProblemDetails.
    /// </summary>
    private static RecordsParameters AddJsonRequired(
        RecordsParameters recordsParameters)
    {
        var records = recordsParameters.Parameters
            .Select(WithJsonRequired)
            .ToList();

        const string jsonSerializationUsing = "using System.Text.Json.Serialization;";
        var header = recordsParameters.HeaderContent;
        if (header is not null && !header.Contains(jsonSerializationUsing, StringComparison.Ordinal))
        {
            const string dataAnnotationsUsing = "using System.ComponentModel.DataAnnotations;";
            header = header.Contains(dataAnnotationsUsing, StringComparison.Ordinal)
                ? header.Replace(dataAnnotationsUsing, dataAnnotationsUsing + "\n" + jsonSerializationUsing)
                : header + jsonSerializationUsing + "\n";
        }

        return recordsParameters with { HeaderContent = header, Parameters = records };
    }

    /// <summary>
    /// The record with <c>[JsonRequired]</c> next to every <c>[Required]</c> (<c>strictRequiredProperties</c>).
    /// </summary>
    private static RecordParameters WithJsonRequired(RecordParameters record)
        => record with
        {
            Parameters = record.Parameters?
                .Select(param => param.Attributes?.Any(a => a.Name == "Required") == true
                    ? param with { Attributes = [.. param.Attributes, new AttributeParameters("JsonRequired", null)] }
                    : param)
                .ToList(),
        };

    private static RecordsParameters ConvertToClientTypes(
        RecordsParameters recordsParameters)
    {
        var convertedRecords = new List<RecordParameters>();

        foreach (var record in recordsParameters.Parameters)
        {
            var convertedParams = new List<ParameterBaseParameters>();

            foreach (var param in record.Parameters ?? [])
            {
                var typeName = param.TypeName;

                // Convert IFormFile to IFileContent, IFormFileCollection/List<IFormFile>/IFormFile[] to List<IFileContent>
                if (typeName == "IFormFile")
                {
                    typeName = "IFileContent";
                }
                else if (typeName == "IFormFileCollection" || typeName == "List<IFormFile>" || typeName == "IFormFile[]")
                {
                    typeName = "List<IFileContent>";
                }

                convertedParams.Add(new ParameterBaseParameters(
                    Attributes: param.Attributes,
                    GenericTypeName: param.GenericTypeName,
                    IsGenericListType: param.IsGenericListType,
                    TypeName: typeName,
                    IsNullableType: param.IsNullableType,
                    IsReferenceType: param.IsReferenceType,
                    Name: param.Name,
                    DefaultValue: param.DefaultValue));
            }

            // Constructing a fresh RecordParameters here dropped every field not restated, which
            // silently lost all inheritance on the client side. Rewriting only the parameter list
            // keeps it, and keeps any field added later - the allOf base included, so a client model
            // derives from its base exactly as the server's does.
            //
            // The exception is a base named like a built-in error contract (ProblemDetails): the client
            // supplies that type itself and it is not the spec's model, so such a record stays flat and
            // carries every property - and its attributes - itself.
            var extendsBuiltInErrorContract = record.BaseTypeName is { Length: > 0 } baseTypeName &&
                                              BuiltInErrorContractNames.IsReserved(baseTypeName);

            convertedRecords.Add(extendsBuiltInErrorContract
                ? record with { Parameters = convertedParams, BaseTypeName = null, BaseConstructorArguments = null }
                : record with { Parameters = convertedParams });
        }

        // Update header content to use Atc.Rest.Client instead of Microsoft.AspNetCore.Http
        var headerContent = recordsParameters.HeaderContent ?? string.Empty;
        if (headerContent.Contains("using Microsoft.AspNetCore.Http;", StringComparison.Ordinal))
        {
            headerContent = headerContent.Replace("using Microsoft.AspNetCore.Http;", "using Atc.Rest.Client;");
        }

        return new RecordsParameters(
            HeaderContent: headerContent,
            Namespace: recordsParameters.Namespace,
            DocumentationTags: recordsParameters.DocumentationTags,
            Attributes: recordsParameters.Attributes,
            DeclarationModifier: recordsParameters.DeclarationModifier,
            Parameters: convertedRecords);
    }

    /// <summary>
    /// Generates a single typed client covering every operation in the document, with all models
    /// flattened into "{projectName}.Generated.Models".
    /// </summary>
    /// <remarks>
    /// Reuses the per-area code paths with an empty segment, which is the established way of saying
    /// "no segment qualifier" (see <c>ResolveEffectivePathSegment</c>). Because every model lands in
    /// one namespace, schema keys that normalise to the same C# identifier would collide, so
    /// <see cref="SingleClientCollisionValidator"/> gates generation first.
    /// </remarks>
    private static void GenerateSingleTypedClient(
        SourceProductionContext context,
        GeneratedSourceContext generatedContext,
        OpenApiDocument openApiDoc,
        string projectName,
        ClientConfig config,
        ISet<string> conflicts,
        SystemTypeConflictResolver systemTypeResolver,
        string yamlPath,
        bool generateDiExtension,
        ISet<string> xmlModels)
    {
        // Flattening removes the per-area namespaces that would otherwise keep colliding type names
        // apart. Report and stop, rather than emitting code that fails with an opaque CS0101.
        var collisions = SingleClientCollisionValidator.Validate(
            openApiDoc,
            config.ClientGranularity,
            yamlPath);

        if (collisions.Count > 0)
        {
            foreach (var collision in collisions)
            {
                context.ReportDiagnostic(DiagnosticHelpers.ToRoslynDiagnostic(collision));
            }

            return;
        }

        // Empty segment => everything is emitted directly under "{projectName}.Generated[.Models]".
        const string NoSegment = "";

        var registry = TypeConflictRegistry.ForSegment(conflicts, projectName, NoSegment, extensibleEnums: config.ExtensibleEnums);

        var allSchemas = openApiDoc.Components?.Schemas?.Keys is { } keys
            ? new HashSet<string>(keys, StringComparer.Ordinal)
            : [];

        if (allSchemas.Count > 0)
        {
            GenerateModelsForSchemas(generatedContext, openApiDoc, projectName, allSchemas, NoSegment, registry, config.IncludeDeprecated, config.GeneratePartialModels, strictRequiredProperties: config.StrictRequiredProperties, xmlModels: xmlModels, extensibleEnums: config.ExtensibleEnums);
            GenerateEnumsForSchemas(generatedContext, openApiDoc, projectName, allSchemas, NoSegment, config.ExtensibleEnums);
            GenerateTuplesForSchemas(generatedContext, openApiDoc, projectName, allSchemas, NoSegment);
            PolymorphicTypeEmitter.Emit(generatedContext, openApiDoc, projectName, allSchemas, NoSegment);
        }

        GenerateTypedClient(
            generatedContext,
            openApiDoc,
            projectName,
            pathSegment: NoSegment,
            namespaceSegment: NoSegment,
            registry,
            systemTypeResolver,
            config.IncludeDeprecated,
            hasSegmentModels: allSchemas.Count > 0,
            hasSharedModels: false,
            config.UseServersBasePath,
            config.ValidateSpecificationStrategy,
            config.ClientSuffix,
            config.ClientName,
            generateDiExtension,
            config.TypedClientResultStyle,
            config.StrictRequiredProperties,
            config.PreferXml,
            config.ExtensibleEnums);

        if (StreamReadersExtractor.DocumentRequiresStreamReaders(openApiDoc))
        {
            var streamReadersContent = StreamReadersExtractor.GenerateContent(projectName, perOperation: false);
            generatedContext.AddGeneratedSource(
                $"{projectName}.Streaming.StreamReaders.g.cs",
                SourceText.From(streamReadersContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    private static void GenerateTypedClient(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        string? namespaceSegment,
        TypeConflictRegistry registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated,
        bool hasSegmentModels,
        bool hasSharedModels,
        bool useServersBasePath,
        ValidateSpecificationStrategy validateStrategy = ValidateSpecificationStrategy.Strict,
        string? clientSuffix = null,
        string? clientName = null,
        bool generateDiExtension = false,
        TypedClientResultStyleType typedClientResultStyle = TypedClientResultStyleType.Throw,
        bool strictRequiredProperties = false,
        bool preferXml = false,
        bool extensibleEnums = false)
    {
        // An empty namespace segment means Single granularity: the client and its parameter
        // records are flattened into "{projectName}.Generated" rather than the per-area
        // "{projectName}.Generated.Client". Models keep their own ".Models" suffix either way.
        var clientNamespace = string.IsNullOrEmpty(namespaceSegment)
            ? NamespaceBuilder.BuildBase(projectName)
            : NamespaceBuilder.ForClient(projectName, namespaceSegment);
        var modelsNamespace = NamespaceBuilder.ForModels(projectName, namespaceSegment);

        GenerateClientParameters(context, openApiDoc, projectName, pathSegment, namespaceSegment, clientNamespace, registry, includeDeprecated, validateStrategy, hasSegmentModels, hasSharedModels, extensibleEnums);

        // Use HttpClientExtractor to extract HTTP client class parameters filtered by path segment
        // This also extracts inline schemas for type generation
        var (classParameters, inlineSchemas) = HttpClientExtractor.ExtractWithInlineSchemas(openApiDoc, projectName, pathSegment, registry, systemTypeResolver, includeDeprecated, useServersBasePath, hasSegmentModels, hasSharedModels, namespaceSegment, clientSuffix, clientName, typedClientResultStyle, preferXml);

        // A segment whose only models are the inline (anonymous) ones this client generates still needs the
        // segment's Models namespace: extract again with it imported.
        if (!hasSegmentModels && inlineSchemas.Count > 0)
        {
            (classParameters, inlineSchemas) = HttpClientExtractor.ExtractWithInlineSchemas(openApiDoc, projectName, pathSegment, registry, systemTypeResolver, includeDeprecated, useServersBasePath, hasSegmentModels: true, hasSharedModels, namespaceSegment, clientSuffix, clientName, typedClientResultStyle, preferXml);
        }

        if (classParameters is null)
        {
            return;
        }

        // Use GenerateContentForClass to generate the code
        var codeDocGenerator2 = new CodeDocumentationTagsGenerator();
        var contentGenerator = new GenerateContentForClass(
            codeDocGenerator2,
            classParameters);

        var generatedContent = contentGenerator.Generate();

        // An explicit 'clientName' is the author stating the full type name, so it is used verbatim
        // with no suffix appended.
        var clientTypeName = !string.IsNullOrWhiteSpace(clientName)
            ? clientName!.Trim()
            : string.IsNullOrEmpty(namespaceSegment)
                ? CasingHelper.BuildClientTypeName(projectName, clientSuffix)
                : CasingHelper.BuildClientTypeName(namespaceSegment, clientSuffix);

        context.AddGeneratedSource(
            NamespaceBuilder.ToFileName(clientNamespace, clientTypeName),
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));

        // Emit the matching I{ClientName} contract next to the client so consumers can depend on -
        // and mock - the API surface instead of stubbing HttpClient
        var interfaceParameters = HttpClientInterfaceExtractor.Extract(classParameters);
        if (interfaceParameters is not null)
        {
            var interfaceContent = new GenerateContentForInterface(
                codeDocGenerator2,
                interfaceParameters).Generate();

            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(clientNamespace, interfaceParameters.InterfaceTypeName),
                SourceText.From(interfaceContent.NormalizeForSourceOutput(), Encoding.UTF8));

            // The DI extension calls AddHttpClient<TInterface, TClient>(), which lives in
            // Microsoft.Extensions.Http. Typed-client projects are not required to reference it,
            // so only emit the extension when the consumer actually has it.
            if (generateDiExtension)
            {
                var diContent = BuildTypedClientDiExtension(
                    projectName,
                    clientNamespace,
                    clientTypeName,
                    interfaceParameters.InterfaceTypeName);

                context.AddGeneratedSource(
                    NamespaceBuilder.ToFileName(clientNamespace, $"{clientTypeName}ServiceCollectionExtensions"),
                    SourceText.From(diContent.NormalizeForSourceOutput(), Encoding.UTF8));
            }
        }

        // Generate inline model files for any inline schemas discovered - under Single granularity (an empty namespace
        // segment) flat in "{projectName}.Generated.Models", where the client looks for them
        if (inlineSchemas.Count > 0)
        {
            var inlineTypes = CodeGenerationService.GenerateInlineModels(
                strictRequiredProperties
                    ? inlineSchemas.ToDictionary(kvp => kvp.Key, kvp => kvp.Value with { RecordParameters = WithJsonRequired(kvp.Value.RecordParameters) }, StringComparer.Ordinal)
                    : inlineSchemas,
                projectName,
                granularity: string.IsNullOrEmpty(namespaceSegment) ? ClientGranularityType.Single : ClientGranularityType.PerArea);
            foreach (var inlineType in inlineTypes)
            {
                var inlineContent = CodeGenerationService.FormatAsFile(inlineType);
                context.AddGeneratedSource(
                    NamespaceBuilder.ToFileName(modelsNamespace, inlineType.TypeName),
                    SourceText.From(inlineContent.NormalizeForSourceOutput(), Encoding.UTF8));
            }
        }
    }

    /// <summary>
    /// Generates <c>ApiClientJsonOptions</c>: the serializer options of the typed clients registered with their
    /// generated DI extension - the clients' own defaults to start from, adjusted through the options pattern in any
    /// order, as ASP.NET Core's <c>JsonOptions</c> on the server.
    /// </summary>
    private static void GenerateApiClientJsonOptions(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {NamespaceBuilder.BuildBase(projectName)};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// The serializer options of the typed clients registered with their generated <c>Add...Client</c> extension. They");
        sb.AppendLine("/// start from the clients' defaults; adjust them with <c>services.Configure&lt;ApiClientJsonOptions&gt;(...)</c>");
        sb.AppendLine("/// (before or after the registration) or with the extension's <c>configureJsonSerializerOptions</c> callback.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine("public sealed class ApiClientJsonOptions");
        sb.AppendLine("{");
        sb.AppendLine(4, "public JsonSerializerOptions SerializerOptions { get; } = new JsonSerializerOptions");
        sb.AppendLine(4, "{");
        foreach (var member in HttpClientExtractor.DefaultJsonSerializerOptionsInitializers(openApiDoc, projectName))
        {
            sb.AppendLine(8, member);
        }

        sb.AppendLine(4, "};");
        sb.Append('}');

        context.AddGeneratedSource(
            $"{projectName}.ApiClientJsonOptions.g.cs",
            SourceText.From(sb.ToString().NormalizeForSourceOutput(), Encoding.UTF8));
    }

    /// <summary>
    /// Builds the DI registration extension for a typed client, registering the generated
    /// interface as the service type so consumers can depend on (and fake) the contract.
    /// </summary>
    private static string BuildTypedClientDiExtension(
        string projectName,
        string clientNamespace,
        string clientTypeName,
        string interfaceTypeName)
    {
        var sb = new StringBuilder();
        var jsonOptionsType = $"global::{NamespaceBuilder.BuildBase(projectName)}.ApiClientJsonOptions";

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using System.Net.Http;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.Options;");
        sb.AppendLine();
        sb.AppendLine($"namespace {clientNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Registration extensions for <see cref=\"{interfaceTypeName}\"/>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"public static class {clientTypeName}ServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Adds <see cref=\"{clientTypeName}\"/> as <see cref=\"{interfaceTypeName}\"/> using IHttpClientFactory.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"services\">The service collection.</param>");
        sb.AppendLine("    /// <param name=\"configureClient\">Optional callback to configure the underlying <see cref=\"HttpClient\"/>.</param>");
        sb.AppendLine($"    /// <param name=\"configureJsonSerializerOptions\">Optional callback to adjust the serializer options (<see cref=\"{jsonOptionsType}\"/>), which start from the client's defaults.</param>");
        sb.AppendLine($"    public static IHttpClientBuilder Add{clientTypeName}(");
        sb.AppendLine("        this IServiceCollection services,");
        sb.AppendLine("        Action<HttpClient>? configureClient = null,");
        sb.AppendLine("        Action<JsonSerializerOptions>? configureJsonSerializerOptions = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (services is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            throw new ArgumentNullException(nameof(services));");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (configureJsonSerializerOptions is not null)");
        sb.AppendLine("        {");
        sb.AppendLine($"            services.Configure<{jsonOptionsType}>(o => configureJsonSerializerOptions(o.SerializerOptions));");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var builder = configureClient is null");
        sb.AppendLine($"            ? services.AddHttpClient<{interfaceTypeName}>()");
        sb.AppendLine($"            : services.AddHttpClient<{interfaceTypeName}>(configureClient);");
        sb.AppendLine();
        sb.AppendLine("        // An explicit factory: the client has a (HttpClient) and a (HttpClient, JsonSerializerOptions)");
        sb.AppendLine("        // constructor, which ActivatorUtilities cannot choose between, and the options come from DI.");
        sb.AppendLine($"        return builder.AddTypedClient<{interfaceTypeName}>(");
        sb.AppendLine($"            static (httpClient, serviceProvider) => new {clientTypeName}(");
        sb.AppendLine("                httpClient,");
        sb.AppendLine($"                serviceProvider.GetRequiredService<IOptions<{jsonOptionsType}>>().Value.SerializerOptions));");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static bool GenerateEndpointPerOperation(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        string? namespaceSegment,
        TypeConflictRegistry registry,
        bool includeDeprecated,
        bool hasSegmentModels,
        bool hasSharedModels,
        bool useServersBasePath,
        string? httpClientName,
        ErrorResponseFormatType errorResponseFormat,
        string? customErrorTypeName,
        ValidateSpecificationStrategy validateStrategy = ValidateSpecificationStrategy.Strict,
        bool strictRequiredProperties = false,
        bool preferXml = false,
        bool extensibleEnums = false)
    {
        var clientNamespace = NamespaceBuilder.ForClient(projectName, namespaceSegment);
        var modelsNamespace = NamespaceBuilder.ForModels(projectName, namespaceSegment);

        GenerateClientParameters(context, openApiDoc, projectName, pathSegment, namespaceSegment, clientNamespace, registry, includeDeprecated, validateStrategy, hasSegmentModels, hasSharedModels, extensibleEnums);

        // Generate endpoint files using EndpointPerOperationExtractor (with inline schema support)
        var (operationFiles, inlineSchemas) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            openApiDoc,
            projectName,
            pathSegment,
            registry,
            includeDeprecated,
            errorResponseFormat: errorResponseFormat,
            customErrorTypeName: customErrorTypeName,
            customHttpClientName: httpClientName,
            hasSegmentModels: hasSegmentModels,
            hasSharedModels: hasSharedModels,
            useServersBasePath: useServersBasePath,
            namespaceSegment: namespaceSegment,
            preferXml: preferXml);

        // Generate inline model types if any were discovered
        if (inlineSchemas.Count > 0)
        {
            var inlineTypes = CodeGenerationService.GenerateInlineModels(
                strictRequiredProperties
                    ? inlineSchemas.ToDictionary(kvp => kvp.Key, kvp => kvp.Value with { RecordParameters = WithJsonRequired(kvp.Value.RecordParameters) }, StringComparer.Ordinal)
                    : inlineSchemas,
                projectName);
            foreach (var inlineType in inlineTypes)
            {
                var inlineContent = CodeGenerationService.FormatAsFile(inlineType);
                context.AddGeneratedSource(
                    NamespaceBuilder.ToFileName(modelsNamespace, inlineType.TypeName),
                    SourceText.From(inlineContent.NormalizeForSourceOutput(), Encoding.UTF8));
            }
        }

        var endpointNames = new List<string>();

        foreach (var opFiles in operationFiles)
        {
            endpointNames.Add(opFiles.OperationName);

            // Endpoint Interface
            context.AddGeneratedSource(
                opFiles.EndpointInterfaceFileName,
                SourceText.From(opFiles.EndpointInterfaceContent.NormalizeForSourceOutput(), Encoding.UTF8));

            // Endpoint Class
            context.AddGeneratedSource(
                opFiles.EndpointClassFileName,
                SourceText.From(opFiles.EndpointClassContent.NormalizeForSourceOutput(), Encoding.UTF8));

            // Result Interface (null for binary endpoints that use BinaryEndpointResponse)
            if (opFiles is { ResultInterfaceFileName: not null, ResultInterfaceContent: not null })
            {
                context.AddGeneratedSource(
                    opFiles.ResultInterfaceFileName,
                    SourceText.From(opFiles.ResultInterfaceContent.NormalizeForSourceOutput(), Encoding.UTF8));
            }

            // Result Class (null for binary endpoints that use BinaryEndpointResponse)
            if (opFiles is { ResultClassFileName: not null, ResultClassContent: not null })
            {
                context.AddGeneratedSource(
                    opFiles.ResultClassFileName,
                    SourceText.From(opFiles.ResultClassContent.NormalizeForSourceOutput(), Encoding.UTF8));
            }
        }

        // Generate DI extension method
        if (endpointNames.Count > 0)
        {
            var diExtensionContent = GenerateEndpointDiExtension(projectName, namespaceSegment, endpointNames, EnumWireConverterExtractor.HasEnums(openApiDoc));
            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(NamespaceBuilder.ForEndpoints(projectName, namespaceSegment), "DependencyInjection"),
                SourceText.From(diExtensionContent.NormalizeForSourceOutput(), Encoding.UTF8));
            return true;
        }

        return false;
    }

    private static string GenerateEndpointDiExtension(
        string projectName,
        string? pathSegment,
        List<string> endpointNames,
        bool hasEnums)
    {
        // When the path segment resolves away, the registration lives directly under
        // "{projectName}.Generated" and is named after the project instead of the segment.
        var hasSegment = !string.IsNullOrEmpty(pathSegment);
        var registrationName = hasSegment
            ? pathSegment!
            : CodeGenerationService.SanitizeProjectNameForIdentifier(projectName);
        var displayName = hasSegment ? pathSegment! : projectName;
        var containerNamespace = hasSegment
            ? $"{NamespaceBuilder.BuildBase(projectName)}.{pathSegment}"
            : NamespaceBuilder.BuildBase(projectName);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using Atc.Rest.Client.Options;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine($"using {NamespaceBuilder.ForEndpoints(projectName, pathSegment)};");
        sb.AppendLine($"using {NamespaceBuilder.ForEndpointInterfaces(projectName, pathSegment)};");
        sb.AppendLine();
        sb.AppendLine($"namespace {containerNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Extension methods for registering {displayName} API endpoints in the dependency injection container.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine("public static class EndpointsServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, $"/// Registers all {displayName} API endpoint implementations.");
        sb.AppendLine(4, "/// Also ensures IHttpMessageFactory and IContractSerializer are registered (from Atc.Rest.Client).");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "/// <param name=\"services\">The service collection.</param>");
        sb.AppendLine(4, "/// <returns>The service collection for method chaining.</returns>");
        sb.AppendLine(4, $"public static IServiceCollection Add{registrationName}Endpoints(this IServiceCollection services)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "// Ensure Atc.Rest.Client core services are registered (IHttpMessageFactory, IContractSerializer)");
        if (hasEnums)
        {
            // Atc.Rest.Client's default serializer options, with every generated enum as the specification writes it
            // ahead of its JsonStringEnumConverter (which would write an integer enum's member name)
            sb.AppendLine(8, "// - its default serializer, with every generated enum on the wire as the specification writes it");
            sb.AppendLine(8, "services.AddAtcRestClientCore(new global::Atc.Rest.Client.Serialization.DefaultJsonContractSerializer(new global::System.Text.Json.JsonSerializerOptions");
            sb.AppendLine(8, "{");
            sb.AppendLine(12, "PropertyNamingPolicy = global::System.Text.Json.JsonNamingPolicy.CamelCase,");
            sb.AppendLine(12, "DefaultIgnoreCondition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,");
            sb.AppendLine(12, "WriteIndented = true,");
            sb.AppendLine(12, $"Converters = {{ new global::{NamespaceBuilder.BuildBase(projectName)}.{EnumWireConverterExtractor.ClassName}(), new global::System.Text.Json.Serialization.JsonStringEnumConverter() }},");
            sb.AppendLine(8, "}));");
        }
        else
        {
            sb.AppendLine(8, "services.AddAtcRestClientCore();");
        }

        sb.AppendLine();

        foreach (var endpointName in endpointNames)
        {
            sb.AppendLine(8, $"services.AddTransient<I{endpointName}Endpoint, {endpointName}Endpoint>();");
        }

        sb.AppendLine();
        sb.AppendLine(8, "return services;");
        sb.AppendLine(4, "}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateConsolidatedDiExtension(
        string projectName,
        List<string> pathSegments)
    {
        // Sanitize project name for use in C# identifiers (remove dots)
        var sanitizedProjectName = CodeGenerationService.SanitizeProjectNameForIdentifier(projectName);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");

        // Add using statements for each path segment namespace
        foreach (var pathSegment in pathSegments)
        {
            sb.AppendLine($"using {NamespaceBuilder.BuildBase(projectName)}.{pathSegment};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Extension methods for registering all {projectName} API endpoints in the dependency injection container.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"public static class {sanitizedProjectName}EndpointsServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, $"/// Registers all {projectName} API endpoint implementations from all path segments.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "/// <param name=\"services\">The service collection.</param>");
        sb.AppendLine(4, "/// <returns>The service collection for method chaining.</returns>");
        sb.AppendLine(4, $"public static IServiceCollection Add{sanitizedProjectName}Endpoints(this IServiceCollection services)");
        sb.AppendLine(4, "{");

        // Call each path segment's extension method
        foreach (var pathSegment in pathSegments)
        {
            sb.AppendLine(8, $"services.Add{pathSegment}Endpoints();");
        }

        sb.AppendLine();
        sb.AppendLine(8, "return services;");
        sb.AppendLine(4, "}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Reports <see cref="RuleIdentifiers.ProblemDetailsSchemaShadowedByBuiltIn"/> for every
    /// specification schema that collides with a built-in error contract, and removes it from
    /// <paramref name="sharedSchemas"/> so it is never materialized.
    /// </summary>
    private static void ReportAndRemoveShadowedErrorContracts(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        HashSet<string> sharedSchemas,
        string yamlPath)
    {
        var schemaNames = openApiDoc.Components?.Schemas?.Keys;
        if (schemaNames is null)
        {
            return;
        }

        foreach (var schemaName in schemaNames.Where(BuiltInErrorContractNames.IsReserved))
        {
            context.ReportDiagnostic(
                DiagnosticHelpers.ToRoslynDiagnostic(
                    GeneratorDiagnosticMessage.Warning(
                        RuleIdentifiers.ProblemDetailsSchemaShadowedByBuiltIn,
                        $"The schema '{schemaName}' is ignored because 'generationMode' is " +
                        $"'EndpointPerOperation', which generates its own '{schemaName}' type used by the " +
                        "endpoint results and ProblemDetailsFactory. Remove the schema from the " +
                        "specification, or switch to a TypedClient generation mode to use your own definition.",
                        yamlPath)));

            sharedSchemas.Remove(schemaName);
        }
    }

    /// <summary>
    /// Whether any operation downloads a file into memory: in EndpointPerOperation mode not the streamed
    /// <c>x-return-stream</c> opt-in, which the TypedClient reads into memory like any other download.
    /// </summary>
    private static bool HasBufferedFileDownload(
        OpenApiDocument openApiDoc,
        bool streamedDownloadsAreBuffered = false)
        => openApiDoc.Paths.Values
            .SelectMany(path => path.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            .Any(operation => (operation.HasFileDownload() && (streamedDownloadsAreBuffered || !operation.IsStreamedDownloadOperation())) || operation.GetModelOrFileMediaTypes().Count > 0);

    /// <summary>
    /// Whether any operation answers a model or a file (a 200 that declares both).
    /// </summary>
    private static bool HasModelOrFileResponse(OpenApiDocument openApiDoc)
        => openApiDoc.Paths.Values
            .SelectMany(path => path.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            .Any(operation => operation.GetModelOrFileMediaTypes().Count > 0);

    /// <summary>
    /// Generates <c>ContentOrFile&lt;T&gt;</c>: the answer of a TypedClient operation that is a model or a file - the
    /// model (JSON or XML), or the file, told apart by the response's Content-Type.
    /// </summary>
    private static void GenerateContentOrFile(
        GeneratedSourceContext context,
        string projectName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// The answer of an operation that is a model or a file, told apart by the response's Content-Type.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <typeparam name=\"T\">The model.</typeparam>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine("public sealed class ContentOrFile<T>");
        sb.AppendLine("{");
        sb.AppendLine(4, "private ContentOrFile(");
        sb.AppendLine(8, "T? content,");
        sb.AppendLine(8, "DownloadedFile? file)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "Content = content;");
        sb.AppendLine(8, "File = file;");
        sb.AppendLine(4, "}");
        sb.AppendLine();
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// The model, or the default when the answer is a file.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public T? Content { get; }");
        sb.AppendLine();
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// The file, or <see langword=\"null\"/> when the answer is the model.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public DownloadedFile? File { get; }");
        sb.AppendLine();
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Whether the answer is the file.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public bool IsFile => File is not null;");
        sb.AppendLine();
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// The answer as the model.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public static ContentOrFile<T> FromContent(T content)");
        sb.AppendLine(8, "=> new(content, null);");
        sb.AppendLine();
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// The answer as the file.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public static ContentOrFile<T> FromFile(DownloadedFile file)");
        sb.AppendLine(8, "=> new(default, file);");
        sb.AppendLine("}");

        context.AddGeneratedSource(
            $"{projectName}.ContentOrFile.g.cs",
            SourceText.From(sb.ToString().NormalizeForSourceOutput(), Encoding.UTF8));
    }

    /// <summary>
    /// Generates <c>DownloadedFile</c>: a downloaded file - its bytes, content type, file name (RFC 6266 / 5987
    /// <c>filename*</c> first) and length - read from a successful response.
    /// </summary>
    private static void GenerateDownloadedFile(
        GeneratedSourceContext context,
        string projectName,
        bool includeEndpointResponse)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using System.Net.Http.Headers;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// A downloaded file.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <param name=\"Content\">The file's bytes.</param>");
        sb.AppendLine("/// <param name=\"ContentType\">The media type (<c>Content-Type</c>), e.g. <c>application/pdf</c>.</param>");
        sb.AppendLine("/// <param name=\"FileName\">The file name (<c>Content-Disposition</c>, <c>filename*</c> first), or <see langword=\"null\"/> for an inline file.</param>");
        sb.AppendLine("/// <param name=\"ContentLength\">The length in bytes.</param>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine("public sealed record DownloadedFile(");
        sb.AppendLine(4, "byte[] Content,");
        sb.AppendLine(4, "string? ContentType,");
        sb.AppendLine(4, "string? FileName,");
        sb.AppendLine(4, "long ContentLength)");
        sb.AppendLine("{");
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Reads the file from a successful HTTP response: its bytes and the Content-Type and Content-Disposition headers.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public static async System.Threading.Tasks.Task<DownloadedFile> FromHttpResponseAsync(");
        sb.AppendLine(8, "System.Net.Http.HttpResponseMessage response,");
        sb.AppendLine(8, "System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine(8, "return FromHeaders(content, response.Content.Headers);");
        sb.AppendLine(4, "}");
        sb.AppendLine();
        if (includeEndpointResponse)
        {
            sb.AppendLine(4, "/// <summary>");
            sb.AppendLine(4, "/// Reads the file from a successful response: the body Atc.Rest.Client kept as bytes (or as text for a text media");
            sb.AppendLine(4, "/// type), and the Content-Type and Content-Disposition headers.");
            sb.AppendLine(4, "/// </summary>");
            sb.AppendLine(4, "public static DownloadedFile FromResponse(global::Atc.Rest.Client.EndpointResponse response)");
            sb.AppendLine(8, "=> FromHeaders(response.ContentObject as byte[] ?? System.Text.Encoding.UTF8.GetBytes(response.Content ?? string.Empty), response.Headers);");
            sb.AppendLine();
            sb.AppendLine(4, "/// <summary>");
            sb.AppendLine(4, "/// Whether a response's body is a file: its Content-Type is neither JSON nor XML (a model's).");
            sb.AppendLine(4, "/// </summary>");
            sb.AppendLine(4, "public static bool IsFileResponse(global::Atc.Rest.Client.EndpointResponse response)");
            sb.AppendLine(8, "=> IsFileMediaType(FromHeaders(System.Array.Empty<byte>(), response.Headers).ContentType);");
            sb.AppendLine();
        }

        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Whether a body of this media type is a file rather than a model: neither JSON nor XML.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public static bool IsFileMediaType(string? mediaType)");
        sb.AppendLine(8, "=> !string.IsNullOrEmpty(mediaType) &&");
        sb.AppendLine(8, "   !string.Equals(mediaType, \"application/json\", System.StringComparison.OrdinalIgnoreCase) &&");
        sb.AppendLine(8, "   !mediaType!.EndsWith(\"+json\", System.StringComparison.OrdinalIgnoreCase) &&");
        sb.AppendLine(8, "   !string.Equals(mediaType, \"application/xml\", System.StringComparison.OrdinalIgnoreCase) &&");
        sb.AppendLine(8, "   !string.Equals(mediaType, \"text/xml\", System.StringComparison.OrdinalIgnoreCase) &&");
        sb.AppendLine(8, "   !mediaType.EndsWith(\"+xml\", System.StringComparison.OrdinalIgnoreCase);");
        sb.AppendLine();
        sb.AppendLine(4, "private static DownloadedFile FromHeaders(");
        sb.AppendLine(8, "byte[] content,");
        sb.AppendLine(8, "System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>>> headers)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "string? contentType = null;");
        sb.AppendLine(8, "string? fileName = null;");
        sb.AppendLine(8, "foreach (var header in headers)");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "var value = string.Join(\",\", header.Value);");
        sb.AppendLine(12, "if (string.Equals(header.Key, \"Content-Type\", System.StringComparison.OrdinalIgnoreCase) &&");
        sb.AppendLine(12, "    MediaTypeHeaderValue.TryParse(value, out var mediaType))");
        sb.AppendLine(12, "{");
        sb.AppendLine(16, "contentType = mediaType.MediaType;");
        sb.AppendLine(12, "}");
        sb.AppendLine(12, "else if (string.Equals(header.Key, \"Content-Disposition\", System.StringComparison.OrdinalIgnoreCase) &&");
        sb.AppendLine(12, "         ContentDispositionHeaderValue.TryParse(value, out var disposition))");
        sb.AppendLine(12, "{");
        sb.AppendLine(16, "fileName = SafeFileName((disposition.FileNameStar ?? disposition.FileName)?.Trim('\"'));");
        sb.AppendLine(12, "}");
        sb.AppendLine(8, "}");
        sb.AppendLine();
        sb.AppendLine(8, "return new DownloadedFile(content, contentType, fileName, content.LongLength);");
        sb.AppendLine(4, "}");
        sb.AppendLine();
        sb.AppendLine(4, "// The server's name, as a file name only: the last segment of whatever path it sent (either separator, on any");
        sb.AppendLine(4, "// OS) - never \"..\\..\\x\" or \"/etc/x\" a consumer would write outside its folder with");
        sb.AppendLine(4, "private static string? SafeFileName(string? name)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "if (string.IsNullOrWhiteSpace(name))");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "return null;");
        sb.AppendLine(8, "}");
        sb.AppendLine();
        sb.AppendLine(8, "var lastSegment = name!.Substring(name.LastIndexOfAny(['/', '\\\\']) + 1).Trim();");
        sb.AppendLine(8, "return lastSegment is \"\" or \".\" or \"..\" ? null : lastSegment;");
        sb.AppendLine(4, "}");
        sb.AppendLine("}");

        context.AddGeneratedSource(
            $"{projectName}.DownloadedFile.g.cs",
            SourceText.From(sb.ToString().NormalizeForSourceOutput(), Encoding.UTF8));
    }

    /// <summary>
    /// <c>WithXmlProblemBody</c>: an <c>application/problem+xml</c> error body (RFC 9457 Appendix B) read into the
    /// typed ProblemDetails (a ValidationProblemDetails when it has <c>errors</c>), so the typed accessors read it like
    /// a problem+json body.
    /// </summary>
    private static void AppendWithXmlProblemBody(StringBuilder sb)
    {
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// An <c>application/problem+xml</c> error body (RFC 9457 Appendix B) read into the typed ProblemDetails - a");
        sb.AppendLine(4, "/// ValidationProblemDetails when it has <c>errors</c> - so the typed accessors read it like a problem+json body.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public static global::Atc.Rest.Client.EndpointResponse WithXmlProblemBody(global::Atc.Rest.Client.EndpointResponse response)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "var isProblemXml = response.Headers.Any(h =>");
        sb.AppendLine(12, "string.Equals(h.Key, \"Content-Type\", System.StringComparison.OrdinalIgnoreCase) &&");
        sb.AppendLine(12, "h.Value.Any(v => v.TrimStart().StartsWith(\"application/problem+xml\", System.StringComparison.OrdinalIgnoreCase)));");
        sb.AppendLine(8, "if (response.IsSuccess || !isProblemXml || string.IsNullOrWhiteSpace(response.Content))");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "return response;");
        sb.AppendLine(8, "}");
        sb.AppendLine();
        sb.AppendLine(8, "System.Xml.Linq.XElement root;");
        sb.AppendLine(8, "try");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "root = XmlModelSupport.Parse(response.Content);");
        sb.AppendLine(8, "}");
        sb.AppendLine(8, "catch (System.Xml.XmlException)");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "return response;");
        sb.AppendLine(8, "}");
        sb.AppendLine();
        sb.AppendLine(8, "string? Member(string name)");
        sb.AppendLine(12, "=> root.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;");
        sb.AppendLine(8, "static string Key(System.Xml.Linq.XElement element)");
        sb.AppendLine(12, "=> element.Name.LocalName == \"_\" ? string.Empty : System.Xml.XmlConvert.DecodeName(element.Name.LocalName)!;");
        sb.AppendLine();
        sb.AppendLine(8, "int? status = int.TryParse(Member(\"status\"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var code) ? code : null;");
        sb.AppendLine(8, "var members = new[] { \"type\", \"title\", \"status\", \"detail\", \"instance\", \"errors\" };");
        sb.AppendLine(8, "var extensions = root.Elements()");
        sb.AppendLine(12, ".Where(e => !members.Contains(e.Name.LocalName))");
        sb.AppendLine(12, ".ToDictionary(Key, e => System.Text.Json.JsonSerializer.SerializeToElement(e.HasElements ? e.ToString() : e.Value), System.StringComparer.Ordinal);");
        sb.AppendLine(8, "var errors = root.Elements().FirstOrDefault(e => e.Name.LocalName == \"errors\");");
        sb.AppendLine(8, "ProblemDetails problem = errors is null");
        sb.AppendLine(12, "? new ProblemDetails(Member(\"type\"), Member(\"title\"), status, Member(\"detail\"), Member(\"instance\"))");
        sb.AppendLine(12, ": new ValidationProblemDetails(");
        sb.AppendLine(16, "Member(\"type\"),");
        sb.AppendLine(16, "Member(\"title\"),");
        sb.AppendLine(16, "status,");
        sb.AppendLine(16, "Member(\"detail\"),");
        sb.AppendLine(16, "Member(\"instance\"),");
        sb.AppendLine(16, "errors.Elements().ToDictionary(Key, e => e.HasElements ? e.Elements().Select(i => i.Value).ToArray() : new[] { e.Value }, System.StringComparer.Ordinal));");
        sb.AppendLine(8, "problem = problem with { Extensions = extensions.Count > 0 ? extensions : null };");
        sb.AppendLine(8, "return new global::Atc.Rest.Client.EndpointResponse(response.IsSuccess, response.StatusCode, response.Content, problem, response.Headers);");
        sb.AppendLine(4, "}");
        sb.AppendLine();
    }

    private static void GenerateProblemDetails(
        GeneratedSourceContext context,
        string projectName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// A machine-readable format for specifying errors in HTTP API responses based on RFC 7807.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <param name=\"Type\">A URI reference that identifies the problem type.</param>");
        sb.AppendLine("/// <param name=\"Title\">A short, human-readable summary of the problem type.</param>");
        sb.AppendLine("/// <param name=\"Status\">The HTTP status code generated by the origin server for this occurrence of the problem.</param>");
        sb.AppendLine("/// <param name=\"Detail\">A human-readable explanation specific to this occurrence of the problem.</param>");
        sb.AppendLine("/// <param name=\"Instance\">A URI reference that identifies the specific occurrence of the problem.</param>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine("public record ProblemDetails(");
        sb.AppendLine(4, "string? Type,");
        sb.AppendLine(4, "string? Title,");
        sb.AppendLine(4, "int? Status,");
        sb.AppendLine(4, "string? Detail,");
        sb.AppendLine(4, "string? Instance)");
        sb.AppendLine("{");
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Gets the extension members for this problem details instance.");
        sb.AppendLine(4, "/// Extension members appear in the same namespace as other members of a problem type per RFC 7807.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "[JsonExtensionData]");
        sb.AppendLine(4, "public Dictionary<string, JsonElement>? Extensions { get; init; }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// A ProblemDetails response that includes validation errors.");
        sb.AppendLine("/// Used for 400 Bad Request responses with validation failures.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <param name=\"Type\">A URI reference that identifies the problem type.</param>");
        sb.AppendLine("/// <param name=\"Title\">A short, human-readable summary of the problem type.</param>");
        sb.AppendLine("/// <param name=\"Status\">The HTTP status code generated by the origin server for this occurrence of the problem.</param>");
        sb.AppendLine("/// <param name=\"Detail\">A human-readable explanation specific to this occurrence of the problem.</param>");
        sb.AppendLine("/// <param name=\"Instance\">A URI reference that identifies the specific occurrence of the problem.</param>");
        sb.AppendLine("/// <param name=\"Errors\">A dictionary of validation errors keyed by field name.</param>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine("public record ValidationProblemDetails(");
        sb.AppendLine(4, "string? Type,");
        sb.AppendLine(4, "string? Title,");
        sb.AppendLine(4, "int? Status,");
        sb.AppendLine(4, "string? Detail,");
        sb.AppendLine(4, "string? Instance,");
        sb.AppendLine(4, "Dictionary<string, string[]>? Errors)");
        sb.AppendLine(4, ": ProblemDetails(Type, Title, Status, Detail, Instance);");

        var normalizeForSourceOutput = sb
            .ToString()
            .NormalizeForSourceOutput();

        context.AddGeneratedSource(
            $"{projectName}.ProblemDetails.g.cs",
            SourceText.From(normalizeForSourceOutput, Encoding.UTF8));
    }

    /// <summary>
    /// Generates a ProblemDetailsFactory for creating ProblemDetails from plain text responses.
    /// Used as a fallback when servers return plain text instead of JSON for 401/403 responses.
    /// </summary>
    private static void GenerateProblemDetailsFactory(
        GeneratedSourceContext context,
        string projectName,
        bool hasXml = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using System.Net;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Factory for creating ProblemDetails from plain text responses.");
        sb.AppendLine("/// Used as a fallback when servers return plain text instead of JSON for error responses.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine("public static class ProblemDetailsFactory");
        sb.AppendLine("{");
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Creates a ProblemDetails instance from a status code and optional message.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public static ProblemDetails Create(HttpStatusCode statusCode, string? message = null)");
        sb.AppendLine(8, "=> new(");
        sb.AppendLine(12, "Type: GetTypeUri(statusCode),");
        sb.AppendLine(12, "Title: GetTitle(statusCode),");
        sb.AppendLine(12, "Status: (int)statusCode,");
        sb.AppendLine(12, "Detail: ToDetail(message),");
        sb.AppendLine(12, "Instance: null);");
        sb.AppendLine();
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Creates a ValidationProblemDetails instance (without errors) from a status code and optional message.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public static ValidationProblemDetails CreateValidation(HttpStatusCode statusCode, string? message = null)");
        sb.AppendLine(8, "=> new(");
        sb.AppendLine(12, "Type: GetTypeUri(statusCode),");
        sb.AppendLine(12, "Title: GetTitle(statusCode),");
        sb.AppendLine(12, "Status: (int)statusCode,");
        sb.AppendLine(12, "Detail: ToDetail(message),");
        sb.AppendLine(12, "Instance: null,");
        sb.AppendLine(12, "Errors: null);");
        sb.AppendLine();
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Reads a ProblemDetails from the raw response body - a body that was read as another type (e.g. the");
        sb.AppendLine(4, "/// operation's <c>default</c> model) - or creates one with the body as the detail.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public static ProblemDetails FromContent(HttpStatusCode statusCode, string? content)");
        sb.AppendLine(8, "=> TryRead<ProblemDetails>(content) is { } problem");
        sb.AppendLine(12, "? problem with { Status = problem.Status ?? (int)statusCode }");
        sb.AppendLine(12, ": Create(statusCode, content);");
        sb.AppendLine();
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Reads a ValidationProblemDetails from the raw response body - a body that was read as another type - or");
        sb.AppendLine(4, "/// creates one with the body as the detail.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public static ValidationProblemDetails ValidationFromContent(HttpStatusCode statusCode, string? content)");
        sb.AppendLine(8, "=> TryRead<ValidationProblemDetails>(content) is { } problem");
        sb.AppendLine(12, "? problem with { Status = problem.Status ?? (int)statusCode }");
        sb.AppendLine(12, ": CreateValidation(statusCode, content);");
        sb.AppendLine();
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Reads the field errors of a validation problem (its <c>errors</c>) from the raw response body - a body read");
        sb.AppendLine(4, "/// as a declared model that cannot hold them; <see langword=\"null\"/> when the body has none.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public static System.Collections.Generic.Dictionary<string, string[]>? ValidationErrorsFromContent(string? content)");
        sb.AppendLine(8, "=> TryRead<ValidationProblemDetails>(content)?.Errors;");
        sb.AppendLine();
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Reads an error model (the declared one, or the configured custom one) from the raw response body - a body");
        sb.AppendLine(4, "/// the client could not read as it (e.g. a number where the model has a string, or another shape) - accepting");
        sb.AppendLine(4, "/// any JSON value for a string property. A body that is no JSON object gives an empty model; the raw body stays");
        sb.AppendLine(4, "/// in <c>Content</c>.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public static T ModelFromContent<T>(string? content)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "if (TryReadAny<T>(content, out var model) || TryReadAny<T>(\"{}\", out model))");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "return model!;");
        sb.AppendLine(8, "}");
        sb.AppendLine();
        sb.AppendLine(8, "throw new System.InvalidOperationException($\"The error body cannot be read as {typeof(T).Name}; it is in Content.\");");
        sb.AppendLine(4, "}");
        sb.AppendLine();
        sb.AppendLine(4, "private static bool TryReadAny<T>(string? content, out T? value)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "value = default;");
        sb.AppendLine(8, "if (string.IsNullOrWhiteSpace(content))");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "return false;");
        sb.AppendLine(8, "}");
        sb.AppendLine();
        sb.AppendLine(8, "try");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "value = System.Text.Json.JsonSerializer.Deserialize<T>(content!, LenientOptions);");
        sb.AppendLine(12, "return value is not null;");
        sb.AppendLine(8, "}");
        sb.AppendLine(8, "catch (System.Text.Json.JsonException)");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "return false;");
        sb.AppendLine(8, "}");
        sb.AppendLine(4, "}");
        sb.AppendLine();
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// An error response whose body was kept as bytes - a media type that is neither JSON nor text, such as");
        sb.AppendLine(4, "/// <c>application/xml</c> or <c>application/octet-stream</c> - with the body as text (by the Content-Type charset,");
        sb.AppendLine(4, "/// else UTF-8), so <c>Content</c> and the typed accessors read it like any other error body.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public static global::Atc.Rest.Client.EndpointResponse WithTextErrorBody(global::Atc.Rest.Client.EndpointResponse response)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "if (response.IsSuccess || response.ContentObject is not byte[] bytes || response.Content.Length > 0)");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "return response;");
        sb.AppendLine(8, "}");
        sb.AppendLine();
        sb.AppendLine(8, "var encoding = System.Text.Encoding.UTF8;");
        sb.AppendLine(8, "foreach (var header in response.Headers)");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "if (string.Equals(header.Key, \"Content-Type\", System.StringComparison.OrdinalIgnoreCase) &&");
        sb.AppendLine(12, "    System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(string.Join(\",\", header.Value), out var mediaType) &&");
        sb.AppendLine(12, "    mediaType.CharSet is { Length: > 0 } charSet)");
        sb.AppendLine(12, "{");
        sb.AppendLine(16, "try");
        sb.AppendLine(16, "{");
        sb.AppendLine(20, "encoding = System.Text.Encoding.GetEncoding(charSet.Trim('\"'));");
        sb.AppendLine(16, "}");
        sb.AppendLine(16, "catch (System.ArgumentException)");
        sb.AppendLine(16, "{");
        sb.AppendLine(20, "// An unknown charset: keep UTF-8.");
        sb.AppendLine(16, "}");
        sb.AppendLine(12, "}");
        sb.AppendLine(8, "}");
        sb.AppendLine();
        sb.AppendLine(8, "var text = encoding.GetString(bytes);");
        sb.AppendLine(8, "return new global::Atc.Rest.Client.EndpointResponse(response.IsSuccess, response.StatusCode, text, text, response.Headers);");
        sb.AppendLine(4, "}");
        sb.AppendLine();
        if (hasXml)
        {
            AppendWithXmlProblemBody(sb);
        }

        sb.AppendLine(4, "private static readonly System.Text.Json.JsonSerializerOptions LenientOptions = CreateLenientOptions();");
        sb.AppendLine();
        sb.AppendLine(4, "private static System.Text.Json.JsonSerializerOptions CreateLenientOptions()");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);");
        sb.AppendLine(8, "options.Converters.Add(new LenientStringConverter());");
        sb.AppendLine(8, "return options;");
        sb.AppendLine(4, "}");
        sb.AppendLine();
        sb.AppendLine(4, "private static T? TryRead<T>(string? content, System.Text.Json.JsonSerializerOptions? options = null)");
        sb.AppendLine(8, "where T : class");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "if (string.IsNullOrWhiteSpace(content) || content!.TrimStart()[0] != '{')");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "return null;");
        sb.AppendLine(8, "}");
        sb.AppendLine();
        sb.AppendLine(8, "try");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "return System.Text.Json.JsonSerializer.Deserialize<T>(content, options ?? new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));");
        sb.AppendLine(8, "}");
        sb.AppendLine(8, "catch (System.Text.Json.JsonException)");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "return null;");
        sb.AppendLine(8, "}");
        sb.AppendLine(4, "}");
        sb.AppendLine();
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// The message as detail text: a JSON string body (<c>\"text\"</c>) is unwrapped to its value.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "private static string? ToDetail(string? message)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "if (string.IsNullOrWhiteSpace(message))");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "return null;");
        sb.AppendLine(8, "}");
        sb.AppendLine();
        sb.AppendLine(8, "var trimmed = message!.Trim();");
        sb.AppendLine(8, "if (trimmed.Length >= 2 && trimmed[0] == '\"' && trimmed[trimmed.Length - 1] == '\"')");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "try");
        sb.AppendLine(12, "{");
        sb.AppendLine(16, "return System.Text.Json.JsonSerializer.Deserialize<string>(trimmed);");
        sb.AppendLine(12, "}");
        sb.AppendLine(12, "catch (System.Text.Json.JsonException)");
        sb.AppendLine(12, "{");
        sb.AppendLine(16, "// Not a JSON string after all: keep the text as it is.");
        sb.AppendLine(12, "}");
        sb.AppendLine(8, "}");
        sb.AppendLine();
        sb.AppendLine(8, "return message;");
        sb.AppendLine(4, "}");
        sb.AppendLine();
        sb.AppendLine(4, "private static string GetTypeUri(HttpStatusCode statusCode) => statusCode switch");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "HttpStatusCode.Unauthorized => \"https://tools.ietf.org/html/rfc7235#section-3.1\",");
        sb.AppendLine(8, "HttpStatusCode.Forbidden => \"https://tools.ietf.org/html/rfc7231#section-6.5.3\",");
        sb.AppendLine(8, "_ => $\"https://httpstatuses.io/{(int)statusCode}\"");
        sb.AppendLine(4, "};");
        sb.AppendLine();
        sb.AppendLine(4, "private static string GetTitle(HttpStatusCode statusCode) => statusCode switch");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "HttpStatusCode.Unauthorized => \"Unauthorized\",");
        sb.AppendLine(8, "HttpStatusCode.Forbidden => \"Forbidden\",");
        sb.AppendLine(8, "_ => statusCode.ToString()");
        sb.AppendLine(4, "};");
        sb.AppendLine();
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Reads any JSON value into a string property: a string as its value, anything else as its JSON text.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "private sealed class LenientStringConverter : System.Text.Json.Serialization.JsonConverter<string>");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "public override string? Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options)");
        sb.AppendLine(12, "=> reader.TokenType == System.Text.Json.JsonTokenType.String");
        sb.AppendLine(16, "? reader.GetString()");
        sb.AppendLine(16, ": System.Text.Json.JsonDocument.ParseValue(ref reader).RootElement.GetRawText();");
        sb.AppendLine();
        sb.AppendLine(8, "public override void Write(System.Text.Json.Utf8JsonWriter writer, string value, System.Text.Json.JsonSerializerOptions options)");
        sb.AppendLine(12, "=> writer.WriteStringValue(value);");
        sb.AppendLine(4, "}");
        sb.AppendLine("}");

        var normalizedContent = sb
            .ToString()
            .NormalizeForSourceOutput();

        context.AddGeneratedSource(
            $"{projectName}.ProblemDetailsFactory.g.cs",
            SourceText.From(normalizedContent, Encoding.UTF8));
    }

    /// <summary>
    /// Generates a Constants class with the HttpClientName for DI wireup.
    /// </summary>
    private static void GenerateConstants(
        GeneratedSourceContext context,
        string projectName,
        string? configuredHttpClientName)
    {
        var effectiveHttpClientName = EndpointPerOperationExtractor.GetEffectiveHttpClientName(
            projectName, configuredHttpClientName);

        var content = ConstantsExtractor.Generate(projectName, effectiveHttpClientName);

        var normalizedContent = content.NormalizeForSourceOutput();

        context.AddGeneratedSource(
            $"{projectName}.Constants.g.cs",
            SourceText.From(normalizedContent, Encoding.UTF8));
    }

    /// <summary>
    /// Generates the custom error response model class.
    /// </summary>
    private static void GenerateCustomErrorResponse(
        GeneratedSourceContext context,
        CustomErrorResponseModelConfig config,
        string namespaceName,
        string projectName)
    {
        var content = CustomErrorResponseExtractor.Generate(config, namespaceName);

        var normalizedContent = content.NormalizeForSourceOutput();

        context.AddGeneratedSource(
            $"{projectName}.{config.Name}.g.cs",
            SourceText.From(normalizedContent, Encoding.UTF8));
    }

    /// <summary>
    /// Emits the parameter records of the operations (and the inline enums they use) into the client namespace -
    /// the same files for the typed and the per-operation client, which share the parameters.
    /// </summary>
    private static void GenerateClientParameters(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        string? namespaceSegment,
        string clientNamespace,
        TypeConflictRegistry? registry,
        bool includeDeprecated,
        ValidateSpecificationStrategy validateStrategy,
        bool hasSegmentModels,
        bool hasSharedModels,
        bool extensibleEnums)
    {
        var (parameterRecords, parameterInlineEnums) = HttpClientExtractor.ExtractParametersWithInlineEnums(openApiDoc, projectName, pathSegment, registry, includeDeprecated, validateStrategy, namespaceSegment);

        // The inline enums first: the parameter records reference them
        foreach (var inlineEnum in parameterInlineEnums)
        {
            var enumContent = InlineEnumExtractor.GenerateEnumContent(inlineEnum.EnumParameters, extensibleEnums);
            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(clientNamespace, inlineEnum.TypeName),
                SourceText.From(enumContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }

        if (parameterRecords is not { Count: > 0 })
        {
            return;
        }

        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        foreach (var paramRecord in parameterRecords)
        {
            // One record per file (a file-scoped namespace per file)
            var recordsContainer = new RecordsParameters(
                HeaderContent: BuildClientParameterHeader(projectName, namespaceSegment, hasSegmentModels, hasSharedModels, paramRecord),
                Namespace: clientNamespace,
                DocumentationTags: null,
                Attributes: null,
                DeclarationModifier: DeclarationModifiers.Public,
                Parameters: [paramRecord]);

            var generatedParamContent = RecordContentGenerator.Generate(codeDocGenerator, recordsContainer);
            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(clientNamespace, paramRecord.Name),
                SourceText.From(generatedParamContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Builds the header content for client parameter files.
    /// </summary>
    private static string BuildClientParameterHeader(
        string projectName,
        string? pathSegment,
        bool hasSegmentModels,
        bool hasSharedModels,
        RecordParameters? paramRecord = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Collect all usings
        var usings = new List<string>
        {
            "Atc.Rest.Client",
            "System.CodeDom.Compiler",
            "System.ComponentModel",
            "System.ComponentModel.DataAnnotations",
        };

        if (paramRecord is not null && UsingStatementHelper.RecordUsesSystemTypes(paramRecord))
        {
            usings.Add("System");
        }

        var sharedModelsNamespace = NamespaceBuilder.ForModels(projectName);
        var segmentModelsNamespace = NamespaceBuilder.ForModels(projectName, pathSegment);

        if (hasSharedModels)
        {
            usings.Add(sharedModelsNamespace);
        }

        // When the path segment resolves away both namespaces are identical, so skip the duplicate.
        if (hasSegmentModels &&
            !(hasSharedModels && string.Equals(segmentModelsNamespace, sharedModelsNamespace, StringComparison.Ordinal)))
        {
            usings.Add(segmentModelsNamespace);
        }

        // Sort per SA1210: System.* first, then alphabetical
        UsingStatementHelper.AppendUsings(sb, usings);
        sb.AppendLine();
        return sb.ToString();
    }
}