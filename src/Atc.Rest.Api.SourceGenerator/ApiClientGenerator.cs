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
    // The Atc.Rest.Client an EndpointPerOperation client needs: 2.4.0 reads the streamed responses (StreamFraming,
    // typed errors and headers on StreamingEndpointResponse<T>) and looks response headers up whatever their casing;
    // 2.5.0 has the default error response without a type (a declared XML default) and the last event id of an
    // event stream; 2.6.0 has ServerSentEventReader, which the typed client's Result style reads an event stream with;
    // 2.7.0 sends a streamed request body (WithStreamedBody) and keeps an empty text body as the empty text
    private static readonly Version MinimumAtcRestClientVersion = new(2, 7, 0);

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
        var yamlFiles = SpecificationFileProvider.Create(context);

        // Check for required package references
        var packageReferencesProvider = context.CompilationProvider
            .Select(static (compilation, _) => new ClientPackageReferences
            {
                AtcRestClientVersion = compilation.VersionOf("Atc.Rest.Client"),
                HasResilience = compilation.References("Microsoft.Extensions.Http.Resilience"),
                HasHttpClientFactory = compilation.References("Microsoft.Extensions.Http"),
                HasSignalRClient = compilation.References("Microsoft.AspNetCore.SignalR.Client"),
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

        if (GeneratorStartup.ReadMarker<ClientConfig>(productionContext, yamlFiles, markers) is not { } started)
        {
            return;
        }

        var (marker, config) = started;

        // Check for Atc.Rest.Client reference when using EndpointPerOperation mode
        if (config.GenerationMode == GenerationModeType.EndpointPerOperation && packages.AtcRestClientVersion is null)
        {
            DiagnosticHelpers.ReportClientRequiresAtcRestClient(productionContext);
            return;
        }

        // ... in a version that has every member the generated client calls. The typed client's Result style
        // builds on the package too (its envelopes, its event-stream reader)
        var usesAtcRestClient = config.GenerationMode == GenerationModeType.EndpointPerOperation ||
                                config.TypedClientResultStyle == TypedClientResultStyleType.Result;
        if (usesAtcRestClient && packages.AtcRestClientVersion is not null && packages.AtcRestClientVersion < MinimumAtcRestClientVersion)
        {
            DiagnosticHelpers.ReportAtcRestClientTooOld(productionContext, MinimumAtcRestClientVersion, packages.AtcRestClientVersion!);
            return;
        }

        // Everything from here runs under the catch: a failure before the base file is known is reported on the marker
        var filePath = marker.Path;
        try
        {
            GeneratorFaultInjection.ThrowIfRequested(RuleIdentifiers.ClientGenerationError);

            if (GeneratorStartup.FindSpecification(productionContext, marker.Path, config.Overlays, yamlFiles) is not { } files)
            {
                return;
            }

            filePath = files.BaseFile.Path;
            GenerateApiClient(productionContext, files, config, packages);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            DiagnosticHelpers.ReportClientGenerationError(productionContext, filePath, ex);
        }
    }

    /// <summary>
    /// Record to hold client package reference information.
    /// </summary>
    private sealed record ClientPackageReferences
    {
        /// <summary>
        /// The referenced Atc.Rest.Client's assembly version (it follows the package version), or null when it is not
        /// referenced.
        /// </summary>
        public Version? AtcRestClientVersion { get; init; }

        public bool HasResilience { get; init; }

        /// <summary>
        /// True when Microsoft.Extensions.Http is referenced. The typed-client DI extension calls
        /// AddHttpClient&lt;TInterface, TClient&gt;(), so it can only be emitted when present.
        /// </summary>
        public bool HasHttpClientFactory { get; init; }

        /// <summary>
        /// True when Microsoft.AspNetCore.SignalR.Client is referenced: the typed hub clients of x-signalr-hubs build
        /// on its HubConnection.
        /// </summary>
        public bool HasSignalRClient { get; init; }
    }

    /// <summary>
    /// The part of the specification one typed client is generated for: the path segment it holds the operations of,
    /// the namespace segment (empty: Single granularity), which models the segment has and the client's explicit name.
    /// </summary>
    /// <param name="PathSegment">The path segment whose operations the client has (empty: all).</param>
    /// <param name="NamespaceSegment">The segment of the client's namespace and type name (empty: none).</param>
    /// <param name="HasSegmentModels">Whether the segment has models of its own.</param>
    /// <param name="HasSharedModels">Whether there are shared models.</param>
    /// <param name="ClientName">The client's type name as the author states it, or <see langword="null"/>.</param>
    private sealed record TypedClientSegment(
        string PathSegment,
        string? NamespaceSegment,
        bool HasSegmentModels,
        bool HasSharedModels,
        string? ClientName);

    /// <summary>
    /// Generates the API client from the project's specification (its parts merged).
    /// </summary>
    private static void GenerateApiClient(
        SourceProductionContext context,
        SpecificationFiles files,
        ClientConfig config,
        ClientPackageReferences packages)
    {
        var openApiDoc = GeneratorStartup.Load(context, files, config, config.MultiPartConfiguration, parametersMustBind: false, DiagnosticHelpers.ReportClientParsingError);
        if (openApiDoc is null)
        {
            return;
        }

        // A TypedClient needs Atc.Rest.Client only for a file (IFileContent) or the Result style: without the package
        // that is an error naming it, not a compile error in generated code
        if (config.GenerationMode == GenerationModeType.TypedClient &&
            packages.AtcRestClientVersion is null &&
            TypedClientNeedsAtcRestClient(openApiDoc, config) is { } reason)
        {
            DiagnosticHelpers.ReportTypedClientRequiresAtcRestClient(context, reason);
            return;
        }

        // Resolve the root namespace: marker namespace, qualifying info.title, then base file name.
        var projectName = DiagnosticHelpers.ResolveRootNamespace(context, config.Namespace, openApiDoc, files.BaseFile.Path);

        GenerateApiClientFromDocument(context, files.BaseFile.Path, openApiDoc, config, packages, projectName);
    }

    /// <summary>
    /// What makes a TypedClient use Atc.Rest.Client, or null: the Result style, an operation that uploads a file (its
    /// parameter is an <c>IFileContent</c>), a model with a binary property (an <c>IFileContent</c> member).
    /// </summary>
    private static string? TypedClientNeedsAtcRestClient(
        OpenApiDocument openApiDoc,
        ClientConfig config)
    {
        if (config.TypedClientResultStyle == TypedClientResultStyleType.Result)
        {
            return "the Result style (typedClientResultStyle: Result)";
        }

        // A property that is a file: binary, or a list of binary
        static string? BinaryProperty(IOpenApiSchema? schema)
            => ((schema is OpenApiSchemaReference reference ? reference.Target : schema) as OpenApiSchema)?.Properties?
                .FirstOrDefault(p => p.Value is OpenApiSchema { Format: "binary" } ||
                                     p.Value is OpenApiSchema { Items: OpenApiSchema { Format: "binary" } })
                .Key;

        // An operation that sends a file: a raw binary body, or a form with a binary field
        foreach (var path in openApiDoc.Paths ?? [])
        {
            foreach (var operation in path.Value.Operations ?? new Dictionary<HttpMethod, OpenApiOperation>())
            {
                foreach (var content in operation.Value.RequestBody?.Content ?? new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal))
                {
                    if (OpenApiOperationExtensions.IsRawFileUploadContentType(content.Key) ||
                        (OpenApiOperationExtensions.IsFileUploadContentType(content.Key) && BinaryProperty(content.Value.Schema) is not null))
                    {
                        return $"the file upload of {operation.Key.Method.ToUpperInvariant()} {path.Key} (IFileContent)";
                    }
                }
            }
        }

        // A model with a file property
        foreach (var schema in openApiDoc.Components?.Schemas ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            if (BinaryProperty(schema.Value) is { } property)
            {
                return $"the binary property {schema.Key}.{property} (IFileContent)";
            }
        }

        return null;
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
            readEnumMemberNames: config.ReadEnumMemberNames);
        if (config.ReadEnumMemberNames)
        {
            DiagnosticHelpers.ReportReadEnumMemberNamesOn(context, ".atc-rest-api-client");
        }

        // The typed client and the endpoint-per-operation client each have their own support types; the custom error
        // model's type name is as the generated code refers to it (fully qualified when it collides)
        var isTypedClient = config.GenerationMode != GenerationModeType.EndpointPerOperation;
        var customErrorTypeName = isTypedClient
            ? config.CustomErrorResponseModel?.Name
            : EmitEndpointPerOperationSupport(generatedContext, openApiDoc, projectName, config);
        if (isTypedClient)
        {
            EmitTypedClientSupport(generatedContext, openApiDoc, projectName, packages, modelNames);
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
        if (!isTypedClient)
        {
            ReportAndRemoveShadowedErrorContracts(context, openApiDoc, sharedSchemas, yamlPath);
        }

        // The trim- and AOT-safe length-limit attributes the models and parameters are written with
        if (LengthLimitAttributesExtractor.IsNeeded(openApiDoc))
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.Generated.LengthLimitAttributes.g.cs",
                LengthLimitAttributesExtractor.Generate(NamespaceBuilder.BuildBase(projectName)));
        }

        // The attributes a pattern (on a value or on the items of an array) and a format: uri are written with
        if (LengthLimitAttributesExtractor.NeededValueAttributes(openApiDoc) is { Count: > 0 } valueAttributes)
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.Generated.ValueAttributes.g.cs",
                LengthLimitAttributesExtractor.GenerateValueAttributes(NamespaceBuilder.BuildBase(projectName), valueAttributes));
        }

        // The SignalR hubs of the specification (x-signalr-hubs): a typed client per hub, where the project has the
        // SignalR client to build it on
        EmitSignalRHubClients(context, generatedContext, openApiDoc, projectName, config, packages);

        // Single-client mode: emit one client covering every operation, with all models flattened
        // into "{projectName}.Generated.Models". Handled before the per-area loop because the two
        // layouts are mutually exclusive.
        if (isTypedClient && config.ClientGranularity == ClientGranularityType.Single)
        {
            GenerateSingleTypedClient(context, generatedContext, openApiDoc, projectName, config, conflicts, systemTypeResolver, yamlPath, packages.HasHttpClientFactory, xmlModels);
            GenerateEnumWireConverterFactory(generatedContext, openApiDoc, projectName);
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

        // Shared models and enums first (under the common namespace without a segment suffix)
        if (sharedSchemas.Count > 0)
        {
            var sharedRegistry = TypeConflictRegistry.ForSegment(conflicts, projectName, extensibleEnums: config.ExtensibleEnums, componentTypeNames: componentTypeNames);
            EmitClientSchemaTypes(generatedContext, openApiDoc, projectName, sharedSchemas, pathSegment: null, sharedRegistry, config, includeSharedModelsUsing: false, xmlModels);
        }

        // Track generated path segments for consolidated DI extension
        var generatedPathSegments = new List<string>();

        // Generate files per path segment (excluding shared schemas)
        foreach (var pathSegment in pathSegments)
        {
            // A superseded IDE run stops here instead of emitting the rest
            context.CancellationToken.ThrowIfCancellationRequested();

            if (EmitSegmentClient(generatedContext, openApiDoc, projectName, pathSegment, config, packages, conflicts, sharedSchemas, componentTypeNames, systemTypeResolver, customErrorTypeName, xmlModels) is { } generatedSegment)
            {
                generatedPathSegments.Add(generatedSegment);
            }
        }

        // The typed client's stream readers and its writer of a streamed request body (the endpoint-per-operation client
        // has Atc.Rest.Client's)
        if (isTypedClient)
        {
            EmitTypedClientStreaming(generatedContext, openApiDoc, projectName);
        }

        // Generate consolidated DI extension method for all path segments (EndpointPerOperation mode only).
        // When the only segment resolved away, the per-segment extension is already project-named and
        // lives in "{projectName}.Generated", so a consolidated wrapper would be a duplicate.
        if (!isTypedClient &&
            generatedPathSegments.Count > 0 &&
            !generatedPathSegments.TrueForAll(string.IsNullOrEmpty))
        {
            var consolidatedDiContent = GenerateConsolidatedDiExtension(projectName, generatedPathSegments);
            generatedContext.AddGeneratedSource(
                $"{projectName}.Endpoints.DependencyInjection.g.cs",
                consolidatedDiContent);
        }

        // Resilience (x-retry-*) and OAuth token management, where the specification declares them
        EmitClientFeatures(context, generatedContext, openApiDoc, projectName, config, packages);

        GenerateEnumWireConverterFactory(generatedContext, openApiDoc, projectName);

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

    // The typed client's support types: a downloaded or streamed file, the content-or-file of a model or a file, the
    // JSON options its DI registrations read, the exception of an error response and the JSON options it writes with
    private static void EmitTypedClientSupport(
        GeneratedSourceContext generatedContext,
        OpenApiDocument openApiDoc,
        string projectName,
        ClientPackageReferences packages,
        ICollection<string> modelNames)
    {
        // A TypedClient download returns a DownloadedFile (without the Atc.Rest.Client-based FromResponse), a streamed one
        // (x-return-stream) a StreamedFile, and a model or a file a ContentOrFile<T>
        if (HasStreamedFileDownload(openApiDoc))
        {
            GenerateStreamedFile(generatedContext, projectName);
        }

        if (HasTypedClientBufferedFileDownload(openApiDoc))
        {
            GenerateDownloadedFile(generatedContext, projectName, includeEndpointResponse: false);
            if (HasModelOrFileResponse(openApiDoc))
            {
                GenerateContentOrFile(generatedContext, projectName);
            }
        }

        // The serializer options the typed clients' DI registrations read (only with Microsoft.Extensions.Http, as the
        // registrations themselves)
        if (packages.HasHttpClientFactory)
        {
            GenerateApiClientJsonOptions(generatedContext, openApiDoc, projectName);
        }

        // The typed clients read and write JSON through the metadata their options resolve (trim- and AOT-safe), and
        // throw one exception for an error response: an HttpRequestException with the response's headers and body
        var exceptionName = ApiExceptionExtractor.ClassNameFor(modelNames);
        generatedContext.AddGeneratedSource(
            $"{projectName}.Generated.{exceptionName}.g.cs",
            ApiExceptionExtractor.Generate(NamespaceBuilder.BuildBase(projectName), exceptionName));

        generatedContext.AddGeneratedSource(
            $"{projectName}.Generated.{GeneratedJsonOptionsExtractor.ClassName}.g.cs",
            GeneratedJsonOptionsExtractor.Generate(NamespaceBuilder.BuildBase(projectName)));
    }

    // The endpoint-per-operation client's support types: the ProblemDetails and its factory, the result interface, the
    // constants, a downloaded file, the custom error model (in a namespace of its own when it would shadow a schema).
    // Returns the custom error model's type name as the generated code refers to it
    private static string? EmitEndpointPerOperationSupport(
        GeneratedSourceContext generatedContext,
        OpenApiDocument openApiDoc,
        string projectName,
        ClientConfig config)
    {
        GenerateProblemDetails(generatedContext, projectName);
        GenerateProblemDetailsFactory(generatedContext, projectName, hasXml: XmlBodyReachability.Collect(openApiDoc).Count > 0);
        GenerateEndpointResultInterface(generatedContext, projectName);
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
                return $"global::{customNamespace}.{customName}";
            }
        }

        return config.CustomErrorResponseModel?.Name;
    }

    // A set of schemas as the client's types: the models, the enums, the tuples and the polymorphic bases
    private static void EmitClientSchemaTypes(
        GeneratedSourceContext generatedContext,
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemas,
        string? pathSegment,
        TypeConflictRegistry registry,
        ClientConfig config,
        bool includeSharedModelsUsing,
        ISet<string> xmlModels)
    {
        GenerateModelsForSchemas(generatedContext, openApiDoc, projectName, schemas, pathSegment, registry, config.IncludeDeprecated, config.GeneratePartialModels, includeSharedModelsUsing: includeSharedModelsUsing, strictRequiredProperties: config.StrictRequiredProperties, xmlModels: xmlModels, extensibleEnums: config.ExtensibleEnums);
        SchemaTypeEmitter.EmitEnums(generatedContext, openApiDoc, projectName, schemas, pathSegment, config.ExtensibleEnums, stringOnTheWire: false);
        SchemaTypeEmitter.EmitTuples(generatedContext, openApiDoc, projectName, schemas, pathSegment);
        PolymorphicTypeEmitter.Emit(generatedContext, openApiDoc, projectName, schemas, pathSegment);
    }

    // A typed client per SignalR hub, where the project has the SignalR client to build it on (else a recommendation)
    private static void EmitSignalRHubClients(
        SourceProductionContext context,
        GeneratedSourceContext generatedContext,
        OpenApiDocument openApiDoc,
        string projectName,
        ClientConfig config,
        ClientPackageReferences packages)
    {
        var hubClients = SignalRHubExtractor.ExtractClient(
            openApiDoc,
            projectName,
            singleModelsNamespace: config.GenerationMode != GenerationModeType.EndpointPerOperation && config.ClientGranularity == ClientGranularityType.Single);
        if (hubClients.Count > 0 && packages.HasSignalRClient)
        {
            foreach (var (hintName, content) in hubClients)
            {
                generatedContext.AddGeneratedSource(hintName, content);
            }
        }
        else if (hubClients.Count > 0)
        {
            DiagnosticHelpers.ReportSignalRClientPackageRecommended(
                context,
                SignalRHubExtractor.ClientTypeNames(openApiDoc));
        }
    }

    // One path segment's client: its models, then its endpoints (endpoint per operation) or its typed client. Returns
    // the segment as its namespace uses it when it has endpoints for the consolidated DI extension
    private static string? EmitSegmentClient(
        GeneratedSourceContext generatedContext,
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        ClientConfig config,
        ClientPackageReferences packages,
        ISet<string> conflicts,
        HashSet<string> sharedSchemas,
        ISet<string> componentTypeNames,
        SystemTypeConflictResolver systemTypeResolver,
        string? customErrorTypeName,
        ISet<string> xmlModels)
    {
        // Resolve the segment used for namespaces/file names. Redundant segments (single-segment
        // documents, or segments that merely echo the root namespace tail) resolve away so
        // generated types land directly under "{projectName}.Generated.{Category}".
        // Normalize to string.Empty (never null) so the extractors treat this as an explicitly
        // resolved value instead of "not specified" and never fall back to the raw segment.
        var effectiveSegment = PathSegmentHelper.ResolveEffectivePathSegment(openApiDoc, projectName, pathSegment) ?? string.Empty;

        // Create conflict registry for this path segment (lightweight: O(1))
        var registry = TypeConflictRegistry.ForSegment(conflicts, projectName, effectiveSegment, sharedSchemas, config.ExtensibleEnums, componentTypeNames);

        // Get segment-specific schemas (excluding shared ones) — uses pre-computed sharedSchemas to avoid O(S²)
        var segmentSchemas = PathSegmentHelper.GetSegmentSpecificSchemas(openApiDoc, pathSegment, sharedSchemas);

        // Built-in error contracts are emitted once under "{projectName}.Generated"; drop any
        // spec-defined namesake so it is not re-emitted per segment. Already reported above.
        if (config.GenerationMode == GenerationModeType.EndpointPerOperation)
        {
            segmentSchemas.RemoveWhere(BuiltInErrorContractNames.IsReserved);
        }

        // Segment-specific models (excluding shared ones), with the shared models' using
        if (segmentSchemas.Count > 0)
        {
            EmitClientSchemaTypes(generatedContext, openApiDoc, projectName, segmentSchemas, effectiveSegment, registry, config, includeSharedModelsUsing: sharedSchemas.Count > 0, xmlModels);
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
            return hasEndpoints ? effectiveSegment : null;
        }
        else
        {
            var hasSegmentModelsTyped = segmentSchemas.Count > 0;
            var hasSharedModelsTyped = sharedSchemas.Count > 0;
            var segment = new TypedClientSegment(pathSegment, effectiveSegment, hasSegmentModelsTyped, hasSharedModelsTyped, ClientName: null);
            GenerateTypedClient(generatedContext, openApiDoc, projectName, segment, registry, systemTypeResolver, config, packages.HasHttpClientFactory);
            return null;
        }
    }

    // The typed client's stream readers (an operation reads a stream) and the writer of a request body sent as a stream
    // of records, with the typed client's JSON options
    private static void EmitTypedClientStreaming(
        GeneratedSourceContext generatedContext,
        OpenApiDocument openApiDoc,
        string projectName)
    {
        // Emit the shared StreamReaders helper once when an operation of the typed client references it. The
        // EndpointPerOperation client has none: Atc.Rest.Client reads its streamed responses.
        if (StreamReadersExtractor.DocumentRequiresStreamReaders(openApiDoc))
        {
            var streamReadersContent = StreamReadersExtractor.GenerateContent(projectName);
            generatedContext.AddGeneratedSource(
                $"{projectName}.Streaming.StreamReaders.g.cs",
                streamReadersContent);
        }

        // A request body sent as a stream of records (JSON Lines, JSON Text Sequences) is written by the generated
        // StreamedRequestContent<T> with the typed client's JSON options; the endpoint-per-operation client sends it
        // with Atc.Rest.Client's request builder (WithStreamedBody)
        if (StreamedRequestBodyExtractor.DocumentHasStreamedRequestBody(openApiDoc))
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.Streaming.{StreamedRequestContentExtractor.TypeName}.g.cs",
                StreamedRequestContentExtractor.GenerateContent(projectName));
        }
    }

    // Resilience policies (x-retry-*) and OAuth token management, each where the specification declares it
    private static void EmitClientFeatures(
        SourceProductionContext context,
        GeneratedSourceContext generatedContext,
        OpenApiDocument openApiDoc,
        string projectName,
        ClientConfig config,
        ClientPackageReferences packages)
    {
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
            errors,
            handlersNoun: "client methods");
    }

    // The converter both client modes put first in their JSON options, when there is an enum to write - written last,
    // from the enums generated before it
    private static void GenerateEnumWireConverterFactory(
        GeneratedSourceContext generatedContext,
        OpenApiDocument openApiDoc,
        string projectName)
    {
        if (EnumWireConverterExtractor.HasEnums(openApiDoc))
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.Generated.{EnumWireConverterExtractor.ClassName}.g.cs",
                EnumWireConverterExtractor.Generate(NamespaceBuilder.BuildBase(projectName), generatedContext.Enums, generatedContext.ReadEnumMemberNames));
        }
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
                content);
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
                content);
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
                optionsContent);
        }

        // Generate OAuthTokenResponse model
        var tokenResponseContent = OAuthTokenProviderExtractor.ExtractTokenResponse(projectName);
        context.AddGeneratedSource(
            $"{projectName}.OAuth.OAuthTokenResponse.g.cs",
            tokenResponseContent);

        // Generate IOAuthTokenProvider interface
        var interfaceContent = OAuthTokenProviderExtractor.ExtractInterface(projectName);
        context.AddGeneratedSource(
            $"{projectName}.OAuth.IOAuthTokenProvider.g.cs",
            interfaceContent);

        // Generate OAuthTokenProvider implementation
        var implementationContent = OAuthTokenProviderExtractor.ExtractImplementation(oauthConfig, projectName);
        if (implementationContent is not null)
        {
            context.AddGeneratedSource(
                $"{projectName}.OAuth.OAuthTokenProvider.g.cs",
                implementationContent);
        }

        // Generate OAuthAuthenticationHandler
        var handlerContent = OAuthHandlerExtractor.Extract(projectName);
        context.AddGeneratedSource(
            $"{projectName}.OAuth.OAuthAuthenticationHandler.g.cs",
            handlerContent);

        // Generate OAuth DI extensions
        var diContent = OAuthDependencyInjectionExtractor.Extract(oauthConfig, projectName);
        if (diContent is not null)
        {
            context.AddGeneratedSource(
                $"{projectName}.OAuth.DependencyInjection.g.cs",
                diContent);
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
            var enumContent = InlineEnumExtractor.GenerateEnumContent(inlineEnum.EnumParameters, extensibleEnums, enumWireConverter: context.EnumWireConverter, readMemberNames: context.ReadEnumMemberNames);
            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(modelsNamespace, inlineEnum.TypeName),
                enumContent);
        }

        if (recordsParameters is null || recordsParameters.Parameters.Count == 0)
        {
            return;
        }

        // For client-side, convert IFormFile types to IFileContent (from Atc.Rest.Client)
        recordsParameters = ConvertToClientTypes(recordsParameters);

        // strictRequiredProperties: a response that leaves out a required property fails when it is read
        recordsParameters = strictRequiredProperties
            ? JsonRequiredHelper.AddJsonRequired(recordsParameters)
            : JsonRequiredHelper.RemoveJsonRequired(recordsParameters);

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
                generatedContent);
        }

        if (xmlModels is not null)
        {
            XmlModelEmitter.EmitCompanions(context, xmlSources, recordsParameters.HeaderContent, modelsNamespace, projectName, xmlModels, extensibleEnums);
        }
    }

    /// <summary>
    /// Converts server-side types (IFormFile) to client-side types (IFileContent from Atc.Rest.Client) in the records parameters.
    /// </summary>
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

        var registry = TypeConflictRegistry.ForSegment(conflicts, projectName, NoSegment, extensibleEnums: config.ExtensibleEnums, componentTypeNames: TypeConflictRegistry.ComponentTypeNamesOf(openApiDoc));

        var allSchemas = openApiDoc.Components?.Schemas?.Keys is { } keys
            ? new HashSet<string>(keys, StringComparer.Ordinal)
            : [];

        if (allSchemas.Count > 0)
        {
            GenerateModelsForSchemas(generatedContext, openApiDoc, projectName, allSchemas, NoSegment, registry, config.IncludeDeprecated, config.GeneratePartialModels, strictRequiredProperties: config.StrictRequiredProperties, xmlModels: xmlModels, extensibleEnums: config.ExtensibleEnums);
            SchemaTypeEmitter.EmitEnums(generatedContext, openApiDoc, projectName, allSchemas, NoSegment, config.ExtensibleEnums, stringOnTheWire: false);
            SchemaTypeEmitter.EmitTuples(generatedContext, openApiDoc, projectName, allSchemas, NoSegment);
            PolymorphicTypeEmitter.Emit(generatedContext, openApiDoc, projectName, allSchemas, NoSegment);
        }

        var segment = new TypedClientSegment(
            PathSegment: NoSegment,
            NamespaceSegment: NoSegment,
            HasSegmentModels: allSchemas.Count > 0,
            HasSharedModels: false,
            config.ClientName);
        GenerateTypedClient(generatedContext, openApiDoc, projectName, segment, registry, systemTypeResolver, config, generateDiExtension);

        if (StreamReadersExtractor.DocumentRequiresStreamReaders(openApiDoc))
        {
            var streamReadersContent = StreamReadersExtractor.GenerateContent(projectName);
            generatedContext.AddGeneratedSource(
                $"{projectName}.Streaming.StreamReaders.g.cs",
                streamReadersContent);
        }

        if (StreamedRequestBodyExtractor.DocumentHasStreamedRequestBody(openApiDoc))
        {
            generatedContext.AddGeneratedSource(
                $"{projectName}.Streaming.{StreamedRequestContentExtractor.TypeName}.g.cs",
                StreamedRequestContentExtractor.GenerateContent(projectName));
        }
    }

    /// <summary>
    /// The headers the responses of each operation declare, typed: <c>{Operation}ResponseHeaders</c> next to the
    /// client, built from a Result-style response's headers or an <c>ApiException</c>'s, with the enums the headers
    /// declare inline.
    /// </summary>
    private static void GenerateTypedClientResponseHeaders(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        string clientNamespace,
        string modelsNamespace,
        TypeConflictRegistry registry,
        bool includeDeprecated,
        bool hasSegmentModels,
        bool hasSharedModels,
        bool extensibleEnums)
    {
        var usings = new List<string>();
        if (hasSharedModels)
        {
            usings.Add(NamespaceBuilder.ForModels(projectName));
        }

        if (hasSegmentModels && !usings.Contains(modelsNamespace, StringComparer.Ordinal))
        {
            usings.Add(modelsNamespace);
        }

        var componentTypeNames = TypeConflictRegistry.ComponentTypeNamesOf(openApiDoc);
        foreach (var path in openApiDoc.Paths ?? new OpenApiPaths())
        {
            if (path.Key.ShouldSkipForPathSegment(pathSegment) || path.Value?.Operations is null)
            {
                continue;
            }

            foreach (var operation in path.Value.Operations)
            {
                if (operation.Value is null || (!includeDeprecated && operation.Value.Deprecated))
                {
                    continue;
                }

                var operationId = OperationIdHelper.Effective(operation.Value, operation.Key.ToString(), path.Key);
                var operationName = operationId.ToPascalCaseForDotNet();
                var headers = ResponseHeaderFactoryHelper.DeclaredBy(operation.Value, registry, operationName, componentTypeNames);
                if (headers.Count == 0)
                {
                    continue;
                }

                foreach (var (typeName, schema) in ResponseHeaderFactoryHelper.InlineEnumsOf(operation.Value, operationName, componentTypeNames))
                {
                    if (InlineEnumExtractor.ExtractEnumFromInlineSchema(schema, typeName, clientNamespace) is { } enumParameters)
                    {
                        context.AddGeneratedSource(
                            NamespaceBuilder.ToFileName(clientNamespace, typeName),
                            InlineEnumExtractor.GenerateEnumContent(enumParameters, extensible: extensibleEnums));
                    }
                }

                var className = operationName + "ResponseHeaders";
                var content = ResponseHeaderFactoryHelper.TypedClientHeadersSource(clientNamespace, usings, className, operationId, headers, extensibleEnums);
                context.AddGeneratedSource(
                    NamespaceBuilder.ToFileName(clientNamespace, className),
                    content);
            }
        }
    }

    private static void GenerateTypedClient(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        TypedClientSegment segment,
        TypeConflictRegistry registry,
        SystemTypeConflictResolver systemTypeResolver,
        ClientConfig config,
        bool generateDiExtension)
    {
        var pathSegment = segment.PathSegment;
        var namespaceSegment = segment.NamespaceSegment;
        var hasSegmentModels = segment.HasSegmentModels;
        var hasSharedModels = segment.HasSharedModels;
        var clientName = segment.ClientName;
        var includeDeprecated = config.IncludeDeprecated;
        var useServersBasePath = config.UseServersBasePath;
        var validateStrategy = config.ValidateSpecificationStrategy;
        var clientSuffix = config.ClientSuffix;
        var typedClientResultStyle = config.TypedClientResultStyle;
        var strictRequiredProperties = config.StrictRequiredProperties;
        var preferXml = config.PreferXml;
        var extensibleEnums = config.ExtensibleEnums;

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
            generatedContent);

        GenerateTypedClientResponseHeaders(context, openApiDoc, projectName, pathSegment, clientNamespace, modelsNamespace, registry, includeDeprecated, hasSegmentModels, hasSharedModels, extensibleEnums);

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
                interfaceContent);

            // The DI extension calls AddHttpClient<TInterface>() and AddTypedClient, which live in
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
                    diContent);
            }
        }

        // Generate inline model files for any inline schemas discovered - under Single granularity (an empty namespace
        // segment) flat in "{projectName}.Generated.Models", where the client looks for them
        if (inlineSchemas.Count > 0)
        {
            var inlineTypes = CodeGenerationService.GenerateInlineModels(
                strictRequiredProperties
                    ? inlineSchemas.ToDictionary(kvp => kvp.Key, kvp => kvp.Value with { RecordParameters = JsonRequiredHelper.WithJsonRequired(kvp.Value.RecordParameters) }, StringComparer.Ordinal)
                    : inlineSchemas.ToDictionary(kvp => kvp.Key, kvp => kvp.Value with { RecordParameters = JsonRequiredHelper.WithoutJsonRequired(kvp.Value.RecordParameters) }, StringComparer.Ordinal),
                projectName,
                CodeGenerationService.GeneratorType.Client,
                granularity: string.IsNullOrEmpty(namespaceSegment) ? ClientGranularityType.Single : ClientGranularityType.PerArea);
            foreach (var inlineType in inlineTypes)
            {
                var inlineContent = CodeGenerationService.FormatAsFile(inlineType);
                context.AddGeneratedSource(
                    NamespaceBuilder.ToFileName(modelsNamespace, inlineType.TypeName),
                    inlineContent);
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
            sb.ToString());
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
        sb.AppendLine($"    /// Adds <see cref=\"{clientTypeName}\"/> as <see cref=\"{interfaceTypeName}\"/> and as itself, using IHttpClientFactory.");
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
        sb.AppendLine("        // The class is registered too, on the same named HttpClient, for a consumer that depends on it.");
        sb.AppendLine($"        builder.AddTypedClient<{clientTypeName}>(Create);");
        sb.AppendLine($"        return builder.AddTypedClient<{interfaceTypeName}>(Create);");
        sb.AppendLine();
        sb.AppendLine($"        static {clientTypeName} Create(HttpClient httpClient, IServiceProvider serviceProvider)");
        sb.AppendLine($"            => new {clientTypeName}(");
        sb.AppendLine("                httpClient,");
        sb.AppendLine($"                serviceProvider.GetRequiredService<IOptions<{jsonOptionsType}>>().Value.SerializerOptions);");
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
            preferXml: preferXml,
            extensibleEnums: extensibleEnums);

        // Generate inline model types if any were discovered
        if (inlineSchemas.Count > 0)
        {
            var inlineTypes = CodeGenerationService.GenerateInlineModels(
                strictRequiredProperties
                    ? inlineSchemas.ToDictionary(kvp => kvp.Key, kvp => kvp.Value with { RecordParameters = JsonRequiredHelper.WithJsonRequired(kvp.Value.RecordParameters) }, StringComparer.Ordinal)
                    : inlineSchemas.ToDictionary(kvp => kvp.Key, kvp => kvp.Value with { RecordParameters = JsonRequiredHelper.WithoutJsonRequired(kvp.Value.RecordParameters) }, StringComparer.Ordinal),
                projectName,
                CodeGenerationService.GeneratorType.Client);
            foreach (var inlineType in inlineTypes)
            {
                var inlineContent = CodeGenerationService.FormatAsFile(inlineType);
                context.AddGeneratedSource(
                    NamespaceBuilder.ToFileName(modelsNamespace, inlineType.TypeName),
                    inlineContent);
            }
        }

        var endpointNames = new List<string>();

        foreach (var opFiles in operationFiles)
        {
            endpointNames.Add(opFiles.OperationName);

            // Endpoint Interface
            context.AddGeneratedSource(
                opFiles.EndpointInterfaceFileName,
                opFiles.EndpointInterfaceContent);

            // Endpoint Class
            context.AddGeneratedSource(
                opFiles.EndpointClassFileName,
                opFiles.EndpointClassContent);

            // Result Interface (null for binary endpoints that use BinaryEndpointResponse)
            if (opFiles is { ResultInterfaceFileName: not null, ResultInterfaceContent: not null })
            {
                context.AddGeneratedSource(
                    opFiles.ResultInterfaceFileName,
                    opFiles.ResultInterfaceContent);
            }

            // Result Class (null for binary endpoints that use BinaryEndpointResponse)
            if (opFiles is { ResultClassFileName: not null, ResultClassContent: not null })
            {
                context.AddGeneratedSource(
                    opFiles.ResultClassFileName,
                    opFiles.ResultClassContent);
            }

            // The enums the operation's response headers declare inline
            foreach (var (fileName, content) in opFiles.HeaderEnumFiles ?? [])
            {
                context.AddGeneratedSource(
                    fileName,
                    content);
            }
        }

        // Generate DI extension method
        if (endpointNames.Count > 0)
        {
            var diExtensionContent = GenerateEndpointDiExtension(projectName, namespaceSegment, endpointNames, EnumWireConverterExtractor.HasEnums(openApiDoc));
            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(NamespaceBuilder.ForEndpoints(projectName, namespaceSegment), "DependencyInjection"),
                diExtensionContent);
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
        sb.AppendLine(4, "/// <remarks>");
        sb.AppendLine(4, "/// The serializer is shared by every client in the app. Its options start from Atc.Rest.Client's defaults, and every");
        sb.AppendLine(4, "/// registration adds its configure step (ConfigureAtcRestClientJsonOptions), so the steps of several clients are all");
        sb.AppendLine(4, "/// kept - for example each library's JsonSerializerContext in a trimmed or Native AOT app.");
        sb.AppendLine(4, "/// </remarks>");
        sb.AppendLine(4, "/// <param name=\"services\">The service collection.</param>");
        sb.AppendLine(4, "/// <param name=\"configureJsonSerializerOptions\">Optional callback to adjust the serializer options, which start from Atc.Rest.Client's defaults.</param>");
        sb.AppendLine(4, "/// <returns>The service collection for method chaining.</returns>");
        sb.AppendLine(4, $"public static IServiceCollection Add{registrationName}Endpoints(");
        sb.AppendLine(8, "this IServiceCollection services,");
        sb.AppendLine(8, "global::System.Action<global::System.Text.Json.JsonSerializerOptions>? configureJsonSerializerOptions = null)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "// Ensure Atc.Rest.Client core services are registered (IHttpMessageFactory, IContractSerializer)");
        sb.AppendLine(8, "services.AddAtcRestClientCore();");
        if (hasEnums)
        {
            // Every generated enum as the specification writes it, ahead of Atc.Rest.Client's own enum converter (which
            // would write an integer enum's member name) - once, though each area of the project registers it
            var converter = $"global::{NamespaceBuilder.BuildBase(projectName)}.{EnumWireConverterExtractor.ClassName}";
            sb.AppendLine();
            sb.AppendLine(8, "// Every generated enum on the wire as the specification writes it");
            sb.AppendLine(8, "services.ConfigureAtcRestClientJsonOptions(options =>");
            sb.AppendLine(8, "{");
            sb.AppendLine(12, $"if (!global::System.Linq.Enumerable.Any(options.Converters, converter => converter is {converter}))");
            sb.AppendLine(12, "{");
            sb.AppendLine(16, $"options.Converters.Insert(0, new {converter}());");
            sb.AppendLine(12, "}");
            sb.AppendLine(8, "});");
        }

        sb.AppendLine();
        sb.AppendLine(8, "if (configureJsonSerializerOptions is not null)");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "services.ConfigureAtcRestClientJsonOptions(configureJsonSerializerOptions);");
        sb.AppendLine(8, "}");
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
        sb.AppendLine("using Atc.Rest.Client.Options;");
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
        sb.AppendLine(4, "/// <param name=\"configureJsonSerializerOptions\">Optional callback to adjust the serializer options, which start from Atc.Rest.Client's defaults.</param>");
        sb.AppendLine(4, "/// <returns>The service collection for method chaining.</returns>");
        sb.AppendLine(4, $"public static IServiceCollection Add{sanitizedProjectName}Endpoints(");
        sb.AppendLine(8, "this IServiceCollection services,");
        sb.AppendLine(8, "global::System.Action<global::System.Text.Json.JsonSerializerOptions>? configureJsonSerializerOptions = null)");
        sb.AppendLine(4, "{");

        // Call each path segment's extension method, and add the callback's configure step once
        foreach (var pathSegment in pathSegments)
        {
            sb.AppendLine(8, $"services.Add{pathSegment}Endpoints();");
        }

        sb.AppendLine();
        sb.AppendLine(8, "if (configureJsonSerializerOptions is not null)");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "services.ConfigureAtcRestClientJsonOptions(configureJsonSerializerOptions);");
        sb.AppendLine(8, "}");
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

        foreach (var schemaName in schemaNames.Where(BuiltInErrorContractNames.IsReserved).ToList())
        {
            // RFC 9457's shape is the built-in type itself: nothing is lost, so nothing to say (the recipe that keeps a
            // server's NotFound(string? message) declares exactly that)
            if (BuiltInErrorContractNames.IsRfc9457Shape(schemaName, openApiDoc.Components!.Schemas![schemaName]))
            {
                sharedSchemas.Remove(schemaName);
                continue;
            }

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

    // A TypedClient's buffered downloads: every file download but a streamed one (a model or a file is buffered)
    private static bool HasTypedClientBufferedFileDownload(
        OpenApiDocument openApiDoc)
        => openApiDoc.Paths.Values
            .SelectMany(path => path.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            .Any(operation => (operation.HasFileDownload() && !HttpClientExtractor.IsStreamedFileDownload(operation)) || operation.GetModelOrFileMediaTypes().Count > 0);

    // A TypedClient's streamed downloads (x-return-stream)
    private static bool HasStreamedFileDownload(OpenApiDocument openApiDoc)
        => openApiDoc.Paths.Values
            .SelectMany(path => path.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            .Any(HttpClientExtractor.IsStreamedFileDownload);

    private static void GenerateStreamedFile(
        GeneratedSourceContext context,
        string projectName)
        => context.AddGeneratedSource(
            $"{projectName}.StreamedFile.g.cs",
            ClientRuntimeTypesExtractor.StreamedFile(projectName));

    /// <summary>
    /// Whether any operation answers a model or a file (a 200 that declares both).
    /// </summary>
    private static bool HasModelOrFileResponse(OpenApiDocument openApiDoc)
        => openApiDoc.Paths.Values
            .SelectMany(path => path.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            .Any(operation => operation.GetModelOrFileMediaTypes().Count > 0);

    private static void GenerateContentOrFile(
        GeneratedSourceContext context,
        string projectName)
        => context.AddGeneratedSource(
            $"{projectName}.ContentOrFile.g.cs",
            ClientRuntimeTypesExtractor.ContentOrFile(projectName));

    private static void GenerateDownloadedFile(
        GeneratedSourceContext context,
        string projectName,
        bool includeEndpointResponse)
        => context.AddGeneratedSource(
            $"{projectName}.DownloadedFile.g.cs",
            ClientRuntimeTypesExtractor.DownloadedFile(projectName, includeEndpointResponse));

    private static void GenerateProblemDetails(
        GeneratedSourceContext context,
        string projectName)
        => context.AddGeneratedSource(
            $"{projectName}.ProblemDetails.g.cs",
            ClientRuntimeTypesExtractor.ProblemDetails(projectName));

    private static void GenerateEndpointResultInterface(
        GeneratedSourceContext context,
        string projectName)
        => context.AddGeneratedSource(
            $"{projectName}.IEndpointResult.g.cs",
            ClientRuntimeTypesExtractor.EndpointResultInterface(projectName));

    private static void GenerateProblemDetailsFactory(
        GeneratedSourceContext context,
        string projectName,
        bool hasXml = false)
        => context.AddGeneratedSource(
            $"{projectName}.ProblemDetailsFactory.g.cs",
            ClientRuntimeTypesExtractor.ProblemDetailsFactory(projectName, hasXml));

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
            var enumContent = InlineEnumExtractor.GenerateEnumContent(inlineEnum.EnumParameters, extensibleEnums, enumWireConverter: context.EnumWireConverter, readMemberNames: context.ReadEnumMemberNames);
            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(clientNamespace, inlineEnum.TypeName),
                enumContent);
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
                generatedParamContent);
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
            "System.CodeDom.Compiler",
            "System.ComponentModel",
            "System.ComponentModel.DataAnnotations",
        };

        // Atc.Rest.Client for IFileContent, only where a parameter is a file (a TypedClient project need not reference it)
        if (paramRecord is null || UsingStatementHelper.RecordsUseFileContent([paramRecord]))
        {
            usings.Add("Atc.Rest.Client");
        }

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