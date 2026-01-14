// ReSharper disable UnusedVariable
// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
namespace Atc.Rest.Api.SourceGenerator;

[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "OK.")]
[Generator(LanguageNames.CSharp)]
public class ApiServerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find marker files - marker file presence IS the trigger for this generator
        var markerFiles = context.AdditionalTextsProvider
            .Where(static file => Path.GetFileName(file.Path).Equals(Constants.MarkerFile.Server, StringComparison.OrdinalIgnoreCase) ||
                                  Path.GetFileName(file.Path).Equals(Constants.MarkerFile.ServerJson, StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) =>
            {
                var content = file.GetText(cancellationToken)?.ToString() ?? "{}";

                ServerConfig config;

                try
                {
                    config = JsonSerializer.Deserialize<ServerConfig>(content, JsonHelper.ConfigOptions) ?? new ServerConfig();
                }
                catch
                {
                    config = new ServerConfig();
                }

                return (file.Path, Config: config);
            })
            .Collect();

        // Register a pipeline to collect ALL OpenAPI YAML files (for multi-parts support)
        var yamlFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                                 file.Path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => (file.Path, Content: file.GetText(cancellationToken)?.ToString() ?? string.Empty))
            .Where(static file => !string.IsNullOrEmpty(file.Content))
            .Collect();

        // Check for ASP.NET Core references (required for compilation)
        var hasAspNetCoreProvider = context.CompilationProvider
            .Select(static (compilation, _) => compilation.ReferencedAssemblyNames
                .Any(a => a.Name.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase)));

        // Check for Atc.Rest.MinimalApi package reference (for IEndpointDefinition interface)
        var hasMinimalApiProvider = context.CompilationProvider
            .Select(static (compilation, _) => compilation.ReferencedAssemblyNames
                .Any(a => a.Name.Equals("Atc.Rest.MinimalApi", StringComparison.OrdinalIgnoreCase)));

        // Combine ALL YAML files (as collection) with marker files and compilation info
        var combined = yamlFiles
            .Combine(markerFiles)
            .Combine(hasAspNetCoreProvider)
            .Combine(hasMinimalApiProvider);

        // Register source output - processes all YAML files together for multi-parts support
        context.RegisterSourceOutput(combined, RegisterSourceOutputAction);
    }

    private static void RegisterSourceOutputAction(
        SourceProductionContext productionContext,
        (((ImmutableArray<(string Path, string Content)> Left,
            ImmutableArray<(string Path, ServerConfig Config)> Right) Left,
            bool Right) Left,
            bool Right) combinedData)
    {
        var (((yamlFiles, markers), hasAspNetCore), hasMinimalApi) = combinedData;

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

        // Identify the base file (non-part file or the first file that is not a part file)
        // Part files follow the naming convention: {BaseName}_{PartName}.yaml
        var baseFile = IdentifyBaseFile(yamlFiles);
        if (baseFile == null)
        {
            return;
        }

        try
        {
            // Check if multi-parts specification
            var baseName = Path.GetFileNameWithoutExtension(baseFile.Value.Path);
            var partFiles = yamlFiles
                .Where(f => IsPartFile(f.Path, baseName))
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
                    useGlobalErrorHandler);
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
                    useGlobalErrorHandler);
            }
        }
        catch (Exception ex)
        {
            DiagnosticHelpers.ReportServerGenerationError(productionContext, baseFile.Value.Path, ex);
        }
    }

    /// <summary>
    /// Identifies the base file from the collection of YAML files.
    /// The base file is the one that doesn't follow the part file naming convention.
    /// </summary>
    private static (string Path, string Content)? IdentifyBaseFile(
        ImmutableArray<(string Path, string Content)> yamlFiles)
    {
        // Get all file names without extension
        var files = yamlFiles
            .Select(f => (f.Path, f.Content, Name: Path.GetFileNameWithoutExtension(f.Path)))
            .ToList();

        // Find files that are not part files (don't contain underscore that indicates part)
        // A base file either:
        // 1. Has no underscore in the name
        // 2. The part before the underscore doesn't match any other file name
        foreach (var file in files)
        {
            var underscoreIndex = file.Name.LastIndexOf('_');
            if (underscoreIndex <= 0)
            {
                // No underscore - this is likely the base file
                return (file.Path, file.Content);
            }

            // Check if the part before underscore matches another file
            var potentialBase = file.Name.Substring(0, underscoreIndex);
            var hasMatchingBase = files.Any(f =>
                f.Name.Equals(potentialBase, StringComparison.OrdinalIgnoreCase));

            if (!hasMatchingBase)
            {
                // No matching base file, so this could be the base
                return (file.Path, file.Content);
            }
        }

        // If all files look like parts, take the shortest name as base
        return files
            .OrderBy(f => f.Name.Length)
            .Select(f => ((string Path, string Content)?)(f.Path, f.Content))
            .FirstOrDefault();
    }

    /// <summary>
    /// Checks if a file is a part file for the given base name.
    /// Part files follow the naming convention: {BaseName}_{PartName}.yaml
    /// </summary>
    private static bool IsPartFile(
        string filePath,
        string baseName)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return fileName.StartsWith($"{baseName}_", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Generates API server code from a multi-parts specification.
    /// Merges all part files with the base file before generation.
    /// </summary>
    private static void GenerateApiServerMultiPart(
        SourceProductionContext context,
        (string Path, string Content) baseFile,
        List<(string Path, string Content)> partFiles,
        ServerConfig config,
        bool useMinimalApiPackage,
        bool useValidationFilter,
        bool useGlobalErrorHandler)
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
        GenerateApiServerFromDocument(
            context,
            baseFile.Path,
            mergeResult.Document,
            config,
            projectName,
            useMinimalApiPackage,
            useValidationFilter,
            useGlobalErrorHandler);
    }

    private static void GenerateApiServer(
        SourceProductionContext context,
        string yamlPath,
        string yamlContent,
        ServerConfig config,
        bool useMinimalApiPackage,
        bool useValidationFilter,
        bool useGlobalErrorHandler)
    {
        // Parse the OpenAPI YAML content
        var (openApiDoc, openApiDiagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yamlContent, yamlPath);

        if (openApiDoc == null)
        {
            DiagnosticHelpers.ReportServerParsingError(context, yamlPath);
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
        GenerateApiServerFromDocument(
            context,
            yamlPath,
            openApiDoc,
            config,
            projectName,
            useMinimalApiPackage,
            useValidationFilter,
            useGlobalErrorHandler);
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
        bool useGlobalErrorHandler)
    {
        // Get unique path segments for grouping generated files
        var pathSegments = PathSegmentHelper.GetUniquePathSegments(openApiDoc);

        // Scan for type conflicts once (optimization: O(schemas) instead of O(pathSegments × schemas))
        var conflicts = TypeConflictRegistry.ScanForConflicts(openApiDoc);

        // Create system type conflict resolver for conditional Task qualification
        var modelNames = openApiDoc.Components?.Schemas?.Keys ?? [];
        var systemTypeResolver = new SystemTypeConflictResolver(modelNames);

        // Detect shared schemas (used by multiple path segments) for deduplication
        var sharedSchemas = PathSegmentHelper.GetSharedSchemas(openApiDoc);

        // Generate shared models/enums first (under common namespace without segment suffix)
        if (sharedSchemas.Count > 0)
        {
            var sharedRegistry = TypeConflictRegistry.ForSegment(conflicts, projectName);
            GenerateModelsForSchemas(
                context,
                openApiDoc,
                projectName,
                sharedSchemas,
                pathSegment: null,
                sharedRegistry,
                config.IncludeDeprecated,
                config.GeneratePartialModels);

            GenerateEnumsForSchemas(
                context,
                openApiDoc,
                projectName,
                sharedSchemas,
                pathSegment: null);

            GenerateTuplesForSchemas(
                context,
                openApiDoc,
                projectName,
                sharedSchemas,
                pathSegment: null);
        }

        // Generate files per path segment (excluding shared schemas)
        foreach (var pathSegment in pathSegments)
        {
            // Create conflict registry for this path segment (lightweight: O(1))
            var registry = TypeConflictRegistry.ForSegment(conflicts, projectName, pathSegment);

            // Get segment-specific schemas (excluding shared ones)
            var segmentSchemas = PathSegmentHelper.GetSegmentSpecificSchemas(openApiDoc, pathSegment);

            // Generate segment-specific models (excluding shared ones)
            // Include shared models using directive so segment types can reference shared types
            if (segmentSchemas.Count > 0)
            {
                GenerateModelsForSchemas(
                    context,
                    openApiDoc,
                    projectName,
                    segmentSchemas,
                    pathSegment,
                    registry,
                    config.IncludeDeprecated,
                    config.GeneratePartialModels,
                    includeSharedModelsUsing: sharedSchemas.Count > 0);

                GenerateEnumsForSchemas(
                    context,
                    openApiDoc,
                    projectName,
                    segmentSchemas,
                    pathSegment);

                GenerateTuplesForSchemas(
                    context,
                    openApiDoc,
                    projectName,
                    segmentSchemas,
                    pathSegment);
            }

            // Determine if this segment has its own models namespace
            var hasSegmentModels = segmentSchemas.Count > 0;

            // Generate parameter classes
            GenerateParameterClasses(
                context,
                openApiDoc,
                projectName,
                pathSegment,
                registry,
                config.IncludeDeprecated,
                includeSharedModelsUsing: sharedSchemas.Count > 0,
                includeSegmentModelsUsing: hasSegmentModels);

            // Generate result classes
            GenerateResultClasses(
                context,
                openApiDoc,
                projectName,
                pathSegment,
                registry,
                systemTypeResolver,
                config.IncludeDeprecated,
                includeSharedModelsUsing: sharedSchemas.Count > 0,
                includeSegmentModelsUsing: hasSegmentModels);

            // Generate handler interfaces
            GenerateHandlerInterfaces(
                context,
                openApiDoc,
                projectName,
                pathSegment,
                systemTypeResolver,
                config.IncludeDeprecated);

            // Generate endpoint registrations
            GenerateEndpointRegistrations(
                context,
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
                includeSharedModelsUsing: sharedSchemas.Count > 0);
        }

        // Generate combined endpoint mapping extension (calls all path segment endpoint methods)
        GenerateCombinedEndpointMapping(context, projectName, pathSegments);

        // Generate DI registration (not segmented - registers all handlers)
        GenerateDependencyInjection(context, openApiDoc, projectName, pathSegments, config.IncludeDeprecated);

        // Generate versioning DI registration (only if versioning is enabled)
        if (config.VersioningStrategy != VersioningStrategyType.None)
        {
            GenerateVersioningDependencyInjection(context, projectName, config);
        }

        // Generate security policies and DI extensions (only if OAuth2 scopes exist)
        // Note: Package availability checks are not performed here as Contracts projects
        // don't need the packages directly - consuming API projects will need them
        if (openApiDoc.HasSecuritySchemes())
        {
            GenerateSecurityPolicies(context, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateSecurityDependencyInjection(context, openApiDoc, projectName, config.IncludeDeprecated);
        }

        // Generate OpenID Connect authentication extension (only if OpenID Connect is configured)
        if (openApiDoc.HasOpenIdConnectSecurity())
        {
            GenerateOpenIdConnectAuthentication(context, openApiDoc, projectName);
        }

        // Generate rate limiting policies and DI extensions (only if rate limiting is configured)
        // Note: Package availability checks are not performed here as Contracts projects
        // don't need the packages directly - consuming API projects will need them
        if (openApiDoc.HasRateLimiting())
        {
            GenerateRateLimitPolicies(context, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateRateLimitDependencyInjection(context, openApiDoc, projectName, config.IncludeDeprecated);
        }

        // Generate Output Caching policies and DI extensions (only if output caching is configured)
        if (openApiDoc.HasOutputCaching())
        {
            GenerateOutputCachePolicies(context, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateOutputCachingDependencyInjection(context, openApiDoc, projectName, config.IncludeDeprecated);
        }

        // Generate HybridCache policies and DI extensions (only if hybrid caching is configured)
        if (openApiDoc.HasHybridCaching())
        {
            GenerateHybridCachePolicies(context, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateHybridCachingDependencyInjection(context, openApiDoc, projectName, config.IncludeDeprecated);
        }

        // Generate webhook handlers, parameters, results, endpoints, and DI (only if webhooks exist and enabled)
        if (config.GenerateWebhooks && openApiDoc.HasWebhooks())
        {
            GenerateWebhookHandlerInterfaces(context, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateWebhookParameterClasses(context, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateWebhookResultClasses(context, openApiDoc, projectName, config.IncludeDeprecated);
            GenerateWebhookEndpoints(context, openApiDoc, projectName, config);
            GenerateWebhookDependencyInjection(context, openApiDoc, projectName, config.IncludeDeprecated);
        }

        // Generate WebApplication extensions (GlobalErrorHandler middleware setup)
        if (useGlobalErrorHandler)
        {
            GenerateWebApplicationExtensions(context, projectName, useGlobalErrorHandler);
        }

        // Generate simplified API surface (only if global error handler is enabled, which implies MinimalApi package)
        if (useGlobalErrorHandler)
        {
            GenerateApiOptions(context, openApiDoc, projectName, config);
            GenerateUnifiedServiceCollection(context, openApiDoc, projectName, config, pathSegments);
            GenerateUnifiedWebApplicationExtensions(context, openApiDoc, projectName, config);
        }

        // Report generation summary
        var operationCount = openApiDoc.Paths?.Sum(p => p.Value.Operations?.Count ?? 0) ?? 0;
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

    private static string GetProjectNameFromPath(string yamlPath)
    {
        // Extract the file name without extension (e.g., "PetStoreSimple" from "PetStoreSimple.yaml")
        var fileName = Path.GetFileNameWithoutExtension(yamlPath);
        return fileName;
    }

    /// <summary>
    /// Generates models for a specific set of schemas.
    /// Used for both shared types (pathSegment = null) and segment-specific types.
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
        // Use SchemaExtractor.ExtractForSchemas to extract only specific schemas
        var recordsParameters = SchemaExtractor.ExtractForSchemas(openApiDoc, projectName, schemaNames, pathSegment, registry, includeDeprecated, generatePartialModels, includeSharedModelsUsing);

        if (recordsParameters == null || recordsParameters.Parameters.Count == 0)
        {
            return;
        }

        var codeDocGenerator = new CodeDocumentationTagsGenerator();

        // Generate each model as a separate file
        foreach (var record in recordsParameters.Parameters)
        {
            // Create a single-record container with the same header and namespace
            var singleRecordParams = recordsParameters with { Parameters = [record] };

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
    /// Generates enums for a specific set of schemas.
    /// Used for both shared types (pathSegment = null) and segment-specific types.
    /// </summary>
    private static void GenerateEnumsForSchemas(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemaNames,
        string? pathSegment)
    {
        // Use EnumExtractor.ExtractForSchemas to extract only specific enums
        var enumParametersList = EnumExtractor.ExtractForSchemas(openApiDoc, projectName, schemaNames, pathSegment);

        if (enumParametersList == null || enumParametersList.Count == 0)
        {
            return;
        }

        // Use "Shared" for file name when no path segment (shared types)
        var fileNameSegment = pathSegment ?? "Shared";

        // Generate each enum as a separate file
        foreach (var enumParams in enumParametersList)
        {
            var generatedContent = EnumExtractor.GenerateEnumContent(enumParams);

            context.AddSource(
                $"{projectName}.{fileNameSegment}.{enumParams.EnumTypeName}.g.cs",
                SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Generates tuple records for a specific set of schemas.
    /// Tuples are schemas with prefixItems (JSON Schema 2020-12 / OpenAPI 3.1).
    /// </summary>
    private static void GenerateTuplesForSchemas(
        SourceProductionContext context,
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

        if (tupleParametersList == null || tupleParametersList.Count == 0)
        {
            return;
        }

        // Use "Shared" for file name when no path segment (shared types)
        var fileNameSegment = pathSegment ?? "Shared";
        var ns = NamespaceBuilder.ForModels(projectName, pathSegment);

        // Generate each tuple as a separate file
        foreach (var tupleParams in tupleParametersList)
        {
            var generatedContent = TupleExtractor.GenerateTupleContent(tupleParams, ns);

            context.AddSource(
                $"{projectName}.{fileNameSegment}.{tupleParams.Name}.g.cs",
                SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    private static void GenerateHandlerInterfaces(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated)
    {
        // Use HandlerExtractor to extract operations into InterfaceParameters list filtered by path segment
        var interfacesList = HandlerExtractor.Extract(openApiDoc, projectName, pathSegment, systemTypeResolver, includeDeprecated);

        if (interfacesList == null || interfacesList.Count == 0)
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
        allGeneratedCode.AppendSegmentUsings(projectName, pathSegment, namespaces, includeHandlers: false);

        allGeneratedCode.AppendLine();
        allGeneratedCode.AppendLine($"namespace {projectName}.Generated.{pathSegment}.Handlers;");
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

        context.AddSource(
            $"{projectName}.{pathSegment}.Handlers.g.cs",
            SourceText.From(normalizeForSourceOutput, Encoding.UTF8));
    }

    private static void GenerateEndpointRegistrations(
        SourceProductionContext context,
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
        bool includeSharedModelsUsing = false)
    {
        // Use EndpointDefinitionExtractor to extract interface and class parameters filtered by path segment
        var (interfaceParams, classParameters) = EndpointDefinitionExtractor.Extract(openApiDoc, projectName, pathSegment, registry, systemTypeResolver, subFolderStrategy, includeDeprecated, useMinimalApiPackage, useValidationFilter, versioningStrategy, defaultApiVersion, useServersBasePath);

        if (interfaceParams == null && (classParameters == null || classParameters.Count == 0))
        {
            return;
        }

        // Get namespace availability for this segment
        var namespaces = PathSegmentHelper.GetPathSegmentNamespaces(openApiDoc, pathSegment);

        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var allGeneratedCode = new StringBuilder();

        // Generate header and usings once
        allGeneratedCode.AppendLine("// <auto-generated />");
        allGeneratedCode.AppendLine("#nullable enable");
        allGeneratedCode.AppendLine();
        allGeneratedCode.AppendLine("using System.CodeDom.Compiler;");
        allGeneratedCode.AppendLine("using System.Threading;");
        allGeneratedCode.AppendLine("using System.Threading.Tasks;");

        // Add package namespace when using Atc.Rest.MinimalApi's IEndpointDefinition
        if (useMinimalApiPackage)
        {
            allGeneratedCode.AppendLine("using Atc.Rest.MinimalApi.Abstractions;");
        }

        // Add ValidationFilter namespace when using validation
        if (useValidationFilter)
        {
            allGeneratedCode.AppendLine("using Atc.Rest.MinimalApi.Filters.Endpoints;");
        }

        // Add versioning namespace when versioning is enabled
        if (versioningStrategy != VersioningStrategyType.None)
        {
            allGeneratedCode.AppendLine("using Asp.Versioning;");
        }

        // Add output caching namespace when output caching is used
        if (openApiDoc.HasOutputCaching())
        {
            allGeneratedCode.AppendLine("using Microsoft.AspNetCore.OutputCaching;");
            allGeneratedCode.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        }

        allGeneratedCode.AppendLine("using Microsoft.AspNetCore.Builder;");
        allGeneratedCode.AppendLine("using Microsoft.AspNetCore.Http;");
        allGeneratedCode.AppendLine("using Microsoft.AspNetCore.Mvc;");

        // Only include shared models using if there are shared schemas
        if (includeSharedModelsUsing)
        {
            allGeneratedCode.AppendLine($"using {projectName}.Generated.Models;");
        }

        // Add conditional segment namespace usings
        allGeneratedCode.AppendSegmentUsings(projectName, pathSegment, namespaces);

        // Add caching namespace when output caching is used
        if (openApiDoc.HasOutputCaching())
        {
            allGeneratedCode.AppendLine($"using {projectName}.Generated.Caching;");
        }

        allGeneratedCode.AppendLine();
        allGeneratedCode.AppendLine($"namespace {projectName}.Generated.{pathSegment}.Endpoints;");
        allGeneratedCode.AppendLine();

        // Generate the IEndpointDefinition interface only if not using Atc.Rest.MinimalApi package
        if (interfaceParams != null && !useMinimalApiPackage)
        {
            var interfaceGenerator = new GenerateContentForInterface(
                codeDocGenerator,
                interfaceParams);

            var generatedInterface = interfaceGenerator.Generate();

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

        // Generate each endpoint definition class and collect class names for the extension method
        var endpointDefinitionClassNames = new List<string>();
        if (classParameters != null)
        {
            foreach (var classParams in classParameters)
            {
                endpointDefinitionClassNames.Add(classParams.ClassTypeName);

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
        }

        // Generate the endpoint mapping extension class (MapApiEndpoints extension method)
        var extensionParams = EndpointRegistrationExtractor.ExtractEndpointMappingExtension(
            projectName,
            pathSegment,
            endpointDefinitionClassNames);

        if (extensionParams != null)
        {
            var extensionGenerator = new GenerateContentForClass(
                codeDocGenerator,
                extensionParams);

            var generatedExtension = extensionGenerator.Generate();

            // Skip the header part and append just the class
            var lines = generatedExtension.SplitIntoLinesPreserveEmpty();
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

        context.AddSource(
            $"{projectName}.{pathSegment}.Endpoints.g.cs",
            SourceText.From(normalizeForSourceOutput, Encoding.UTF8));
    }

    private static void GenerateCombinedEndpointMapping(
        SourceProductionContext context,
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

        // Add usings for each path segment's endpoints namespace
        foreach (var segment in pathSegments.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"using {projectName}.Generated.{segment}.Endpoints;");
        }

        builder.AppendLine();
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

        // Call each path segment's endpoint mapping method
        foreach (var segment in pathSegments.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine(8, $"app.Map{segment}Endpoints();");
        }

        builder.AppendLine();
        builder.AppendLine(8, "return app;");
        builder.AppendLine(4, "}");
        builder.AppendLine("}");

        var normalizeForSourceOutput = builder
            .ToString()
            .NormalizeForSourceOutput();

        context.AddSource(
            $"{projectName}.EndpointMapping.g.cs",
            SourceText.From(normalizeForSourceOutput, Encoding.UTF8));
    }

    private static void GenerateDependencyInjection(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        List<string> pathSegments,
        bool includeDeprecated)
    {
        // Use ServerDependencyInjectionExtractor to extract class parameters with path segments
        var classParameters = ServerDependencyInjectionExtractor.Extract(openApiDoc, projectName, pathSegments, includeDeprecated);

        if (classParameters == null)
        {
            return;
        }

        // Use GenerateContentForClass to generate the code
        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var contentGenerator = new GenerateContentForClass(
            codeDocGenerator,
            classParameters);

        var generatedContent = contentGenerator.Generate();

        context.AddSource(
            $"{projectName}.DependencyInjection.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    private static void GenerateWebApplicationExtensions(
        SourceProductionContext context,
        string projectName,
        bool useGlobalErrorHandler)
    {
        // Use WebApplicationExtensionsExtractor to generate middleware setup helpers
        var classParameters = WebApplicationExtensionsExtractor.Extract(projectName, useGlobalErrorHandler);

        if (classParameters == null)
        {
            return;
        }

        // Use GenerateContentForClass to generate the code
        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var contentGenerator = new GenerateContentForClass(
            codeDocGenerator,
            classParameters);

        var generatedContent = contentGenerator.Generate();

        context.AddSource(
            $"{projectName}.WebApplicationExtensions.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    private static void GenerateParameterClasses(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        TypeConflictRegistry registry,
        bool includeDeprecated,
        bool includeSharedModelsUsing = false,
        bool includeSegmentModelsUsing = true)
    {
        // Use OperationParameterExtractor to extract operation parameters into RecordsParameters filtered by path segment
        var recordsParams = OperationParameterExtractor.Extract(openApiDoc, projectName, pathSegment, registry, includeDeprecated, includeSharedModelsUsing, includeSegmentModelsUsing);

        if (recordsParams == null || recordsParams.Parameters.Count == 0)
        {
            return;
        }

        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var contentGenerator = new GenerateContentForRecords(codeDocGenerator, recordsParams);
        var generatedContent = contentGenerator.Generate();

        context.AddSource(
            $"{projectName}.{pathSegment}.Parameters.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    private static void GenerateResultClasses(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        TypeConflictRegistry registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated,
        bool includeSharedModelsUsing = false,
        bool includeSegmentModelsUsing = true)
    {
        // Use ResultClassExtractor to extract class parameters filtered by path segment
        // This also extracts inline schemas for type generation
        var (classesList, inlineSchemas) = ResultClassExtractor.ExtractWithInlineSchemas(openApiDoc, projectName, pathSegment, registry, systemTypeResolver, includeDeprecated);

        if (classesList == null || classesList.Count == 0)
        {
            return;
        }

        // Generate inline model files for any inline schemas discovered
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

        // Only include segment-specific models using if there are segment-specific models
        if (includeSegmentModelsUsing)
        {
            allGeneratedCode.AppendLine($"using {projectName}.Generated.{pathSegment}.Models;");
        }

        allGeneratedCode.AppendLine();
        allGeneratedCode.AppendLine($"namespace {projectName}.Generated.{pathSegment}.Results;");
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

        var normalizeForSourceOutput = allGeneratedCode
            .ToString()
            .NormalizeForSourceOutput();

        context.AddSource(
            $"{projectName}.{pathSegment}.Results.g.cs",
            SourceText.From(normalizeForSourceOutput, Encoding.UTF8));
    }

    private static void GenerateSecurityPolicies(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        // Use SecurityPoliciesExtractor to extract security policy constants
        var generatedContent = SecurityPoliciesExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (generatedContent == null)
        {
            return;
        }

        context.AddSource(
            $"{projectName}.Security.Policies.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    private static void GenerateSecurityDependencyInjection(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        // Use SecurityDependencyInjectionExtractor to extract security DI extension
        var generatedContent = SecurityDependencyInjectionExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (generatedContent == null)
        {
            return;
        }

        context.AddSource(
            $"{projectName}.Security.DependencyInjection.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    private static void GenerateRateLimitPolicies(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        // Use RateLimitPoliciesExtractor to extract rate limit policy constants
        var generatedContent = RateLimitPoliciesExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (generatedContent == null)
        {
            return;
        }

        context.AddSource(
            $"{projectName}.RateLimiting.Policies.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    private static void GenerateRateLimitDependencyInjection(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        // Use RateLimitDependencyInjectionExtractor to extract rate limit DI extension
        var generatedContent = RateLimitDependencyInjectionExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (generatedContent == null)
        {
            return;
        }

        context.AddSource(
            $"{projectName}.RateLimiting.DependencyInjection.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    private static void GenerateOutputCachePolicies(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        // Use OutputCachePoliciesExtractor to extract output cache policy constants
        var generatedContent = OutputCachePoliciesExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (generatedContent == null)
        {
            return;
        }

        context.AddSource(
            $"{projectName}.OutputCaching.Policies.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    private static void GenerateOutputCachingDependencyInjection(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        // Use OutputCacheDependencyInjectionExtractor to extract output caching DI extension
        var generatedContent = OutputCacheDependencyInjectionExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (generatedContent == null)
        {
            return;
        }

        context.AddSource(
            $"{projectName}.OutputCaching.DependencyInjection.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    private static void GenerateHybridCachePolicies(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        // Use HybridCachePoliciesExtractor to extract HybridCache policy constants
        var generatedContent = HybridCachePoliciesExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (generatedContent == null)
        {
            return;
        }

        context.AddSource(
            $"{projectName}.Caching.Policies.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    private static void GenerateHybridCachingDependencyInjection(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        // Use HybridCacheDependencyInjectionExtractor to extract HybridCache DI extension
        var generatedContent = HybridCacheDependencyInjectionExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (generatedContent == null)
        {
            return;
        }

        context.AddSource(
            $"{projectName}.Caching.DependencyInjection.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    private static void GenerateOpenIdConnectAuthentication(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName)
    {
        // Extract OpenID Connect configuration
        var oidcConfig = OpenIdConnectConfigExtractor.Extract(openApiDoc);

        if (oidcConfig == null)
        {
            return;
        }

        // Generate OpenID Connect DI extension
        var generatedContent = OpenIdConnectDependencyInjectionExtractor.Extract(oidcConfig, projectName);

        if (generatedContent == null)
        {
            return;
        }

        context.AddSource(
            $"{projectName}.Authentication.OpenIdConnect.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    private static void GenerateVersioningDependencyInjection(
        SourceProductionContext context,
        string projectName,
        ServerConfig config)
    {
        // Use VersioningDependencyInjectionExtractor to extract versioning DI extension
        var classParameters = VersioningDependencyInjectionExtractor.Extract(projectName, config);

        if (classParameters == null)
        {
            return;
        }

        // Use GenerateContentForClass to generate the code
        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var contentGenerator = new GenerateContentForClass(
            codeDocGenerator,
            classParameters);

        var generatedContent = contentGenerator.Generate();

        context.AddSource(
            $"{projectName}.Versioning.DependencyInjection.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    /// <summary>
    /// Generates ApiServiceOptions and ApiMiddlewareOptions classes for simplified API configuration.
    /// </summary>
    private static void GenerateApiOptions(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config)
    {
        // Generate ApiServiceOptions
        var serviceOptionsContent = ApiOptionsExtractor.ExtractServiceOptions(openApiDoc, projectName, config);
        context.AddSource(
            $"{projectName}.ApiServiceOptions.g.cs",
            SourceText.From(serviceOptionsContent.NormalizeForSourceOutput(), Encoding.UTF8));

        // Generate ApiMiddlewareOptions
        var middlewareOptionsContent = ApiOptionsExtractor.ExtractMiddlewareOptions(openApiDoc, projectName);
        context.AddSource(
            $"{projectName}.ApiMiddlewareOptions.g.cs",
            SourceText.From(middlewareOptionsContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    /// <summary>
    /// Generates the unified Add{ProjectName}Api() service collection extension method.
    /// </summary>
    private static void GenerateUnifiedServiceCollection(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config,
        List<string> pathSegments)
    {
        var generatedContent = UnifiedServiceCollectionExtractor.Extract(openApiDoc, projectName, config, pathSegments);

        context.AddSource(
            $"{projectName}.UnifiedServiceCollectionExtensions.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    /// <summary>
    /// Generates the unified Map{ProjectName}Api() WebApplication extension method.
    /// </summary>
    private static void GenerateUnifiedWebApplicationExtensions(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config)
    {
        var generatedContent = WebApplicationExtensionsExtractor.ExtractUnified(openApiDoc, projectName, config);

        context.AddSource(
            $"{projectName}.UnifiedWebApplicationExtensions.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    /// <summary>
    /// Generates webhook handler interfaces from OpenAPI webhooks.
    /// </summary>
    private static void GenerateWebhookHandlerInterfaces(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        foreach (var generatedType in CodeGenerationService.GenerateWebhookHandlerInterfaces(openApiDoc, projectName, includeDeprecated))
        {
            var formattedCode = CodeGenerationService.FormatAsFile(generatedType);
            context.AddSource(
                $"Webhooks.Handlers.{generatedType.TypeName}.g.cs",
                SourceText.From(formattedCode.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Generates webhook parameter classes from OpenAPI webhooks.
    /// </summary>
    private static void GenerateWebhookParameterClasses(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        foreach (var generatedType in CodeGenerationService.GenerateWebhookParameters(openApiDoc, projectName, includeDeprecated))
        {
            var formattedCode = CodeGenerationService.FormatAsFile(generatedType);
            context.AddSource(
                $"Webhooks.Parameters.{generatedType.TypeName}.g.cs",
                SourceText.From(formattedCode.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Generates webhook result classes from OpenAPI webhooks.
    /// </summary>
    private static void GenerateWebhookResultClasses(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        foreach (var generatedType in CodeGenerationService.GenerateWebhookResults(openApiDoc, projectName, includeDeprecated))
        {
            var formattedCode = CodeGenerationService.FormatAsFile(generatedType);
            context.AddSource(
                $"Webhooks.Results.{generatedType.TypeName}.g.cs",
                SourceText.From(formattedCode.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Generates webhook endpoint registration from OpenAPI webhooks.
    /// </summary>
    private static void GenerateWebhookEndpoints(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config)
    {
        var generatedType = CodeGenerationService.GenerateWebhookEndpoints(openApiDoc, projectName, config);
        if (generatedType != null)
        {
            var formattedCode = CodeGenerationService.FormatAsFile(generatedType);
            context.AddSource(
                $"Webhooks.Endpoints.{generatedType.TypeName}.g.cs",
                SourceText.From(formattedCode.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Generates webhook DI registration from OpenAPI webhooks.
    /// </summary>
    private static void GenerateWebhookDependencyInjection(
        SourceProductionContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated)
    {
        var generatedType = CodeGenerationService.GenerateWebhookDependencyInjection(openApiDoc, projectName, includeDeprecated);
        if (generatedType != null)
        {
            var formattedCode = CodeGenerationService.FormatAsFile(generatedType);
            context.AddSource(
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
}