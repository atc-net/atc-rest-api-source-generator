// ReSharper disable InvertIf
// ReSharper disable UseWithExpressionToCopyRecord
namespace Atc.Rest.Api.SourceGenerator;

[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "OK.")]
[Generator(LanguageNames.CSharp)]
public class ApiClientGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find marker files for client contracts configuration - THIS IS THE TRIGGER
        var markerFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".atc-rest-api-client-contracts", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) =>
            {
                var content = file.GetText(cancellationToken)?.ToString() ?? "{}";

                ClientConfig config;

                try
                {
                    config = JsonSerializer.Deserialize<ClientConfig>(content, JsonHelper.ConfigOptions) ?? new ClientConfig();
                }
                catch
                {
                    config = new ClientConfig();
                }

                return (file.Path, Config: config);
            })
            .Collect();

        // Register a pipeline to collect ALL OpenAPI YAML files (for multi-part support)
        var yamlFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                                 file.Path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => (Path: file.Path, Content: file.GetText(cancellationToken)?.ToString() ?? string.Empty))
            .Where(static file => !string.IsNullOrEmpty(file.Content))
            .Collect();

        // Check for required package references
        var packageReferencesProvider = context.CompilationProvider
            .Select(static (compilation, _) => new ClientPackageReferences
            {
                HasAtcRestClient = compilation.ReferencedAssemblyNames
                    .Any(a => a.Name.Equals("Atc.Rest.Client", StringComparison.OrdinalIgnoreCase)),
                HasResilience = compilation.ReferencedAssemblyNames
                    .Any(a => a.Name.Equals("Microsoft.Extensions.Http.Resilience", StringComparison.OrdinalIgnoreCase)),
            });

        // Combine ALL YAML files (as collection) with marker files and compilation info
        var combined = yamlFiles.Combine(markerFiles).Combine(packageReferencesProvider);

        // Register source output - processes all YAML files together for multi-part support
        context.RegisterSourceOutput(combined, RegisterSourceOutputAction);
    }

    private static void RegisterSourceOutputAction(
        SourceProductionContext productionContext,
        ((ImmutableArray<(string Path, string Content)> Left,
            ImmutableArray<(string Path, ClientConfig Config)> Right) Left,
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

        var (markerPath, config) = markers.First();

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

        // Identify the base file (non-part file or the first file that is not a part file)
        var baseFile = IdentifyBaseFile(yamlFiles);
        if (baseFile == null)
        {
            return;
        }

        try
        {
            // Check if multi-part specification
            var baseName = Path.GetFileNameWithoutExtension(baseFile.Value.Path);
            var partFiles = yamlFiles
                .Where(f => IsPartFile(f.Path, baseName))
                .ToList();

            if (partFiles.Count > 0)
            {
                // Multi-part mode: merge all files
                GenerateApiClientMultiPart(productionContext, baseFile.Value, partFiles, config, packages);
            }
            else
            {
                // Single file mode
                GenerateApiClient(productionContext, baseFile.Value.Path, baseFile.Value.Content, config, packages);
            }
        }
        catch (Exception ex)
        {
            DiagnosticHelpers.ReportClientGenerationError(productionContext, baseFile.Value.Path, ex);
        }
    }

    /// <summary>
    /// Identifies the base file from the collection of YAML files.
    /// </summary>
    private static (string Path, string Content)? IdentifyBaseFile(
        ImmutableArray<(string Path, string Content)> yamlFiles)
    {
        var files = yamlFiles
            .Select(f => (f.Path, f.Content, Name: Path.GetFileNameWithoutExtension(f.Path)))
            .ToList();

        foreach (var file in files)
        {
            var underscoreIndex = file.Name.LastIndexOf('_');
            if (underscoreIndex <= 0)
            {
                return (file.Path, file.Content);
            }

            var potentialBase = file.Name.Substring(0, underscoreIndex);
            var hasMatchingBase = files.Any(f =>
                f.Name.Equals(potentialBase, StringComparison.OrdinalIgnoreCase));

            if (!hasMatchingBase)
            {
                return (file.Path, file.Content);
            }
        }

        return files
            .OrderBy(f => f.Name.Length)
            .Select(f => ((string Path, string Content)?)(f.Path, f.Content))
            .FirstOrDefault();
    }

    /// <summary>
    /// Checks if a file is a part file for the given base name.
    /// </summary>
    private static bool IsPartFile(
        string filePath,
        string baseName)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return fileName.StartsWith($"{baseName}_", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Generates API client code from a multi-part specification.
    /// </summary>
    private static void GenerateApiClientMultiPart(
        SourceProductionContext context,
        (string Path, string Content) baseFile,
        List<(string Path, string Content)> partFiles,
        ClientConfig config,
        ClientPackageReferences packages)
    {
        // Convert to SpecificationFile objects
        var baseName = Path.GetFileNameWithoutExtension(baseFile.Path);
        var baseSpec = SpecificationService.ReadFromContent(baseFile.Content, baseFile.Path);
        var partSpecs = partFiles
            .Select(p => SpecificationService.ReadFromContent(p.Content, p.Path))
            .ToList();

        // Merge specifications using SpecificationService
        var mergeConfig = config.MultiPartConfiguration ?? MultiPartConfiguration.Default;
        var mergeResult = SpecificationService.MergeSpecifications(baseSpec, partSpecs, mergeConfig);

        // Report merge diagnostics
        foreach (var diagnostic in mergeResult.Diagnostics)
        {
            context.ReportDiagnostic(DiagnosticHelpers.ToRoslynDiagnostic(diagnostic));
        }

        // Stop if merge failed
        if (!mergeResult.IsSuccess || mergeResult.Document == null)
        {
            return;
        }

        // Extract project name from base file path (or use config namespace)
        var projectName = config.Namespace ?? baseName;

        // Continue with normal generation using merged document
        GenerateApiClientFromDocument(context, baseFile.Path, mergeResult.Document, config, packages, projectName);
    }

    /// <summary>
    /// Record to hold client package reference information.
    /// </summary>
    private sealed record ClientPackageReferences
    {
        public bool HasAtcRestClient { get; init; }

        public bool HasResilience { get; init; }
    }

    private static void GenerateApiClient(
        SourceProductionContext context,
        string yamlPath,
        string yamlContent,
        ClientConfig config,
        ClientPackageReferences packages)
    {
        // Parse the OpenAPI YAML content
        var (openApiDoc, openApiDiagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yamlContent, yamlPath);

        if (openApiDoc == null)
        {
            DiagnosticHelpers.ReportClientParsingError(context, yamlPath);
            return;
        }

        // Validate OpenAPI document according to configured strategy
        var validationDiagnostics = OpenApiDocumentValidator.Validate(
            config.ValidateSpecificationStrategy,
            openApiDoc,
            openApiDiagnostic?.Errors ?? Array.Empty<OpenApiError>(),
            yamlPath);

        // Report all validation diagnostics (convert from platform-agnostic to Roslyn)
        foreach (var diagnostic in validationDiagnostics)
        {
            context.ReportDiagnostic(DiagnosticHelpers.ToRoslynDiagnostic(diagnostic));
        }

        // Stop generation if there are validation errors (not warnings)
        if (DiagnosticHelpers.HasErrors(validationDiagnostics))
        {
            return;
        }

        // Extract project name from YAML file path (or use config namespace)
        var projectName = config.Namespace ?? GetProjectNameFromPath(yamlPath);

        // Continue with generation from the parsed document
        GenerateApiClientFromDocument(context, yamlPath, openApiDoc, config, packages, projectName);
    }

    /// <summary>
    /// Generates API client code from a pre-parsed OpenAPI document.
    /// Used by both single-file and multi-part generation flows.
    /// </summary>
    private static void GenerateApiClientFromDocument(
        SourceProductionContext context,
        string yamlPath,
        OpenApiDocument openApiDoc,
        ClientConfig config,
        ClientPackageReferences packages,
        string projectName)
    {
        // Get unique path segments for grouping generated files
        var pathSegments = PathSegmentHelper.GetUniquePathSegments(openApiDoc);

        // Scan for type conflicts once (optimization: O(schemas) instead of O(pathSegments × schemas))
        var conflicts = TypeConflictRegistry.ScanForConflicts(openApiDoc);

        // Create system type conflict resolver for conditional Task qualification
        var modelNames = openApiDoc.Components?.Schemas?.Keys ?? Array.Empty<string>();
        var systemTypeResolver = new SystemTypeConflictResolver(modelNames);

        // Generate shared ProblemDetails once for EndpointPerOperation mode
        if (config.GenerationMode == GenerationModeType.EndpointPerOperation)
        {
            GenerateProblemDetails(context, projectName);
        }

        // Detect shared schemas (used by multiple path segments) for deduplication
        var sharedSchemas = PathSegmentHelper.GetSharedSchemas(openApiDoc);

        // Generate shared models/enums first (under common namespace without segment suffix)
        if (sharedSchemas.Count > 0)
        {
            // Create conflict registry for shared types (no segment)
            var sharedRegistry = TypeConflictRegistry.ForSegment(conflicts, projectName, null);

            GenerateModelsForSchemas(context, openApiDoc, projectName, sharedSchemas, null, sharedRegistry, config.IncludeDeprecated, config.GeneratePartialModels);
            GenerateEnumsForSchemas(context, openApiDoc, projectName, sharedSchemas, null);
            GenerateTuplesForSchemas(context, openApiDoc, projectName, sharedSchemas, null);
        }

        // Track generated path segments for consolidated DI extension
        var generatedPathSegments = new List<string>();

        // Generate files per path segment (excluding shared schemas)
        foreach (var pathSegment in pathSegments)
        {
            // Create conflict registry for this path segment (lightweight: O(1))
            var registry = TypeConflictRegistry.ForSegment(conflicts, projectName, pathSegment);

            // Get segment-specific schemas (excluding shared ones)
            var segmentSchemas = PathSegmentHelper.GetSegmentSpecificSchemas(openApiDoc, pathSegment);

            // Generate segment-specific models (excluding shared schemas)
            // Include shared models using directive so segment types can reference shared types
            if (segmentSchemas.Count > 0)
            {
                GenerateModelsForSchemas(context, openApiDoc, projectName, segmentSchemas, pathSegment, registry, config.IncludeDeprecated, config.GeneratePartialModels, includeSharedModelsUsing: sharedSchemas.Count > 0);
                GenerateEnumsForSchemas(context, openApiDoc, projectName, segmentSchemas, pathSegment);
                GenerateTuplesForSchemas(context, openApiDoc, projectName, segmentSchemas, pathSegment);
            }

            // Generate client code based on generation mode
            if (config.GenerationMode == GenerationModeType.EndpointPerOperation)
            {
                var hasSegmentModels = segmentSchemas.Count > 0;
                var hasSharedModels = sharedSchemas.Count > 0;
                var hasEndpoints = GenerateEndpointPerOperation(context, openApiDoc, projectName, pathSegment, registry, config.IncludeDeprecated, hasSegmentModels, hasSharedModels);
                if (hasEndpoints)
                {
                    generatedPathSegments.Add(pathSegment);
                }
            }
            else
            {
                var hasSegmentModelsTyped = segmentSchemas.Count > 0;
                var hasSharedModelsTyped = sharedSchemas.Count > 0;
                GenerateTypedClient(context, openApiDoc, projectName, pathSegment, registry, systemTypeResolver, config.IncludeDeprecated, hasSegmentModelsTyped, hasSharedModelsTyped);
            }
        }

        // Generate consolidated DI extension method for all path segments (EndpointPerOperation mode only)
        if (config.GenerationMode == GenerationModeType.EndpointPerOperation &&
            generatedPathSegments.Count > 0)
        {
            var consolidatedDiContent = GenerateConsolidatedDiExtension(projectName, generatedPathSegments);
            context.AddSource(
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

            GenerateResiliencePolicies(context, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateResilienceDependencyInjection(context, openApiDoc, projectName, config.IncludeDeprecated);
        }

        // Generate OAuth token management infrastructure (only if OAuth2 is configured and enabled)
        if (config.GenerateOAuthTokenManagement && OAuthConfigExtractor.HasOAuth2Security(openApiDoc))
        {
            GenerateOAuthInfrastructure(context, openApiDoc, projectName);
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
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        var content = ResiliencePoliciesExtractor.Extract(openApiDoc, projectName, includeDeprecated);
        if (content != null)
        {
            context.AddSource(
                $"{projectName}.Resilience.Policies.g.cs",
                SourceText.From(content.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    private static void GenerateResilienceDependencyInjection(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        var content = ResilienceDependencyInjectionExtractor.Extract(openApiDoc, projectName, includeDeprecated);
        if (content != null)
        {
            context.AddSource(
                $"{projectName}.Resilience.DependencyInjection.g.cs",
                SourceText.From(content.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    private static void GenerateOAuthInfrastructure(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName)
    {
        // Extract OAuth configuration from OpenAPI document
        var oauthConfig = OAuthConfigExtractor.Extract(openApiDoc);
        if (oauthConfig == null)
        {
            return;
        }

        // Generate OAuthClientOptions (IOptions<T> configuration)
        var optionsContent = OAuthOptionsExtractor.Extract(oauthConfig, projectName);
        if (optionsContent != null)
        {
            context.AddSource(
                $"{projectName}.OAuth.OAuthClientOptions.g.cs",
                SourceText.From(optionsContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }

        // Generate OAuthTokenResponse model
        var tokenResponseContent = OAuthTokenProviderExtractor.ExtractTokenResponse(projectName);
        context.AddSource(
            $"{projectName}.OAuth.OAuthTokenResponse.g.cs",
            SourceText.From(tokenResponseContent.NormalizeForSourceOutput(), Encoding.UTF8));

        // Generate IOAuthTokenProvider interface
        var interfaceContent = OAuthTokenProviderExtractor.ExtractInterface(projectName);
        context.AddSource(
            $"{projectName}.OAuth.IOAuthTokenProvider.g.cs",
            SourceText.From(interfaceContent.NormalizeForSourceOutput(), Encoding.UTF8));

        // Generate OAuthTokenProvider implementation
        var implementationContent = OAuthTokenProviderExtractor.ExtractImplementation(oauthConfig, projectName);
        if (implementationContent != null)
        {
            context.AddSource(
                $"{projectName}.OAuth.OAuthTokenProvider.g.cs",
                SourceText.From(implementationContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }

        // Generate OAuthAuthenticationHandler
        var handlerContent = OAuthHandlerExtractor.Extract(projectName);
        context.AddSource(
            $"{projectName}.OAuth.OAuthAuthenticationHandler.g.cs",
            SourceText.From(handlerContent.NormalizeForSourceOutput(), Encoding.UTF8));

        // Generate OAuth DI extensions
        var diContent = OAuthDependencyInjectionExtractor.Extract(oauthConfig, projectName);
        if (diContent != null)
        {
            context.AddSource(
                $"{projectName}.OAuth.DependencyInjection.g.cs",
                SourceText.From(diContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    private static string GetProjectNameFromPath(string yamlPath)
        => Path.GetFileNameWithoutExtension(yamlPath);

    /// <summary>
    /// Generates models for specific schemas (used for shared or segment-specific types).
    /// </summary>
    private static void GenerateModelsForSchemas(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemaNames,
        string? pathSegment,
        TypeConflictRegistry registry,
        bool includeDeprecated,
        bool generatePartialModels,
        bool includeSharedModelsUsing = false)
    {
        // Use SchemaExtractor to extract specific schemas
        var recordsParameters = SchemaExtractor.ExtractForSchemas(
            openApiDoc,
            projectName,
            schemaNames,
            pathSegment,
            registry,
            includeDeprecated,
            generatePartialModels,
            includeSharedModelsUsing);

        if (recordsParameters == null || recordsParameters.Parameters.Count == 0)
        {
            return;
        }

        // For client-side, convert IFormFile types to Stream (portable type)
        recordsParameters = ConvertToClientTypes(recordsParameters);

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

            var contentGenerator = new GenerateContentForRecords(
                codeDocGenerator,
                singleRecordParams);

            var generatedContent = contentGenerator.Generate();

            // Sanitize record name for file system (generic types have <> which are invalid)
            var sanitizedName = record.Name.SanitizeForFileName();

            // Use different file name based on whether this is shared or segment-specific
            // Format: {projectName}.{pathSegment?}.{ModelName}.g.cs
            var fileName = string.IsNullOrEmpty(pathSegment)
                ? $"{projectName}.{sanitizedName}.g.cs"
                : $"{projectName}.{pathSegment}.{sanitizedName}.g.cs";

            context.AddSource(
                fileName,
                SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Generates enums for specific schemas (used for shared or segment-specific types).
    /// </summary>
    private static void GenerateEnumsForSchemas(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemaNames,
        string? pathSegment)
    {
        // Use EnumExtractor to extract specific enum schemas
        var enumParametersList = EnumExtractor.ExtractForSchemas(
            openApiDoc,
            projectName,
            schemaNames,
            pathSegment);

        if (enumParametersList == null || enumParametersList.Count == 0)
        {
            return;
        }

        // Generate each enum as a separate file
        foreach (var enumParams in enumParametersList)
        {
            var generatedContent = EnumExtractor.GenerateEnumContent(enumParams);

            // Use different file name based on whether this is shared or segment-specific
            var fileName = string.IsNullOrEmpty(pathSegment)
                ? $"{projectName}.{enumParams.EnumTypeName}.g.cs"
                : $"{projectName}.{pathSegment}.{enumParams.EnumTypeName}.g.cs";

            context.AddSource(
                fileName,
                SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Generates tuple records for specific schemas (used for shared or segment-specific types).
    /// Tuples are schemas with prefixItems (JSON Schema 2020-12 / OpenAPI 3.1).
    /// </summary>
    private static void GenerateTuplesForSchemas(
        SourceProductionContext context,
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

        if (tupleParametersList == null || tupleParametersList.Count == 0)
        {
            return;
        }

        var ns = NamespaceBuilder.ForModels(projectName, pathSegment);

        // Generate each tuple as a separate file
        foreach (var tupleParams in tupleParametersList)
        {
            var generatedContent = TupleExtractor.GenerateTupleContent(tupleParams, ns);

            // Use different file name based on whether this is shared or segment-specific
            var fileName = string.IsNullOrEmpty(pathSegment)
                ? $"{projectName}.{tupleParams.Name}.g.cs"
                : $"{projectName}.{pathSegment}.{tupleParams.Name}.g.cs";

            context.AddSource(
                fileName,
                SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Converts server-side types (IFormFile) to client-side types (Stream) in the records parameters.
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

                // Convert IFormFile to Stream, IFormFileCollection/IFormFile[] to Stream[]
                if (typeName == "IFormFile")
                {
                    typeName = "Stream";
                }
                else if (typeName == "IFormFileCollection" || typeName == "IFormFile[]")
                {
                    typeName = "Stream[]";
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

            convertedRecords.Add(new RecordParameters(
                DocumentationTags: record.DocumentationTags,
                DeclarationModifier: record.DeclarationModifier,
                Name: record.Name,
                Parameters: convertedParams));
        }

        // Update header content to use System.IO instead of Microsoft.AspNetCore.Http
        var headerContent = recordsParameters.HeaderContent ?? string.Empty;
        if (headerContent.Contains("using Microsoft.AspNetCore.Http;", StringComparison.Ordinal))
        {
            headerContent = headerContent.Replace("using Microsoft.AspNetCore.Http;", "using System.IO;");
        }

        return new RecordsParameters(
            HeaderContent: headerContent,
            Namespace: recordsParameters.Namespace,
            DocumentationTags: recordsParameters.DocumentationTags,
            Attributes: recordsParameters.Attributes,
            DeclarationModifier: recordsParameters.DeclarationModifier,
            Parameters: convertedRecords);
    }

    private static void GenerateTypedClient(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        TypeConflictRegistry registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated,
        bool hasSegmentModels,
        bool hasSharedModels)
    {
        // Generate client parameters using shared OperationParameterExtractor (without binding attributes)
        // Each parameter record is generated as a separate file to avoid multiple file-scoped namespace declarations
        var parameterRecords = HttpClientExtractor.ExtractParameters(openApiDoc, projectName, pathSegment, registry, includeDeprecated);
        if (parameterRecords is { Count: > 0 })
        {
            var codeDocGenerator = new CodeDocumentationTagsGenerator();

            foreach (var paramRecord in parameterRecords)
            {
                // Wrap single record in RecordsParameters container with proper header
                var recordsContainer = new RecordsParameters(
                    HeaderContent: BuildClientParameterHeader(projectName, pathSegment, hasSegmentModels, hasSharedModels),
                    Namespace: $"{projectName}.Generated.{pathSegment}.Client",
                    DocumentationTags: null,
                    Attributes: null,
                    DeclarationModifier: DeclarationModifiers.Public,
                    Parameters: [paramRecord]);

                var paramContentGenerator = new GenerateContentForRecords(
                    codeDocGenerator,
                    recordsContainer);

                var generatedParamContent = paramContentGenerator.Generate();

                context.AddSource(
                    $"{projectName}.{pathSegment}.{paramRecord.Name}.g.cs",
                    SourceText.From(generatedParamContent.NormalizeForSourceOutput(), Encoding.UTF8));
            }
        }

        // Use HttpClientExtractor to extract HTTP client class parameters filtered by path segment
        // This also extracts inline schemas for type generation
        var (classParameters, inlineSchemas) = HttpClientExtractor.ExtractWithInlineSchemas(openApiDoc, projectName, pathSegment, registry, systemTypeResolver, includeDeprecated);

        if (classParameters == null)
        {
            return;
        }

        // Use GenerateContentForClass to generate the code
        var codeDocGenerator2 = new CodeDocumentationTagsGenerator();
        var contentGenerator = new GenerateContentForClass(
            codeDocGenerator2,
            classParameters);

        var generatedContent = contentGenerator.Generate();

        context.AddSource(
            $"{projectName}.{pathSegment}.Client.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));

        // Generate inline model files for any inline schemas discovered
        if (inlineSchemas.Count > 0)
        {
            var inlineTypes = CodeGenerationService.GenerateInlineModels(inlineSchemas, projectName, CodeGenerationService.GeneratorType.Client);
            foreach (var inlineType in inlineTypes)
            {
                var inlineContent = CodeGenerationService.FormatAsFile(inlineType);
                context.AddSource(
                    $"{projectName}.{pathSegment}.{inlineType.TypeName}.g.cs",
                    SourceText.From(inlineContent.NormalizeForSourceOutput(), Encoding.UTF8));
            }
        }
    }

    private static bool GenerateEndpointPerOperation(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        TypeConflictRegistry registry,
        bool includeDeprecated,
        bool hasSegmentModels,
        bool hasSharedModels)
    {
        // Generate client parameters using shared OperationParameterExtractor (without binding attributes)
        // Parameters are shared between TypedClient and EndpointPerOperation modes
        var parameterRecords = HttpClientExtractor.ExtractParameters(openApiDoc, projectName, pathSegment, registry, includeDeprecated);
        if (parameterRecords is { Count: > 0 })
        {
            var codeDocGenerator = new CodeDocumentationTagsGenerator();

            foreach (var paramRecord in parameterRecords)
            {
                // Wrap single record in RecordsParameters container with proper header
                var recordsContainer = new RecordsParameters(
                    HeaderContent: BuildClientParameterHeader(projectName, pathSegment, hasSegmentModels, hasSharedModels),
                    Namespace: $"{projectName}.Generated.{pathSegment}.Client",
                    DocumentationTags: null,
                    Attributes: null,
                    DeclarationModifier: DeclarationModifiers.Public,
                    Parameters: [paramRecord]);

                var paramContentGenerator = new GenerateContentForRecords(
                    codeDocGenerator,
                    recordsContainer);

                var generatedParamContent = paramContentGenerator.Generate();

                context.AddSource(
                    $"{projectName}.{pathSegment}.{paramRecord.Name}.g.cs",
                    SourceText.From(generatedParamContent.NormalizeForSourceOutput(), Encoding.UTF8));
            }
        }

        // Generate endpoint files using EndpointPerOperationExtractor (with inline schema support)
        var (operationFiles, inlineSchemas) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            openApiDoc,
            projectName,
            pathSegment,
            registry,
            includeDeprecated,
            customErrorTypeName: null,
            customHttpClientName: null,
            hasSegmentModels: hasSegmentModels,
            hasSharedModels: hasSharedModels);

        // Generate inline model types if any were discovered
        if (inlineSchemas.Count > 0)
        {
            var inlineTypes = CodeGenerationService.GenerateInlineModels(inlineSchemas, projectName);
            foreach (var inlineType in inlineTypes)
            {
                var inlineContent = CodeGenerationService.FormatAsFile(inlineType);
                context.AddSource(
                    $"{projectName}.{pathSegment}.{inlineType.TypeName}.g.cs",
                    SourceText.From(inlineContent.NormalizeForSourceOutput(), Encoding.UTF8));
            }
        }

        var endpointNames = new List<string>();

        foreach (var opFiles in operationFiles)
        {
            endpointNames.Add(opFiles.OperationName);

            // Endpoint Interface
            context.AddSource(
                opFiles.EndpointInterfaceFileName,
                SourceText.From(opFiles.EndpointInterfaceContent.NormalizeForSourceOutput(), Encoding.UTF8));

            // Endpoint Class
            context.AddSource(
                opFiles.EndpointClassFileName,
                SourceText.From(opFiles.EndpointClassContent.NormalizeForSourceOutput(), Encoding.UTF8));

            // Result Interface (null for binary endpoints that use BinaryEndpointResponse)
            if (opFiles.ResultInterfaceFileName != null && opFiles.ResultInterfaceContent != null)
            {
                context.AddSource(
                    opFiles.ResultInterfaceFileName,
                    SourceText.From(opFiles.ResultInterfaceContent.NormalizeForSourceOutput(), Encoding.UTF8));
            }

            // Result Class (null for binary endpoints that use BinaryEndpointResponse)
            if (opFiles.ResultClassFileName != null && opFiles.ResultClassContent != null)
            {
                context.AddSource(
                    opFiles.ResultClassFileName,
                    SourceText.From(opFiles.ResultClassContent.NormalizeForSourceOutput(), Encoding.UTF8));
            }
        }

        // Generate DI extension method
        if (endpointNames.Count > 0)
        {
            var diExtensionContent = GenerateEndpointDiExtension(projectName, pathSegment, endpointNames);
            context.AddSource(
                $"{projectName}.{pathSegment}.Endpoints.DependencyInjection.g.cs",
                SourceText.From(diExtensionContent.NormalizeForSourceOutput(), Encoding.UTF8));
            return true;
        }

        return false;
    }

    private static string GenerateEndpointDiExtension(
        string projectName,
        string pathSegment,
        List<string> endpointNames)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Atc.Rest.Client.Options;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine($"using {projectName}.Generated.{pathSegment}.Endpoints;");
        sb.AppendLine($"using {projectName}.Generated.{pathSegment}.Endpoints.Interfaces;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Generated.{pathSegment};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Extension methods for registering {pathSegment} API endpoints in the dependency injection container.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class EndpointsServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, $"/// Registers all {pathSegment} API endpoint implementations.");
        sb.AppendLine(4, "/// Also ensures IHttpMessageFactory and IContractSerializer are registered (from Atc.Rest.Client).");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "/// <param name=\"services\">The service collection.</param>");
        sb.AppendLine(4, "/// <returns>The service collection for method chaining.</returns>");
        sb.AppendLine(4, $"public static IServiceCollection Add{pathSegment}Endpoints(this IServiceCollection services)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "// Ensure Atc.Rest.Client core services are registered (IHttpMessageFactory, IContractSerializer)");
        sb.AppendLine(8, "services.AddAtcRestClientCore();");
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
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");

        // Add using statements for each path segment namespace
        foreach (var pathSegment in pathSegments)
        {
            sb.AppendLine($"using {projectName}.Generated.{pathSegment};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Extension methods for registering all {projectName} API endpoints in the dependency injection container.");
        sb.AppendLine("/// </summary>");
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
    /// Generates a ProblemDetails record for RFC 7807 error responses.
    /// This type is shared across all path segments and used by endpoint result classes for error content.
    /// </summary>
    private static void GenerateProblemDetails(
        SourceProductionContext context,
        string projectName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Generic;");
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
        sb.AppendLine("public record ProblemDetails(");
        sb.AppendLine(4, "string? Type,");
        sb.AppendLine(4, "string? Title,");
        sb.AppendLine(4, "int? Status,");
        sb.AppendLine(4, "string? Detail,");
        sb.AppendLine(4, "string? Instance);");
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
        sb.AppendLine("public record ValidationProblemDetails(");
        sb.AppendLine(4, "string? Type,");
        sb.AppendLine(4, "string? Title,");
        sb.AppendLine(4, "int? Status,");
        sb.AppendLine(4, "string? Detail,");
        sb.AppendLine(4, "string? Instance,");
        sb.AppendLine(4, "Dictionary<string, string[]>? Errors);");

        context.AddSource(
            $"{projectName}.ProblemDetails.g.cs",
            SourceText.From(sb.ToString().NormalizeForSourceOutput(), Encoding.UTF8));
    }

    /// <summary>
    /// Builds the header content for client parameter files.
    /// </summary>
    private static string BuildClientParameterHeader(
        string projectName,
        string pathSegment,
        bool hasSegmentModels,
        bool hasSharedModels)
    {
        var sb = new StringBuilder();
        sb.Append("// <auto-generated />\r\n#nullable enable\r\n\r\n");
        sb.Append("using System.ComponentModel;\r\n");
        sb.Append("using System.ComponentModel.DataAnnotations;\r\n");
        sb.Append("using System.IO;\r\n");

        // Add shared Models using if there are shared models
        if (hasSharedModels)
        {
            sb.Append($"using {projectName}.Generated.Models;\r\n");
        }

        // Add segment-specific Models using if there are segment-specific models
        if (hasSegmentModels)
        {
            sb.Append($"using {projectName}.Generated.{pathSegment}.Models;\r\n");
        }

        sb.Append("\r\n");
        return sb.ToString();
    }
}