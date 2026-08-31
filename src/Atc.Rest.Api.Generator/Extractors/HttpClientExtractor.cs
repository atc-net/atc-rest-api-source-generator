// ReSharper disable InvertIf
namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI operations and converts them to ClassParameters for HTTP client class generation.
/// </summary>
public static class HttpClientExtractor
{
    /// <summary>
    /// Extracts HTTP client class from OpenAPI document paths and operations.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace and class name).</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <param name="useServersBasePath">Whether to prepend the base path from OpenAPI servers[0].url to URLs. Default: true.</param>
    /// <returns>ClassParameters for the HTTP client class, or null if no paths exist.</returns>
    public static ClassParameters? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated = false,
        bool useServersBasePath = true)
        => Extract(openApiDoc, projectName, pathSegment: null, registry: registry, systemTypeResolver: systemTypeResolver, includeDeprecated: includeDeprecated, useServersBasePath: useServersBasePath);

    /// <summary>
    /// Extracts HTTP client class from OpenAPI document paths and operations filtered by path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace and class name).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets"). If null, extracts all operations.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <param name="useServersBasePath">Whether to prepend the base path from OpenAPI servers[0].url to URLs. Default: true.</param>
    /// <returns>ClassParameters for the HTTP client class, or null if no paths exist.</returns>
    public static ClassParameters? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated = false,
        bool useServersBasePath = true)
        => ExtractInternal(openApiDoc, projectName, pathSegment, registry, systemTypeResolver, includeDeprecated, inlineSchemas: null, useServersBasePath: useServersBasePath);

    /// <summary>
    /// Extracts HTTP client class from OpenAPI document along with any inline schemas discovered.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace and class name).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets"). If null, extracts all operations.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <param name="useServersBasePath">Whether to prepend the base path from OpenAPI servers[0].url to URLs. Default: true.</param>
    /// <param name="hasSegmentModels">Whether the segment has segment-specific models.</param>
    /// <param name="hasSharedModels">Whether there are shared models in the project.</param>
    /// <param name="namespaceSegment">The segment used for namespaces and the client class name. When null, <paramref name="pathSegment"/> is used. Pass an explicitly resolved value (possibly empty) to omit the segment.</param>
    /// <param name="clientSuffix">The client class name suffix. Defaults to "Client" when null or empty.</param>
    /// <param name="clientName">Explicit client type name, used verbatim when supplied.</param>
    /// <returns>A tuple containing the ClassParameters and a dictionary of discovered inline schemas.</returns>
    public static (ClassParameters? ClientClass, Dictionary<string, HttpClientInlineSchemaInfo> InlineSchemas) ExtractWithInlineSchemas(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated = false,
        bool useServersBasePath = true,
        bool? hasSegmentModels = null,
        bool? hasSharedModels = null,
        string? namespaceSegment = null,
        string? clientSuffix = null,
        string? clientName = null)
    {
        var inlineSchemas = new Dictionary<string, HttpClientInlineSchemaInfo>(StringComparer.Ordinal);
        var clientClass = ExtractInternal(openApiDoc, projectName, pathSegment, registry, systemTypeResolver, includeDeprecated, inlineSchemas, useServersBasePath, hasSegmentModels, hasSharedModels, namespaceSegment, clientSuffix, clientName);
        return (clientClass, inlineSchemas);
    }

    private static ClassParameters? ExtractInternal(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated,
        Dictionary<string, HttpClientInlineSchemaInfo>? inlineSchemas,
        bool useServersBasePath = true,
        bool? hasSegmentModels = null,
        bool? hasSharedModels = null,
        string? namespaceSegment = null,
        string? clientSuffix = null,
        string? clientName = null)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (openApiDoc.Paths is null || openApiDoc.Paths.Count == 0)
        {
            return null;
        }

        var effectiveNamespaceSegment = namespaceSegment ?? pathSegment;

        // An explicit client name is the author stating the full type name, so it wins outright.
        var className = !string.IsNullOrWhiteSpace(clientName)
            ? clientName!.Trim()
            : string.IsNullOrEmpty(effectiveNamespaceSegment)
                ? CasingHelper.BuildClientTypeName(projectName, clientSuffix)
                : CasingHelper.BuildClientTypeName(effectiveNamespaceSegment, clientSuffix);

        // An empty namespace segment means Single granularity: the client is flattened into
        // "{projectName}.Generated" rather than the per-area "{projectName}.Generated.Client".
        var namespaceValue = string.IsNullOrEmpty(effectiveNamespaceSegment)
            ? NamespaceBuilder.BuildBase(projectName)
            : NamespaceBuilder.ForClient(projectName, effectiveNamespaceSegment);
        var modelsNamespace = NamespaceBuilder.ForModels(projectName, effectiveNamespaceSegment);

        var additionalFieldDeclarations = new List<string>
        {
            "private static readonly JsonSerializerOptions defaultJsonSerializerOptions = new JsonSerializerOptions",
            "{",
            "    PropertyNameCaseInsensitive = true,",
            "    Converters = { new JsonStringEnumConverter() },",
            "};",
        };

        var constructor1Params = new List<ConstructorParameterBaseParameters>
        {
            new(
                GenericTypeName: null,
                TypeName: "HttpClient",
                IsNullableType: false,
                Name: "httpClient",
                DefaultValue: null,
                PassToInheritedClass: false,
                CreateAsPrivateReadonlyMember: true,
                CreateAaOneLiner: false),
        };

        var constructor1 = new ConstructorParameters(
            DocumentationTags: null,
            DeclarationModifier: DeclarationModifiers.Public,
            GenericTypeName: null,
            TypeName: className,
            InheritedClassTypeName: null,
            Parameters: constructor1Params,
            AdditionalStatements: ["this.jsonSerializerOptions = defaultJsonSerializerOptions;"]);

        var constructor2Params = new List<ConstructorParameterBaseParameters>
        {
            new(
                GenericTypeName: null,
                TypeName: "HttpClient",
                IsNullableType: false,
                Name: "httpClient",
                DefaultValue: null,
                PassToInheritedClass: false,
                CreateAsPrivateReadonlyMember: false,
                CreateAaOneLiner: false),
            new(
                GenericTypeName: null,
                TypeName: "JsonSerializerOptions",
                IsNullableType: false,
                Name: "jsonSerializerOptions",
                DefaultValue: null,
                PassToInheritedClass: false,
                CreateAsPrivateReadonlyMember: true,
                CreateAaOneLiner: false),
        };

        var constructor2 = new ConstructorParameters(
            DocumentationTags: null,
            DeclarationModifier: DeclarationModifiers.Public,
            GenericTypeName: null,
            TypeName: className,
            InheritedClassTypeName: null,
            Parameters: constructor2Params);

        var methods = new List<MethodParameters>();

        foreach (var path in openApiDoc.Paths)
        {
            var pathKey = path.Key;
            var pathItemInterface = path.Value;

            // Apply path segment filter if provided
            if (!string.IsNullOrEmpty(pathSegment))
            {
                var currentSegment = PathSegmentHelper.GetFirstPathSegment(pathKey);
                if (!currentSegment.Equals(pathSegment, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            if (pathItemInterface is not IOpenApiPathItem pathItem)
            {
                continue;
            }

            if (pathItem.Operations is not null)
            {
                // Get path-level parameters (defined on the path, not the operation)
                var pathLevelParameters = pathItem.Parameters;

                foreach (var operation in pathItem.Operations)
                {
                    // Skip deprecated operations if not including them
                    if (!includeDeprecated && operation.Value?.Deprecated == true)
                    {
                        continue;
                    }

                    var httpMethod = operation
                        .Key
                        .ToString()
                        .ToUpperInvariant();

                    var currentPathSegment = PathSegmentHelper.GetFirstPathSegment(pathKey);
                    var methodParams = ExtractMethod(pathKey, httpMethod, operation.Value, pathLevelParameters, openApiDoc, registry, systemTypeResolver, currentPathSegment, inlineSchemas, useServersBasePath);

                    if (methodParams is not null)
                    {
                        methods.Add(methodParams);
                    }
                }
            }
        }

        // Return null if no methods were extracted for this segment
        if (methods.Count == 0)
        {
            return null;
        }

        // Build content preview to analyze for required usings
        var contentPreview = new StringBuilder();
        foreach (var method in methods)
        {
            contentPreview.AppendLine(method.ReturnTypeName);
            contentPreview.AppendLine(method.ReturnGenericTypeName);
            contentPreview.AppendLine(method.Content);
            if (method.Parameters is not null)
            {
                foreach (var param in method.Parameters)
                {
                    contentPreview.AppendLine(param.TypeName);
                    if (param.Attributes is not null)
                    {
                        foreach (var attr in param.Attributes)
                        {
                            contentPreview.AppendLine($"[{attr.Name}]");
                        }
                    }
                }
            }
        }

        contentPreview.AppendLine("JsonSerializerOptions");
        contentPreview.AppendLine("JsonStringEnumConverter");

        var contentForAnalysis = contentPreview.ToString();

        // Add the EnsureSuccessAsync helper method that reads error body before throwing
        methods.Add(CreateEnsureSuccessMethod());

        // Build header content with only required usings
        var usings = UsingStatementHelper.GetRequiredUsings(
            contentForAnalysis,
            NamespaceConstants.SystemCodeDomCompiler);

        // Models usings. When the caller knows which models exist (Roslyn per-segment client),
        // reference the shared and/or segment namespaces precisely — emitting a using for a
        // namespace that has no types (e.g. a segment that only references shared models) would
        // not compile. The legacy/CodeGenerationService callers pass null and keep the single
        // resolved namespace as before.
        if (!string.IsNullOrEmpty(pathSegment) && (hasSegmentModels.HasValue || hasSharedModels.HasValue))
        {
            if (hasSegmentModels == true)
            {
                usings.Add(modelsNamespace);
            }

            if (hasSharedModels == true)
            {
                usings.Add(NamespaceBuilder.ForModels(projectName));
            }
        }
        else
        {
            usings.Add(modelsNamespace);
        }

        // Reference the emitted StreamReaders helper namespace when this segment's methods use it
        // (Server-Sent Events reads). NOTE: a typed bool ("does any operation use StreamReaders")
        // would be cleaner than this content scan, but it must be computed PER SEGMENT — the
        // doc-wide StreamReadersExtractor.DocumentRequiresStreamReaders would over-inject the using
        // into a sibling segment client that has no SSE op. Until a per-segment framing signal is
        // threaded through, the content scan keeps the using precisely scoped to where it is used.
        if (contentForAnalysis.IndexOf("StreamReaders", StringComparison.Ordinal) >= 0)
        {
            usings.Add($"{projectName}.Generated.Streaming");
        }

        // multipart/mixed extracts the response Content-Type boundary via LINQ (.FirstOrDefault) —
        // ensure System.Linq is imported (the content-scan type map doesn't cover LINQ calls).
        if (contentForAnalysis.IndexOf(".FirstOrDefault(", StringComparison.Ordinal) >= 0)
        {
            usings.Add(NamespaceConstants.SystemLinq);
        }

        var headerBuilder = new StringBuilder();
        headerBuilder.AppendLine("// <auto-generated />");
        headerBuilder.AppendLine("#nullable enable");
        headerBuilder.AppendLine();
        UsingStatementHelper.AppendUsings(headerBuilder, usings);
        headerBuilder.AppendLine();

        return new ClassParameters(
            HeaderContent: headerBuilder.ToString(),
            Namespace: namespaceValue,
            DocumentationTags: null,
            Attributes:
            [
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            ],
            DeclarationModifier: DeclarationModifiers.PublicSealedClass,
            ClassTypeName: className,
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: [constructor1, constructor2],
            Properties: null,
            Methods: methods,
            GenerateToStringMethod: false,
            AdditionalFieldDeclarations: additionalFieldDeclarations);
    }

    /// <summary>
    /// Extracts client parameter classes from OpenAPI document operations filtered by path segment.
    /// Uses OperationParameterExtractor with binding attributes disabled.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets"). If null, extracts all operations.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>List of RecordParameters for parameter DTOs.</returns>
    public static List<RecordParameters>? ExtractParameters(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false,
        ValidateSpecificationStrategy validateStrategy = ValidateSpecificationStrategy.Strict)
        => OperationParameterExtractor.ExtractIndividual(
            openApiDoc,
            projectName,
            pathSegment,
            registry: registry,
            includeBindingAttributes: false,
            namespaceSubFolder: "Client",
            includeDeprecated: includeDeprecated,
            validateStrategy: validateStrategy);

    /// <summary>
    /// Inline-enum-aware variant of <see cref="ExtractParameters"/>. Returns both the
    /// parameter records and any inline enums discovered on parameter schemas, so the
    /// Roslyn client generator can emit them as separate <c>.g.cs</c> files.
    /// </summary>
    public static (List<RecordParameters>? Records, List<InlineEnumInfo> InlineEnums) ExtractParametersWithInlineEnums(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false,
        ValidateSpecificationStrategy validateStrategy = ValidateSpecificationStrategy.Strict)
        => OperationParameterExtractor.ExtractIndividualWithInlineEnums(
            openApiDoc,
            projectName,
            pathSegment,
            registry,
            includeBindingAttributes: false,
            namespaceSubFolder: "Client",
            includeDeprecated: includeDeprecated,
            validateStrategy: validateStrategy);

    private static MethodParameters? ExtractMethod(
        string path,
        string httpMethod,
        OpenApiOperation? operation,
        IList<IOpenApiParameter>? pathLevelParameters,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        string pathSegment,
        Dictionary<string, HttpClientInlineSchemaInfo>? inlineSchemas,
        bool useServersBasePath = true)
    {
        if (operation is null)
        {
            return null;
        }

        // Check if this is an async enumerable streaming operation (x-* annotation or 3.2 itemSchema)
        var isAsyncEnumerable = operation.IsStreamingResponse();
        var normalizedPath = path
            .Replace('/', '_')
            .Replace("{", string.Empty)
            .Replace("}", string.Empty);
        var operationId = operation.OperationId ?? $"{httpMethod}{normalizedPath}";
        var methodName = operationId.ToPascalCaseForDotNet() + "Async";
        var parametersClassName = $"{operationId.ToPascalCaseForDotNet()}Parameters";

        // Determine return type - check both 200 and 201 responses
        var returnType = nameof(Task);
        string? streamingItemType = null;
        var hasLocationHeader = false;

        // Try 200 first, then 201 for created responses
        IOpenApiResponse? response = null;
        if (operation.Responses is not null &&
            !operation.Responses.TryGetValue("200", out response))
        {
            operation.Responses.TryGetValue("201", out response);
        }

        // Check for JSON content first
        if (response?.Content is not null && response.Content.TryGetValue("application/json", out var mediaType1))
        {
            var contentType = GetSchemaTypeName(mediaType1.Schema, openApiDoc, registry, operationId, pathSegment, "Response", inlineSchemas);
            if (!string.IsNullOrEmpty(contentType))
            {
                returnType = contentType;

                // For async enumerable, extract the List<T> item type (shared List<T>/T[] extractor)
                if (isAsyncEnumerable && TryGetListElementType(contentType, out var itemType))
                {
                    streamingItemType = itemType;
                }
            }
        }
        else if (response?.Content is not null && IsBinaryResponseContent(response.Content))
        {
            // Binary content (application/octet-stream, image/*, etc.) returns byte[]
            returnType = "byte[]";
        }
        else if (response?.Content is not null && IsTextResponseContent(response.Content))
        {
            // Plain text content returns string
            returnType = "string";
        }
        else if (response is OpenApiResponse openApiResp &&
                 openApiResp.Headers is not null &&
                 openApiResp.Headers.TryGetValue("Location", out var locationHeader) &&
                 locationHeader.Schema is OpenApiSchema { Format: "uri" })
        {
            returnType = "Uri";
            hasLocationHeader = true;
        }

        // OpenAPI 3.2 streaming: when the response declares an itemSchema, the element
        // type comes from it directly (not from an application/json array body).
        if (isAsyncEnumerable && streamingItemType is null)
        {
            var streamingItemSchema = operation.GetStreamingItemSchema();
            if (streamingItemSchema is not null)
            {
                streamingItemType = GetSchemaTypeName(streamingItemSchema, openApiDoc, registry, operationId, pathSegment, "Response", inlineSchemas);
            }
        }

        // Check if operation has parameters or request body (including path-level parameters)
        var hasQueryRouteParams = operation.Parameters is { Count: > 0 } || pathLevelParameters is { Count: > 0 };
        var hasRequestBody = operation.RequestBody is { Content: not null };
        var hasParameters = hasQueryRouteParams || hasRequestBody;

        // Build method parameters
        var parameters = new List<ParameterBaseParameters>();

        // If operation has parameters, use a parameters object
        if (hasParameters)
        {
            parameters.Add(new ParameterBaseParameters(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: parametersClassName,
                IsNullableType: false,
                IsReferenceType: true,
                Name: "parameters",
                DefaultValue: null));
        }

        // Add [EnumeratorCancellation] attribute only for methods that actually return IAsyncEnumerable<T>
        // (requires both the x-return-async-enumerable extension AND a streaming item type from an array response)
        var willReturnAsyncEnumerable = isAsyncEnumerable && streamingItemType is not null;
        var cancellationTokenAttrs = willReturnAsyncEnumerable
            ? new List<AttributeParameters> { new("EnumeratorCancellation", null) }
            : null;

        parameters.Add(new ParameterBaseParameters(
            Attributes: cancellationTokenAttrs,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: "CancellationToken",
            IsNullableType: false,
            IsReferenceType: false,
            Name: "cancellationToken",
            DefaultValue: "default"));

        // Generate method body content
        var hasReturnType = returnType != nameof(Task);
        var methodContent = GenerateMethodBody(path, httpMethod, operation, pathLevelParameters, openApiDoc, returnType, hasParameters, isAsyncEnumerable, streamingItemType, hasReturnType, hasLocationHeader, useServersBasePath, parametersClassName);

        // For async enumerable methods, return IAsyncEnumerable<T> directly
        if (isAsyncEnumerable && streamingItemType is not null)
        {
            return new MethodParameters(
                DocumentationTags: null,
                Attributes: null,
                DeclarationModifier: DeclarationModifiers.PublicAsync,
                ReturnGenericTypeName: "IAsyncEnumerable",
                ReturnTypeName: streamingItemType,
                Name: methodName,
                Parameters: parameters,
                AlwaysBreakDownParameters: false,
                UseExpressionBody: false,
                Content: methodContent);
        }

        var taskTypeName = systemTypeResolver.EnsureFullNamespaceIfNeeded(nameof(Task));

        string? returnGenericTypeName = null;
        string returnTypeName;

        if (returnType == nameof(Task))
        {
            returnTypeName = taskTypeName;
        }
        else
        {
            returnGenericTypeName = taskTypeName;
            returnTypeName = returnType;
        }

        return new MethodParameters(
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicAsync,
            ReturnGenericTypeName: returnGenericTypeName,
            ReturnTypeName: returnTypeName,
            Name: methodName,
            Parameters: parameters,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: methodContent);
    }

    private static string GenerateMethodBody(
        string path,
        string httpMethod,
        OpenApiOperation operation,
        IList<IOpenApiParameter>? pathLevelParameters,
        OpenApiDocument openApiDoc,
        string returnType,
        bool hasParameters,
        bool isAsyncEnumerable,
        string? streamingItemType,
        bool hasReturnType,
        bool hasLocationHeader,
        bool useServersBasePath = true,
        string parametersClassName = "")
    {
        var builder = new StringBuilder();

        // Get server base path if enabled (e.g., "/api/v1" from servers[0].url)
        var serverBasePath = useServersBasePath ? ServerUrlHelper.GetServersBasePath(openApiDoc) : null;

        // Build the URL - optionally prepend server base path, then replace path parameters
        var urlBuilder = serverBasePath is not null ? $"{serverBasePath}{path}" : path;

        // Process path-level parameters first
        if (pathLevelParameters is not null)
        {
            foreach (var paramInterface in pathLevelParameters)
            {
                var resolved = paramInterface.Resolve();
                var (param, _) = (resolved.Parameter, resolved.ReferenceId);
                if (param is null || string.IsNullOrEmpty(param.Name))
                {
                    continue;
                }

                if (param.In == ParameterLocation.Path)
                {
                    var propName = param.Name!.ToPascalCaseForDotNet();
                    var paramType = GetParameterTypeWithInlineEnumAwareness(param, openApiDoc, parametersClassName);
                    var replacement = BuildPathParameterReplacement(propName, paramType);
                    urlBuilder = urlBuilder.Replace($"{{{param.Name}}}", replacement);
                }
            }
        }

        // Then process operation-level parameters
        if (operation.Parameters is not null)
        {
            foreach (var paramInterface in operation.Parameters)
            {
                // Resolve parameter reference if needed
                var resolved = paramInterface.Resolve();
                var (param, _) = (resolved.Parameter, resolved.ReferenceId);
                if (param is null || string.IsNullOrEmpty(param.Name))
                {
                    continue;
                }

                if (param.In == ParameterLocation.Path)
                {
                    var propName = param.Name!.ToPascalCaseForDotNet();
                    var paramType = GetParameterTypeWithInlineEnumAwareness(param, openApiDoc, parametersClassName);
                    var replacement = BuildPathParameterReplacement(propName, paramType);
                    urlBuilder = urlBuilder.Replace($"{{{param.Name}}}", replacement);
                }
            }
        }

        builder.AppendLine(
            urlBuilder.Contains('{')
                ? $"var url = $\"{urlBuilder}\";"
                : $"var url = \"{urlBuilder}\";");

        // Add query parameters - resolve parameter references first
        var queryParams = new List<(OpenApiParameter Param, string? ReferenceId)>();
        var headerParams = new List<(OpenApiParameter Param, string? ReferenceId)>();
        var cookieParams = new List<(OpenApiParameter Param, string? ReferenceId)>();
        var querystringParams = new List<(OpenApiParameter Param, string? ReferenceId)>();
        if (operation.Parameters is not null)
        {
            foreach (var paramInterface in operation.Parameters)
            {
                var resolved = paramInterface.Resolve();
                var (param, referenceId) = (resolved.Parameter, resolved.ReferenceId);
                switch (param)
                {
                    case { In: ParameterLocation.Query }:
                        queryParams.Add((param, referenceId));
                        break;
                    case { In: ParameterLocation.Header }:
                        headerParams.Add((param, referenceId));
                        break;
                    case { In: ParameterLocation.Cookie }:
                        cookieParams.Add((param, referenceId));
                        break;
                    case { In: ParameterLocation.QueryString }:
                        querystringParams.Add((param, referenceId));
                        break;
                }
            }
        }

        if (queryParams.Count > 0)
        {
            builder.AppendLine("var queryParams = new List<string>();");
            foreach (var (param, _) in queryParams)
            {
                var propName = param.Name!.ToPascalCaseForDotNet();
                var paramAccess = $"parameters.{propName}";
                var paramType = GetParameterTypeWithInlineEnumAwareness(param, openApiDoc, parametersClassName);
                var isRequired = param.Required;
                var serialization = param.GetParameterSerialization();

                // Supported form-explode array: emit repeated-key foreach
                if (serialization.ValueKind == ParameterValueKind.Array &&
                    serialization.Style == ParameterStyle.Form &&
                    serialization.Explode &&
                    serialization.IsSupported)
                {
                    AppendQueryArrayForeach(builder, param.Name!, paramAccess, paramType, serialization.AllowReserved, isRequired);
                    continue;
                }

                // allowReserved primitive: emit raw value without URL encoding
                if (serialization.AllowReserved && serialization.ValueKind == ParameterValueKind.Primitive)
                {
                    if (isRequired)
                    {
                        builder.AppendLine($"queryParams.Add($\"{param.Name}={{{paramAccess}}}\");");
                    }
                    else
                    {
                        var nullCheck = BuildNullCheck(paramAccess, paramType);

                        builder.AppendLine();
                        builder.AppendLine($"if ({nullCheck})");
                        builder.AppendLine("{");
                        builder.AppendLine(4, $"queryParams.Add($\"{param.Name}={{{paramAccess}}}\");");
                        builder.AppendLine("}");
                    }

                    continue;
                }

                // Default path: scalar primitive with URL encoding (existing behavior)
                if (isRequired)
                {
                    var valueExpression = BuildEncodedExpression(paramAccess, paramType);
                    builder.AppendLine($"queryParams.Add($\"{param.Name}={{{valueExpression}}}\");");
                }
                else
                {
                    // Use appropriate null check based on type for optional parameters. A bare
                    // T[] that reaches this scalar path (not the form-explode foreach above) keeps
                    // its length guard; everything else delegates to the shared BuildNullCheck.
                    var nullCheck = paramType.EndsWith("[]", StringComparison.Ordinal)
                        ? $"{paramAccess} is not null && {paramAccess}.Length > 0"
                        : BuildNullCheck(paramAccess, paramType);

                    var valueExpression = BuildEncodedExpression(paramAccess, paramType);

                    builder.AppendLine();
                    builder.AppendLine($"if ({nullCheck})");
                    builder.AppendLine("{");
                    builder.AppendLine(4, $"queryParams.Add($\"{param.Name}={{{valueExpression}}}\");");
                    builder.AppendLine("}");
                }
            }

            builder.AppendLine();
            builder.AppendLine("if (queryParams.Count > 0)");
            builder.AppendLine("{");
            builder.AppendLine(4, "url += \"?\" + string.Join(\"&\", queryParams);");
            builder.AppendLine("}");
            builder.AppendLine();
        }

        // OAS 3.2 in:querystring — raw query string appended to URL as-is (no key=value encoding).
        // Multiple querystring params on the same endpoint are unusual but handled by appending.
        foreach (var (param, _) in querystringParams)
        {
            var propName = param.Name!.ToPascalCaseForDotNet();
            var paramAccess = $"parameters.{propName}";

            builder.AppendLine();
            builder.AppendLine($"if (!string.IsNullOrEmpty({paramAccess}))");
            builder.AppendLine("{");
            builder.AppendLine(4, $"url += (url.Contains('?') ? \"&\" : \"?\") + {paramAccess};");
            builder.AppendLine("}");

            builder.AppendLine();
        }

        switch (httpMethod)
        {
            case "GET":
                GenerateGetMethodBody(returnType, isAsyncEnumerable, streamingItemType, operation.GetStreamingFraming(), hasReturnType, builder, headerParams, cookieParams);
                break;
            case "POST":
                GeneratePostMethodBody(operation, openApiDoc, returnType, hasParameters, hasReturnType, hasLocationHeader, builder);
                break;
            case "PUT":
                GeneratePutMethodBody(operation, returnType, hasParameters, hasReturnType, builder);
                break;
            case "DELETE":
                GenerateDeleteMethodBody(returnType, hasReturnType, builder);
                break;
            default:
                // Non-standard verbs: OpenAPI 3.2 `query` and `additionalOperations`
                // (e.g. LINK), plus PATCH. No dedicated HttpClient JSON helper exists,
                // so build the request explicitly and dispatch by body/return presence.
                GenerateGenericMethodBody(httpMethod, operation, returnType, hasParameters, hasReturnType, builder);
                break;
        }

        return builder
            .ToString()
            .Trim();
    }

    private static void GenerateGetMethodBody(
        string returnType,
        bool isAsyncEnumerable,
        string? streamingItemType,
        StreamingFraming streamingFraming,
        bool hasReturnType,
        StringBuilder builder,
        List<(OpenApiParameter Param, string? ReferenceId)> headerParams,
        List<(OpenApiParameter Param, string? ReferenceId)> cookieParams)
    {
        // Special handling for async enumerable streaming
        if (isAsyncEnumerable && !string.IsNullOrEmpty(streamingItemType))
        {
            builder.AppendLine("using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);");
            builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken);");
            builder.AppendLine();
            builder.AppendLine("var stream = await response.Content.ReadAsStreamAsync(cancellationToken);");
            builder.AppendLine();

            // Server-Sent Events, JSON Lines and JSON Text Sequence use the emitted StreamReaders
            // helper to parse their wire framing; multipart/mixed additionally extracts the boundary
            // from the response Content-Type and passes it to the reader. The remaining framings
            // (JsonArray, ...) keep the legacy DeserializeAsyncEnumerable brace-scan path byte-for-byte.
            if (streamingFraming == StreamingFraming.ServerSentEvents)
            {
                builder.AppendLine($"await foreach (var item in StreamReaders.ReadServerSentEventsAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken))");
            }
            else if (streamingFraming == StreamingFraming.JsonLines)
            {
                builder.AppendLine($"await foreach (var item in StreamReaders.ReadJsonLinesAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken))");
            }
            else if (streamingFraming == StreamingFraming.JsonSequence)
            {
                builder.AppendLine($"await foreach (var item in StreamReaders.ReadJsonSequenceAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken))");
            }
            else if (streamingFraming == StreamingFraming.MultipartMixed)
            {
                builder.AppendLine("var boundary = response.Content.Headers.ContentType?.Parameters");
                builder.AppendLine(4, ".FirstOrDefault(p => string.Equals(p.Name, \"boundary\", StringComparison.OrdinalIgnoreCase))?.Value?.Trim('\"')");
                builder.AppendLine(4, $"?? \"{SequentialResultsExtractor.MultipartBoundaryValue}\";");
                builder.AppendLine($"await foreach (var item in StreamReaders.ReadMultipartMixedAsync<{streamingItemType}>(stream, boundary, jsonSerializerOptions, cancellationToken))");
            }
            else if (streamingFraming == StreamingFraming.JsonArray)
            {
                builder.AppendLine($"await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken))");
            }
            else
            {
                // JsonArray is the legitimate legacy fallthrough above. Any other framing reaching
                // here is a StreamReaders-based wire framing that was added without an explicit
                // reader branch — fail loudly at generation time rather than silently emitting the
                // JSON-array brace-scan path (wrong bytes). Mirrors the per-op ReaderMethodName switch.
                throw new InvalidOperationException(
                    $"No typed-client stream reader is defined for framing '{streamingFraming}'.");
            }

            builder.AppendLine("{");
            builder.AppendLine(4, "if (item is not null)");
            builder.AppendLine(4, "{");
            builder.AppendLine(8, "yield return item;");
            builder.AppendLine(4, "}");
            builder.AppendLine("}");
        }
        else if (headerParams.Count > 0 || cookieParams.Count > 0)
        {
            // Use HttpRequestMessage when request headers or cookies are needed
            builder.AppendLine();
            builder.AppendLine("using var request = new HttpRequestMessage(HttpMethod.Get, url);");
            builder.AppendLine();

            foreach (var (param, referenceId) in headerParams)
            {
                var propName = !string.IsNullOrEmpty(referenceId)
                    ? referenceId!.ToPascalCaseForDotNet()
                    : param.Name!.ToHeaderPropertyName();
                var paramAccess = $"parameters.{propName}";
                var headerName = param.Name!;

                if (param.Required)
                {
                    builder.AppendLine($"request.Headers.Add(\"{headerName}\", {paramAccess});");
                }
                else
                {
                    builder.AppendLine($"if (!string.IsNullOrEmpty({paramAccess}))");
                    builder.AppendLine("{");
                    builder.AppendLine(4, $"request.Headers.Add(\"{headerName}\", {paramAccess});");
                    builder.AppendLine("}");
                    builder.AppendLine();
                }
            }

            // OAS 3.2 in:cookie — build RFC 6265 Cookie header (name=value; name2=value2).
            // style:cookie omits percent-encoding; style:form (default) uses the raw value here
            // since cookie values rarely need encoding in practice. For full correctness, the
            // application layer should configure HttpClientHandler.UseCookies = false.
            if (cookieParams.Count > 0)
            {
                builder.AppendLine("var cookieParts = new List<string>();");
                foreach (var (param, _) in cookieParams)
                {
                    var propName = param.Name!.ToPascalCaseForDotNet();
                    var paramAccess = $"parameters.{propName}";

                    if (param.Required)
                    {
                        builder.AppendLine($"cookieParts.Add($\"{param.Name}={{{paramAccess}}}\");");
                    }
                    else
                    {
                        builder.AppendLine($"if (!string.IsNullOrEmpty({paramAccess}))");
                        builder.AppendLine("{");
                        builder.AppendLine(4, $"cookieParts.Add($\"{param.Name}={{{paramAccess}}}\");");
                        builder.AppendLine("}");
                        builder.AppendLine();
                    }
                }

                builder.AppendLine("if (cookieParts.Count > 0)");
                builder.AppendLine("{");
                builder.AppendLine(4, "request.Headers.TryAddWithoutValidation(\"Cookie\", string.Join(\"; \", cookieParts));");
                builder.AppendLine("}");
                builder.AppendLine();
            }

            builder.AppendLine("var response = await httpClient.SendAsync(request, cancellationToken);");
            builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken);");

            if (hasReturnType)
            {
                builder.Append($"return (await response.Content.ReadFromJsonAsync<{returnType}>(jsonSerializerOptions, cancellationToken))!;");
            }
        }
        else if (hasReturnType && returnType == "byte[]")
        {
            // Binary content download - use ReadAsByteArrayAsync
            builder.AppendLine("var response = await httpClient.GetAsync(url, cancellationToken);");
            builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken);");
            builder.Append("return await response.Content.ReadAsByteArrayAsync(cancellationToken);");
        }
        else if (hasReturnType && returnType == "string")
        {
            // Text content - use ReadAsStringAsync
            builder.AppendLine("var response = await httpClient.GetAsync(url, cancellationToken);");
            builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken);");
            builder.Append("return await response.Content.ReadAsStringAsync(cancellationToken);");
        }
        else if (hasReturnType)
        {
            builder.Append($"return (await httpClient.GetFromJsonAsync<{returnType}>(url, jsonSerializerOptions, cancellationToken))!;");
        }
        else
        {
            builder.AppendLine("var response = await httpClient.GetAsync(url, cancellationToken);");
            builder.Append("await EnsureSuccessAsync(response, cancellationToken);");
        }
    }

    private static void GenerateGenericMethodBody(
        string httpMethod,
        OpenApiOperation operation,
        string returnType,
        bool hasParameters,
        bool hasReturnType,
        StringBuilder builder)
    {
        var hasJsonBody = operation.RequestBody?.Content?.ContainsKey("application/json") ?? false;
        var requestAccess = hasParameters ? "parameters.Request" : "request";
        var verb = httpMethod.ToUpperInvariant();

        builder.AppendLine();
        builder.AppendLine($"using var requestMessage = new HttpRequestMessage(new HttpMethod(\"{verb}\"), url);");

        if (hasJsonBody)
        {
            builder.AppendLine($"requestMessage.Content = JsonContent.Create({requestAccess}, options: jsonSerializerOptions);");
        }

        builder.AppendLine();
        builder.AppendLine("var response = await httpClient.SendAsync(requestMessage, cancellationToken);");
        builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken);");

        if (hasReturnType)
        {
            builder.Append($"return (await response.Content.ReadFromJsonAsync<{returnType}>(jsonSerializerOptions, cancellationToken))!;");
        }
    }

    private static void GeneratePostMethodBody(
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        string returnType,
        bool hasParameters,
        bool hasReturnType,
        bool hasLocationHeader,
        StringBuilder builder)
    {
        var hasJsonBody = operation.RequestBody?.Content?.ContainsKey("application/json") ?? false;
        var requestAccess = hasParameters
            ? "parameters.Request"
            : "request";

        // Check for direct file upload (schema is binary or array of binary)
        // Schema references to objects should use the Request pattern, not File
        var isDirectFileUpload = IsDirectFileUpload(operation);
        var fileUploadContentType = operation.GetFileUploadContentType();

        // Check for schema-based multipart/form-data (object with file properties)
        var schemaBasedMultipartSchema = GetSchemaBasedMultipartSchema(operation, openApiDoc);

        if (isDirectFileUpload && hasParameters)
        {
            // Check if this is a multi-file upload (array of binary)
            var isMultiFileUpload = IsMultiFileUpload(operation);

            builder.AppendLine();

            // Generate file upload code (use null-forgiving operator since File is optional in spec but required for upload)
            if (fileUploadContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
            {
                builder.AppendLine("using var content = new MultipartFormDataContent();");

                if (isMultiFileUpload)
                {
                    // Multi-file upload - iterate over the array
                    builder.AppendLine();
                    builder.AppendLine("for (var i = 0; i < parameters.File!.Length; i++)");
                    builder.AppendLine("{");
                    builder.AppendLine(4, "var fileItem = parameters.File[i];");
                    builder.AppendLine(4, "var streamContent = new StreamContent(fileItem.OpenReadStream());");
                    builder.AppendLine();
                    builder.AppendLine(4, "if (fileItem.ContentType is not null)");
                    builder.AppendLine(4, "{");
                    builder.AppendLine(8, "streamContent.Headers.ContentType = new MediaTypeHeaderValue(fileItem.ContentType);");
                    builder.AppendLine(4, "}");
                    builder.AppendLine();
                    builder.AppendLine(4, "content.Add(streamContent, \"files\", fileItem.FileName);");
                    builder.AppendLine("}");
                }
                else
                {
                    // Single file upload
                    builder.AppendLine("var streamContent = new StreamContent(parameters.File!.OpenReadStream());");
                    builder.AppendLine();
                    builder.AppendLine("if (parameters.File!.ContentType is not null)");
                    builder.AppendLine("{");
                    builder.AppendLine(4, "streamContent.Headers.ContentType = new MediaTypeHeaderValue(parameters.File.ContentType);");
                    builder.AppendLine("}");
                    builder.AppendLine();
                    builder.AppendLine("content.Add(streamContent, \"file\", parameters.File.FileName);");
                }
            }
            else
            {
                // application/octet-stream or image/* content types
                builder.AppendLine("using var content = new StreamContent(parameters.File!.OpenReadStream());");
                builder.AppendLine($"content.Headers.ContentType = new MediaTypeHeaderValue(parameters.File.ContentType ?? \"{fileUploadContentType ?? "application/octet-stream"}\");");
            }

            builder.AppendLine();
            builder.AppendLine("var response = await httpClient.PostAsync(url, content, cancellationToken);");
        }
        else if (schemaBasedMultipartSchema is not null && hasParameters)
        {
            // Schema-based multipart/form-data with object containing file properties
            GenerateSchemaBasedMultipartFormData(builder, schemaBasedMultipartSchema, requestAccess);
            builder.AppendLine();
            builder.AppendLine("var response = await httpClient.PostAsync(url, content, cancellationToken);");
        }
        else if (hasJsonBody)
        {
            builder.AppendLine($"var response = await httpClient.PostAsJsonAsync(url, {requestAccess}, jsonSerializerOptions, cancellationToken);");
        }
        else
        {
            builder.AppendLine("var response = await httpClient.PostAsync(url, null, cancellationToken);");
        }

        builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken);");

        if (hasLocationHeader)
        {
            // Return the Location header as Uri
            builder.Append("return response.Headers.Location!;");
        }
        else if (hasReturnType)
        {
            // Use null-forgiving operator since we validated the response succeeded
            builder.Append($"return (await response.Content.ReadFromJsonAsync<{returnType}>(jsonSerializerOptions, cancellationToken))!;");
        }
    }

    private static void GeneratePutMethodBody(
        OpenApiOperation operation,
        string returnType,
        bool hasParameters,
        bool hasReturnType,
        StringBuilder builder)
    {
        var hasJsonBody = operation.RequestBody?.Content?.ContainsKey("application/json") ?? false;
        var requestAccess = hasParameters ? "parameters.Request" : "request";

        if (hasJsonBody)
        {
            builder.AppendLine($"var response = await httpClient.PutAsJsonAsync(url, {requestAccess}, jsonSerializerOptions, cancellationToken);");
            if (hasReturnType)
            {
                builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken);");
                builder.Append($"return (await response.Content.ReadFromJsonAsync<{returnType}>(jsonSerializerOptions, cancellationToken))!;");
            }
            else
            {
                builder.Append("await EnsureSuccessAsync(response, cancellationToken);");
            }
        }
        else
        {
            if (hasReturnType)
            {
                builder.AppendLine("var response = await httpClient.PutAsync(url, null, cancellationToken);");
                builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken);");
                builder.Append($"return (await response.Content.ReadFromJsonAsync<{returnType}>(jsonSerializerOptions, cancellationToken))!;");
            }
            else
            {
                builder.AppendLine("var response = await httpClient.PutAsync(url, null, cancellationToken);");
                builder.Append("await EnsureSuccessAsync(response, cancellationToken);");
            }
        }
    }

    private static void GenerateDeleteMethodBody(
        string returnType,
        bool hasReturnType,
        StringBuilder builder)
    {
        if (hasReturnType)
        {
            builder.AppendLine("var response = await httpClient.DeleteAsync(url, cancellationToken);");
            builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken);");
            builder.Append($"return (await response.Content.ReadFromJsonAsync<{returnType}>(jsonSerializerOptions, cancellationToken))!;");
        }
        else
        {
            builder.AppendLine("var response = await httpClient.DeleteAsync(url, cancellationToken);");
            builder.Append("await EnsureSuccessAsync(response, cancellationToken);");
        }
    }

    private static string GetParameterType(
        OpenApiParameter param,
        OpenApiDocument openApiDoc)
    {
        if (param.Schema is null)
        {
            return "string";
        }

        return GetSchemaTypeName(param.Schema, openApiDoc);
    }

    /// <summary>
    /// Returns the C# type name for a parameter, accounting for inline enums.
    /// When the schema is an inline enum (string + enum values, no $ref), the type is
    /// the generated enum name (<c>{parametersClassName}{PropertyName}</c>) — matches what
    /// <see cref="OperationParameterExtractor"/> writes on the corresponding record property.
    /// The URL-builder then takes the non-string branch in <see cref="BuildEncodedExpression"/>,
    /// which produces <c>Uri.EscapeDataString($"{...}")</c> — interpolation handles the
    /// enum-to-string conversion safely.
    /// </summary>
    private static string GetParameterTypeWithInlineEnumAwareness(
        OpenApiParameter param,
        OpenApiDocument openApiDoc,
        string parametersClassName)
    {
        if (param.Schema is OpenApiSchema actualSchema &&
            InlineEnumExtractor.IsInlineEnumSchema(actualSchema) &&
            !string.IsNullOrEmpty(param.Name))
        {
            var propName = param.In == ParameterLocation.Header
                ? param.Name!.ToHeaderPropertyName()
                : param.Name!.ToPascalCaseForDotNet();
            return InlineEnumExtractor.GenerateInlineEnumTypeName(parametersClassName, propName);
        }

        return GetParameterType(param, openApiDoc);
    }

    private static string GetSchemaTypeName(
        IOpenApiSchema? schema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry = null)
        => GetSchemaTypeName(schema, openApiDoc, registry, operationId: null, pathSegment: null, context: null, inlineSchemas: null);

    private static string GetSchemaTypeName(
        IOpenApiSchema? schema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        string? operationId,
        string? pathSegment,
        string? context,
        Dictionary<string, HttpClientInlineSchemaInfo>? inlineSchemas)
    {
        if (schema is null)
        {
            return "object";
        }

        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refId = schemaRef.Reference.Id;

            if (string.IsNullOrEmpty(refId))
            {
                return "object";
            }

            // Check if this reference points to an array alias (type: array with items but no prefixItems)
            // Array aliases like "Pets" (type: array, items: $ref Pet) should resolve to Pet[]
            // But tuple types with prefixItems (like Coordinate) should keep their type name
            if (openApiDoc.Components?.Schemas is not null &&
                openApiDoc.Components.Schemas.TryGetValue(refId!, out var resolvedSchema) &&
                resolvedSchema is OpenApiSchema { Type: JsonSchemaType.Array } arraySchema &&
                arraySchema.Items is not null &&
                !arraySchema.HasPrefixItems())
            {
                // This is a simple array alias - resolve to the underlying array type
                return GetArraySchemaType(arraySchema, openApiDoc, registry, operationId, pathSegment, inlineSchemas);
            }

            // Return the resolved type name for schema references
            return OpenApiSchemaExtensions.ResolveTypeName(refId!, registry);
        }

        if (schema is OpenApiSchema actualSchema)
        {
            // Handle allOf composition - look for PaginatedResult pattern
            if (actualSchema.AllOf is { Count: > 0 })
            {
                return GetAllOfSchemaTypeName(actualSchema.AllOf, openApiDoc, registry);
            }

            // Handle array type specially (GetPrimitiveCSharpTypeName returns null for arrays)
            if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true)
            {
                // Check if this is a resolved schema reference (e.g., Coordinate tuple)
                // If it's a component schema with prefixItems, return the schema name instead of recursing
                var schemaName = FindSchemaNameByReference(openApiDoc, actualSchema);
                if (!string.IsNullOrEmpty(schemaName))
                {
                    return OpenApiSchemaExtensions.ResolveTypeName(schemaName!, registry);
                }

                return GetArraySchemaType(actualSchema, openApiDoc, registry, operationId, pathSegment, inlineSchemas);
            }

            // Handle inline object schemas with properties
            if (InlineSchemaExtractor.IsInlineObjectSchema(actualSchema) &&
                !string.IsNullOrEmpty(operationId) &&
                pathSegment is not null &&
                !string.IsNullOrEmpty(context) &&
                inlineSchemas is not null)
            {
                var typeName = InlineSchemaExtractor.GenerateInlineTypeName(operationId!, context!);
                if (!inlineSchemas.ContainsKey(typeName))
                {
                    var recordParams = InlineSchemaExtractor.ExtractRecordFromInlineSchema(actualSchema, typeName, registry);
                    inlineSchemas[typeName] = new HttpClientInlineSchemaInfo(typeName, pathSegment!, recordParams);
                }

                return typeName;
            }

            // Use centralized primitive type mapping
            return actualSchema.Type.ToPrimitiveCSharpTypeName(actualSchema.Format) ?? "object";
        }

        return "object";
    }

    private static string GetAllOfSchemaTypeName(
        IList<IOpenApiSchema> allOfSchemas,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry = null)
    {
        // Look for pagination pattern: allOf with $ref to PaginationResult/PaginatedResult and items/results array
        string? baseType = null;
        string? itemType = null;

        foreach (var schemaItem in allOfSchemas)
        {
            if (schemaItem is OpenApiSchemaReference refSchema)
            {
                var refId = refSchema.Reference.Id;
                if (!string.IsNullOrEmpty(refId))
                {
                    baseType = OpenApiSchemaExtensions.ResolveTypeName(refId!, registry);
                }
            }
            else if (schemaItem is OpenApiSchema { Properties: not null } objSchema)
            {
                // Look for "items" or "results" property which contains the array item type
                foreach (var prop in objSchema.Properties)
                {
                    if (prop.Key.Equals("items", StringComparison.OrdinalIgnoreCase) ||
                        prop.Key.Equals("results", StringComparison.OrdinalIgnoreCase))
                    {
                        var propSchema = prop.Value;
                        if (propSchema is OpenApiSchema { Type: JsonSchemaType.Array } arraySchema)
                        {
                            // Get the item type from the array
                            itemType = GetArrayItemType(arraySchema, openApiDoc, registry);
                        }
                        else if (propSchema is OpenApiSchemaReference propRef)
                        {
                            // Resolve the reference
                            var propRefId = propRef.Reference.Id;
                            if (!string.IsNullOrEmpty(propRefId) &&
                                openApiDoc.Components?.Schemas?.TryGetValue(propRefId!, out var resolvedSchema) == true &&
                                resolvedSchema is OpenApiSchema { Type: JsonSchemaType.Array } resolvedArray)
                            {
                                itemType = GetArrayItemType(resolvedArray, openApiDoc, registry);
                            }
                        }

                        break;
                    }
                }
            }
        }

        // If we found PaginationResult<T> or PaginatedResult<T> pattern, return it
        if (baseType is not null && IsPaginationBaseType(baseType) && itemType is not null)
        {
            return $"{baseType}<{itemType}>";
        }

        // Return the base type if found
        return baseType ?? "object";
    }

    /// <summary>
    /// Determines if a type name is a pagination base type.
    /// Supports common naming conventions: PaginationResult, PaginatedResult, PagedResult.
    /// </summary>
    internal static bool IsPaginationBaseType(string typeName)
        => typeName.StartsWith("PaginationResult", StringComparison.Ordinal) ||
           typeName.StartsWith("PaginatedResult", StringComparison.Ordinal) ||
           typeName.StartsWith("PagedResult", StringComparison.Ordinal);

    private static string GetArrayItemType(
        OpenApiSchema arraySchema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry = null)
    {
        if (arraySchema.Items is null)
        {
            return "object";
        }

        if (arraySchema.Items is OpenApiSchemaReference itemRef)
        {
            var itemRefId = itemRef.Reference.Id;
            if (!string.IsNullOrEmpty(itemRefId))
            {
                // Check if this is an array type alias (like Accounts -> Account[])
                if (openApiDoc.Components?.Schemas?.TryGetValue(itemRefId!, out var itemSchema) == true &&
                    itemSchema is OpenApiSchema { Type: JsonSchemaType.Array } innerArray)
                {
                    return GetArrayItemType(innerArray, openApiDoc, registry);
                }

                return OpenApiSchemaExtensions.ResolveTypeName(itemRefId!, registry);
            }
        }
        else if (arraySchema.Items is OpenApiSchema itemSchema)
        {
            return GetSchemaTypeName(itemSchema, openApiDoc, registry);
        }

        return "object";
    }

    private static string GetArraySchemaType(
        OpenApiSchema schema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        string? operationId,
        string? pathSegment,
        Dictionary<string, HttpClientInlineSchemaInfo>? inlineSchemas)
    {
        if (schema.Items is null)
        {
            return "List<object>";
        }

        // For arrays of inline objects, use "ResponseItem" context
        var itemType = GetSchemaTypeName(schema.Items, openApiDoc, registry, operationId, pathSegment, "ResponseItem", inlineSchemas);
        return $"List<{itemType}>";
    }

    /// <summary>
    /// Checks if the operation is a direct file upload (binary or array of binary schema).
    /// Returns false for schema references to objects that may contain file properties.
    /// </summary>
    private static bool IsDirectFileUpload(OpenApiOperation operation)
    {
        if (operation.RequestBody?.Content is null)
        {
            return false;
        }

        foreach (var contentEntry in operation.RequestBody.Content)
        {
            var schema = contentEntry.Value.Schema;

            // Single binary file
            if (schema is OpenApiSchema directSchema &&
                directSchema.Type?.HasFlag(JsonSchemaType.String) == true &&
                string.Equals(directSchema.Format, "binary", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Array of binary files
            if (schema is OpenApiSchema { Type: JsonSchemaType.Array, Items: OpenApiSchema itemSchema } &&
                string.Equals(itemSchema.Format, "binary", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Note: Schema references (like FileAsFormDataRequest) are NOT direct file uploads
        }

        return false;
    }

    /// <summary>
    /// Determines if a C# type needs URL encoding.
    /// String types need encoding, value types don't (their ToString() is URL-safe).
    /// Arrays are excluded - they require special handling (encoding each element separately).
    /// </summary>
    /// <summary>
    /// Builds a Uri.EscapeDataString expression for a parameter access expression.
    /// String types use Uri.EscapeDataString directly.
    /// Other types use .ToString() before encoding.
    /// Returns the original expression if encoding is not needed.
    /// </summary>
    public static string BuildEncodedExpression(
        string accessExpression,
        string paramType)
    {
        if (!NeedsUrlEncoding(paramType))
        {
            return accessExpression;
        }

        var baseType = paramType.TrimEnd('?');
        if (baseType == "string")
        {
            return $"Uri.EscapeDataString({accessExpression})";
        }

        // Use string interpolation for non-string types to avoid nullable ToString() warnings
        return $"Uri.EscapeDataString($\"{{{accessExpression}}}\")";
    }

    /// <summary>
    /// Builds the string interpolation replacement for a path parameter.
    /// String types use Uri.EscapeDataString directly.
    /// Other types that need encoding use .ToString() + Uri.EscapeDataString.
    /// Value types known to produce URL-safe output are used directly.
    /// </summary>
    public static string BuildPathParameterReplacement(
        string propName,
        string paramType)
    {
        if (!NeedsUrlEncoding(paramType))
        {
            return $"{{parameters.{propName}}}";
        }

        var baseType = paramType.TrimEnd('?');
        if (baseType == "string")
        {
            return $"{{Uri.EscapeDataString(parameters.{propName})}}";
        }

        // Use string interpolation for non-string types to avoid nullable ToString() warnings
        return $"{{Uri.EscapeDataString($\"{{parameters.{propName}}}\")}}";
    }

    /// <summary>
    /// Appends a repeated-key foreach block for a form-explode array query parameter.
    /// For required parameters, emits the bare foreach.
    /// For optional parameters, wraps the foreach in a null guard.
    /// Each element is encoded using <see cref="BuildEncodedExpression"/> (or raw when allowReserved).
    /// </summary>
    private static void AppendQueryArrayForeach(
        StringBuilder builder,
        string paramName,
        string paramAccess,
        string paramType,
        bool allowReserved,
        bool isRequired)
    {
        // Element type comes off the shared List<T>/T[] extractor; if neither shape matches
        // (no array param should reach here that way) fall back to the param type itself.
        if (!TryGetListElementType(paramType, out var elementType))
        {
            elementType = paramType;
        }

        // allowReserved => emit the value WITHOUT URL-encoding. Array elements are non-nullable
        // here (the foreach variable over a List<T> of value types / strings), so a string element
        // is used as-is and a non-string value type uses .ToString(). Unlike the sibling
        // BuildEncodedExpression/BuildPathParameterReplacement — which interpolate via $"{...}" to
        // dodge nullable-ToString (CS8602) warnings on possibly-null scalars — `item` is provably
        // non-null, so the cleaner direct .ToString() is safe.
        var encodedItem = allowReserved
            ? (elementType == "string" ? "item" : "item.ToString()")
            : BuildEncodedExpression("item", elementType);

        if (isRequired)
        {
            builder.AppendLine($"foreach (var item in {paramAccess})");
            builder.AppendLine("{");
            builder.AppendLine(4, $"queryParams.Add($\"{paramName}={{{encodedItem}}}\");");
            builder.AppendLine("}");
        }
        else
        {
            builder.AppendLine();
            builder.AppendLine($"if ({paramAccess} is not null)");
            builder.AppendLine("{");
            builder.AppendLine(4, $"foreach (var item in {paramAccess})");
            builder.AppendLine(4, "{");
            builder.AppendLine(8, $"queryParams.Add($\"{paramName}={{{encodedItem}}}\");");
            builder.AppendLine(4, "}");
            builder.AppendLine("}");
        }
    }

    /// <summary>
    /// Extracts the element type from a <c>List&lt;T&gt;</c> or <c>T[]</c> type name.
    /// Upstream contract: array query params arrive as a non-nullable <c>List&lt;T&gt;</c>
    /// (no trailing <c>?</c>, no nesting), and streaming response bodies arrive as
    /// <c>List&lt;T&gt;</c>; both feed this single extractor so the surgery lives in one place.
    /// </summary>
    /// <returns><c>true</c> with <paramref name="elementType"/> set when the shape matched; otherwise <c>false</c>.</returns>
    private static bool TryGetListElementType(
        string paramType,
        out string elementType)
    {
        if (paramType.EndsWith("[]", StringComparison.Ordinal))
        {
            elementType = paramType.Substring(0, paramType.Length - 2);
            return true;
        }

        if (paramType.StartsWith("List<", StringComparison.Ordinal) &&
            paramType.EndsWith(">", StringComparison.Ordinal))
        {
            elementType = paramType.Substring(5, paramType.Length - 6);
            return true;
        }

        elementType = string.Empty;
        return false;
    }

    /// <summary>
    /// Builds the null/empty guard expression for an optional query parameter, selecting the
    /// right form for the parameter's C# type: <c>!string.IsNullOrEmpty(x)</c> for strings,
    /// <c>x.HasValue</c> for nullable value types, and <c>x is not null</c> otherwise.
    /// </summary>
    private static string BuildNullCheck(
        string paramAccess,
        string paramType)
    {
        if (paramType == "string")
        {
            return $"!string.IsNullOrEmpty({paramAccess})";
        }

        if (CSharpTypeHelper.IsBasicValueType(paramType))
        {
            return $"{paramAccess}.HasValue";
        }

        return $"{paramAccess} is not null";
    }

    public static bool NeedsUrlEncoding(string csharpType)
    {
        // Remove nullable indicator for comparison
        var baseType = csharpType.TrimEnd('?');

        // Arrays need special handling (encode each element) - not supported yet
        if (baseType.EndsWith("[]", StringComparison.Ordinal))
        {
            return false;
        }

        // Types whose ToString() always produces URL-safe output (digits, hex, true/false)
        return baseType switch
        {
            "int" => false,
            "long" => false,
            "short" => false,
            "byte" => false,
            "bool" => false,
            "float" => false,
            "double" => false,
            "decimal" => false,
            "Guid" => false,

            // String and DateTimeOffset need encoding (DateTimeOffset contains '+' for timezone)
            "string" => true,
            "DateTimeOffset" => true,
            "DateTime" => true,
            "DateOnly" => true,
            "TimeOnly" => true,

            // Custom/enum types: encode defensively since ToString() may produce URL-unsafe characters
            _ => true,
        };
    }

    /// <summary>
    /// Checks if the operation is a multi-file upload (direct array of binary).
    /// Only returns true for direct arrays, not for schema references to objects.
    /// </summary>
    private static bool IsMultiFileUpload(OpenApiOperation operation)
    {
        if (operation.RequestBody?.Content is null)
        {
            return false;
        }

        foreach (var contentEntry in operation.RequestBody.Content)
        {
            var schema = contentEntry.Value.Schema;

            // Direct array of binary - this is a multi-file upload
            if (schema is OpenApiSchema { Type: JsonSchemaType.Array, Items: OpenApiSchema { Format: "binary" } })
            {
                return true;
            }

            // Note: Schema references to objects (like FilesAsFormDataRequest) that contain
            // file arrays should NOT be treated as direct file uploads - they use the Request pattern
        }

        return false;
    }

    /// <summary>
    /// Finds the schema name in Components.Schemas by comparing schema properties.
    /// This is needed when Microsoft.OpenApi resolves $ref to the actual schema object.
    /// </summary>
    private static string? FindSchemaNameByReference(
        OpenApiDocument openApiDoc,
        OpenApiSchema itemSchema)
    {
        if (openApiDoc.Components?.Schemas is null)
        {
            return null;
        }

        foreach (var kvp in openApiDoc.Components.Schemas)
        {
            // First try: compare by reference (same object instance)
            if (ReferenceEquals(kvp.Value, itemSchema))
            {
                return kvp.Key;
            }

            // Second try: match by Title property if set
            // OpenAPI specs often have title matching the schema name
            if (kvp.Value is OpenApiSchema componentSchema &&
                !string.IsNullOrEmpty(componentSchema.Title) &&
                !string.IsNullOrEmpty(itemSchema.Title) &&
                string.Equals(componentSchema.Title, itemSchema.Title, StringComparison.Ordinal))
            {
                return kvp.Key;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the schema for schema-based multipart/form-data requests.
    /// Returns the resolved schema if the request body is multipart/form-data with a schema reference,
    /// otherwise returns null.
    /// </summary>
    private static IOpenApiSchema? GetSchemaBasedMultipartSchema(
        OpenApiOperation operation,
        OpenApiDocument openApiDoc)
    {
        if (operation.RequestBody?.Content is null)
        {
            return null;
        }

        // Check for multipart/form-data content type with schema reference
        if (!operation.RequestBody.Content.TryGetValue("multipart/form-data", out var mediaType))
        {
            return null;
        }

        // If schema is a reference to a component schema, resolve it
        if (mediaType.Schema is OpenApiSchemaReference schemaRef)
        {
            var schemaId = schemaRef.Reference?.Id;
            if (!string.IsNullOrEmpty(schemaId) &&
                openApiDoc.Components?.Schemas is not null &&
                openApiDoc.Components.Schemas.TryGetValue(schemaId!, out var schema))
            {
                return schema;
            }
        }

        return null;
    }

    /// <summary>
    /// Generates code for schema-based multipart/form-data requests.
    /// Creates MultipartFormDataContent with StringContent for text fields and StreamContent for binary fields.
    /// </summary>
    private static void GenerateSchemaBasedMultipartFormData(
        StringBuilder builder,
        IOpenApiSchema schema,
        string requestAccess)
    {
        builder.AppendLine("using var content = new MultipartFormDataContent();");
        builder.AppendLine();

        var properties = schema.Properties?.ToList() ?? [];
        if (properties.Count == 0)
        {
            return;
        }

        foreach (var prop in properties)
        {
            var propName = prop.Key;
            var propSchema = prop.Value;
            var pascalPropName = propName.ToPascalCaseForDotNet();
            var isBinary = propSchema.Type?.HasFlag(JsonSchemaType.String) == true &&
                           string.Equals(propSchema.Format, "binary", StringComparison.OrdinalIgnoreCase);
            var isArray = propSchema.Type?.HasFlag(JsonSchemaType.Array) == true;
            var isArrayOfBinary = isArray &&
                                  propSchema.Items?.Type?.HasFlag(JsonSchemaType.String) == true &&
                                  string.Equals(propSchema.Items?.Format, "binary", StringComparison.OrdinalIgnoreCase);

            if (isBinary)
            {
                // Single file property - use StreamContent with IFileContent
                builder.AppendLine($"if ({requestAccess}?.{pascalPropName} is not null)");
                builder.AppendLine("{");
                builder.AppendLine(4, $"var fileContent = new StreamContent({requestAccess}.{pascalPropName}.OpenReadStream());");
                builder.AppendLine();
                builder.AppendLine(4, $"if ({requestAccess}.{pascalPropName}.ContentType is not null)");
                builder.AppendLine(4, "{");
                builder.AppendLine(8, $"fileContent.Headers.ContentType = new MediaTypeHeaderValue({requestAccess}.{pascalPropName}.ContentType);");
                builder.AppendLine(4, "}");
                builder.AppendLine();
                builder.AppendLine(4, $"content.Add(fileContent, \"{propName}\", {requestAccess}.{pascalPropName}.FileName);");
                builder.AppendLine("}");
                builder.AppendLine();
            }
            else if (isArrayOfBinary)
            {
                // Array of files - use StreamContent with IFileContent for each
                builder.AppendLine($"if ({requestAccess}?.{pascalPropName} is not null)");
                builder.AppendLine("{");
                builder.AppendLine(4, $"for (var i = 0; i < {requestAccess}.{pascalPropName}.Count; i++)");
                builder.AppendLine(4, "{");
                builder.AppendLine(8, $"var fileItem = {requestAccess}.{pascalPropName}[i];");
                builder.AppendLine(8, "var fileContent = new StreamContent(fileItem.OpenReadStream());");
                builder.AppendLine();
                builder.AppendLine(8, "if (fileItem.ContentType is not null)");
                builder.AppendLine(8, "{");
                builder.AppendLine(12, "fileContent.Headers.ContentType = new MediaTypeHeaderValue(fileItem.ContentType);");
                builder.AppendLine(8, "}");
                builder.AppendLine();
                builder.AppendLine(8, $"content.Add(fileContent, \"{propName}\", fileItem.FileName);");
                builder.AppendLine(4, "}");
                builder.AppendLine("}");
                builder.AppendLine();
            }
            else if (isArray)
            {
                // Array of non-binary values - add as multiple form fields
                builder.AppendLine($"if ({requestAccess}?.{pascalPropName} is not null)");
                builder.AppendLine("{");
                builder.AppendLine(4, $"foreach (var item in {requestAccess}.{pascalPropName})");
                builder.AppendLine(4, "{");
                builder.AppendLine(8, $"content.Add(new StringContent(item?.ToString() ?? string.Empty), \"{propName}\");");
                builder.AppendLine(4, "}");
                builder.AppendLine("}");
                builder.AppendLine();
            }
            else
            {
                // Simple value - use StringContent
                builder.AppendLine($"if ({requestAccess}?.{pascalPropName} is not null)");
                builder.AppendLine("{");
                builder.AppendLine(4, $"content.Add(new StringContent({requestAccess}.{pascalPropName}.ToString()!), \"{propName}\");");
                builder.AppendLine("}");
                builder.AppendLine();
            }
        }
    }

    /// <summary>
    /// Checks if any of the response content types are binary (application/octet-stream, image/*, etc.).
    /// </summary>
    private static bool IsBinaryResponseContent(
        IDictionary<string, IOpenApiMediaType> content)
    {
        foreach (var key in content.Keys)
        {
            if (key.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("application/zip", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if any of the response content types are text-based (text/plain, text/html, etc.).
    /// </summary>
    private static bool IsTextResponseContent(
        IDictionary<string, IOpenApiMediaType> content)
    {
        foreach (var key in content.Keys)
        {
            if (key.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Creates the EnsureSuccessAsync helper method that reads error response body before throwing.
    /// This replaces the standard EnsureSuccessStatusCode() to preserve error details.
    /// </summary>
    private static MethodParameters CreateEnsureSuccessMethod()
    {
        var sb = new StringBuilder();
        sb.AppendLine("if (response.IsSuccessStatusCode)");
        sb.AppendLine("{");
        sb.AppendLine(4, "return;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);");
        sb.AppendLine("throw new HttpRequestException(");
        sb.AppendLine(4, "$\"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {errorContent}\",");
        sb.AppendLine(4, "inner: null,");
        sb.Append(4, "response.StatusCode);");

        return new MethodParameters(
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PrivateStaticAsync,
            ReturnGenericTypeName: null,
            ReturnTypeName: "System.Threading.Tasks.Task",
            Name: "EnsureSuccessAsync",
            Parameters:
            [
                new ParameterBaseParameters(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: "HttpResponseMessage",
                    IsNullableType: false,
                    IsReferenceType: true,
                    Name: "response",
                    DefaultValue: null),
                new ParameterBaseParameters(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: "CancellationToken",
                    IsNullableType: false,
                    IsReferenceType: false,
                    Name: "cancellationToken",
                    DefaultValue: null),
            ],
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: sb.ToString());
    }
}