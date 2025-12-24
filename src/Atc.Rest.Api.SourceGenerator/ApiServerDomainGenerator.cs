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
        // Get compilation for handler scanning (marker file presence is the trigger)
        var compilationProvider = context.CompilationProvider;

        // Find marker files - marker file presence IS the trigger for this generator
        var markerFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".atc-rest-api-server-handlers", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) =>
            {
                var directory = Path.GetDirectoryName(file.Path) ?? string.Empty;
                var content = file.GetText(cancellationToken)?.ToString() ?? "{}";

                ServerDomainConfig config;

                try
                {
                    config = JsonSerializer.Deserialize<ServerDomainConfig>(content, JsonHelper.ConfigOptions) ?? new ServerDomainConfig();
                }
                catch
                {
                    config = new ServerDomainConfig(); // Use defaults if parsing fails
                }

                return (Directory: directory, Config: config);
            })
            .Collect();

        // Find OpenAPI YAML files
        var yamlFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                                 file.Path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => (file.Path, Content: file.GetText(cancellationToken)?.ToString() ?? string.Empty))
            .Where(static file => !string.IsNullOrEmpty(file.Content));

        // Combine all providers
        var combined = yamlFiles
            .Combine(compilationProvider)
            .Combine(markerFiles);

        // Register source output for handler scaffolds
        context.RegisterSourceOutput(combined, (context, combinedData) =>
        {
            var ((yamlFile, compilation), markers) = combinedData;

            // Skip if no marker file found - marker file presence IS the trigger
            if (markers.IsEmpty)
            {
                return;
            }

            var markerInfo = markers.First();
            var markerDirectory = markerInfo.Directory;
            var config = markerInfo.Config ?? new ServerDomainConfig();

            // Skip if generation is disabled
            if (!config.Generate)
            {
                return;
            }

            // Validate ASP.NET Core references - generated handlers require them
            var hasAspNetCore = compilation.ReferencedAssemblyNames
                .Any(a => a.Name.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase));

            if (!hasAspNetCore)
            {
                DiagnosticHelpers.ReportDomainRequiresAspNetCore(context);
                return;
            }

            try
            {
                GenerateHandlerScaffolds(context, yamlFile.Path, yamlFile.Content, compilation, config, markerDirectory);
            }
            catch (Exception ex)
            {
                DiagnosticHelpers.ReportHandlerScaffoldGenerationError(context, yamlFile.Path, ex);
            }
        });
    }

    private static void GenerateHandlerScaffolds(
        SourceProductionContext context,
        string yamlPath,
        string yamlContent,
        Compilation compilation,
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

        // Extract project name from YAML file path
        var projectName = Path.GetFileNameWithoutExtension(yamlPath);

        // Determine the root namespace from project name
        var rootNamespace = projectName.Replace(".Api.Domain", string.Empty)
                                      .Replace(".Domain", string.Empty);

        // Scan assembly for existing handler implementations
        var implementedHandlers = FindImplementedHandlers(compilation);

        // Get all operations from the OpenAPI document
        var operations = openApiDoc.GetAllOperations();

        // Create system type conflict resolver for conditional Task qualification
        var modelNames = openApiDoc.Components?.Schemas?.Keys ?? Array.Empty<string>();
        var systemTypeResolver = new SystemTypeConflictResolver(modelNames);

        // Determine output directory (GenerateHandlersOutput is relative to marker file)
        var outputDirectory = Path.Combine(markerDirectory, config.GenerateHandlersOutput);

        // Get all path segments for GlobalUsings generation
        var pathSegments = PathSegmentHelper.GetUniquePathSegments(openApiDoc);

        // Ensure GlobalUsings.cs is updated at project root (markerDirectory) before generating handlers
        if (!string.IsNullOrEmpty(markerDirectory))
        {
            EnsureGlobalUsingsUpdated(markerDirectory, rootNamespace, pathSegments);
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
                : GetHandlerNamespace(compilation, path, operation, config);

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
        GenerateDependencyRegistration(context, compilation, rootNamespace, allHandlers, config);

        // Generate DI registration extension method for validators
        GenerateValidatorDependencyRegistration(context, compilation, rootNamespace);
    }

    /// <summary>
    /// Generates the dependency injection registration extension method.
    /// </summary>
    private static void GenerateDependencyRegistration(
        SourceProductionContext context,
        Compilation compilation,
        string rootNamespace,
        List<(string OperationId, string HandlerName, string HandlerNamespace)> allHandlers,
        ServerDomainConfig config)
    {
        if (!allHandlers.Any())
        {
            return;
        }

        var assemblyName = compilation.AssemblyName ?? "Generated";

        // Use DependencyRegistrationExtractor to extract class parameters
        var classParameters = DependencyRegistrationExtractor.Extract(
            rootNamespace,
            assemblyName,
            allHandlers,
            config.HandlerSuffix);

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
        Compilation compilation,
        string rootNamespace)
    {
        // Find all validators in the compilation
        var validators = FindImplementedValidators(compilation);

        if (validators.Count == 0)
        {
            return;
        }

        var assemblyName = compilation.AssemblyName ?? "Generated";

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
    /// Ensures GlobalUsings.cs at the project root contains proper usings for handlers.
    /// Updates the file by adding missing usings while preserving existing content.
    /// </summary>
    private static void EnsureGlobalUsingsUpdated(
        string projectRootDirectory,
        string rootNamespace,
        List<string> pathSegments)
    {
        var globalUsingsPath = Path.Combine(projectRootDirectory, "GlobalUsings.cs");

        // Build required usings set
        var requiredUsings = new HashSet<string>(StringComparer.Ordinal)
        {
            "global using System;",
            "global using System.Threading;",
            "global using System.Threading.Tasks;",
        };

        // Add path segment usings (sorted)
        var sortedSegments = pathSegments.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var segment in sortedSegments)
        {
            requiredUsings.Add($"global using {rootNamespace}.Generated.{segment}.Handlers;");
            requiredUsings.Add($"global using {rootNamespace}.Generated.{segment}.Parameters;");
            requiredUsings.Add($"global using {rootNamespace}.Generated.{segment}.Results;");
        }

        // Read existing content
        var existingContent = File.Exists(globalUsingsPath)
            ? File.ReadAllText(globalUsingsPath)
            : string.Empty;

        // Parse existing global usings
        var existingUsings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in existingContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("global using ", StringComparison.Ordinal))
            {
                existingUsings.Add(trimmed);
            }
        }

        // Find missing usings
        var missingUsings = requiredUsings.Except(existingUsings, StringComparer.Ordinal).ToList();

        // Check if file ends with newline
        var endsWithNewline = existingContent.Length > 0 &&
            (existingContent.EndsWith("\n", StringComparison.Ordinal) ||
             existingContent.EndsWith("\r", StringComparison.Ordinal));

        // If nothing to add and file ends with newline, skip
        if (missingUsings.Count == 0 && endsWithNewline)
        {
            return;
        }

        // If nothing to add but file doesn't end with newline, fix it
        if (missingUsings.Count == 0)
        {
            FileHelper.WriteCsFile(globalUsingsPath, existingContent);
            return;
        }

        // Build updated content
        var builder = new StringBuilder();

        // Preserve existing content
        var hasExistingContent = !string.IsNullOrWhiteSpace(existingContent);
        if (hasExistingContent)
        {
            // Keep existing content and append missing usings
            builder.Append(existingContent.TrimEnd());
            builder.AppendLine();
            builder.AppendLine();
        }

        // Add missing usings (System namespaces first, then others - all alphabetically)
        // Extract namespace for proper sorting (removes "global using " prefix and ";" suffix)
        static string ExtractNamespace(string usingDirective)
        {
            const string prefix = "global using ";
            if (usingDirective.StartsWith(prefix, StringComparison.Ordinal))
            {
                var ns = usingDirective.Substring(prefix.Length);
                return ns.EndsWith(";", StringComparison.Ordinal)
                    ? ns.Substring(0, ns.Length - 1)
                    : ns;
            }

            return usingDirective;
        }

        var sortedMissingList = missingUsings
            .OrderBy(u => !u.StartsWith("global using System", StringComparison.Ordinal))
            .ThenBy(ExtractNamespace, StringComparer.Ordinal)
            .ToList();

        // Add all missing usings
        foreach (var missing in sortedMissingList)
        {
            builder.AppendLine(missing);
        }

        // Write file using helper (ensures proper newline handling)
        FileHelper.WriteCsFile(globalUsingsPath, builder.ToString());
    }

    /// <summary>
    /// Scans the compilation to find all handler implementations.
    /// Returns a dictionary mapping handler class names to their actual namespaces (e.g., "CreatePetsHandler" -> "PetStoreSimple.Api.Domain.ApiHandlers").
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
            // Check if this type implements any handler interfaces
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
    /// Determines the handler namespace based on configuration and OpenAPI operation.
    /// </summary>
    private static string GetHandlerNamespace(
        Compilation compilation,
        string path,
        OpenApiOperation operation,
        ServerDomainConfig config)
    {
        var baseNamespace = config.Namespace ?? compilation.AssemblyName ?? "Generated";

        // If no sub-folders, use ApiHandlers directly
        if (config.SubFolderStrategy == SubFolderStrategyType.None)
        {
            return $"{baseNamespace}.ApiHandlers";
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
            return $"{baseNamespace}.ApiHandlers";
        }

        return $"{baseNamespace}.ApiHandlers.{subFolder.ToPascalCaseForDotNet()}";
    }

    /// <summary>
    /// Extracts the first path segment from an OpenAPI path (e.g., "/pets/{id}" -> "Pets").
    /// Returns pluralized form for consistency with REST conventions.
    /// </summary>
    private static string ExtractFirstPathSegment(string path)
    {
        var segments = path.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return string.Empty;
        }

        var segment = segments[0].ToPascalCaseForDotNet();
        return segment.Pluralize();
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
        var returnType = $"Task<{operationIdPascal}Result>";

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
            File.WriteAllText(filePath, updatedContent, System.Text.Encoding.UTF8);
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