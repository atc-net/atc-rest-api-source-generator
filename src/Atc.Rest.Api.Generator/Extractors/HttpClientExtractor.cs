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
    /// <returns>A tuple containing the ClassParameters and a dictionary of discovered inline schemas.</returns>
    public static (ClassParameters? ClientClass, Dictionary<string, HttpClientInlineSchemaInfo> InlineSchemas) ExtractWithInlineSchemas(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated = false,
        bool useServersBasePath = true)
    {
        var inlineSchemas = new Dictionary<string, HttpClientInlineSchemaInfo>(StringComparer.Ordinal);
        var clientClass = ExtractInternal(openApiDoc, projectName, pathSegment, registry, systemTypeResolver, includeDeprecated, inlineSchemas, useServersBasePath);
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
        bool useServersBasePath = true)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (openApiDoc.Paths == null || openApiDoc.Paths.Count == 0)
        {
            return null;
        }

        var className = string.IsNullOrEmpty(pathSegment)
            ? $"{projectName}Client"
            : $"{pathSegment}Client";
        var namespaceValue = NamespaceBuilder.ForClient(projectName, pathSegment);
        var modelsNamespace = NamespaceBuilder.ForModels(projectName, pathSegment);

        var constructorParams = new List<ConstructorParameterBaseParameters>
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

        var constructor = new ConstructorParameters(
            DocumentationTags: null,
            DeclarationModifier: DeclarationModifiers.Public,
            GenericTypeName: null,
            TypeName: className,
            InheritedClassTypeName: null,
            Parameters: constructorParams);

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

            if (pathItemInterface is not OpenApiPathItem pathItem)
            {
                continue;
            }

            if (pathItem.Operations != null)
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

                    if (methodParams != null)
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
            if (method.Parameters != null)
            {
                foreach (var param in method.Parameters)
                {
                    contentPreview.AppendLine(param.TypeName);
                    if (param.Attributes != null)
                    {
                        foreach (var attr in param.Attributes)
                        {
                            contentPreview.AppendLine($"[{attr.Name}]");
                        }
                    }
                }
            }
        }

        var contentForAnalysis = contentPreview.ToString();

        // Build header content with only required usings
        var usings = UsingStatementHelper.GetRequiredUsings(
            contentForAnalysis,
            NamespaceConstants.SystemCodeDomCompiler);

        // Always include the models namespace
        usings.Add(modelsNamespace);

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
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicSealedClass,
            ClassTypeName: className,
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: new List<ConstructorParameters> { constructor },
            Properties: null,
            Methods: methods,
            GenerateToStringMethod: false);
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
        bool includeDeprecated = false)
        => OperationParameterExtractor.ExtractIndividual(
            openApiDoc,
            projectName,
            pathSegment,
            registry: registry,
            includeBindingAttributes: false,
            namespaceSubFolder: "Client",
            includeDeprecated: includeDeprecated);

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
        if (operation == null)
        {
            return null;
        }

        // Check if this is an async enumerable streaming operation
        var isAsyncEnumerable = operation.IsAsyncEnumerableOperation();
        var normalizedPath = path
            .Replace("/", "_")
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
        if (operation.Responses != null &&
            !operation.Responses.TryGetValue("200", out response))
        {
            operation.Responses.TryGetValue("201", out response);
        }

        // Check for JSON content first
        if (response?.Content != null && response.Content.TryGetValue("application/json", out var mediaType1))
        {
            var contentType = GetSchemaTypeName(mediaType1.Schema, openApiDoc, registry, operationId, pathSegment, "Response", inlineSchemas);
            if (!string.IsNullOrEmpty(contentType))
            {
                returnType = contentType;

                // For async enumerable, extract the List<T> item type
                if (isAsyncEnumerable && contentType.StartsWith("List<", StringComparison.Ordinal) && contentType.EndsWith(">", StringComparison.Ordinal))
                {
                    streamingItemType = contentType.Substring(5, contentType.Length - 6); // Extract T from List<T>
                }
            }
        }
        else if (response is OpenApiResponse openApiResp &&
                 openApiResp.Headers != null &&
                 openApiResp.Headers.TryGetValue("Location", out var locationHeader) &&
                 locationHeader.Schema is OpenApiSchema { Format: "uri" })
        {
            returnType = "Uri";
            hasLocationHeader = true;
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
        var willReturnAsyncEnumerable = isAsyncEnumerable && streamingItemType != null;
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
        var methodContent = GenerateMethodBody(path, httpMethod, operation, pathLevelParameters, openApiDoc, returnType, hasParameters, isAsyncEnumerable, streamingItemType, hasReturnType, hasLocationHeader, useServersBasePath);

        // For async enumerable methods, return IAsyncEnumerable<T> directly
        if (isAsyncEnumerable && streamingItemType != null)
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
        bool useServersBasePath = true)
    {
        var builder = new StringBuilder();

        // Get server base path if enabled (e.g., "/api/v1" from servers[0].url)
        var serverBasePath = useServersBasePath ? ServerUrlHelper.GetServersBasePath(openApiDoc) : null;

        // Build the URL - optionally prepend server base path, then replace path parameters
        var urlBuilder = serverBasePath != null ? $"{serverBasePath}{path}" : path;

        // Process path-level parameters first
        if (pathLevelParameters != null)
        {
            foreach (var paramInterface in pathLevelParameters)
            {
                var resolved = paramInterface.Resolve();
                var (param, _) = (resolved.Parameter, resolved.ReferenceId);
                if (param == null || string.IsNullOrEmpty(param.Name))
                {
                    continue;
                }

                if (param.In == ParameterLocation.Path)
                {
                    var propName = param.Name!.ToPascalCaseForDotNet();
                    var paramType = GetParameterType(param, openApiDoc);

                    // URL-encode string path parameters to handle special characters (RFC 3986)
                    var replacement = NeedsUrlEncoding(paramType)
                        ? $"{{Uri.EscapeDataString(parameters.{propName})}}"
                        : $"{{parameters.{propName}}}";
                    urlBuilder = urlBuilder.Replace($"{{{param.Name}}}", replacement);
                }
            }
        }

        // Then process operation-level parameters
        if (operation.Parameters != null)
        {
            foreach (var paramInterface in operation.Parameters)
            {
                // Resolve parameter reference if needed
                var resolved = paramInterface.Resolve();
                var (param, _) = (resolved.Parameter, resolved.ReferenceId);
                if (param == null || string.IsNullOrEmpty(param.Name))
                {
                    continue;
                }

                if (param.In == ParameterLocation.Path)
                {
                    var propName = param.Name!.ToPascalCaseForDotNet();
                    var paramType = GetParameterType(param, openApiDoc);

                    // URL-encode string path parameters to handle special characters (RFC 3986)
                    var replacement = NeedsUrlEncoding(paramType)
                        ? $"{{Uri.EscapeDataString(parameters.{propName})}}"
                        : $"{{parameters.{propName}}}";
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
        if (operation.Parameters != null)
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
                var paramType = GetParameterType(param, openApiDoc);
                var isRequired = param.Required;
                var needsEncoding = NeedsUrlEncoding(paramType);

                // Required parameters are always added (non-nullable)
                if (isRequired)
                {
                    // URL-encode string parameters to handle special characters (RFC 3986)
                    var valueExpression = needsEncoding
                        ? $"Uri.EscapeDataString({paramAccess})"
                        : paramAccess;
                    builder.AppendLine($"queryParams.Add($\"{param.Name}={{{valueExpression}}}\");");
                }
                else
                {
                    // Use appropriate null check based on type for optional parameters
                    string nullCheck;

                    if (paramType == "string")
                    {
                        nullCheck = $"!string.IsNullOrEmpty({paramAccess})";
                    }
                    else if (paramType.EndsWith("[]", StringComparison.Ordinal))
                    {
                        nullCheck = $"{paramAccess} != null && {paramAccess}.Length > 0";
                    }
                    else if (CSharpTypeHelper.IsBasicValueType(paramType))
                    {
                        nullCheck = $"{paramAccess}.HasValue";
                    }
                    else
                    {
                        nullCheck = $"{paramAccess} != null";
                    }

                    // URL-encode string parameters to handle special characters (RFC 3986)
                    var valueExpression = needsEncoding
                        ? $"Uri.EscapeDataString({paramAccess})"
                        : paramAccess;

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

        switch (httpMethod)
        {
            case "GET":
                GenerateGetMethodBody(returnType, isAsyncEnumerable, streamingItemType, hasReturnType, builder, headerParams);
                break;
            case "POST":
                GeneratePostMethodBody(operation, returnType, hasParameters, hasReturnType, hasLocationHeader, builder);
                break;
            case "PUT":
                GeneratePutMethodBody(operation, returnType, hasParameters, hasReturnType, builder);
                break;
            case "DELETE":
                GenerateDeleteMethodBody(returnType, hasReturnType, builder);
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
        bool hasReturnType,
        StringBuilder builder,
        List<(OpenApiParameter Param, string? ReferenceId)> headerParams)
    {
        // Special handling for async enumerable streaming
        if (isAsyncEnumerable && !string.IsNullOrEmpty(streamingItemType))
        {
            builder.AppendLine("var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };");
            builder.AppendLine();
            builder.AppendLine("using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);");
            builder.AppendLine("response.EnsureSuccessStatusCode();");
            builder.AppendLine();
            builder.AppendLine("var stream = await response.Content.ReadAsStreamAsync(cancellationToken);");
            builder.AppendLine();
            builder.AppendLine($"await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<{streamingItemType}>(stream, jsonOptions, cancellationToken))");
            builder.AppendLine("{");
            builder.AppendLine(4, "if (item != null)");
            builder.AppendLine(4, "{");
            builder.AppendLine(8, "yield return item;");
            builder.AppendLine(4, "}");
            builder.AppendLine("}");
        }
        else if (headerParams.Count > 0)
        {
            // Use HttpRequestMessage when headers are needed
            builder.AppendLine();
            builder.AppendLine("using var request = new HttpRequestMessage(HttpMethod.Get, url);");
            if (headerParams.Count > 0)
            {
                builder.AppendLine();
            }

            foreach (var (param, referenceId) in headerParams)
            {
                // For header parameters, use reference ID as property name if available
                var propName = !string.IsNullOrEmpty(referenceId)
                    ? referenceId!.ToPascalCaseForDotNet()
                    : param.Name!.ToPascalCaseForDotNet();
                var paramAccess = $"parameters.{propName}";
                var headerName = param.Name!;

                if (param.Required)
                {
                    builder.AppendLine($"request.Headers.Add(\"{headerName}\", {paramAccess});");
                }
                else
                {
                    builder.AppendLine();
                    builder.AppendLine($"if (!string.IsNullOrEmpty({paramAccess}))");
                    builder.AppendLine("{");
                    builder.AppendLine(4, $"request.Headers.Add(\"{headerName}\", {paramAccess});");
                    builder.AppendLine("}");
                }
            }

            builder.AppendLine();
            builder.AppendLine("var response = await httpClient.SendAsync(request, cancellationToken);");
            builder.AppendLine("response.EnsureSuccessStatusCode();");

            if (hasReturnType)
            {
                builder.Append($"return (await response.Content.ReadFromJsonAsync<{returnType}>(cancellationToken: cancellationToken))!;");
            }
        }
        else if (hasReturnType)
        {
            builder.Append($"return (await httpClient.GetFromJsonAsync<{returnType}>(url, cancellationToken))!;");
        }
        else
        {
            builder.AppendLine("var response = await httpClient.GetAsync(url, cancellationToken);");
            builder.Append("response.EnsureSuccessStatusCode();");
        }
    }

    private static void GeneratePostMethodBody(
        OpenApiOperation operation,
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
                    builder.AppendLine(4, "var streamContent = new StreamContent(parameters.File[i]);");
                    builder.AppendLine(4, "content.Add(streamContent, \"files\", $\"file{i}\");");
                    builder.AppendLine("}");
                }
                else
                {
                    // Single file upload
                    builder.AppendLine("var streamContent = new StreamContent(parameters.File!);");
                    builder.AppendLine("content.Add(streamContent, \"file\", parameters.FileName ?? \"file\");");
                }
            }
            else
            {
                // application/octet-stream or image/* content types
                builder.AppendLine("using var content = new StreamContent(parameters.File!);");
                builder.AppendLine($"content.Headers.ContentType = new MediaTypeHeaderValue(\"{fileUploadContentType ?? "application/octet-stream"}\");");
            }

            builder.AppendLine();
            builder.AppendLine("var response = await httpClient.PostAsync(url, content, cancellationToken);");
        }
        else if (hasJsonBody)
        {
            builder.AppendLine($"var response = await httpClient.PostAsJsonAsync(url, {requestAccess}, cancellationToken);");
        }
        else
        {
            builder.AppendLine("var response = await httpClient.PostAsync(url, null, cancellationToken);");
        }

        builder.AppendLine("response.EnsureSuccessStatusCode();");

        if (hasLocationHeader)
        {
            // Return the Location header as Uri
            builder.Append("return response.Headers.Location!;");
        }
        else if (hasReturnType)
        {
            // Use null-forgiving operator since we validated the response succeeded
            builder.Append($"return (await response.Content.ReadFromJsonAsync<{returnType}>(cancellationToken: cancellationToken))!;");
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
            builder.AppendLine($"var response = await httpClient.PutAsJsonAsync(url, {requestAccess}, cancellationToken);");
            if (hasReturnType)
            {
                builder.AppendLine("response.EnsureSuccessStatusCode();");
                builder.Append($"return (await response.Content.ReadFromJsonAsync<{returnType}>(cancellationToken: cancellationToken))!;");
            }
            else
            {
                builder.Append("response.EnsureSuccessStatusCode();");
            }
        }
        else
        {
            if (hasReturnType)
            {
                builder.AppendLine("var response = await httpClient.PutAsync(url, null, cancellationToken);");
                builder.AppendLine("response.EnsureSuccessStatusCode();");
                builder.Append($"return (await response.Content.ReadFromJsonAsync<{returnType}>(cancellationToken: cancellationToken))!;");
            }
            else
            {
                builder.AppendLine("var response = await httpClient.PutAsync(url, null, cancellationToken);");
                builder.Append("response.EnsureSuccessStatusCode();");
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
            builder.AppendLine("response.EnsureSuccessStatusCode();");
            builder.Append($"return (await response.Content.ReadFromJsonAsync<{returnType}>(cancellationToken: cancellationToken))!;");
        }
        else
        {
            builder.AppendLine("var response = await httpClient.DeleteAsync(url, cancellationToken);");
            builder.Append("response.EnsureSuccessStatusCode();");
        }
    }

    private static string GetParameterType(
        OpenApiParameter param,
        OpenApiDocument openApiDoc)
    {
        if (param.Schema == null)
        {
            return "string";
        }

        return GetSchemaTypeName(param.Schema, openApiDoc);
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
        if (schema == null)
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
            if (openApiDoc.Components?.Schemas != null &&
                openApiDoc.Components.Schemas.TryGetValue(refId!, out var resolvedSchema) &&
                resolvedSchema is OpenApiSchema { Type: JsonSchemaType.Array } arraySchema &&
                arraySchema.Items != null &&
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
                !string.IsNullOrEmpty(pathSegment) &&
                !string.IsNullOrEmpty(context) &&
                inlineSchemas != null)
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
        if (baseType != null && IsPaginationBaseType(baseType) && itemType != null)
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
        if (arraySchema.Items == null)
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
        if (schema.Items == null)
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
        if (operation.RequestBody?.Content == null)
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
    public static bool NeedsUrlEncoding(string csharpType)
    {
        // Remove nullable indicator for comparison
        var baseType = csharpType.TrimEnd('?');

        // Arrays need special handling (encode each element) - not supported yet
        if (baseType.EndsWith("[]", StringComparison.Ordinal))
        {
            return false;
        }

        // Value types that don't need encoding (their ToString() produces URL-safe output)
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
            _ => baseType == "string",
        };
    }

    /// <summary>
    /// Checks if the operation is a multi-file upload (direct array of binary).
    /// Only returns true for direct arrays, not for schema references to objects.
    /// </summary>
    private static bool IsMultiFileUpload(OpenApiOperation operation)
    {
        if (operation.RequestBody?.Content == null)
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
        if (openApiDoc.Components?.Schemas == null)
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
}