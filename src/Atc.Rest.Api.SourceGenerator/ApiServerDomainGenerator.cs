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
    private static readonly Regex WhitespaceNormalizerRegex = new(@"\s+", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    // The parameter list runs to its own closing parenthesis (balanced): a default value or an attribute argument
    // with parentheses of its own is part of it, not its end
    private static readonly Regex ExecuteAsyncSignatureRegex = new(
        @"public\s+(?:async\s+)?(?:System\.Threading\.Tasks\.)?Task<\w+Result>\s+ExecuteAsync\s*\((?:[^()]|(?<open>\()|(?<-open>\)))*(?(open)(?!))\)",
        RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Registers the pipeline: the <c>.atc-rest-api-server-handlers</c> marker file and the specifications as additional texts, the handler interfaces the compilation already implements, and the scaffolds of the missing handlers as the output.
    /// </summary>
    /// <param name="context">The initialization context.</param>
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

        // Find OpenAPI YAML files - collect ALL files for multi-part spec support
        var yamlFiles = SpecificationFileProvider.Create(context);

        // The server markers among the AdditionalFiles - the project's own, and a sibling project's that the package's
        // targets add (AtcRestApiSiblingMarker) - for the contracts namespace: a declared input, so a change to it runs
        // this generator again
        var serverMarkers = context.AdditionalTextsProvider
            .Where(static file => MarkerFileHelper.IsServerMarker(file.Path))
            .Select(static (file, cancellationToken) =>
                new MarkerFileInfo(file.Path, file.GetText(cancellationToken)?.ToString() ?? "{}"))
            .Collect()
            .Select(static (array, _) => new EquatableArray<MarkerFileInfo>(array));

        // Whether the scaffolds may be written into the project: not when AtcRestApiScaffoldHandlers is false (the
        // package's targets make it false for a CI build)
        var writesFiles = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) => BuildSettings.WritesScaffolds(options.GlobalOptions));

        // Combine ALL YAML files (as collection) with the stable compilation summary and marker files.
        // This means the callback only fires when YAML content, marker config,
        // or meaningful compilation state changes — not on every C# keystroke.
        var combined = yamlFiles
            .Combine(compilationSummary)
            .Combine(markerFiles)
            .Combine(serverMarkers)
            .Combine(writesFiles);

        // Register source output for handler scaffolds
        context.RegisterSourceOutput(combined, (productionContext, combinedData) =>
        {
            var ((((yamlFiles, summary), markers), declaredServerMarkers), writes) = combinedData;
            var scaffolding = new ScaffoldSettings(declaredServerMarkers.Values, writes);

            if (GeneratorStartup.ReadMarker<ServerDomainConfig>(productionContext, yamlFiles, markers) is not { } started)
            {
                return;
            }

            var (markerInfo, config) = started;
            var markerDirectory = Path.GetDirectoryName(markerInfo.Path) ?? string.Empty;

            // Validate ASP.NET Core references - generated handlers require them
            if (!summary.HasAspNetCore)
            {
                DiagnosticHelpers.ReportDomainRequiresAspNetCore(productionContext);
                return;
            }

            // Everything from here runs under the catch: a failure before the base file is known is reported on the marker
            var filePath = markerInfo.Path;
            try
            {
                GeneratorFaultInjection.ThrowIfRequested(RuleIdentifiers.HandlerScaffoldGenerationError);

                if (GeneratorStartup.FindSpecification(productionContext, markerInfo.Path, config.Overlays, yamlFiles) is not { } files)
                {
                    return;
                }

                filePath = files.BaseFile.Path;

                // A multi-part specification is merged with the server marker's merge settings, so the handlers see
                // the specification the contracts do
                var openApiDoc = GeneratorStartup.Load(
                    productionContext,
                    files,
                    config,
                    MarkerFileHelper.MultiPartConfigurationOf(declaredServerMarkers.Values),
                    parametersMustBind: true,
                    DiagnosticHelpers.ReportDomainParsingError);
                if (openApiDoc is null)
                {
                    return;
                }

                GenerateHandlerScaffoldsFromDocument(productionContext, files.BaseFile.Path, openApiDoc, summary, config, markerDirectory, scaffolding);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                DiagnosticHelpers.ReportHandlerScaffoldGenerationError(productionContext, filePath, ex);
            }
        });
    }

    /// <summary>
    /// Updates the ExecuteAsync method signature in an existing handler file if it doesn't match the expected signature.
    /// Preserves the method body and all other code.
    /// </summary>
    internal static void UpdateHandlerSignatureIfNeeded(
        string filePath,
        string expectedSignature)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var hadBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var content = Encoding.UTF8.GetString(bytes, hadBom ? 3 : 0, bytes.Length - (hadBom ? 3 : 0));

            // Pattern matches:
            // - public (optional async) Task<XxxResult> ExecuteAsync(...)
            // - Also handles fully qualified System.Threading.Tasks.Task<...>
            // - Multi-line parameter lists (ATC201/ATC202 compliant), up to and including the closing parenthesis
            var match = ExecuteAsyncSignatureRegex.Match(content);
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

            // Replace that one signature while preserving everything else, and write the file back as it was read:
            // without a BOM unless it had one
            var updatedContent = ExecuteAsyncSignatureRegex.Replace(content, replacementSignature, 1);
            ScaffoldFileWriter.Replace(filePath, updatedContent, hadBom ? Encoding.UTF8 : Utf8NoBom);
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

    /// <summary>
    /// Scans the compilation to find all handler implementations.
    /// Returns a dictionary mapping handler class names to their actual namespaces (e.g., "CreatePetsHandler" -> "PetStoreSimple.Api.Domain.ApiHandlers").
    /// Detection is done both by interface implementation AND by class naming convention to handle
    /// cases where the interface isn't yet resolved during source generation.
    /// </summary>
    internal static List<(string HandlerName, string Namespace)> FindImplementedHandlers(
        Compilation compilation)
    {
        // All discovered (name, namespace) pairs are returned WITHOUT collapsing to one
        // namespace per name. When the same handler name exists in two namespaces — e.g. a
        // previously scaffolded stub under "...ApiHandlers" AND a hand-written handler the
        // user placed elsewhere — collapsing would let discovery order decide which one
        // wins, silently registering the stub (HTTP 501). The caller, which knows the
        // scaffold namespace per operation, picks the right one and can warn on shadowing.
        var seen = new HashSet<(string, string)>();
        var implementedHandlers = new List<(string HandlerName, string Namespace)>();

        void Add(
            string handlerName,
            string ns)
        {
            if (seen.Add((handlerName, ns)))
            {
                implementedHandlers.Add((handlerName, ns));
            }
        }

        // Get type symbols from the current assembly only (avoids scanning all referenced assemblies)
        var allTypes = GetAllTypesFromAssembly(compilation.Assembly.GlobalNamespace);

        foreach (var typeSymbol in allTypes)
        {
            // Skip interfaces and abstract classes
            if (typeSymbol.TypeKind == TypeKind.Interface ||
                typeSymbol.IsAbstract)
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

                    Add(handlerName, actualNamespace);
                }
            }

            // Method 2: Check by class name convention (e.g., SetShutdownHandler)
            // This catches handlers even when the interface isn't resolved yet during source generation
            var className = typeSymbol.Name;
            if (className.EndsWith("Handler", StringComparison.Ordinal))
            {
                // Verify it's a user-defined class (has source location in the current project)
                var syntaxRef = typeSymbol.DeclaringSyntaxReferences.FirstOrDefault();
                if (syntaxRef is not null)
                {
                    var filePath = syntaxRef.SyntaxTree.FilePath;

                    // Skip generated files (obj/Generated folder or .g.cs extension)
                    if (!filePath.Contains("obj" + Path.DirectorySeparatorChar + "Generated") &&
                        !filePath.Contains("obj" + Path.AltDirectorySeparatorChar + "Generated") &&
                        !filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
                    {
                        var actualNamespace = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                        Add(className, actualNamespace);
                    }
                }
            }
        }

        return implementedHandlers;
    }

    /// <summary>
    /// Extracts a stable, equatable summary from the compilation.
    /// Scans for implemented handlers, interface namespaces, and validators so that
    /// the pipeline can detect when these sets actually change.
    /// </summary>
    private static DomainCompilationSummary ExtractCompilationSummary(
        Compilation compilation)
    {
        var hasAspNetCore = compilation.ReferencesAnyStartingWith("Microsoft.AspNetCore");

        var hasLogging = compilation.References("Microsoft.Extensions.Logging.Abstractions");

        var assemblyName = compilation.AssemblyName;

        // Sort all collections to ensure deterministic equality comparison. All discovered
        // (name, namespace) pairs are kept — including duplicate names across namespaces — so
        // the generation phase can prefer a user handler over a scaffolded stub.
        var implementedHandlers = FindImplementedHandlers(compilation)
            .Select(h => new HandlerInfo(h.HandlerName, h.Namespace))
            .OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(h => h.Namespace, StringComparer.Ordinal)
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
            hasLogging,
            assemblyName,
            new EquatableArray<HandlerInfo>(implementedHandlers),
            new EquatableArray<string>(interfaceNamespaces),
            new EquatableArray<ValidatorInfo>(validators));
    }

    private static void GenerateHandlerScaffoldsFromDocument(
        SourceProductionContext context,
        string yamlPath,
        OpenApiDocument openApiDoc,
        DomainCompilationSummary summary,
        ServerDomainConfig config,
        string markerDirectory,
        ScaffoldSettings scaffolding)
    {
        // An inline allOf over a plain base becomes one inline object, so it gets a model of its own
        InlineAllOfFlattener.Flatten(openApiDoc);

        // The same path filter as the server's: a scaffold for an operation the server leaves out would
        // implement a handler interface that is never generated (on a copy: the parsed document is cached and shared)
        openApiDoc = PathFilterHelper.WithFilteredPaths(openApiDoc, config.IncludePaths, config.ExcludePaths);

        // Determine contracts namespace for GlobalUsings:
        // Priority: explicit config > server marker (same dir or sibling) > derive from domain namespace
        var serverNamespace = MarkerFileHelper.TryGetServerNamespace(markerDirectory, scaffolding.ServerMarkers);
        var contractsNamespace = config.ContractsNamespace ?? serverNamespace;

        // If we couldn't auto-detect the contracts namespace AND multiple sibling markers
        // exist, the configuration is ambiguous — emit a diagnostic and abort. Falling
        // through to the legacy derivation path here would silently emit wrong global
        // usings (the symptom that GEN011 is meant to catch).
        if (contractsNamespace is null &&
            !string.IsNullOrEmpty(markerDirectory) &&
            MarkerFileHelper.HasMultipleSiblingServerMarkers(markerDirectory, scaffolding.ServerMarkers))
        {
            DiagnosticHelpers.ReportContractsNamespaceAmbiguous(
                context,
                Path.GetFileName(markerDirectory) ?? string.Empty);
            return;
        }

        // Determine the root namespace for GlobalUsings (contracts namespace)
        // Without one: the handlers' own namespace without its Domain suffix, else what the server resolves from the
        // same specification (a qualifying info.title, then the file name)
        var rootNamespace = contractsNamespace is not null
            ? contractsNamespace
            : string.IsNullOrWhiteSpace(config.Namespace)
                ? NamespaceResolver.Resolve(configNamespace: null, openApiDoc.Info?.Title, yamlPath)
                : NamespaceResolver.ToValidNamespace(config.Namespace)
                    .Replace(".Api.Domain", string.Empty)
                    .Replace(".Domain", string.Empty);

        var assemblyName = summary.AssemblyName ?? "Generated";

        // Convert summary handler data to a lookup of handler name -> all namespaces that
        // implement it. Keeping every namespace (rather than collapsing to one) lets the
        // loop below prefer a user-written handler over a scaffolded stub of the same name.
        var implementedHandlers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var handler in summary.ImplementedHandlers.Values)
        {
            if (!implementedHandlers.TryGetValue(handler.Name, out var namespaces))
            {
                namespaces = new List<string>();
                implementedHandlers[handler.Name] = namespaces;
            }

            if (!namespaces.Contains(handler.Namespace))
            {
                namespaces.Add(handler.Namespace);
            }
        }

        // Create system type conflict resolver for conditional Task qualification
        var modelNames = openApiDoc.Components?.Schemas?.Keys ?? [];
        var systemTypeResolver = new SystemTypeConflictResolver(modelNames);

        // Get all path segments for GlobalUsings generation. These are resolved the same way the
        // contracts generator resolves them, so the handler namespaces referenced here match the
        // namespaces actually emitted (a redundant segment collapses away on both sides).
        var pathSegments = PathSegmentHelper.GetUniquePathSegments(openApiDoc)
            .Select(segment => PathSegmentHelper.ResolveEffectivePathSegment(openApiDoc, rootNamespace, segment) ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Raw segments are kept for stale global-using detection, so entries written before a
        // segment collapsed away are still recognized as this generator's output.
        var rawPathSegments = PathSegmentHelper.GetUniquePathSegments(openApiDoc);

        // Convert summary interface namespaces to HashSet
        var interfaceNamespaces = new HashSet<string>(summary.InterfaceNamespaces.Values, StringComparer.Ordinal);

        // Report diagnostic if injectLogger is enabled but logging is not available
        if (config.InjectLogger && !summary.HasLogging)
        {
            DiagnosticHelpers.ReportLoggingRequiresPackage(context);
        }

        // Ensure GlobalUsings.cs is updated at project root (markerDirectory) before generating handlers - when the
        // project's files may be written; the usings below are part of the build either way
        if (scaffolding.WritesFiles && !string.IsNullOrEmpty(markerDirectory))
        {
            DomainGlobalUsingsHelper.EnsureUpdated(markerDirectory, interfaceNamespaces, rootNamespace, pathSegments, openApiDoc, config, rawPathSegments);
        }

        // Also emit global usings as in-memory source so the current compilation can resolve types
        // (on first build, the physical GlobalUsings.cs isn't part of the compilation yet)
        var requiredUsings = DomainGlobalUsingsHelper.BuildRequiredUsings(
            interfaceNamespaces, rootNamespace, pathSegments, openApiDoc, config.InjectLogger, config.InjectTracing);
        var globalUsingsContent = string.Join("\n", requiredUsings.OrderBy(u => u, StringComparer.Ordinal));

        // Proxy over `context` that adds [ExcludeFromCodeCoverage] alongside [GeneratedCode] when
        // configured. Handler scaffolds are excluded by design (they carry no [GeneratedCode]),
        // so only the DI registration methods below (which take this proxy) are affected.
        var generatedContext = new GeneratedSourceContext(context, config.ExcludeFromCodeCoverage, DeprecationDetector.HasDeprecatedMembers(openApiDoc));
        generatedContext.AddGeneratedSource("DomainGlobalUsings.g.cs", SourceText.From(globalUsingsContent, Encoding.UTF8));

        // Every operation's handler: a user-written one before a leftover stub, a stub scaffolded for the others
        var plans = HandlerScaffoldPlanner.Plan(openApiDoc, config, assemblyName, markerDirectory, implementedHandlers);
        foreach (var plan in plans)
        {
            // A user handler shadowing a leftover stub is silent otherwise - surface it so the developer knows to
            // delete the stale stub in the scaffold folder
            if (plan.ShadowsStub)
            {
                DiagnosticHelpers.ReportHandlerStubShadowed(context, plan.HandlerName, plan.Namespace, plan.ScaffoldNamespace);
            }

            // If handler is already implemented, check if signature needs updating (when the project's files may be
            // written; a signature that does not match fails the build either way)
            if (plan.IsImplemented)
            {
                if (scaffolding.WritesFiles && File.Exists(plan.FilePath))
                {
                    UpdateHandlerSignatureIfNeeded(plan.FilePath, plan.ExpectedSignature);
                }

                continue;
            }

            GenerateHandlerScaffold(context, plan, config, markerDirectory, systemTypeResolver, scaffolding.WritesFiles);
        }

        // Every handler is registered, implemented or not
        var allHandlers = plans.Select(p => (p.OperationId, p.HandlerName, p.Namespace)).ToList();

        // Generate DI registration extension method for handlers
        GenerateDependencyRegistration(generatedContext, assemblyName, rootNamespace, allHandlers, config, interfaceNamespaces, pathSegments);

        // Generate DI registration extension method for validators
        var validators = summary.ImplementedValidators.Values
            .Select(v => (v.Name, v.Namespace, v.ModelType))
            .ToList();
        GenerateValidatorDependencyRegistration(generatedContext, assemblyName, rootNamespace, validators);
    }

    /// <summary>
    /// Generates the dependency injection registration extension method.
    /// </summary>
    private static void GenerateDependencyRegistration(
        GeneratedSourceContext context,
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
                .Select(segment => NamespaceBuilder.Build(rootNamespace, NamespaceBuilder.Categories.Handlers, segment))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        // Use DependencyRegistrationExtractor to extract class parameters
        var classParameters = DependencyRegistrationExtractor.Extract(
            rootNamespace,
            assemblyName,
            allHandlers,
            config.HandlerSuffix,
            handlerInterfaceNamespaces);

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
            "ApiHandlerDependencyRegistration.g.cs",
            generatedContent);
    }

    /// <summary>
    /// Generates the dependency injection registration extension method for validators.
    /// </summary>
    private static void GenerateValidatorDependencyRegistration(
        GeneratedSourceContext context,
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
            "ApiValidatorDependencyRegistration.g.cs",
            generatedContent);
    }

    /// <summary>
    /// Scans the compilation to find all FluentValidation validator implementations.
    /// Returns a list of tuples containing (ValidatorName, ValidatorNamespace, ModelType).
    /// </summary>
    private static List<(string ValidatorName, string ValidatorNamespace, string ModelType)> FindImplementedValidators(
        Compilation compilation)
    {
        var validators = new List<(string ValidatorName, string ValidatorNamespace, string ModelType)>();

        // Get type symbols from the current assembly only (avoids scanning all referenced assemblies)
        var allTypes = GetAllTypesFromAssembly(compilation.Assembly.GlobalNamespace);

        foreach (var typeSymbol in allTypes)
        {
            // Walk up the inheritance hierarchy to find AbstractValidator<T>
            var baseType = typeSymbol.BaseType;
            while (baseType is not null)
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
    /// Recursively collects all named type symbols from a namespace (current assembly only).
    /// This avoids GetSymbolsWithName(_ => true) which scans all referenced assemblies.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> GetAllTypesFromAssembly(
        INamespaceSymbol namespaceSymbol)
    {
        foreach (var member in namespaceSymbol.GetTypeMembers())
        {
            yield return member;
        }

        foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var type in GetAllTypesFromAssembly(nestedNamespace))
            {
                yield return type;
            }
        }
    }

    private static void GenerateHandlerScaffold(
        SourceProductionContext context,
        Atc.Rest.Api.Generator.Models.HandlerPlan plan,
        ServerDomainConfig config,
        string markerDirectory,
        SystemTypeConflictResolver systemTypeResolver,
        bool writesFiles)
    {
        // Without a marker folder and a handler folder there is nowhere to scaffold (GenerateHandlersOutput is relative
        // to the marker file)
        if (string.IsNullOrEmpty(Path.Combine(markerDirectory, config.GenerateHandlersOutput)))
        {
            DiagnosticHelpers.ReportOutputDirectoryNotSpecified(context);
            return;
        }

        // Skip if file already exists
        var filePath = plan.FilePath;
        if (File.Exists(filePath))
        {
            return;
        }

        // Use HandlerScaffoldExtractor to extract class parameters
        var classParameters = HandlerScaffoldExtractor.Extract(
            plan.HandlerName,
            plan.Namespace,
            plan.Operation,
            plan.PathItem,
            plan.OperationId,
            config.HandlerSuffix,
            config.StubImplementation,
            systemTypeResolver,
            config.InjectLogger,
            config.MaxLineLength,
            config.InjectTracing);

        // Use GenerateContentForClass to generate the code
        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var contentGenerator = new GenerateContentForClass(
            codeDocGenerator,
            classParameters);

        var content = contentGenerator.Generate();

        // Write physical file - created only when no other host has written it meanwhile. A folder that is a file, a
        // path too long or a folder without access is the project's, not a failed generation: say which scaffold, and
        // keep it in this build. Not written at all where the project's files are not to be touched (a CI build): the
        // stub is part of the build, and the missing handler is reported
        if (writesFiles)
        {
            try
            {
                ScaffoldFileWriter.TryCreate(filePath, content);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                DiagnosticHelpers.ReportHandlerScaffoldNotWritten(context, plan.HandlerName, filePath, ex);
            }
        }
        else
        {
            DiagnosticHelpers.ReportHandlerNotImplemented(context, plan.HandlerName, filePath);
        }

        // Also emit as in-memory source so the current compilation can reference this type
        context.AddSource($"{plan.HandlerName}.scaffold.g.cs", SourceText.From(content, Encoding.UTF8));
    }

    /// <summary>
    /// Normalizes a method signature by removing extra whitespace for comparison.
    /// </summary>
    private static string NormalizeSignature(string signature)
        => WhitespaceNormalizerRegex.Replace(signature.Trim(), " ");
}