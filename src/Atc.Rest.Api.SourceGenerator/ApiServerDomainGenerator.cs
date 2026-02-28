namespace Atc.Rest.Api.SourceGenerator;

/// <summary>
/// Source generator for creating handler scaffolds in Domain projects.
/// Only generates scaffolds for handlers that are not already implemented in the assembly.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "OK.")]
[SuppressMessage("", "RS1035:The symbol 'File' is banned for use by analyzers: Do not do file IO in analyzers", Justification = "OK.")]
[Generator(LanguageNames.CSharp)]
public class ApiServerDomainGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Extract a stable, equatable summary from the compilation.
        // This runs on every C# edit but its output only changes when the handler set,
        // validator set, interface namespaces, or assembly references actually change.
        // Because DomainCompilationSummary has value equality, unchanged results
        // prevent the downstream RegisterSourceOutput callback from firing.
        var compilationSummary = context.CompilationProvider
            .Select(static (compilation, _) => ExtractCompilationSummary(compilation));

        // Find marker files - marker file presence IS the trigger for this generator
        var markerFiles = context.AdditionalTextsProvider
            .Where(static file => Path.GetFileName(file.Path).Equals(Constants.MarkerFile.ServerHandlers, StringComparison.OrdinalIgnoreCase) ||
                                  Path.GetFileName(file.Path).Equals(Constants.MarkerFile.ServerHandlersJson, StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) =>
                new MarkerFileInfo(file.Path, file.GetText(cancellationToken)?.ToString() ?? "{}"))
            .Collect()
            .Select(static (array, _) => new EquatableArray<MarkerFileInfo>(array));

        // Find OpenAPI YAML files
        var yamlFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                                 file.Path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) =>
                new YamlFileInfo(file.Path, file.GetText(cancellationToken)?.ToString() ?? string.Empty))
            .Where(static file => !string.IsNullOrEmpty(file.Content));

        // Combine YAML files with the stable compilation summary (not raw compilation).
        // This means the callback only fires when YAML content, marker config,
        // or meaningful compilation state changes — not on every C# keystroke.
        var combined = yamlFiles
            .Combine(compilationSummary)
            .Combine(markerFiles);

        // Register source output for handler scaffolds
        context.RegisterSourceOutput(combined, (productionContext, combinedData) =>
        {
            var ((yamlFile, summary), markers) = combinedData;

            // Skip if no marker file found - marker file presence IS the trigger
            if (markers.IsEmpty)
            {
                return;
            }

            var markerInfo = markers.Values.First();
            var markerDirectory = Path.GetDirectoryName(markerInfo.Path) ?? string.Empty;

            ServerDomainConfig config;
            try
            {
                config = JsonSerializer.Deserialize<ServerDomainConfig>(markerInfo.Content, JsonHelper.ConfigOptions) ?? new ServerDomainConfig();
            }
            catch
            {
                config = new ServerDomainConfig();
            }

            // Skip if generation is disabled
            if (!config.Generate)
            {
                return;
            }

            // Validate ASP.NET Core references - generated handlers require them
            if (!summary.HasAspNetCore)
            {
                DiagnosticHelpers.ReportDomainRequiresAspNetCore(productionContext);
                return;
            }

            try
            {
                GenerateHandlerScaffolds(productionContext, yamlFile.Path, yamlFile.Content, summary, config, markerDirectory);
            }
            catch (Exception ex)
            {
                DiagnosticHelpers.ReportHandlerScaffoldGenerationError(productionContext, yamlFile.Path, ex);
            }
        });
    }

    /// <summary>
    /// Extracts a stable, equatable summary from the compilation.
    /// Scans for implemented handlers, interface namespaces, and validators so that
    /// the pipeline can detect when these sets actually change.
    /// </summary>
    private static DomainCompilationSummary ExtractCompilationSummary(
        Compilation compilation)
    {
        var hasAspNetCore = compilation.ReferencedAssemblyNames
            .Any(a => a.Name.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase));

        var assemblyName = compilation.AssemblyName;

        // Sort all collections to ensure deterministic equality comparison
        var implementedHandlers = FindImplementedHandlers(compilation)
            .Select(kvp => new HandlerInfo(kvp.Key, kvp.Value))
            .OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

        var interfaceNamespaces = FindHandlerInterfaceNamespaces(compilation)
            .OrderBy(ns => ns, StringComparer.Ordinal)
            .ToImmutableArray();

        var validators = FindImplementedValidators(compilation)
            .Select(v => new ValidatorInfo(v.ValidatorName, v.ValidatorNamespace, v.ModelType))
            .OrderBy(v => v.Name, StringComparer.Ordinal)
            .ToImmutableArray();

        return new DomainCompilationSummary(
            hasAspNetCore,
            assemblyName,
            new EquatableArray<HandlerInfo>(implementedHandlers),
            new EquatableArray<string>(interfaceNamespaces),
            new EquatableArray<ValidatorInfo>(validators));
    }

    private static void GenerateHandlerScaffolds(
        SourceProductionContext context,
        string yamlPath,
        string yamlContent,
        DomainCompilationSummary summary,
        ServerDomainConfig config,
        string markerDirectory)
    {
        // Parse the OpenAPI YAML content
        var (openApiDoc, openApiDiagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yamlContent, yamlPath);

        if (openApiDoc == null)
        {
            DiagnosticHelpers.ReportDomainParsingError(context, yamlPath);
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

        // Determine contracts namespace for GlobalUsings:
        // Priority: explicit config > server marker (same dir or sibling) > derive from domain namespace
        var serverNamespace = MarkerFileHelper.TryGetServerNamespace(markerDirectory);
        var contractsNamespace = config.ContractsNamespace ?? serverNamespace;

        // Determine the root namespace for GlobalUsings (contracts namespace)
        var rootNamespace = contractsNamespace is not null
            ? contractsNamespace
            : (config.Namespace ?? Path.GetFileNameWithoutExtension(yamlPath))
                .Replace(".Api.Domain", string.Empty)
                .Replace(".Domain", string.Empty);

        var assemblyName = summary.AssemblyName ?? "Generated";

        // Convert summary handler data to lookup dictionary
        var implementedHandlers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var handler in summary.ImplementedHandlers.Values)
        {
            implementedHandlers[handler.Name] = handler.Namespace;
        }

        // Get all operations from the OpenAPI document
        var operations = openApiDoc.GetAllOperations();

        // Create system type conflict resolver for conditional Task qualification
        var modelNames = openApiDoc.Components?.Schemas?.Keys ?? [];
        var systemTypeResolver = new SystemTypeConflictResolver(modelNames);

        // Get all path segments for GlobalUsings generation
        var pathSegments = PathSegmentHelper.GetUniquePathSegments(openApiDoc);

        // Convert summary interface namespaces to HashSet
        var interfaceNamespaces = new HashSet<string>(summary.InterfaceNamespaces.Values, StringComparer.Ordinal);

        // Ensure GlobalUsings.cs is updated at project root (markerDirectory) before generating handlers
        if (!string.IsNullOrEmpty(markerDirectory))
        {
            DomainGlobalUsingsHelper.EnsureUpdated(markerDirectory, interfaceNamespaces, rootNamespace, pathSegments, openApiDoc, config);
        }

        // Collect all handler info for DI registration
        var allHandlers = new List<(string OperationId, string HandlerName, string HandlerNamespace)>();

        // Generate scaffolds for unimplemented handlers only
        foreach (var (path, operationType, operation) in operations)
        {
            // Skip deprecated operations if not including them
            if (operation is null || (!config.IncludeDeprecated && operation.Deprecated))
            {
                continue;
            }

            // Get the path item to access path-level parameters
            openApiDoc.Paths.TryGetValue(path, out var pathItem);

            var operationId = operation.GetOperationId(path, operationType);
            var handlerName = $"{operationId.ToPascalCaseForDotNet()}{config.HandlerSuffix}";

            // Check if handler is already implemented and get its actual namespace
            var isImplemented = implementedHandlers.TryGetValue(handlerName, out var actualNamespace);

            // Use actual namespace if implemented, otherwise use config-based namespace
            var handlerNamespace = isImplemented && !string.IsNullOrEmpty(actualNamespace)
                ? actualNamespace
                : GetHandlerNamespace(assemblyName, path, operation, config);

            // Add to all handlers list for DI registration (regardless of implementation status)
            allHandlers.Add((operationId, handlerName, handlerNamespace));

            // Determine sub-folder path for physical file (if enabled)
            var subFolderPath = GetSubFolderPath(path, operation, config);

            // If handler is already implemented, check if signature needs updating
            if (isImplemented)
            {
                var handlerFilePath = GetHandlerFilePath(handlerName, markerDirectory, subFolderPath, config);
                if (File.Exists(handlerFilePath))
                {
                    var expectedSignature = BuildExpectedSignature(openApiDoc, path, operation, operationId);
                    UpdateHandlerSignatureIfNeeded(handlerFilePath, expectedSignature);
                }

                continue;
            }

            // Generate handler scaffold as physical file
            GenerateHandlerScaffold(context, handlerName, handlerNamespace, operation, pathItem, operationId, config, markerDirectory, subFolderPath, systemTypeResolver);
        }

        // Generate DI registration extension method for handlers
        GenerateDependencyRegistration(context, assemblyName, rootNamespace, allHandlers, config, interfaceNamespaces, pathSegments);

        // Generate DI registration extension method for validators
        var validators = summary.ImplementedValidators.Values
            .Select(v => (v.Name, v.Namespace, v.ModelType))
            .ToList();
        GenerateValidatorDependencyRegistration(context, assemblyName, rootNamespace, validators);
    }

    /// <summary>
    /// Generates the dependency injection registration extension method.
    /// </summary>
    private static void GenerateDependencyRegistration(
        SourceProductionContext context,
        string assemblyName,
        string rootNamespace,
        List<(string OperationId, string HandlerName, string HandlerNamespace)> allHandlers,
        ServerDomainConfig config,
        HashSet<string> discoveredInterfaceNamespaces,
        List<string> pathSegments)
    {
        if (!allHandlers.Any())
        {
            return;
        }

        // Use discovered interface namespaces if available, otherwise fall back to path-segment based
        List<string> handlerInterfaceNamespaces;
        if (discoveredInterfaceNamespaces.Count > 0)
        {
            handlerInterfaceNamespaces = discoveredInterfaceNamespaces
                .OrderBy(ns => ns, StringComparer.Ordinal)
                .ToList();
        }
        else
        {
            handlerInterfaceNamespaces = pathSegments
                .Select(segment => $"{rootNamespace}.Generated.{segment}.Handlers")
                .ToList();
        }

        // Use DependencyRegistrationExtractor to extract class parameters
        var classParameters = DependencyRegistrationExtractor.Extract(
            rootNamespace,
            assemblyName,
            allHandlers,
            config.HandlerSuffix,
            handlerInterfaceNamespaces);

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
            "ApiHandlerDependencyRegistration.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    /// <summary>
    /// Generates the dependency injection registration extension method for validators.
    /// </summary>
    private static void GenerateValidatorDependencyRegistration(
        SourceProductionContext context,
        string assemblyName,
        string rootNamespace,
        List<(string ValidatorName, string ValidatorNamespace, string ModelType)> validators)
    {
        if (validators.Count == 0)
        {
            return;
        }

        // Use ValidatorDependencyRegistrationExtractor to extract class parameters
        var classParameters = ValidatorDependencyRegistrationExtractor.Extract(
            rootNamespace,
            assemblyName,
            validators);

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
            "ApiValidatorDependencyRegistration.g.cs",
            SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
    }

    /// <summary>
    /// Scans the compilation to find all handler implementations.
    /// Returns a dictionary mapping handler class names to their actual namespaces (e.g., "CreatePetsHandler" -> "PetStoreSimple.Api.Domain.ApiHandlers").
    /// Detection is done both by interface implementation AND by class naming convention to handle
    /// cases where the interface isn't yet resolved during source generation.
    /// </summary>
    private static Dictionary<string, string> FindImplementedHandlers(
        Compilation compilation)
    {
        var implementedHandlers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Get all named type symbols in the compilation
        var allTypes = compilation.GetSymbolsWithName(
            _ => true,
            SymbolFilter.Type);

        foreach (var typeSymbol in allTypes.OfType<INamedTypeSymbol>())
        {
            // Skip interfaces, abstract classes, and generated code
            if (typeSymbol.TypeKind == TypeKind.Interface ||
                typeSymbol.IsAbstract ||
                typeSymbol.DeclaringSyntaxReferences.Length == 0)
            {
                continue;
            }

            // Method 1: Check if this type implements any handler interfaces
            foreach (var interfaceSymbol in typeSymbol.AllInterfaces)
            {
                var interfaceName = interfaceSymbol.Name;

                // Check if it's a handler interface (e.g., ICreatePetsHandler)
                if (interfaceName.StartsWith("I", StringComparison.Ordinal) &&
                    interfaceName.EndsWith("Handler", StringComparison.Ordinal))
                {
                    // Extract handler name (e.g., CreatePetsHandler from ICreatePetsHandler)
                    var handlerName = interfaceName.Substring(1); // Remove "I" prefix

                    // Get the actual namespace of the implementation
                    var actualNamespace = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

                    implementedHandlers[handlerName] = actualNamespace;
                }
            }

            // Method 2: Check by class name convention (e.g., SetShutdownHandler)
            // This catches handlers even when the interface isn't resolved yet during source generation
            var className = typeSymbol.Name;
            if (className.EndsWith("Handler", StringComparison.Ordinal) &&
                !implementedHandlers.ContainsKey(className))
            {
                // Verify it's a user-defined class (has source location in the current project)
                var syntaxRef = typeSymbol.DeclaringSyntaxReferences.FirstOrDefault();
                if (syntaxRef != null)
                {
                    var filePath = syntaxRef.SyntaxTree.FilePath;

                    // Skip generated files (obj/Generated folder or .g.cs extension)
                    if (!filePath.Contains("obj" + Path.DirectorySeparatorChar + "Generated") &&
                        !filePath.Contains("obj" + Path.AltDirectorySeparatorChar + "Generated") &&
                        !filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
                    {
                        var actualNamespace = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                        implementedHandlers[className] = actualNamespace;
                    }
                }
            }
        }

        return implementedHandlers;
    }

    /// <summary>
    /// Scans the compilation to find all FluentValidation validator implementations.
    /// Returns a list of tuples containing (ValidatorName, ValidatorNamespace, ModelType).
    /// </summary>
    private static List<(string ValidatorName, string ValidatorNamespace, string ModelType)> FindImplementedValidators(
        Compilation compilation)
    {
        var validators = new List<(string ValidatorName, string ValidatorNamespace, string ModelType)>();

        // Get all named type symbols in the compilation
        var allTypes = compilation.GetSymbolsWithName(
            _ => true,
            SymbolFilter.Type);

        foreach (var typeSymbol in allTypes.OfType<INamedTypeSymbol>())
        {
            // Walk up the inheritance hierarchy to find AbstractValidator<T>
            var baseType = typeSymbol.BaseType;
            while (baseType != null)
            {
                // Check if this is AbstractValidator<T> from FluentValidation
                if (baseType.Name == "AbstractValidator" &&
                    baseType.TypeArguments.Length == 1 &&
                    baseType.ContainingNamespace?.ToDisplayString() == "FluentValidation")
                {
                    var modelType = baseType.TypeArguments[0].ToDisplayString();
                    var validatorNamespace = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

                    validators.Add((typeSymbol.Name, validatorNamespace, modelType));
                    break;
                }

                baseType = baseType.BaseType;
            }
        }

        return validators;
    }

    /// <summary>
    /// Determines the handler namespace based on assembly name and configuration.
    /// Uses assembly name as the base namespace, with optional override via config.Namespace.
    /// </summary>
    private static string GetHandlerNamespace(
        string assemblyName,
        string path,
        OpenApiOperation operation,
        ServerDomainConfig config)
    {
        // Use assembly name as base namespace by default, config.Namespace only as explicit override
        var baseNamespace = assemblyName;

        // Build handler folder name from config (default: "ApiHandlers")
        var handlerFolder = config.GenerateHandlersOutput
            .Replace("/", ".")
            .Replace("\\", ".");

        // If no sub-folders, use handler folder directly
        if (config.SubFolderStrategy == SubFolderStrategyType.None)
        {
            return $"{baseNamespace}.{handlerFolder}";
        }

        // Extract sub-folder name based on strategy
        var subFolder = config.SubFolderStrategy switch
        {
            SubFolderStrategyType.FirstPathSegment => ExtractFirstPathSegment(path),
            SubFolderStrategyType.OpenApiTag => operation.GetFirstTag(),
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(subFolder))
        {
            return $"{baseNamespace}.{handlerFolder}";
        }

        return $"{baseNamespace}.{handlerFolder}.{subFolder.ToPascalCaseForDotNet()}";
    }

    /// <summary>
    /// Scans the compilation to find all handler interface namespaces.
    /// Returns a set of unique namespaces where IXxxHandler interfaces are defined.
    /// </summary>
    private static HashSet<string> FindHandlerInterfaceNamespaces(
        Compilation compilation)
    {
        var namespaces = new HashSet<string>(StringComparer.Ordinal);

        // Get all named type symbols in the compilation (including referenced assemblies)
        var allTypes = compilation.GetSymbolsWithName(
            name => name.StartsWith("I", StringComparison.Ordinal) && name.EndsWith("Handler", StringComparison.Ordinal),
            SymbolFilter.Type);

        foreach (var typeSymbol in allTypes.OfType<INamedTypeSymbol>())
        {
            // Check if it's an interface
            if (typeSymbol.TypeKind == TypeKind.Interface)
            {
                var ns = typeSymbol.ContainingNamespace?.ToDisplayString();
                if (ns is not null && ns.Length > 0)
                {
                    namespaces.Add(ns);
                }
            }
        }

        return namespaces;
    }

    /// <summary>
    /// Extracts the first path segment from an OpenAPI path (e.g., "/pets/{id}" -> "Pet").
    /// </summary>
    private static string ExtractFirstPathSegment(string path)
    {
        var segments = path.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return string.Empty;
        }

        return segments[0].ToPascalCaseForDotNet();
    }

    /// <summary>
    /// Gets the sub-folder path for physical file generation based on configuration.
    /// </summary>
    private static string GetSubFolderPath(
        string path,
        OpenApiOperation operation,
        ServerDomainConfig config)
    {
        // If no sub-folders, return empty string
        if (config.SubFolderStrategy == SubFolderStrategyType.None)
        {
            return string.Empty;
        }

        // Extract sub-folder name based on strategy
        var subFolder = config.SubFolderStrategy switch
        {
            SubFolderStrategyType.FirstPathSegment => ExtractFirstPathSegment(path),
            SubFolderStrategyType.OpenApiTag => operation.GetFirstTag(),
            _ => string.Empty
        };

        return subFolder;
    }

    private static void GenerateHandlerScaffold(
        SourceProductionContext context,
        string handlerName,
        string handlerNamespace,
        OpenApiOperation operation,
        IOpenApiPathItem? pathItem,
        string operationId,
        ServerDomainConfig config,
        string markerDirectory,
        string subFolderPath,
        SystemTypeConflictResolver systemTypeResolver)
    {
        // Determine output directory (GenerateHandlersOutput is relative to marker file)
        var outputDirectory = Path.Combine(markerDirectory, config.GenerateHandlersOutput);

        // If no marker file found, report warning
        if (string.IsNullOrEmpty(outputDirectory))
        {
            DiagnosticHelpers.ReportOutputDirectoryNotSpecified(context);
            return;
        }

        // Apply sub-folder if configured
        if (!string.IsNullOrEmpty(subFolderPath))
        {
            outputDirectory = Path.Combine(outputDirectory, subFolderPath);
        }

        // Ensure directory exists
        Directory.CreateDirectory(outputDirectory);

        // Generate file path
        var fileName = $"{handlerName}.cs";
        var filePath = Path.Combine(outputDirectory, fileName);

        // Skip if file already exists
        if (File.Exists(filePath))
        {
            return;
        }

        // Use HandlerScaffoldExtractor to extract class parameters
        var classParameters = HandlerScaffoldExtractor.Extract(
            handlerName,
            handlerNamespace,
            operation,
            pathItem,
            operationId,
            config.HandlerSuffix,
            config.StubImplementation,
            systemTypeResolver);

        // Use GenerateContentForClass to generate the code
        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var contentGenerator = new GenerateContentForClass(
            codeDocGenerator,
            classParameters);

        var content = contentGenerator.Generate();

        // Write physical file
        FileHelper.WriteCsFile(filePath, content);
    }

    /// <summary>
    /// Gets the file path where a handler would be located based on configuration.
    /// </summary>
    private static string GetHandlerFilePath(
        string handlerName,
        string markerDirectory,
        string subFolderPath,
        ServerDomainConfig config)
    {
        var outputDirectory = Path.Combine(markerDirectory, config.GenerateHandlersOutput);

        if (!string.IsNullOrEmpty(subFolderPath))
        {
            outputDirectory = Path.Combine(outputDirectory, subFolderPath);
        }

        return Path.Combine(outputDirectory, $"{handlerName}.cs");
    }

    /// <summary>
    /// Builds the expected ExecuteAsync method signature based on the OpenAPI operation.
    /// </summary>
    private static string BuildExpectedSignature(
        OpenApiDocument openApiDoc,
        string path,
        OpenApiOperation operation,
        string operationId)
    {
        var operationIdPascal = operationId.ToPascalCaseForDotNet();

        // Check if operation has parameters (operation-level)
        var hasOperationParams = operation.Parameters is { Count: > 0 };

        // Check for path-level parameters
        var hasPathParams = false;
        if (openApiDoc.Paths != null &&
            openApiDoc.Paths.TryGetValue(path, out var pathItemInterface) &&
            pathItemInterface is OpenApiPathItem pathItem)
        {
            hasPathParams = pathItem.Parameters is { Count: > 0 };
        }

        // Check for request body
        var hasRequestBody = operation.HasRequestBody();
        var hasParameters = hasOperationParams || hasPathParams || hasRequestBody;

        if (hasParameters)
        {
            // Multi-line format to comply with ATC202 (multi parameters on separate lines)
            return $"public Task<{operationIdPascal}Result> ExecuteAsync(\n" +
                   $"        {operationIdPascal}Parameters parameters,\n" +
                   "        CancellationToken cancellationToken = default)";
        }

        // Single parameter - use multi-line format to comply with ATC201 (line exceeds 80 chars)
        return $"public Task<{operationIdPascal}Result> ExecuteAsync(\n" +
               "        CancellationToken cancellationToken = default)";
    }

    /// <summary>
    /// Normalizes a method signature by removing extra whitespace for comparison.
    /// </summary>
    private static string NormalizeSignature(string signature)
        => Regex.Replace(signature.Trim(), @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Updates the ExecuteAsync method signature in an existing handler file if it doesn't match the expected signature.
    /// Preserves the method body and all other code.
    /// </summary>
    private static void UpdateHandlerSignatureIfNeeded(
        string filePath,
        string expectedSignature)
    {
        try
        {
            var content = File.ReadAllText(filePath);

            // Pattern matches:
            // - public (optional async) Task<XxxResult> ExecuteAsync(...)
            // - Also handles fully qualified System.Threading.Tasks.Task<...>
            // - Uses [\s\S]*? to match multi-line parameter lists (ATC201/ATC202 compliant)
            // - Captures everything up to and including the closing parenthesis
            var signaturePattern = @"public\s+(async\s+)?(System\.Threading\.Tasks\.)?Task<\w+Result>\s+ExecuteAsync\s*\(\s*[\s\S]*?\)";
            var regexTimeout = TimeSpan.FromSeconds(1);

            var match = Regex.Match(content, signaturePattern, RegexOptions.Singleline, regexTimeout);
            if (!match.Success)
            {
                // Can't find ExecuteAsync method, skip update
                return;
            }

            var currentSignature = match.Value;

            // Check if async modifier is present in current signature
            var hasAsync = currentSignature.Contains("async ");

            // Check if fully qualified Task name is used
            var hasFullyQualifiedTask = currentSignature.Contains("System.Threading.Tasks.Task<");

            // Build the replacement signature, preserving async and fully qualified Task if present
            var replacementSignature = expectedSignature;
            if (hasAsync)
            {
                replacementSignature = replacementSignature.Replace("public Task<", "public async Task<");
            }

            if (hasFullyQualifiedTask)
            {
                replacementSignature = replacementSignature.Replace("Task<", "System.Threading.Tasks.Task<");
            }

            // Compare normalized signatures
            if (NormalizeSignature(currentSignature) == NormalizeSignature(replacementSignature))
            {
                // Signatures match, no update needed
                return;
            }

            // Replace the signature while preserving everything else
            var updatedContent = Regex.Replace(
                content,
                signaturePattern,
                replacementSignature,
                RegexOptions.Singleline,
                regexTimeout);

            // Write the updated content back to the file
            File.WriteAllText(filePath, updatedContent, Encoding.UTF8);
        }
        catch (IOException)
        {
            // File may be read-only or locked, skip update
        }
        catch (RegexMatchTimeoutException)
        {
            // Regex took too long, skip update
        }
    }
}