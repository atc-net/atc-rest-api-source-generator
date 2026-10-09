namespace Atc.Rest.Api.Generator.Services;

/// <summary>
/// Service for generating code from OpenAPI documents.
/// Provides methods to generate individual types or combined files.
/// </summary>
public static class CodeGenerationService
{
    /// <summary>
    /// Generator types for subfolder calculation.
    /// </summary>
    public enum GeneratorType
    {
        /// <summary>The server: models, parameters, results, handler interfaces and endpoints.</summary>
        Server,

        /// <summary>The client: models and HTTP clients.</summary>
        Client,

        /// <summary>The domain project: handler scaffolds.</summary>
        ServerDomain,
    }

    /// <summary>
    /// Sanitizes a project name for use in C# identifiers (class names, method names).
    /// Removes dots since they're not valid in identifiers.
    /// </summary>
    /// <param name="projectName">The project name that may contain dots.</param>
    /// <returns>A sanitized name suitable for use in C# identifiers.</returns>
    public static string SanitizeProjectNameForIdentifier(string projectName)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            return projectName;
        }

        // Remove dots from project names for use in identifiers
        return projectName.Replace(".", string.Empty);
    }

    private static readonly string[] ModelUsings =
    [
        NamespaceConstants.SystemCodeDomCompiler,
        NamespaceConstants.SystemComponentModelDataAnnotations,
    ];

    private static readonly string[] HandlerUsings =
    [
        NamespaceConstants.SystemCodeDomCompiler,
        NamespaceConstants.SystemThreading,
        NamespaceConstants.SystemThreadingTasks,
    ];

    private static readonly string[] EndpointUsings =
    [
        NamespaceConstants.SystemThreading,
        NamespaceConstants.MicrosoftAspNetCoreBuilder,
        NamespaceConstants.MicrosoftAspNetCoreHttp,
        NamespaceConstants.MicrosoftAspNetCoreMvc,
        NamespaceConstants.MicrosoftAspNetCoreRouting,
    ];

    private static readonly string[] DependencyInjectionUsings =
    [
        NamespaceConstants.MicrosoftExtensionsDependencyInjection,
    ];

    /// <summary>
    /// Gets a group name from a path by extracting the first significant segment.
    /// </summary>
    public static string GetGroupNameFromPath(string path)
    {
        var segments = path.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            // Skip version segments like v1, v2
            if (segment.StartsWith("v", StringComparison.OrdinalIgnoreCase) &&
                segment.Length > 1 &&
                char.IsDigit(segment[1]))
            {
                continue;
            }

            // Skip parameter segments like {id}
            if (segment.StartsWith("{", StringComparison.Ordinal))
            {
                continue;
            }

            // Capitalize and return
            return segment.ToPascalCaseForDotNet();
        }

        return "Api";
    }

    /// <summary>
    /// Gets the group name for a schema by finding which operations reference it.
    /// </summary>
    public static string? GetGroupNameForSchema(
        OpenApiDocument openApiDoc,
        string schemaName)
    {
        if (openApiDoc.Paths is null)
        {
            return null;
        }

        // Search through all operations to find which one references this schema
        foreach (var path in openApiDoc.Paths)
        {
            var pathKey = path.Key;
            if (path.Value is not IOpenApiPathItem pathItem || pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operation in pathItem.Operations)
            {
                var op = operation.Value;
                if (op is null)
                {
                    continue;
                }

                // Check request body
                if (op.RequestBody?.Content is not null)
                {
                    foreach (var mediaType in op.RequestBody.Content.Values)
                    {
                        if (SchemaReferencesName(mediaType.Schema, schemaName))
                        {
                            return GetGroupNameForOperation(op, pathKey);
                        }
                    }
                }

                // Check responses
                if (op.Responses is not null)
                {
                    foreach (var response in op.Responses.Values)
                    {
                        if (response is { Content: not null } resp)
                        {
                            foreach (var mediaType in resp.Content.Values)
                            {
                                if (SchemaReferencesName(mediaType.Schema, schemaName))
                                {
                                    return GetGroupNameForOperation(op, pathKey);
                                }
                            }
                        }
                    }
                }

                // Check parameters
                if (op.Parameters is not null)
                {
                    foreach (var param in op.Parameters)
                    {
                        if (param.Resolve().Parameter is { } p && SchemaReferencesName(p.Schema, schemaName))
                        {
                            return GetGroupNameForOperation(op, pathKey);
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Calculates the subfolder path for a generated type.
    /// </summary>
    /// <param name="category">The type category, such as Models, Parameters or Client.</param>
    /// <param name="groupName">The per-area group name; ignored when <paramref name="granularity"/> is Single.</param>
    /// <param name="generatorType">The generator type (Server, Client or ServerDomain).</param>
    /// <param name="granularity">
    /// Client granularity. Under <see cref="ClientGranularityType.Single"/> a single client covers every
    /// path segment, so the per-area folder segment is dropped to give a flat layout matching the flat
    /// <c>{root}.Generated.Models</c> namespace. Defaults to <see cref="ClientGranularityType.PerArea"/>
    /// so existing output is unchanged. Only affects <see cref="GeneratorType.Client"/>.
    /// </param>
    public static string GetSubFolder(
        string category,
        string? groupName,
        GeneratorType generatorType,
        ClientGranularityType granularity = ClientGranularityType.PerArea)
    {
        if (generatorType == GeneratorType.Client &&
            granularity == ClientGranularityType.Single)
        {
            return category switch
            {
                "Models" => "Contracts",
                "Parameters" => "Contracts/RequestParameters",
                "Client" => "Endpoints",
                _ => category,
            };
        }

        var groupNamePart = string.IsNullOrEmpty(groupName) ? "Common" : groupName;

        return (generatorType, category) switch
        {
            // Server paths
            (GeneratorType.Server, "Models") => $"Contracts\\{groupNamePart}/Models",
            (GeneratorType.Server, "Parameters") => $"Contracts\\{groupNamePart}/Parameters",
            (GeneratorType.Server, "Results") => $"Contracts\\{groupNamePart}/Results",
            (GeneratorType.Server, "Handlers") => $"Contracts\\{groupNamePart}/Interfaces",
            (GeneratorType.Server, "Endpoints") => string.IsNullOrEmpty(groupName)
                ? "Endpoints"
                : $"Endpoints\\{groupName}",
            (GeneratorType.Server, "DependencyInjection") => "Extensions",

            // Client paths
            (GeneratorType.Client, "Models") => $"Contracts\\{groupNamePart}",
            (GeneratorType.Client, "Parameters") => $"Contracts\\{groupNamePart}/RequestParameters",
            (GeneratorType.Client, "Client") => $"Endpoints\\{groupNamePart}",

            // ServerDomain paths
            (GeneratorType.ServerDomain, "Handlers") => $"Handlers\\{groupNamePart}",
            (GeneratorType.ServerDomain, "DependencyInjection") => "Extensions",

            // Default
            _ => category,
        };
    }

    /// <summary>
    /// Generates model types from OpenAPI schemas.
    /// Includes support for polymorphic types - variant records will inherit from their base types.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace generation.</param>
    /// <param name="generatorType">The generator type (Server or Client).</param>
    /// <param name="generatePartialModels">Whether to generate partial records for extensibility.</param>
    /// <param name="granularity">Client granularity; under Single, models are placed in a flat folder.</param>
    public static List<GeneratedType> GenerateModels(
        OpenApiDocument openApiDoc,
        string projectName,
        GeneratorType generatorType = GeneratorType.Server,
        bool generatePartialModels = false,
        ClientGranularityType granularity = ClientGranularityType.PerArea)
    {
        var result = new List<GeneratedType>();

        // Extract polymorphic configurations for inheritance tracking
        var polymorphicConfigs = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(openApiDoc);

        // Use polymorphism-aware extraction so variant types get inheritance
        var records = SchemaExtractor.ExtractIndividualWithPolymorphism(openApiDoc, polymorphicConfigs, generatePartialModels: generatePartialModels);

        if (records is null || records.Count == 0)
        {
            return result;
        }

        var @namespace = $"{projectName}.Generated.Models";
        var codeDocGenerator = new CodeDocumentationTagsGenerator();

        foreach (var recordParams in records)
        {
            var content = GenerateRecordContentOnly(codeDocGenerator, recordParams);
            var usings = new List<string>(ModelUsings);

            // Add System.Collections.Generic if any property uses Dictionary type
            var usesDictionary = recordParams.Parameters?.Any(p => p.TypeName.StartsWith("Dictionary<", StringComparison.Ordinal));
            if (usesDictionary == true)
            {
                usings.Add(NamespaceConstants.SystemCollectionsGeneric);
            }

            if (UsingStatementHelper.RecordUsesSystemTypes(recordParams))
            {
                usings.Add(NamespaceConstants.System);
            }

            if (UsingStatementHelper.RecordUsesJsonPropertyName(recordParams))
            {
                usings.Add(NamespaceConstants.SystemTextJsonSerialization);
            }

            // Determine group name from schema usage in operations
            var groupName = GetGroupNameForSchema(openApiDoc, recordParams.Name);
            var subFolder = GetSubFolder("Models", groupName, generatorType, granularity);

            result.Add(new GeneratedType(
                TypeName: recordParams.Name,
                Category: "Models",
                Namespace: @namespace,
                Content: content,
                RequiredUsings: usings,
                GroupName: groupName,
                SubFolder: subFolder));
        }

        return result;
    }

    /// <summary>
    /// Generates record types for inline schemas discovered during endpoint extraction.
    /// Inline schemas are object types defined directly in responses/requests rather than as $ref.
    /// </summary>
    /// <param name="inlineSchemas">Dictionary of inline schemas discovered during endpoint extraction.</param>
    /// <param name="projectName">The project name for namespace generation.</param>
    /// <param name="generatorType">The generator type (Server or Client).</param>
    /// <returns>List of generated types for inline schemas.</returns>
    public static List<GeneratedType> GenerateInlineModels(
        Dictionary<string, EndpointPerOperationExtractor.InlineSchemaInfo> inlineSchemas,
        string projectName,
        GeneratorType generatorType = GeneratorType.Client)
        => GenerateInlineModelsInternal(
            inlineSchemas.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value.PathSegment, kvp.Value.RecordParameters),
                StringComparer.Ordinal),
            projectName,
            generatorType);

    /// <summary>
    /// Generates record types for inline schemas from HttpClientExtractor.
    /// </summary>
    /// <param name="inlineSchemas">The dictionary of inline schemas discovered during extraction.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="generatorType">The generator type (Server or Client).</param>
    /// <param name="granularity">Client granularity; under Single, inline models are placed flat.</param>
    /// <returns>List of generated types for inline schemas.</returns>
    public static List<GeneratedType> GenerateInlineModels(
        Dictionary<string, HttpClientInlineSchemaInfo> inlineSchemas,
        string projectName,
        GeneratorType generatorType = GeneratorType.Client,
        ClientGranularityType granularity = ClientGranularityType.PerArea)
        => GenerateInlineModelsInternal(
            inlineSchemas.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value.PathSegment, kvp.Value.RecordParameters),
                StringComparer.Ordinal),
            projectName,
            generatorType,
            granularity);

    /// <summary>
    /// Generates record types for inline schemas from ResultClassExtractor.
    /// </summary>
    /// <param name="inlineSchemas">The dictionary of inline schemas discovered during extraction.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="generatorType">The generator type (Server or Client).</param>
    /// <returns>List of generated types for inline schemas.</returns>
    public static List<GeneratedType> GenerateInlineModels(
        Dictionary<string, ResultClassInlineSchemaInfo> inlineSchemas,
        string projectName,
        GeneratorType generatorType = GeneratorType.Server,
        ClientGranularityType granularity = ClientGranularityType.PerArea)
        => GenerateInlineModelsInternal(
            inlineSchemas.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value.PathSegment, kvp.Value.RecordParameters),
                StringComparer.Ordinal),
            projectName,
            generatorType,
            granularity);

    /// <summary>
    /// Produces the server-side <c>Streaming/SequentialResults.cs</c> helper — the writer-based
    /// sequential-streaming writers and their <c>IResult</c> wrappers for JSON Lines
    /// (<c>JsonLinesResult&lt;T&gt;</c>), JSON Text Sequence (<c>JsonSequenceResult&lt;T&gt;</c>) and
    /// multipart/mixed (<c>MultipartMixedResult&lt;T&gt;</c>) — once when an operation uses one of
    /// those framings, or <c>null</c> when none does. (Server-Sent Events use the first-party
    /// <c>TypedResults.ServerSentEvents</c> writer and need no helper.) Single shared producer for
    /// both the Roslyn server generator and the integration test orchestrator, mirroring the
    /// client-side <c>AddStreamReadersIfNeeded</c>. The full file content is carried in
    /// <see cref="GeneratedType.Content"/> (it already starts with the auto-generated header).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The generated server project name (root namespace prefix).</param>
    /// <returns>The SequentialResults generated type, or <c>null</c> when not required.</returns>
    public static GeneratedType? GenerateSequentialResults(
        OpenApiDocument openApiDoc,
        string projectName)
    {
        if (!SequentialResultsExtractor.DocumentRequiresSequentialResults(openApiDoc))
        {
            return null;
        }

        return new GeneratedType(
            TypeName: "SequentialResults",
            Category: "Streaming",
            Namespace: $"{projectName}.Generated.Streaming",
            Content: SequentialResultsExtractor.GenerateContent(projectName),
            RequiredUsings: [],
            GroupName: null,
            SubFolder: "Streaming");
    }

    /// <summary>
    /// Formats a single generated type as a complete file.
    /// </summary>
    public static string FormatAsFile(GeneratedType type)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        // If content already contains full file structure (header, usings, namespace),
        // return it as-is to avoid duplication
        if (type.Content.StartsWith("// <auto-generated />", StringComparison.Ordinal))
        {
            return type.Content;
        }

        var sb = new StringBuilder();

        // Header
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Usings (sorted per SA1210: System.* first, then alphabetical)
        UsingStatementHelper.AppendUsings(sb, type.RequiredUsings);
        sb.AppendLine();

        // Namespace
        sb.AppendLine($"namespace {type.Namespace};");
        sb.AppendLine();

        // Type content
        sb.Append(type.Content);

        return sb.ToString();
    }

    /// <summary>
    /// Formats a single generated type as a test file (no #nullable enable).
    /// Used for verified test output files.
    /// </summary>
    public static string FormatAsTestFile(GeneratedType type)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        // If content already contains full file structure (header, usings, namespace),
        // return it as-is to avoid duplication
        if (type.Content.StartsWith("// <auto-generated />", StringComparison.Ordinal))
        {
            return type.Content;
        }

        var sb = new StringBuilder();

        // Header
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");

        // Usings (sorted per SA1210: System.* first, then alphabetical)
        if (type.RequiredUsings.Count > 0)
        {
            UsingStatementHelper.AppendUsings(sb, type.RequiredUsings);
            sb.AppendLine();
        }

        // Namespace
        sb.AppendLine($"namespace {type.Namespace};");
        sb.AppendLine();

        // Type content
        sb.Append(type.Content);

        return sb.ToString();
    }

    /// <summary>
    /// Checks if the OpenAPI document contains any query, path or header parameters with array type, or a form body
    /// (bound field by field) with a list field - all are bound through the generated ParsableList types.
    /// </summary>
    public static bool HasQueryArrayParameters(OpenApiDocument openApiDoc)
    {
        if (openApiDoc.Paths is null)
        {
            return false;
        }

        foreach (var path in openApiDoc.Paths)
        {
            if (path.Value is not IOpenApiPathItem pathItem || pathItem.Operations is null)
            {
                continue;
            }

            if (pathItem.Parameters is not null)
            {
                foreach (var param in pathItem.Parameters)
                {
                    if (IsQueryArrayParameter(param))
                    {
                        return true;
                    }
                }
            }

            foreach (var operation in pathItem.Operations)
            {
                if (operation.Value is { } formOperation &&
                    formOperation.RequiresFormBindingFlattening() &&
                    formOperation.GetFormBodySchemaInfo().Properties?.Values.Any(p => p.IsOfType(JsonSchemaType.Array) && !p.GetFileUploadInfo().IsFile) == true)
                {
                    return true;
                }

                if (operation.Value?.Parameters is null)
                {
                    continue;
                }

                foreach (var param in operation.Value.Parameters)
                {
                    if (IsQueryArrayParameter(param))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Generates webhook handler interfaces from OpenAPI webhooks.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing webhook definitions.</param>
    /// <param name="projectName">The project name for namespace generation.</param>
    /// <param name="includeDeprecated">Whether to include deprecated webhooks.</param>
    /// <returns>List of generated webhook handler interface types.</returns>
    public static List<GeneratedType> GenerateWebhookHandlerInterfaces(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
    {
        var result = new List<GeneratedType>();

        if (!openApiDoc.HasWebhooks())
        {
            return result;
        }

        var modelNames = openApiDoc.Components?.Schemas?.Keys ?? [];
        var systemTypeResolver = new SystemTypeConflictResolver(modelNames);
        var interfaces = WebhookHandlerExtractor.Extract(openApiDoc, projectName, systemTypeResolver, includeDeprecated);

        if (interfaces is null || interfaces.Count == 0)
        {
            return result;
        }

        var @namespace = NamespaceBuilder.ForWebhookHandlers(projectName);
        var codeDocGenerator = new CodeDocumentationTagsGenerator();

        foreach (var interfaceParams in interfaces)
        {
            var content = GenerateInterfaceContentOnly(codeDocGenerator, interfaceParams);

            // Handler interfaces reference Parameter and Result types (in Webhooks namespace),
            // not model types directly - models are referenced by Parameter classes
            var usings = new List<string>(HandlerUsings)
            {
                NamespaceBuilder.ForWebhookParameters(projectName),
                NamespaceBuilder.ForWebhookResults(projectName),
            };

            result.Add(new GeneratedType(
                TypeName: interfaceParams.InterfaceTypeName,
                Category: "WebhookHandlers",
                Namespace: @namespace,
                Content: content,
                RequiredUsings: usings,
                GroupName: "Webhooks",
                SubFolder: "Webhooks\\Handlers"));
        }

        return result;
    }

    /// <summary>
    /// Generates webhook parameter types from OpenAPI webhooks.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing webhook definitions.</param>
    /// <param name="projectName">The project name for namespace generation.</param>
    /// <param name="includeDeprecated">Whether to include deprecated webhooks.</param>
    /// <returns>List of generated webhook parameter types.</returns>
    public static List<GeneratedType> GenerateWebhookParameters(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
    {
        var result = new List<GeneratedType>();

        if (!openApiDoc.HasWebhooks())
        {
            return result;
        }

        var modelNames = openApiDoc.Components?.Schemas?.Keys ?? [];
        var systemTypeResolver = new SystemTypeConflictResolver(modelNames);
        var webhookParameters = WebhookParameterExtractor.Extract(openApiDoc, projectName, systemTypeResolver, includeDeprecated);

        if (webhookParameters is null || webhookParameters.Count == 0)
        {
            return result;
        }

        var @namespace = NamespaceBuilder.ForWebhookParameters(projectName);

        foreach (var (className, content) in webhookParameters)
        {
            // The content is already complete, so we use it directly
            result.Add(new GeneratedType(
                TypeName: className,
                Category: "WebhookParameters",
                Namespace: @namespace,
                Content: content,
                RequiredUsings: [], // Content is self-contained
                GroupName: "Webhooks",
                SubFolder: "Webhooks\\Parameters"));
        }

        return result;
    }

    /// <summary>
    /// Generates webhook result types from OpenAPI webhooks.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing webhook definitions.</param>
    /// <param name="projectName">The project name for namespace generation.</param>
    /// <param name="includeDeprecated">Whether to include deprecated webhooks.</param>
    /// <returns>List of generated webhook result types.</returns>
    public static List<GeneratedType> GenerateWebhookResults(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
    {
        var result = new List<GeneratedType>();

        if (!openApiDoc.HasWebhooks())
        {
            return result;
        }

        var webhookResults = WebhookResultExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (webhookResults is null || webhookResults.Count == 0)
        {
            return result;
        }

        var @namespace = NamespaceBuilder.ForWebhookResults(projectName);

        foreach (var (className, content) in webhookResults)
        {
            // The content is already complete, so we use it directly
            result.Add(new GeneratedType(
                TypeName: className,
                Category: "WebhookResults",
                Namespace: @namespace,
                Content: content,
                RequiredUsings: [], // Content is self-contained
                GroupName: "Webhooks",
                SubFolder: "Webhooks\\Results"));
        }

        return result;
    }

    /// <summary>
    /// Generates webhook endpoint registration from OpenAPI webhooks.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing webhook definitions.</param>
    /// <param name="projectName">The project name for namespace generation.</param>
    /// <param name="config">The server configuration (used for webhook base path and includeDeprecated).</param>
    /// <returns>Generated webhook endpoint registration type, or null if no webhooks.</returns>
    public static GeneratedType? GenerateWebhookEndpoints(
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config)
    {
        if (!openApiDoc.HasWebhooks())
        {
            return null;
        }

        var classParams = WebhookEndpointExtractor.Extract(openApiDoc, projectName, config);

        if (classParams is null)
        {
            return null;
        }

        var @namespace = NamespaceBuilder.ForWebhookEndpoints(projectName);
        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var content = GenerateClassContentOnly(codeDocGenerator, classParams);

        var usings = new List<string>(EndpointUsings)
        {
            NamespaceConstants.SystemCodeDomCompiler,
            NamespaceBuilder.ForWebhookHandlers(projectName),
            NamespaceBuilder.ForWebhookParameters(projectName),
            NamespaceBuilder.ForWebhookResults(projectName),
        };

        return new GeneratedType(
            TypeName: classParams.ClassTypeName,
            Category: "WebhookEndpoints",
            Namespace: @namespace,
            Content: content,
            RequiredUsings: usings,
            GroupName: "Webhooks",
            SubFolder: "Webhooks\\Endpoints");
    }

    /// <summary>
    /// Generates webhook dependency injection registration from OpenAPI webhooks.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing webhook definitions.</param>
    /// <param name="projectName">The project name for namespace generation.</param>
    /// <param name="includeDeprecated">Whether to include deprecated webhooks.</param>
    /// <returns>Generated webhook DI registration type, or null if no webhooks.</returns>
    public static GeneratedType? GenerateWebhookDependencyInjection(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
    {
        if (!openApiDoc.HasWebhooks())
        {
            return null;
        }

        var classParams = WebhookDependencyInjectionExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (classParams is null)
        {
            return null;
        }

        var @namespace = $"{projectName}.Generated.DependencyInjection";
        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var content = GenerateClassContentOnly(codeDocGenerator, classParams);

        var usings = new List<string>(DependencyInjectionUsings)
        {
            NamespaceConstants.SystemCodeDomCompiler,
            NamespaceBuilder.ForWebhookHandlers(projectName),
        };

        return new GeneratedType(
            TypeName: classParams.ClassTypeName,
            Category: "WebhookDependencyInjection",
            Namespace: @namespace,
            Content: content,
            RequiredUsings: usings,
            GroupName: "Webhooks",
            SubFolder: "Extensions");
    }

    /// <summary>
    /// Gets the group name from an OpenAPI operation (using tag) or derives one from the path.
    /// Used internally for schema/operation group name lookup.
    /// </summary>
    private static string GetGroupNameForOperation(
        OpenApiOperation operation,
        string path)
    {
        if (operation.Tags is not { Count: > 0 })
        {
            return GetGroupNameFromPath(path);
        }

        var firstTag = operation.Tags.FirstOrDefault()?.Name;
        return string.IsNullOrEmpty(firstTag)
            ? GetGroupNameFromPath(path)
            : firstTag!.ToPascalCaseForDotNet();
    }

    private static bool SchemaReferencesName(
        IOpenApiSchema? schema,
        string schemaName)
    {
        if (schema is null)
        {
            return false;
        }

        // Check if this is a reference to the schema
        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refId = schemaRef.Reference.Id;
            if (string.Equals(refId, schemaName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check array items
        if (schema is OpenApiSchema { Items: OpenApiSchemaReference itemsRef })
        {
            var refId = itemsRef.Reference.Id;
            if (string.Equals(refId, schemaName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<GeneratedType> GenerateInlineModelsInternal(
        Dictionary<string, (string PathSegment, RecordParameters RecordParameters)> inlineSchemas,
        string projectName,
        GeneratorType generatorType,
        ClientGranularityType granularity = ClientGranularityType.PerArea)
    {
        var result = new List<GeneratedType>();

        if (inlineSchemas is null || inlineSchemas.Count == 0)
        {
            return result;
        }

        var codeDocGenerator = new CodeDocumentationTagsGenerator();

        foreach (var kvp in inlineSchemas)
        {
            var typeName = kvp.Key;
            var (pathSegment, recordParams) = kvp.Value;

            // Under Single granularity every inline model shares one flat namespace/folder,
            // matching the flat {root}.Generated.Models placement of component schemas.
            var isSingle = generatorType == GeneratorType.Client &&
                           granularity == ClientGranularityType.Single;
            var effectivePathSegment = isSingle ? null : pathSegment;

            var @namespace = NamespaceBuilder.ForModels(projectName, effectivePathSegment);
            var content = GenerateRecordContentOnly(codeDocGenerator, recordParams);
            var usings = new List<string>(ModelUsings);

            // Add System.Collections.Generic if any property uses Dictionary type
            var usesDictionary = recordParams.Parameters?.Any(p => p.TypeName.StartsWith("Dictionary<", StringComparison.Ordinal));
            if (usesDictionary == true)
            {
                usings.Add(NamespaceConstants.SystemCollectionsGeneric);
            }

            if (UsingStatementHelper.RecordUsesSystemTypes(recordParams))
            {
                usings.Add(NamespaceConstants.System);
            }

            // [JsonPropertyName] for a non-C# property name, [JsonRequired] with strictRequiredProperties
            if (recordParams.Parameters?.Any(p => p.Attributes?.Any(a => a.Name.StartsWith("Json", StringComparison.Ordinal)) == true) == true)
            {
                usings.Add(NamespaceConstants.SystemTextJsonSerialization);
            }

            var subFolder = GetSubFolder("Models", effectivePathSegment, generatorType, granularity);

            result.Add(new GeneratedType(
                TypeName: typeName,
                Category: "InlineModels",
                Namespace: @namespace,
                Content: content,
                RequiredUsings: usings,
                GroupName: effectivePathSegment,
                SubFolder: subFolder));
        }

        return result;
    }

    /// <summary>
    /// Generates class content without header/namespace.
    /// </summary>
    private static string GenerateClassContentOnly(
        ICodeDocumentationTagsGenerator codeDocGenerator,
        ClassParameters classParams)
    {
        // Create a copy with no header/namespace (just generate the class body)
        var modifiedParams = classParams with { HeaderContent = null, Namespace = "TEMP" };

        var contentGenerator = new GenerateContentForClass(codeDocGenerator, modifiedParams);
        var fullContent = contentGenerator.Generate();

        return ExtractContentAfterNamespace(fullContent);
    }

    /// <summary>
    /// Generates interface content without header/namespace.
    /// </summary>
    private static string GenerateInterfaceContentOnly(
        ICodeDocumentationTagsGenerator codeDocGenerator,
        InterfaceParameters interfaceParams)
    {
        // Create a copy with no header/namespace (just generate the interface body)
        var modifiedParams = interfaceParams with { HeaderContent = null, Namespace = "TEMP" };

        var contentGenerator = new GenerateContentForInterface(codeDocGenerator, modifiedParams);
        var fullContent = contentGenerator.Generate();

        return ExtractContentAfterNamespace(fullContent);
    }

    /// <summary>
    /// Generates record content without header/namespace.
    /// </summary>
    private static string GenerateRecordContentOnly(
        ICodeDocumentationTagsGenerator codeDocGenerator,
        RecordParameters recordParams)
    {
        // Wrap single record in RecordsParameters container
        var recordsContainer = new RecordsParameters(
            HeaderContent: null,
            Namespace: "TEMP",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters: [recordParams]);

        var fullContent = RecordContentGenerator.Generate(codeDocGenerator, recordsContainer);

        return ExtractContentAfterNamespace(fullContent);
    }

    /// <summary>
    /// Extracts content after the namespace declaration, preserving blank lines and formatting.
    /// </summary>
    private static string ExtractContentAfterNamespace(string fullContent)
    {
        // Find the namespace line ending with ";\r\n" or ";\n"
        const string namespaceMarker = "namespace TEMP;";
        var namespaceIndex = fullContent.IndexOf(namespaceMarker, StringComparison.Ordinal);

        if (namespaceIndex < 0)
        {
            return fullContent;
        }

        // Skip past the namespace line
        var startIndex = namespaceIndex + namespaceMarker.Length;

        // Skip any line endings after namespace
        while (startIndex < fullContent.Length &&
               (fullContent[startIndex] == '\r' || fullContent[startIndex] == '\n'))
        {
            startIndex++;
        }

        return startIndex < fullContent.Length
            ? fullContent.Substring(startIndex)
            : fullContent;
    }

    /// <summary>
    /// Checks if a parameter is a query parameter with array type.
    /// </summary>
    private static bool IsQueryArrayParameter(IOpenApiParameter parameterOrRef)
    {
        var resolved = parameterOrRef.Resolve();
        var parameter = resolved.Parameter;

        if (parameter is null)
        {
            return false;
        }

        // A query, path or header array: each is bound through ParsableList (a path or header array is its items
        // joined by commas)
        var location = parameter.In ?? ParameterLocation.Query;
        if (location is not (ParameterLocation.Query or ParameterLocation.Path or ParameterLocation.Header))
        {
            return false;
        }

        if (parameter.Schema is OpenApiSchema schema)
        {
            return schema.Type?.HasFlag(JsonSchemaType.Array) == true;
        }

        return false;
    }
}