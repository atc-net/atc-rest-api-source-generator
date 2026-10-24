namespace Atc.Rest.Api.Generator.Cli.Services;

/// <summary>
/// Orchestrates TypeScript client generation from OpenAPI documents.
/// </summary>
public static class TypeScriptClientGenerationService
{
    // The folders the root barrel re-exports, in order, each when the plan has its index.ts
    private static readonly string[] RootBarrelFolders =
    [
        "models",
        "enums",
        "errors",
        "types",
        "client",
        "helpers",
        "hooks",
        "mocks",
        "servers",
        "webhooks",
        "hubs",
    ];

    /// <summary>
    /// Generates all TypeScript client files from an OpenAPI document.
    /// Includes models, enums, error types, ApiResult, base client, and per-segment clients.
    /// </summary>
    /// <param name="openApiDoc">The parsed OpenAPI document.</param>
    /// <param name="outputPath">The root output directory for generated files.</param>
    /// <param name="config">TypeScript client generation configuration.</param>
    /// <returns>A result containing counts of generated files.</returns>
    public static TypeScriptGenerationResult Generate(
        OpenApiDocument openApiDoc,
        string outputPath,
        TypeScriptClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);
        ArgumentNullException.ThrowIfNull(config);

        using var enumsScope = TypeScriptOperationHelper.EnumsScope(config.ExtensibleEnums, config.EnumStyle);

        // Types, files and imports are named after the component schemas: names that are no identifier
        // (Note-Size, 3DModel, Company.Api.Thing) are renamed in the document first.
        openApiDoc = SchemaNameNormalizer.WithIdentifierSchemaNames(openApiDoc);

        // A schema named like the client's own exports or a global the code uses (ApiError, Response) is {Name}Model
        openApiDoc = SchemaNameNormalizer.WithSchemaNamesOutside(openApiDoc, TypeScriptReservedSchemaNames.Names, TypeScriptReservedSchemaNames.Suffix);

        // Every extractor looks a body up by its media type: parameters, casing and model-carrying wildcards first.
        // A response declared for a status range is read as its first status, and a HEAD's responses have no content.
        openApiDoc = SpecificationPipeline.Normalize(openApiDoc);

        // Without includeDeprecated the deprecated operations go with the deprecated schemas they may return
        if (!config.IncludeDeprecated)
        {
            openApiDoc = DeprecationDetector.WithoutDeprecatedOperations(openApiDoc);
        }

        // Every file is decided first (the plan), then written in one go
        var plan = new TypeScriptEmitPlan();
        var headerContent = config.FileHeader;

        // Step 1: Extract enums first (needed for correct import paths in models)
        var enums = TypeScriptEnumExtractor.Extract(openApiDoc, config);
        var enumNameSet = new HashSet<string>(enums.Select(e => e.Name), StringComparer.Ordinal);
        var enumCount = AddEnums(plan, enums, headerContent, config.GenerateZodSchemas);

        // Step 2: Extract and generate models (with enum names for correct import paths)
        var models = TypeScriptModelExtractor.Extract(openApiDoc, config, enumNameSet);
        var arrayTypeAliases = TypeScriptModelExtractor.ExtractArrayTypeAliases(openApiDoc, config);
        var unionTypeAliases = TypeScriptModelExtractor.ExtractUnionTypeAliases(openApiDoc, config);
        var modelCount = AddModels(plan, models, arrayTypeAliases, unionTypeAliases, headerContent, config.GenerateZodSchemas);

        // The keys the models rename (the naming strategy) and, with --convert-dates, the declared dates - the client
        // converts them on the way in and out.
        var wireNames = new TypeScriptWireNames(openApiDoc, config.NamingStrategy, config.ConvertDates);

        // Step 3: Generate error types
        var errorTypeCount = AddErrorTypes(plan, headerContent, config.HttpClient, wireNames.HasRenames);

        // Step 4: Generate ApiResult type
        var typeCount = AddApiResultType(plan, headerContent, config.HttpClient, config.ZodRuntimeValidate);

        // Step 5: Generate client classes
        var specHasRetry = openApiDoc.HasRetryConfiguration();
        var writableSchemas = TypeScriptModelExtractor.CollectSchemasWithWritableVariant(openApiDoc);
        var (clientCount, segmentClientNames) = AddClients(plan, openApiDoc, headerContent, enumNameSet, config.HttpClient, config.NamingStrategy, config.ConvertDates, specHasRetry, writableSchemas, config.BrandedIds, config.ZodRuntimeValidate, wireNames, config.PreferXml);

        // Step 5b: Generate helpers (pagination, retry, key renaming)
        var hasRetry = clientCount > 0 && specHasRetry;
        var hasPagination = clientCount > 0 && HasPaginationSchemas(openApiDoc);
        var wireNamesContent = wireNames.GenerateFile(headerContent);
        var hasDownload = clientCount > 0 && openApiDoc.Paths.Values
            .SelectMany(path => path.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            .Any(operation => operation.HasFileDownload());
        var xmlContent = clientCount > 0 ? TypeScriptXmlHelperExtractor.Generate(openApiDoc, headerContent, config.NamingStrategy, config.ConvertDates) : null;

        // One rule for the helpers folder: it is written, and the root barrel exports it, when one helper is needed
        var hasHelpers = hasPagination || hasRetry || hasDownload || wireNamesContent is not null || xmlContent is not null;
        if (hasHelpers)
        {
            var helperModules = new List<string>();

            if (wireNamesContent is not null)
            {
                plan.Add("helpers/wireNames.ts", wireNamesContent);
                plan.Add($"helpers/{TypeScriptWireNames.RuntimeModule}.ts", TypeScriptWireNames.GenerateRuntimeFile(headerContent));
                helperModules.Add("wireNames");
            }

            if (hasPagination)
            {
                var paginationContent = TypeScriptPaginationHelperExtractor.Generate(headerContent);
                plan.Add("helpers/paginate.ts", paginationContent);
                helperModules.Add("paginate");
            }

            if (hasRetry)
            {
                var retryConfigContent = TypeScriptRetryConfigExtractor.Generate(openApiDoc, headerContent);
                plan.Add("helpers/retryConfig.ts", retryConfigContent);
                plan.Add($"helpers/{TypeScriptRetryConfigExtractor.RuntimeModule}.ts", TypeScriptRetryConfigExtractor.GenerateRuntime(headerContent));
                helperModules.Add("retryConfig");

                var retryInterceptorContent = TypeScriptRetryInterceptorExtractor.Generate(headerContent);
                plan.Add("helpers/retryInterceptor.ts", retryInterceptorContent);
                helperModules.Add("retryInterceptor");
            }

            if (hasDownload)
            {
                plan.Add("helpers/download.ts", TypeScriptDownloadHelperExtractor.Generate(headerContent));
                helperModules.Add("download");
            }

            // XML bodies: the models' XML mapping and the parser the ApiClient uses
            if (xmlContent is not null)
            {
                plan.Add("helpers/xml.ts", xmlContent);
                plan.Add($"helpers/{TypeScriptXmlHelperExtractor.RuntimeModule}.ts", TypeScriptXmlHelperExtractor.GenerateRuntime(headerContent));
                helperModules.Add("xml");
            }

            // Generate barrel export for helpers
            var helperBarrel = TypeScriptBarrelExportExtractor.Create(headerContent, helperModules, isTypeOnly: false);
            plan.Add("helpers/index.ts", new GenerateContentForBarrelExport(helperBarrel).Generate());
        }

        // Step 6: Generate React Query hooks (if configured)
        var hookCount = 0;
        if (config.HooksStyle == TypeScriptHooksStyle.ReactQuery && clientCount > 0)
        {
            hookCount = AddReactQueryHooks(plan, openApiDoc, headerContent, enumNameSet, config.NamingStrategy, config.ConvertDates, writableSchemas, config.HooksMode, config.BrandedIds);
        }
        else if (config.HooksStyle == TypeScriptHooksStyle.Swr && clientCount > 0)
        {
            hookCount = AddSwrHooks(plan, openApiDoc, headerContent, enumNameSet, config.NamingStrategy, config.BrandedIds, config.ConvertDates, writableSchemas);
        }

        // Step 6b: Generate Zod schemas (if configured)
        var zodSchemaCount = 0;
        if (config.GenerateZodSchemas)
        {
            zodSchemaCount = AddZodSchemas(plan, openApiDoc, headerContent, config, enumNameSet);
        }

        // Step 6c: Generate MSW handlers (if configured)
        var mswHandlerCount = 0;
        if (config.GenerateMswHandlers)
        {
            mswHandlerCount = AddMswHandlers(plan, openApiDoc, headerContent, config);
        }

        // Step 6d: Emit a typed Servers map when the spec declares >1 server entry.
        // Single-server specs keep the existing baseUrl ctor-arg pattern unchanged.
        var serversEmitted = AddServersFile(plan, openApiDoc, headerContent);

        // Step 6e: Emit typed Webhook payload aliases for any operation with a
        // callbacks: block whose body resolves to a named component schema.
        var webhooksEmitted = AddWebhooksFile(plan, openApiDoc, headerContent);

        // Step 6f: Emit SignalR-style hubs when the doc declares an x-signalr-hubs vendor extension: a
        // framework-neutral connection per hub, and its React hook when the client has React hooks.
        var hubsEmitted = AddSignalRHubFiles(plan, openApiDoc, headerContent, wireNames, config.HooksStyle is TypeScriptHooksStyle.ReactQuery or TypeScriptHooksStyle.Swr);

        // Step 6g: Emit branded ID type aliases when --branded-ids is set. The file
        // lives under types/ so it joins the existing type-only barrel. typeCount is
        // bumped so the root barrel includes the types/ subdirectory.
        if (config.BrandedIds)
        {
            var brandedEmitted = AddBrandedIdsFile(plan, openApiDoc, headerContent);
            if (brandedEmitted)
            {
                typeCount++;
            }
        }

        // Step 7: Generate package scaffold (if configured)
        var scaffoldGenerated = false;
        if (config.Scaffold)
        {
            scaffoldGenerated = AddPackageScaffold(plan, openApiDoc, config, segmentClientNames, hubsEmitted);
        }

        // Step 8: Generate root barrel export - every folder the plan has an index.ts for
        var subdirectories = RootBarrelFolders
            .Where(folder => plan.Contains(folder + "/index.ts"))
            .ToList();
        if (subdirectories.Count > 0)
        {
            AddRootBarrelExport(plan, headerContent, subdirectories);
        }

        var warnings = CollectCookieParameterWarnings(openApiDoc);

        // A schema, operation or option the specification no longer has leaves no file behind
        var keptFiles = config.DryRun
            ? Array.Empty<string>()
            : TypeScriptEmitWriter.Write(plan, outputPath, removeStale: config.GenerateFileHeaders);

        return new TypeScriptGenerationResult(
            ModelCount: modelCount,
            EnumCount: enumCount,
            ErrorTypeCount: errorTypeCount,
            TypeCount: typeCount,
            ClientCount: clientCount,
            HookCount: hookCount,
            ZodSchemaCount: zodSchemaCount,
            MswHandlerCount: mswHandlerCount,
            ScaffoldGenerated: scaffoldGenerated)
        {
            Warnings = warnings,
            KeptFiles = keptFiles,
        };
    }

    /// <summary>
    /// Scans every operation (and path-item-level parameters) for OpenAPI <c>in: cookie</c>
    /// parameters and produces one warning per operation that declares any. The TypeScript
    /// emitter intentionally skips cookie parameters — cookies are browser-managed via
    /// <c>document.cookie</c> and the fetch <c>credentials</c> option, so SDK methods do
    /// not accept a cookies bag. The warning surfaces the silent skip so spec authors
    /// notice when a cookie param will not be wired through.
    /// </summary>
    private static IReadOnlyList<string> CollectCookieParameterWarnings(
        OpenApiDocument openApiDoc)
    {
        if (openApiDoc.Paths is null || openApiDoc.Paths.Count == 0)
        {
            return Array.Empty<string>();
        }

        var warnings = new List<string>();

        foreach (var pathEntry in openApiDoc.Paths)
        {
            if (pathEntry.Value is not IOpenApiPathItem pathItem)
            {
                continue;
            }

            var pathLevelCookieNames = CollectCookieParameterNames(pathItem.Parameters);

            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operationEntry in pathItem.Operations)
            {
                var operationCookieNames = CollectCookieParameterNames(operationEntry.Value.Parameters);

                if (pathLevelCookieNames.Count == 0 && operationCookieNames.Count == 0)
                {
                    continue;
                }

                // Merge path-item-level params with operation-level params, preserving order
                // and de-duplicating in case the same name appears at both levels.
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var names = new List<string>(pathLevelCookieNames.Count + operationCookieNames.Count);
                foreach (var name in pathLevelCookieNames.Concat(operationCookieNames))
                {
                    if (seen.Add(name))
                    {
                        names.Add(name);
                    }
                }

                var method = operationEntry.Key.ToString().ToUpperInvariant();
                var nameList = string.Join(", ", names);
                warnings.Add(
                    $"Operation {method} {pathEntry.Key}: cookie parameter(s) [{nameList}] are not emitted in the TypeScript client. Cookies are browser-managed — set credentials: 'include' on ApiClientOptions if you need them sent.");
            }
        }

        return warnings;
    }

    private static List<string> CollectCookieParameterNames(
        IList<IOpenApiParameter>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return [];
        }

        var names = new List<string>();
        foreach (var paramInterface in parameters)
        {
            var resolved = paramInterface.Resolve();
            if (resolved.Parameter is { In: ParameterLocation.Cookie } cookie)
            {
                names.Add(cookie.Name ?? "(unnamed)");
            }
        }

        return names;
    }

    /// <summary>
    /// Adds the model files (interfaces and array type aliases) and their barrel export.
    /// </summary>
    private static int AddModels(
        TypeScriptEmitPlan plan,
        List<(string Name, TypeScriptInterfaceParameters Parameters)> models,
        List<(string Name, string Content)> arrayTypeAliases,
        List<(string Name, string Content)> unionTypeAliases,
        string? headerContent,
        bool includeZod)
    {
        var totalCount = models.Count + arrayTypeAliases.Count + unionTypeAliases.Count;
        if (totalCount == 0)
        {
            return 0;
        }

        var jsDocGenerator = new JsDocCommentGenerator();
        var writer = new GenerateContentWriter(jsDocGenerator);

        foreach (var (name, parameters) in models)
        {
            var generator = new GenerateContentForInterface(writer, parameters);
            plan.Add($"models/{name}.ts", generator.Generate());
        }

        // Write array type alias files (e.g., export type Accounts = Account[];)
        foreach (var (name, content) in arrayTypeAliases)
        {
            plan.Add($"models/{name}.ts", content);
        }

        // Write union type alias files (e.g., export type PaymentMethod = CreditCard | BankTransfer;)
        foreach (var (name, content) in unionTypeAliases)
        {
            plan.Add($"models/{name}.ts", content);
        }

        // Generate barrel export for all models (interfaces + array aliases + union aliases)
        var allModelNames = models.Select(m => m.Name)
            .Concat(arrayTypeAliases.Select(a => a.Name))
            .Concat(unionTypeAliases.Select(u => u.Name));

        // The Writable request variants (readOnly/writeOnly split) are never validated at runtime, so they
        // have no .zod file - the barrel must not re-export one.
        var modelNames = models.Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        var writableVariants = modelNames
            .Where(n => n.EndsWith(TypeScriptModelExtractor.WritableSuffix, StringComparison.Ordinal) &&
                        modelNames.Contains(n[..^TypeScriptModelExtractor.WritableSuffix.Length]))
            .ToHashSet(StringComparer.Ordinal);

        var barrelParams = includeZod
            ? TypeScriptBarrelExportExtractor.CreateWithZod(headerContent, allModelNames, namesWithoutZod: writableVariants)
            : TypeScriptBarrelExportExtractor.Create(headerContent, allModelNames);
        plan.Add("models/index.ts", new GenerateContentForBarrelExport(barrelParams).Generate());

        return totalCount;
    }

    /// <summary>
    /// Adds the enum files and their barrel export.
    /// </summary>
    private static int AddEnums(
        TypeScriptEmitPlan plan,
        List<(string Name, string Content)> enums,
        string? headerContent,
        bool includeZod)
    {
        if (enums.Count == 0)
        {
            return 0;
        }

        foreach (var (name, content) in enums)
        {
            plan.Add($"enums/{name}.ts", content);
        }

        // Generate barrel export for enums - use non-type-only for enum style Enum (they are values)
        var enumNames = enums.Select(e => e.Name);
        var barrelParams = includeZod
            ? TypeScriptBarrelExportExtractor.CreateWithZod(headerContent, enumNames, isTypeOnly: false)
            : TypeScriptBarrelExportExtractor.Create(headerContent, enumNames, isTypeOnly: false);
        plan.Add("enums/index.ts", new GenerateContentForBarrelExport(barrelParams).Generate());

        return enums.Count;
    }

    /// <summary>
    /// Adds the error type files and their barrel export.
    /// </summary>
    private static int AddErrorTypes(
        TypeScriptEmitPlan plan,
        string? headerContent,
        TypeScriptHttpClient httpClient,
        bool hasWireNames)
    {
        plan.Add("errors/ApiError.ts", TypeScriptErrorExtractor.GenerateApiError(headerContent, httpClient, hasWireNames));
        plan.Add("errors/ValidationError.ts", TypeScriptErrorExtractor.GenerateValidationError(headerContent, httpClient, hasWireNames));

        // Generate barrel export for errors (not type-only, they are classes)
        var errorNames = new[] { "ApiError", "ValidationError" };
        var barrelParams = TypeScriptBarrelExportExtractor.Create(headerContent, errorNames, isTypeOnly: false);
        plan.Add("errors/index.ts", new GenerateContentForBarrelExport(barrelParams).Generate());

        return 2;
    }

    /// <summary>
    /// Adds the ApiResult type file and its barrel export.
    /// </summary>
    private static int AddApiResultType(
        TypeScriptEmitPlan plan,
        string? headerContent,
        TypeScriptHttpClient httpClient,
        bool zodRuntimeValidate)
    {
        plan.Add("types/ApiResult.ts", TypeScriptApiResultExtractor.Generate(headerContent, httpClient, zodRuntimeValidate));

        // Generate barrel export for types
        var typeNames = new[] { "ApiResult" };
        var barrelParams = TypeScriptBarrelExportExtractor.Create(headerContent, typeNames);
        plan.Add("types/index.ts", new GenerateContentForBarrelExport(barrelParams).Generate());

        return 1;
    }

    /// <summary>
    /// Adds the client class files and their barrel export.
    /// </summary>
    private static (int Count, IReadOnlyList<string> SegmentNames) AddClients(
        TypeScriptEmitPlan plan,
        OpenApiDocument openApiDoc,
        string? headerContent,
        HashSet<string> enumNameSet,
        TypeScriptHttpClient httpClient,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool hasRetry,
        HashSet<string> writableSchemas,
        bool brandedIds,
        bool zodRuntimeValidate,
        TypeScriptWireNames wireNames,
        bool preferXml)
    {
        var clients = TypeScriptClientExtractor.Extract(openApiDoc, headerContent, enumNameSet, namingStrategy, convertDates, httpClient, writableSchemas, brandedIds, zodRuntimeValidate, wireNames, preferXml);
        if (clients.Count == 0)
        {
            return (0, Array.Empty<string>());
        }

        // Write base ApiClient (fetch or axios); with XML bodies: the xml request option, XML responses and
        // application/problem+xml errors
        var hasXml = XmlBodyReachability.Collect(openApiDoc).Count > 0;

        // A model or a file: the modelOrFile request option
        var hasModelOrFile = TypeScriptModelOrFileExtractor.HasModelOrFile(openApiDoc);

        // dateOnly is exported for the segment clients that write a format: date parameter or form field; a
        // specification without one gets no dead export
        var exportsDateOnly = convertDates && clients.Any(c => TypeScriptOperationHelper.ReferencesIdentifier(c.Content, "dateOnly"));

        // The typed headers of the result arms: their readers, for a specification whose responses declare headers
        var hasResponseHeaders = clients.Any(c => TypeScriptOperationHelper.ReferencesIdentifier(c.Content, "responseHeaders"));

        // A request body sent as a stream of records (JSON Lines, JSON Text Sequences): the sequence request option and
        // the record writers
        var hasSequenceBodies = TypeScriptRequestRecords.HasSequenceBodies(openApiDoc);
        var apiClientContent = httpClient == TypeScriptHttpClient.Axios
            ? TypeScriptAxiosApiClientExtractor.Generate(headerContent, hasRetry, zodRuntimeValidate, wireNames.HasRenames, exportsDateOnly, hasXml, hasModelOrFile, hasResponseHeaders, hasSequenceBodies)
            : TypeScriptFetchApiClientExtractor.Generate(headerContent, hasRetry, zodRuntimeValidate, wireNames.HasRenames, exportsDateOnly, hasXml, hasModelOrFile, hasResponseHeaders, hasSequenceBodies);

        plan.Add("client/ApiClient.ts", apiClientContent);

        // Write per-segment client files
        var segmentClientNames = new List<string>();
        foreach (var (className, content) in clients)
        {
            plan.Add($"client/{className}.ts", content);
            segmentClientNames.Add(className);
        }

        // Write ApiService root orchestrator
        var apiServiceContent = TypeScriptApiServiceExtractor.Generate(headerContent, segmentClientNames);
        plan.Add("client/ApiService.ts", apiServiceContent);

        // Generate barrel export for client directory (not type-only, they are classes)
        var clientItems = new List<(string Name, string? Summary)> { ("ApiClient", null) };
        foreach (var className in segmentClientNames)
        {
            var tagName = className.EndsWith("Client", StringComparison.Ordinal)
                ? className[..^6]
                : className;
            var summary = openApiDoc.Tags
                ?.FirstOrDefault(t => string.Equals(
                    t.Name?.ToPascalCaseForDotNet(),
                    tagName,
                    StringComparison.OrdinalIgnoreCase))
                ?.Summary;
            clientItems.Add((className, summary));
        }

        clientItems.Add(("ApiService", null));
        var barrelParams = TypeScriptBarrelExportExtractor.CreateWithSummaries(headerContent, clientItems, isTypeOnly: false);
        plan.Add("client/index.ts", new GenerateContentForBarrelExport(barrelParams).Generate());

        return (clients.Count, segmentClientNames);
    }

    /// <summary>
    /// Adds the React Query hook files (ApiProvider, useApiService, per-segment hooks) and their barrel export.
    /// </summary>
    private static int AddReactQueryHooks(
        TypeScriptEmitPlan plan,
        OpenApiDocument openApiDoc,
        string? headerContent,
        HashSet<string> enumNameSet,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        HashSet<string> writableSchemas,
        TypeScriptHooksMode hooksMode,
        bool brandedIds)
    {
        var hooks = TypeScriptReactQueryHookExtractor.Extract(openApiDoc, headerContent, enumNameSet, namingStrategy, convertDates, writableSchemas, hooksMode, brandedIds);
        if (hooks.Count == 0)
        {
            return 0;
        }

        plan.Add("hooks/ApiProvider.ts", TypeScriptApiProviderExtractor.GenerateApiProvider(headerContent));
        plan.Add("hooks/useApiService.ts", TypeScriptApiProviderExtractor.GenerateUseApiService(headerContent));

        // Write per-segment hook files
        var hookFileNames = new List<string> { "ApiProvider", "useApiService" };
        foreach (var (fileName, content) in hooks)
        {
            plan.Add($"hooks/{fileName}.ts", content);
            hookFileNames.Add(fileName);
        }

        // Generate barrel export (not type-only, they export functions)
        var barrelParams = TypeScriptBarrelExportExtractor.Create(headerContent, hookFileNames, isTypeOnly: false);
        plan.Add("hooks/index.ts", new GenerateContentForBarrelExport(barrelParams).Generate());

        return hooks.Count;
    }

    /// <summary>
    /// Adds the SWR hook files (ApiProvider, useApiService, per-segment hooks) and their barrel export.
    /// </summary>
    private static int AddSwrHooks(
        TypeScriptEmitPlan plan,
        OpenApiDocument openApiDoc,
        string? headerContent,
        HashSet<string> enumNameSet,
        TypeScriptNamingStrategy namingStrategy,
        bool brandedIds,
        bool convertDates,
        HashSet<string> writableSchemas)
    {
        var hooks = TypeScriptSwrHookExtractor.Extract(openApiDoc, headerContent, enumNameSet, namingStrategy, brandedIds, convertDates, writableSchemas);
        if (hooks.Count == 0)
        {
            return 0;
        }

        // Reuse ApiProvider and useApiService from React Query (same context pattern)
        plan.Add("hooks/ApiProvider.ts", TypeScriptApiProviderExtractor.GenerateApiProvider(headerContent));
        plan.Add("hooks/useApiService.ts", TypeScriptApiProviderExtractor.GenerateUseApiService(headerContent));

        // Write per-segment hook files
        var hookFileNames = new List<string> { "ApiProvider", "useApiService" };
        foreach (var (fileName, content) in hooks)
        {
            plan.Add($"hooks/{fileName}.ts", content);
            hookFileNames.Add(fileName);
        }

        // Generate barrel export
        var barrelParams = TypeScriptBarrelExportExtractor.Create(headerContent, hookFileNames, isTypeOnly: false);
        plan.Add("hooks/index.ts", new GenerateContentForBarrelExport(barrelParams).Generate());

        return hooks.Count;
    }

    /// <summary>
    /// Adds the Zod validation schema files for enums, models, and array type aliases.
    /// </summary>
    private static int AddZodSchemas(
        TypeScriptEmitPlan plan,
        OpenApiDocument openApiDoc,
        string? headerContent,
        TypeScriptClientConfig config,
        HashSet<string> enumNameSet)
    {
        var zodCount = 0;

        // Generate Zod enum schemas
        var zodEnums = TypeScriptZodEnumExtractor.Extract(openApiDoc, config);
        if (zodEnums.Count > 0)
        {
            foreach (var (name, content) in zodEnums)
            {
                plan.Add($"enums/{name}.zod.ts", content);
            }

            zodCount += zodEnums.Count;
        }

        // Generate Zod model schemas
        var zodModels = TypeScriptZodModelExtractor.Extract(openApiDoc, config, enumNameSet);
        if (zodModels.Count > 0)
        {
            foreach (var (name, content) in zodModels)
            {
                plan.Add($"models/{name}.zod.ts", content);
            }

            zodCount += zodModels.Count;
        }

        // Generate Zod union schemas (oneOf / anyOf type aliases)
        var zodUnions = TypeScriptZodModelExtractor.ExtractUnionTypeAliases(openApiDoc, config);
        if (zodUnions.Count > 0)
        {
            foreach (var (name, content) in zodUnions)
            {
                plan.Add($"models/{name}.zod.ts", content);
            }

            zodCount += zodUnions.Count;
        }

        // Generate Zod array type alias schemas
        var zodArrays = TypeScriptZodModelExtractor.ExtractArrayTypeAliases(openApiDoc, config);
        if (zodArrays.Count > 0)
        {
            foreach (var (name, content) in zodArrays)
            {
                plan.Add($"models/{name}.zod.ts", content);
            }

            zodCount += zodArrays.Count;
        }

        return zodCount;
    }

    /// <summary>
    /// Adds <c>types/BrandedIds.ts</c> when --branded-ids is enabled and the spec
    /// has at least one qualifying property or path parameter. The <c>types/</c>
    /// barrel is replaced by one with the new module alongside ApiResult.
    /// </summary>
    private static bool AddBrandedIdsFile(
        TypeScriptEmitPlan plan,
        OpenApiDocument openApiDoc,
        string? headerContent)
    {
        var brands = TypeScriptBrandedIdExtractor.CollectBrandNames(openApiDoc);
        if (brands.Count == 0)
        {
            return false;
        }

        var content = TypeScriptBrandedIdExtractor.Generate(brands, headerContent);
        if (content is null)
        {
            return false;
        }

        plan.Add("types/BrandedIds.ts", content);

        // Replace the types/ barrel by one with BrandedIds alongside ApiResult.
        // ApiResult is unconditional (every spec emits it), BrandedIds is new.
        var typeNames = new[] { "ApiResult", "BrandedIds" };
        var barrelParams = TypeScriptBarrelExportExtractor.Create(headerContent, typeNames);
        plan.Add("types/index.ts", new GenerateContentForBarrelExport(barrelParams).Generate());

        return true;
    }

    /// <summary>
    /// Adds <c>servers/Servers.ts</c> + <c>servers/index.ts</c> when the spec declares
    /// more than one <c>servers:</c> entry. Single-server specs keep the existing
    /// single-baseUrl ctor pattern; emitting a one-entry Servers const would add noise
    /// without giving the consumer a real choice.
    /// </summary>
    private static bool AddServersFile(
        TypeScriptEmitPlan plan,
        OpenApiDocument openApiDoc,
        string? headerContent)
    {
        var content = TypeScriptServersExtractor.Generate(openApiDoc, headerContent);
        if (content is null)
        {
            return false;
        }

        plan.Add("servers/Servers.ts", content);

        // Barrel: re-exports both the Servers const and the ServerName type union.
        var barrelParams = TypeScriptBarrelExportExtractor.Create(headerContent, new[] { "Servers" }, isTypeOnly: false);
        plan.Add("servers/index.ts", new GenerateContentForBarrelExport(barrelParams).Generate());

        return true;
    }

    /// <summary>
    /// Adds <c>webhooks/Webhooks.ts</c> + <c>webhooks/index.ts</c> when at least one
    /// operation declares a <c>callbacks:</c> block whose body references a named
    /// component schema. Inline callback bodies are skipped (no canonical name to alias).
    /// </summary>
    private static bool AddWebhooksFile(
        TypeScriptEmitPlan plan,
        OpenApiDocument openApiDoc,
        string? headerContent)
    {
        var content = TypeScriptWebhookExtractor.Generate(openApiDoc, headerContent);
        if (content is null)
        {
            return false;
        }

        plan.Add("webhooks/Webhooks.ts", content);

        var barrelParams = TypeScriptBarrelExportExtractor.Create(headerContent, new[] { "Webhooks" });
        plan.Add("webhooks/index.ts", new GenerateContentForBarrelExport(barrelParams).Generate());

        return true;
    }

    /// <summary>
    /// Adds the per-hub <c>xxxHub.ts</c> files (a framework-neutral connection whose inbound events go to caller-supplied
    /// optional callbacks), with React hooks the <c>useXxxHub.ts</c> hooks that own its lifecycle, plus a
    /// <c>hubs/index.ts</c> barrel, when the document declares an <c>x-signalr-hubs</c> vendor extension. No-op when
    /// the extension is missing or declares no hubs.
    /// </summary>
    private static bool AddSignalRHubFiles(
        TypeScriptEmitPlan plan,
        OpenApiDocument openApiDoc,
        string? headerContent,
        TypeScriptWireNames wireNames,
        bool includeReactHooks)
    {
        var hubs = TypeScriptSignalRHubExtractor.Extract(openApiDoc, headerContent, wireNames, includeReactHooks);
        if (hubs.Count == 0)
        {
            return false;
        }

        var hookFileNames = new List<string>();
        foreach (var (fileName, content) in hubs)
        {
            plan.Add($"hubs/{fileName}.ts", content);
            hookFileNames.Add(fileName);
        }

        // Barrel — exports functions, not type-only.
        var barrelParams = TypeScriptBarrelExportExtractor.Create(headerContent, hookFileNames, isTypeOnly: false);
        plan.Add("hubs/index.ts", new GenerateContentForBarrelExport(barrelParams).Generate());

        return true;
    }

    /// <summary>
    /// Adds the root barrel export (index.ts) that re-exports from subdirectories.
    /// </summary>
    private static void AddRootBarrelExport(
        TypeScriptEmitPlan plan,
        string? headerContent,
        List<string> subdirectories)
    {
        var barrelParams = TypeScriptBarrelExportExtractor.CreateForSubdirectories(headerContent, subdirectories);
        plan.Add("index.ts", new GenerateContentForBarrelExport(barrelParams).Generate());
    }

    /// <summary>
    /// Adds the package.json, tsconfig.json, and README.md scaffold files (written once, then the user's).
    /// </summary>
    private static bool AddPackageScaffold(
        TypeScriptEmitPlan plan,
        OpenApiDocument openApiDoc,
        TypeScriptClientConfig config,
        IReadOnlyList<string> segmentClientNames,
        bool hasSignalRHubs)
    {
        var packageName = config.PackageName
            ?? TypeScriptPackageScaffoldExtractor.DerivePackageName(openApiDoc.Info?.Title ?? string.Empty);

        var description = openApiDoc.Info?.Description;

        var packageJsonContent = TypeScriptPackageScaffoldExtractor.GeneratePackageJson(
            packageName,
            config.PackageVersion,
            description,
            config,
            hasSignalRHubs);
        plan.AddScaffold("package.json", packageJsonContent);

        var tsConfigContent = TypeScriptPackageScaffoldExtractor.GenerateTsConfig();
        plan.AddScaffold("tsconfig.json", tsConfigContent);

        var tsConfigCjsContent = TypeScriptPackageScaffoldExtractor.GenerateTsConfigCjs();
        plan.AddScaffold("tsconfig.cjs.json", tsConfigCjsContent);

        var postbuildContent = TypeScriptPackageScaffoldExtractor.GeneratePostBuildCjsScript();
        plan.AddScaffold("scripts/postbuild-cjs.mjs", postbuildContent);

        var readmeContent = TypeScriptPackageScaffoldExtractor.GenerateReadme(
            packageName,
            openApiDoc.Info?.Title,
            description,
            segmentClientNames,
            config);
        plan.AddScaffold("README.md", readmeContent);

        return true;
    }

    private static bool HasPaginationSchemas(OpenApiDocument openApiDoc)
    {
        if (openApiDoc.Components?.Schemas is null)
        {
            return false;
        }

        foreach (var schemaPair in openApiDoc.Components.Schemas)
        {
            // Check explicit x-pagination annotation first (authoritative)
            var annotation = schemaPair.Value.GetPaginationAnnotation();
            if (annotation == true)
            {
                return true;
            }

            // Heuristic fallback: check schema name convention
            if (annotation is null &&
                (schemaPair.Key.StartsWith("PaginationResult", StringComparison.Ordinal) ||
                 schemaPair.Key.StartsWith("PaginatedResult", StringComparison.Ordinal) ||
                 schemaPair.Key.StartsWith("PagedResult", StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static int AddMswHandlers(
        TypeScriptEmitPlan plan,
        OpenApiDocument openApiDoc,
        string? headerContent,
        TypeScriptClientConfig config)
    {
        // The handlers match any origin and base path (`http.get('*/items')`): the client requests the spec's
        // server path (`/api/v1/items`) or whatever base URL it is given, and a handler for '/items' alone
        // never matches those.
        const string baseUrl = "*";

        var handlers = TypeScriptMswHandlerExtractor.Extract(
            openApiDoc,
            headerContent,
            baseUrl,
            config.NamingStrategy);

        if (handlers.Count == 0)
        {
            return 0;
        }

        var moduleNames = new List<string>();
        foreach (var (segmentName, content) in handlers)
        {
            // segmentName == "handlers" is the synthetic combined-index module — the
            // per-segment files are named after their path segment.
            var moduleName = segmentName == "handlers"
                ? "handlers"
                : segmentName.EnsureFirstCharacterToLower();
            plan.Add($"mocks/{moduleName}.ts", content);
            moduleNames.Add(moduleName);
        }

        // The root barrel (api/index.ts) does `export * from './mocks'` whenever any MSW
        // handler files exist; that requires a real mocks/index.ts. Without this barrel,
        // strict tsc -b emits TS2307: Cannot find module './mocks'.
        var mocksBarrel = TypeScriptBarrelExportExtractor.Create(headerContent, moduleNames, isTypeOnly: false);
        plan.Add("mocks/index.ts", new GenerateContentForBarrelExport(mocksBarrel).Generate());

        // Exclude the combined "handlers" index from count
        return handlers.Count - 1;
    }
}