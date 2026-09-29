// ReSharper disable InvertIf
#pragma warning disable CA1034 // Nested types should not be visible

namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI operations and generates endpoint-per-operation code.
/// This includes endpoint interfaces, endpoint classes, result interfaces, and result classes.
/// </summary>
public static class EndpointPerOperationExtractor
{
    /// <summary>
    /// Represents an inline schema discovered during extraction.
    /// </summary>
    public sealed record InlineSchemaInfo(
        string TypeName,
        string PathSegment,
        RecordParameters RecordParameters);

    /// <summary>
    /// Represents the generated files for a single operation.
    /// </summary>
    /// <remarks>
    /// For binary endpoints (application/octet-stream), the result interface/class content
    /// will be null as we use BinaryEndpointResponse from Atc.Rest.Client directly.
    /// </remarks>
    public sealed record OperationFiles(
        string OperationName,
        string PathSegment,
        string EndpointInterfaceFileName,
        string EndpointInterfaceContent,
        string EndpointClassFileName,
        string EndpointClassContent,
        string? ResultInterfaceFileName,
        string? ResultInterfaceContent,
        string? ResultClassFileName,
        string? ResultClassContent);

    /// <summary>
    /// Checks if an operation returns binary content (application/octet-stream).
    /// </summary>
    /// <remarks>
    /// Binary endpoints use BinaryEndpointResponse from Atc.Rest.Client instead of
    /// generating custom result classes.
    /// </remarks>
    private static bool IsBinaryEndpoint(OpenApiOperation operation)
    {
        if (operation.Responses is null)
        {
            return false;
        }

        // Check if any success response (2xx) returns application/octet-stream
        foreach (var response in operation.Responses)
        {
            if (!int.TryParse(response.Key, out var statusCode))
            {
                continue;
            }

            // Only check success responses
            if (statusCode < 200 || statusCode >= 300)
            {
                continue;
            }

            if (response.Value?.Content is not null &&
                response.Value.Content.ContainsKey("application/octet-stream"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts all endpoint files for operations in the OpenAPI document filtered by path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="pathSegment">The path segment to filter by.</param>
    /// <param name="registry">Optional conflict registry.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <param name="errorResponseFormat">The expected error response format from the API.</param>
    /// <param name="customErrorTypeName">Optional custom error type name (used when ErrorResponseFormat is Custom).</param>
    /// <param name="customHttpClientName">Optional custom HTTP client name.</param>
    /// <param name="hasSegmentModels">Whether the segment has segment-specific models.</param>
    /// <param name="hasSharedModels">Whether there are shared models in the project.</param>
    /// <param name="useServersBasePath">Whether to prepend the base path from OpenAPI servers[0].url to URLs. Default: true.</param>
    public static List<OperationFiles> Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false,
        ErrorResponseFormatType errorResponseFormat = ErrorResponseFormatType.ProblemDetails,
        string? customErrorTypeName = null,
        string? customHttpClientName = null,
        bool hasSegmentModels = true,
        bool hasSharedModels = false,
        bool useServersBasePath = true)
    {
        var (files, _) = ExtractWithInlineSchemas(
            openApiDoc,
            projectName,
            pathSegment,
            registry,
            includeDeprecated,
            errorResponseFormat,
            customErrorTypeName,
            customHttpClientName,
            hasSegmentModels,
            hasSharedModels,
            useServersBasePath);
        return files;
    }

    /// <summary>
    /// Extracts all endpoint files for operations in the OpenAPI document filtered by path segment,
    /// including any inline schemas discovered during extraction.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="pathSegment">The path segment to filter by.</param>
    /// <param name="registry">Optional conflict registry.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <param name="errorResponseFormat">The expected error response format from the API.</param>
    /// <param name="customErrorTypeName">Optional custom error type name (used when ErrorResponseFormat is Custom).</param>
    /// <param name="customHttpClientName">Optional custom HTTP client name.</param>
    /// <param name="hasSegmentModels">Whether the segment has segment-specific models.</param>
    /// <param name="hasSharedModels">Whether there are shared models in the project.</param>
    /// <param name="useServersBasePath">Whether to prepend the base path from OpenAPI servers[0].url to URLs. Default: true.</param>
    /// <param name="namespaceSegment">The segment used for namespaces and file names. When null, <paramref name="pathSegment"/> is used. Pass an explicitly resolved value (possibly empty) to omit the segment from namespaces.</param>
    /// <returns>A tuple containing the operation files and a dictionary of inline schemas.</returns>
    public static (List<OperationFiles> Files, Dictionary<string, InlineSchemaInfo> InlineSchemas) ExtractWithInlineSchemas(
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false,
        ErrorResponseFormatType errorResponseFormat = ErrorResponseFormatType.ProblemDetails,
        string? customErrorTypeName = null,
        string? customHttpClientName = null,
        bool hasSegmentModels = true,
        bool hasSharedModels = false,
        bool useServersBasePath = true,
        string? namespaceSegment = null)
    {
        var effectiveNamespaceSegment = namespaceSegment ?? pathSegment;

        var result = new List<OperationFiles>();
        var inlineSchemas = new Dictionary<string, InlineSchemaInfo>(StringComparer.Ordinal);

        if (openApiDoc.Paths is null || openApiDoc.Paths.Count == 0)
        {
            return (result, inlineSchemas);
        }

        var httpClientName = GetEffectiveHttpClientName(projectName, customHttpClientName);

        foreach (var path in openApiDoc.Paths)
        {
            var pathKey = path.Key;
            var pathItemInterface = path.Value;

            // Apply path segment filter
            var currentSegment = PathSegmentHelper.GetFirstPathSegment(pathKey);
            if (!currentSegment.Equals(pathSegment, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (pathItemInterface is not IOpenApiPathItem pathItem)
            {
                continue;
            }

            if (pathItem.Operations is null)
            {
                continue;
            }

            // Get path-level parameters
            var pathLevelParameters = pathItem.Parameters;

            foreach (var operation in pathItem.Operations)
            {
                // Skip deprecated operations if not including them
                if (!includeDeprecated && operation.Value?.Deprecated == true)
                {
                    continue;
                }

                var httpMethod = operation.Key.ToString();
                var operationValue = operation.Value;

                if (operationValue is null)
                {
                    continue;
                }

                var files = ExtractOperationFiles(
                    projectName,
                    effectiveNamespaceSegment,
                    pathKey,
                    httpMethod,
                    operationValue,
                    pathItem,
                    pathLevelParameters,
                    openApiDoc,
                    registry,
                    httpClientName,
                    errorResponseFormat,
                    customErrorTypeName,
                    hasSegmentModels,
                    hasSharedModels,
                    inlineSchemas,
                    useServersBasePath);

                if (files is not null)
                {
                    result.Add(files);
                }
            }
        }

        return (result, inlineSchemas);
    }

    private static OperationFiles? ExtractOperationFiles(
        string projectName,
        string pathSegment,
        string path,
        string httpMethod,
        OpenApiOperation operation,
        IOpenApiPathItem pathItem,
        IList<IOpenApiParameter>? pathLevelParameters,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        string httpClientName,
        ErrorResponseFormatType errorResponseFormat,
        string? customErrorTypeName,
        bool hasSegmentModels,
        bool hasSharedModels,
        Dictionary<string, InlineSchemaInfo>? inlineSchemas = null,
        bool useServersBasePath = true)
    {
        var normalizedPath = path
            .Replace("/", "_")
            .Replace("{", string.Empty)
            .Replace("}", string.Empty);

        var operationId = operation.OperationId ?? $"{httpMethod}{normalizedPath}";
        var operationName = operationId.ToPascalCaseForDotNet();
        var description = operation.Summary ?? operation.Description ?? $"{operationName} operation";

        // Check if this is a binary endpoint (returns application/octet-stream)
        var isBinaryEndpoint = IsBinaryEndpoint(operation);

        // Check if this operation returns an async enumerable / streamed response
        // (x-return-async-enumerable annotation or an OpenAPI 3.2 itemSchema).
        var isAsyncEnumerable = operation.IsStreamingResponse();

        // Extract response information from OpenAPI spec
        var responses = ExtractResponses(
            operation,
            openApiDoc,
            registry,
            errorResponseFormat,
            customErrorTypeName,
            operationId,
            pathSegment,
            inlineSchemas,
            isAsyncEnumerable);

        // The spec's `default` response covers every error status the operation does not list; it is
        // registered at run time for the actual status (see GenerateEndpointClass).
        var defaultResponse = responses.Find(r => r.IsDefault);
        if (defaultResponse is not null)
        {
            responses.Remove(defaultResponse);
        }

        // Auto-apply responses based on detected features (same as server-side .Produces() metadata)
        // This ensures client is prepared to handle all responses the server may return
        var definedCodes = new HashSet<string>(responses.Select(r => r.StatusCode), StringComparer.Ordinal);
        var features = Helpers.OperationFeaturesHelper.DetectOperationFeatures(operation, pathItem, openApiDoc, httpMethod);
        var errorContentType = string.IsNullOrEmpty(customErrorTypeName) ? "ProblemDetails" : customErrorTypeName;

        // 400 - Has parameters but no 400 defined
        if (features.HasParameters && !definedCodes.Contains("400"))
        {
            var validationContentType = string.IsNullOrEmpty(customErrorTypeName) ? "ValidationProblemDetails" : customErrorTypeName;
            responses.Add(new ResponseInfo("400", "BadRequest", "BadRequest", validationContentType, false, IsAutoAdded: true));
        }

        // 401 - Has security but no 401 defined
        if (features.HasSecurity && !definedCodes.Contains("401"))
        {
            responses.Add(new ResponseInfo("401", "Unauthorized", "Unauthorized", errorContentType, false, IsAutoAdded: true));
        }

        // 403 - Has roles/policies but no 403 defined
        if (features.HasRolesOrPolicies && !definedCodes.Contains("403"))
        {
            responses.Add(new ResponseInfo("403", "Forbidden", "Forbidden", errorContentType, false, IsAutoAdded: true));
        }

        // 404 - GET/PUT/DELETE/PATCH with path params but no 404 defined
        if (features.HasPathParameters &&
            features.HttpMethod is "GET" or "PUT" or "DELETE" or "PATCH" &&
            !definedCodes.Contains("404"))
        {
            responses.Add(new ResponseInfo("404", "NotFound", "NotFound", errorContentType, false, IsAutoAdded: true));
        }

        // 409 - POST/PUT but no 409 defined
        if (features.HttpMethod is "POST" or "PUT" && !definedCodes.Contains("409"))
        {
            responses.Add(new ResponseInfo("409", "Conflict", "Conflict", errorContentType, false, IsAutoAdded: true));
        }

        // 429 - Has rate limiting but no 429 defined
        if (features.HasRateLimiting && !definedCodes.Contains("429"))
        {
            responses.Add(new ResponseInfo("429", "TooManyRequests", "TooManyRequests", errorContentType, false, IsAutoAdded: true));
        }

        // 500 - Always add if not defined (global error handler)
        if (!definedCodes.Contains("500"))
        {
            responses.Add(new ResponseInfo("500", "InternalServerError", "InternalServerError", errorContentType, false, IsAutoAdded: true));
        }

        // 504 - Always add if not defined (GlobalErrorHandler maps TimeoutException
        // and OperationCanceledException to 504 Gateway Timeout)
        if (!definedCodes.Contains("504"))
        {
            responses.Add(new ResponseInfo("504", "GatewayTimeout", "GatewayTimeout", errorContentType, false, IsAutoAdded: true));
        }

        // Check if operation has parameters
        var hasQueryRouteParams = operation.Parameters is { Count: > 0 } || pathLevelParameters is { Count: > 0 };
        var hasRequestBody = operation.RequestBody is { Content: not null };
        var hasParameters = hasQueryRouteParams || hasRequestBody;

        // Extract streaming item type for IAsyncEnumerable endpoints (e.g., "Account" from "IAsyncEnumerable<Account>").
        // OpenAPI 3.2 itemSchema is already the per-element type, so map it directly.
        string? streamingItemType = null;
        if (isAsyncEnumerable)
        {
            var streamingItemSchema = operation.GetStreamingItemSchema();
            streamingItemType = streamingItemSchema is not null
                ? GetSchemaTypeName(streamingItemSchema, openApiDoc, registry, operationId, pathSegment, "Response", inlineSchemas, isAsyncEnumerable: false)
                : ExtractStreamingItemType(responses);
        }

        // Generate endpoint interface
        var endpointInterfaceContent = GenerateEndpointInterface(
            projectName,
            pathSegment,
            operationName,
            description,
            hasParameters,
            httpClientName,
            isBinaryEndpoint,
            isAsyncEnumerable,
            streamingItemType,
            hasSegmentModels,
            hasSharedModels);

        // Generate endpoint class
        var endpointClassContent = GenerateEndpointClass(
            projectName,
            pathSegment,
            operationName,
            description,
            path,
            httpMethod,
            operation,
            pathItem,
            pathLevelParameters,
            openApiDoc,
            hasParameters,
            httpClientName,
            responses,
            defaultResponse,
            isBinaryEndpoint,
            isAsyncEnumerable,
            streamingItemType,
            hasSegmentModels,
            hasSharedModels,
            useServersBasePath);

        // For binary and streaming endpoints, skip generating result interface/class
        // (use BinaryEndpointResponse or StreamingEndpointResponse directly)
        string? resultInterfaceFileName = null;
        string? resultInterfaceContent = null;
        string? resultClassFileName = null;
        string? resultClassContent = null;

        if (!isBinaryEndpoint && !isAsyncEnumerable)
        {
            // Generate result interface
            resultInterfaceContent = GenerateResultInterface(
                projectName,
                pathSegment,
                operationName,
                description,
                responses,
                defaultResponse,
                hasSegmentModels,
                hasSharedModels);
            resultInterfaceFileName = NamespaceBuilder.ToFileName(
                NamespaceBuilder.ForEndpointInterfaces(projectName, pathSegment),
                $"I{operationName}EndpointResult");

            // Generate result class
            resultClassContent = GenerateResultClass(
                projectName,
                pathSegment,
                operationName,
                description,
                responses,
                defaultResponse,
                hasSegmentModels,
                hasSharedModels,
                customErrorTypeName);
            resultClassFileName = NamespaceBuilder.ToFileName(
                NamespaceBuilder.ForEndpointResults(projectName, pathSegment),
                $"{operationName}EndpointResult");
        }

        return new OperationFiles(
            OperationName: operationName,
            PathSegment: pathSegment,
            EndpointInterfaceFileName: NamespaceBuilder.ToFileName(
                NamespaceBuilder.ForEndpointInterfaces(projectName, pathSegment),
                $"I{operationName}Endpoint"),
            EndpointInterfaceContent: endpointInterfaceContent,
            EndpointClassFileName: NamespaceBuilder.ToFileName(
                NamespaceBuilder.ForEndpoints(projectName, pathSegment),
                $"{operationName}Endpoint"),
            EndpointClassContent: endpointClassContent,
            ResultInterfaceFileName: resultInterfaceFileName,
            ResultInterfaceContent: resultInterfaceContent,
            ResultClassFileName: resultClassFileName,
            ResultClassContent: resultClassContent);
    }

    /// <summary>
    /// Information about a response from an operation.
    /// </summary>
    /// <param name="StatusCode">The HTTP status code (e.g., "200", "404").</param>
    /// <param name="StatusEnumName">The HttpStatusCode enum name (e.g., "OK", "NotFound") - used with HttpStatusCode.{value}.</param>
    /// <param name="PropertyName">The property-friendly name (e.g., "Ok", "NotFound") - used for Is{value}, {value}Content.</param>
    /// <param name="ContentType">The content type returned by this response.</param>
    /// <param name="IsSuccess">Whether this is a success (2xx) response.</param>
    /// <param name="TextMediaType">The text media type of a raw text body, or <see langword="null"/>.</param>
    /// <param name="IsAutoAdded">Whether the generator added this response (the spec does not list it).</param>
    /// <param name="ErrorMediaType">The declared media type of an error response's body, or <see langword="null"/>.</param>
    private sealed record ResponseInfo(string StatusCode, string StatusEnumName, string PropertyName, string? ContentType, bool IsSuccess, string? TextMediaType = null, bool IsAutoAdded = false, string? ErrorMediaType = null)
    {
        /// <summary>
        /// Whether this is the spec's <c>default</c> response: the body of any error status the operation does not list.
        /// </summary>
        public bool IsDefault
            => string.Equals(StatusCode, "default", StringComparison.Ordinal);

        /// <summary>
        /// The <c>HttpStatusCode</c> expression for this status: the enum member, or a numeric cast for
        /// codes the enum does not define (418, 499, ...).
        /// </summary>
        public string StatusCodeExpression
            => HttpStatusCodeHelper.ToHttpStatusCodeExpression(int.Parse(StatusCode, NumberStyles.Integer, CultureInfo.InvariantCulture));
    }

    private static List<ResponseInfo> ExtractResponses(
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        ErrorResponseFormatType errorResponseFormat,
        string? customErrorTypeName,
        string? operationId,
        string? pathSegment,
        Dictionary<string, InlineSchemaInfo>? inlineSchemas,
        bool isAsyncEnumerable)
    {
        var responses = new List<ResponseInfo>();

        if (operation.Responses is null)
        {
            return responses;
        }

        foreach (var response in operation.Responses)
        {
            var statusCodeStr = response.Key;
            var isDefault = string.Equals(statusCodeStr, "default", StringComparison.OrdinalIgnoreCase);
            if (!int.TryParse(statusCodeStr, out var statusCodeInt) && !isDefault)
            {
                continue;
            }

            var statusEnumName = isDefault ? "DefaultError" : HttpStatusCodeHelper.ToEnumName(statusCodeInt);
            var propertyName = isDefault ? "DefaultError" : NormalizeStatusName(statusEnumName);
            var isSuccess = statusCodeInt >= 200 && statusCodeInt < 300;
            if (isDefault)
            {
                statusCodeStr = "default";
            }

            // Determine content type from response body (headers like Location are metadata, not content)
            string? contentType = null;
            string? textMediaType = null;

            // Error responses: the same resolution the server uses for its factories, so a client generated
            // from a spec parses what a server generated from that spec writes (+json media types included).
            if (!isSuccess)
            {
                var shape = ErrorResponseShapeResolver.Resolve(response.Value);
                (contentType, textMediaType) = shape.Kind switch
                {
                    ErrorResponseKind.Problem when shape.IsDeclared => ("ProblemDetails", null),
                    ErrorResponseKind.ValidationProblem => ("ValidationProblemDetails", null),
                    ErrorResponseKind.JsonObject => (GetSchemaTypeName(shape.Schema, openApiDoc, registry, operationId, pathSegment, ErrorResponseShapeResolver.InlineTypeContext(statusCodeStr), inlineSchemas, isAsyncEnumerable: false), null),
                    ErrorResponseKind.JsonString => ("string", null),
                    ErrorResponseKind.Text => ("string", shape.MediaType),
                    _ => (GetErrorContentType(errorResponseFormat, customErrorTypeName, statusCodeStr), (string?)null),
                };

                responses.Add(new ResponseInfo(statusCodeStr, statusEnumName, propertyName, contentType, isSuccess, textMediaType, ErrorMediaType: shape.MediaType));
                continue;
            }

            // Check for JSON content
            // Note: Binary/octet-stream endpoints are handled by IsBinaryEndpoint() and skip result class generation
            if (response.Value?.Content is not null &&
                response.Value.Content.TryGetValue("application/json", out var mediaType))
            {
                // Use "Response" context for direct response objects
                // Only apply async enumerable wrapper to success responses
                contentType = GetSchemaTypeName(
                    mediaType.Schema,
                    openApiDoc,
                    registry,
                    operationId,
                    pathSegment,
                    isSuccess ? "Response" : null, // Only generate inline types for success responses
                    inlineSchemas,
                    isAsyncEnumerable && isSuccess);
            }
            else if (response.Value is not null &&
                     response.Value.TryGetTextResponseMediaType(out var matchedTextMediaType, out _))
            {
                // text/plain, text/csv, application/xml, ... — surface as a raw string body
                // and route through Atc.Rest.Client's AddSuccess/ErrorTextResponse (no JSON parse).
                contentType = "string";
                textMediaType = matchedTextMediaType;
            }

            // Determine error content type based on ErrorResponseFormatType
            if (!isSuccess && contentType is null)
            {
                contentType = GetErrorContentType(errorResponseFormat, customErrorTypeName, statusCodeStr);
            }

            responses.Add(new ResponseInfo(statusCodeStr, statusEnumName, propertyName, contentType, isSuccess, textMediaType));
        }

        return responses;
    }

    /// <summary>
    /// Determines the error content type based on the configured error response format.
    /// </summary>
    private static string GetErrorContentType(
        ErrorResponseFormatType errorResponseFormat,
        string? customErrorTypeName,
        string statusCodeStr)
        => errorResponseFormat switch
        {
            // ProblemDetails: ValidationProblemDetails for 400, ProblemDetails for others
            ErrorResponseFormatType.ProblemDetails => statusCodeStr == "400"
                ? "ValidationProblemDetails"
                : "ProblemDetails",

            // PlainText: ValidationProblemDetails for 400, string for others
            ErrorResponseFormatType.PlainText => statusCodeStr == "400"
                ? "ValidationProblemDetails"
                : "string",

            // PlainTextOnly: string for all errors including 400
            ErrorResponseFormatType.PlainTextOnly => "string",

            // Custom: use the custom error type name for all errors
            ErrorResponseFormatType.Custom => customErrorTypeName ?? "ProblemDetails",

            _ => "ProblemDetails",
        };

    private static string GenerateEndpointInterface(
        string projectName,
        string pathSegment,
        string operationName,
        string description,
        bool hasParameters,
        string httpClientName,
        bool isBinaryEndpoint,
        bool isAsyncEnumerable,
        string? streamingItemType,
        bool hasSegmentModels,
        bool hasSharedModels)
    {
        var namespaceValue = NamespaceBuilder.ForEndpointInterfaces(projectName, pathSegment);
        var sharedModelsNamespace = NamespaceBuilder.ForModels(projectName);
        var segmentModelsNamespace = NamespaceBuilder.ForModels(projectName, pathSegment);

        // Build header with usings
        var headerBuilder = new StringBuilder();
        headerBuilder.AppendLine("// <auto-generated />");
        headerBuilder.AppendLine("#nullable enable");
        headerBuilder.AppendLine();
        headerBuilder.AppendLine("using System.CodeDom.Compiler;");
        headerBuilder.AppendLine("using System.Threading;");
        headerBuilder.AppendLine("using System.Threading.Tasks;");

        if (isBinaryEndpoint || isAsyncEnumerable)
        {
            // Add using for BinaryEndpointResponse or StreamingEndpointResponse
            headerBuilder.AppendLine("using Atc.Rest.Client;");
        }

        if (hasParameters)
        {
            headerBuilder.AppendLine($"using {NamespaceBuilder.ForClient(projectName, pathSegment)};");
        }

        // Add using for result type (in Endpoints.Results namespace) - only for non-binary and non-streaming endpoints
        if (!isBinaryEndpoint && !isAsyncEnumerable)
        {
            headerBuilder.AppendLine($"using {NamespaceBuilder.ForEndpointResults(projectName, pathSegment)};");
        }

        // For streaming endpoints, add model usings for the item type
        if (isAsyncEnumerable)
        {
            if (hasSharedModels)
            {
                headerBuilder.AppendLine($"using {sharedModelsNamespace};");
            }

            // When the path segment resolves away, the segment models namespace is identical
            // to the shared one, so avoid emitting a duplicate using directive.
            if (hasSegmentModels &&
                !(hasSharedModels && string.Equals(segmentModelsNamespace, sharedModelsNamespace, StringComparison.Ordinal)))
            {
                headerBuilder.AppendLine($"using {segmentModelsNamespace};");
            }
        }

        // Add blank line before namespace
        headerBuilder.AppendLine();

        // Build method parameters
        var methodParameters = new List<ParameterBaseParameters>();

        if (hasParameters)
        {
            methodParameters.Add(new ParameterBaseParameters(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: $"{operationName}Parameters",
                IsNullableType: false,
                IsReferenceType: true,
                Name: "parameters",
                DefaultValue: null));
        }

        methodParameters.Add(new ParameterBaseParameters(
            Attributes: null,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: "string",
            IsNullableType: false,
            IsReferenceType: true,
            Name: "httpClientName",
            DefaultValue: "Constants.HttpClientName"));

        methodParameters.Add(new ParameterBaseParameters(
            Attributes: null,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: "CancellationToken",
            IsNullableType: false,
            IsReferenceType: false,
            Name: "cancellationToken",
            DefaultValue: "default"));

        // Determine return type: BinaryEndpointResponse for binary, StreamingEndpointResponse for streaming, custom result class otherwise
        string returnTypeName;
        if (isBinaryEndpoint)
        {
            returnTypeName = "Task<BinaryEndpointResponse>";
        }
        else if (isAsyncEnumerable && !string.IsNullOrEmpty(streamingItemType))
        {
            returnTypeName = $"Task<StreamingEndpointResponse<{streamingItemType}>>";
        }
        else
        {
            returnTypeName = $"Task<{operationName}EndpointResult>";
        }

        // Build ExecuteAsync method
        var executeMethod = new MethodParameters(
            DocumentationTags: new CodeDocumentationTags("Execute method."),
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.None,
            ReturnGenericTypeName: null,
            ReturnTypeName: returnTypeName,
            Name: "ExecuteAsync",
            Parameters: methodParameters,
            AlwaysBreakDownParameters: true,
            UseExpressionBody: false,
            Content: null);

        // Build interface documentation
        var docTags = new CodeDocumentationTags(
            $"Interface for Client Endpoint.\nDescription: {description}\nOperation: {operationName}.");

        // Build interface parameters
        var interfaceParams = new InterfaceParameters(
            HeaderContent: headerBuilder.ToString(),
            Namespace: namespaceValue,
            DocumentationTags: docTags,
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicInterface,
            InterfaceTypeName: $"I{operationName}Endpoint",
            InheritedInterfaceTypeName: null,
            Properties: null,
            Methods: new List<MethodParameters> { executeMethod });

        // Generate content using the code generation library
        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var contentGenerator = new GenerateContentForInterface(codeDocGenerator, interfaceParams);
        return contentGenerator.Generate();
    }

    private static string GenerateEndpointClass(
        string projectName,
        string pathSegment,
        string operationName,
        string description,
        string path,
        string httpMethod,
        OpenApiOperation operation,
        IOpenApiPathItem pathItem,
        IList<IOpenApiParameter>? pathLevelParameters,
        OpenApiDocument openApiDoc,
        bool hasParameters,
        string httpClientName,
        List<ResponseInfo> responses,
        ResponseInfo? defaultResponse,
        bool isBinaryEndpoint,
        bool isAsyncEnumerable,
        string? streamingItemType,
        bool hasSegmentModels,
        bool hasSharedModels,
        bool useServersBasePath = true)
    {
        var namespaceValue = NamespaceBuilder.ForEndpoints(projectName, pathSegment);
        var sharedModelsNamespace = NamespaceBuilder.ForModels(projectName);
        var segmentModelsNamespace = NamespaceBuilder.ForModels(projectName, pathSegment);

        // SSE/JSON-Lines/JSON-seq/multipart build the StreamingEndpointResponse<T> inline using the
        // emitted StreamReaders helper, which deserializes each element via the DI-configured
        // IContractSerializer (Atc.Rest.Client.Serialization) and lives in the Streaming namespace.
        var endpointFraming = operation.GetStreamingFraming();
        var usesStreamReaders = isAsyncEnumerable &&
                                !string.IsNullOrEmpty(streamingItemType) &&
                                UsesStreamReaders(endpointFraming);

        // multipart/mixed extracts the response Content-Type boundary via LINQ — needs System
        // (StringComparison) and System.Linq (FirstOrDefault), which are not otherwise imported.
        var isMultipartStreaming = usesStreamReaders && endpointFraming == StreamingFraming.MultipartMixed;

        // Per-operation retry (x-retry-*): tells the API resilience handler how to run this request
        // when it differs from the handler default (its policy for safe methods, no retry for POST/PATCH).
        var resilienceStamp = ResilienceRequestHelper.BuildStampStatement(operation, pathItem, openApiDoc, httpMethod, "requestMessage");

        // Build header with usings
        var headerBuilder = new StringBuilder();
        headerBuilder.AppendLine("// <auto-generated />");
        headerBuilder.AppendLine("#nullable enable");
        headerBuilder.AppendLine();
        if (isMultipartStreaming)
        {
            headerBuilder.AppendLine("using System;");
        }

        headerBuilder.AppendLine("using System.CodeDom.Compiler;");
        headerBuilder.AppendLine("using System.Collections.Generic;");
        if (isMultipartStreaming)
        {
            headerBuilder.AppendLine("using System.Linq;");
        }

        headerBuilder.AppendLine("using System.Net;");
        headerBuilder.AppendLine("using System.Net.Http;");
        headerBuilder.AppendLine("using System.Threading;");
        headerBuilder.AppendLine("using System.Threading.Tasks;");

        if (isBinaryEndpoint || isAsyncEnumerable)
        {
            // Add using for BinaryEndpointResponse or StreamingEndpointResponse
            headerBuilder.AppendLine("using Atc.Rest.Client;");
        }

        headerBuilder.AppendLine("using Atc.Rest.Client.Builder;");
        if (usesStreamReaders)
        {
            headerBuilder.AppendLine("using Atc.Rest.Client.Serialization;");
        }

        headerBuilder.AppendLine("using Microsoft.Extensions.Http;");
        headerBuilder.AppendLine($"using {projectName}.Generated;");
        if (resilienceStamp is not null)
        {
            headerBuilder.AppendLine($"using {projectName}.Generated.Resilience;");
        }

        if (usesStreamReaders)
        {
            headerBuilder.AppendLine($"using {projectName}.Generated.Streaming;");
        }

        headerBuilder.AppendLine($"using {NamespaceBuilder.ForEndpointInterfaces(projectName, pathSegment)};");

        // Add using for result types (in Results namespace) - only for non-binary and non-streaming endpoints
        if (!isBinaryEndpoint && !isAsyncEnumerable)
        {
            headerBuilder.AppendLine($"using {NamespaceBuilder.ForEndpointResults(projectName, pathSegment)};");
        }

        // Add Models usings for non-binary endpoints (they use ProblemDetails etc.) or streaming endpoints (for item type)
        if (!isBinaryEndpoint || isAsyncEnumerable)
        {
            // Add shared Models using if there are shared models
            if (hasSharedModels)
            {
                headerBuilder.AppendLine($"using {sharedModelsNamespace};");
            }

            // Add segment-specific Models using if there are segment-specific models.
            // When the path segment resolves away both namespaces are identical, so skip the duplicate.
            if (hasSegmentModels &&
                !(hasSharedModels && string.Equals(segmentModelsNamespace, sharedModelsNamespace, StringComparison.Ordinal)))
            {
                headerBuilder.AppendLine($"using {segmentModelsNamespace};");
            }
        }

        if (hasParameters)
        {
            headerBuilder.AppendLine($"using {NamespaceBuilder.ForClient(projectName, pathSegment)};");
        }

        // Add blank line before namespace
        headerBuilder.AppendLine();

        // Build constructor parameters
        var constructorParams = new List<ConstructorParameterBaseParameters>
        {
            new(
                GenericTypeName: null,
                TypeName: "IHttpClientFactory",
                IsNullableType: false,
                Name: "factory",
                DefaultValue: null,
                PassToInheritedClass: false,
                CreateAsPrivateReadonlyMember: true,
                CreateAaOneLiner: false),
            new(
                GenericTypeName: null,
                TypeName: "IHttpMessageFactory",
                IsNullableType: false,
                Name: "httpMessageFactory",
                DefaultValue: null,
                PassToInheritedClass: false,
                CreateAsPrivateReadonlyMember: true,
                CreateAaOneLiner: false),
        };

        // SSE and JSON Lines deserialize each element via the DI-configured IContractSerializer
        // (so the per-op client honors the same serialization as the legacy streaming path).
        // Only these StreamReaders-based endpoints need it; others keep the 2-arg constructor.
        if (usesStreamReaders)
        {
            constructorParams.Add(new(
                GenericTypeName: null,
                TypeName: "IContractSerializer",
                IsNullableType: false,
                Name: "contractSerializer",
                DefaultValue: null,
                PassToInheritedClass: false,
                CreateAsPrivateReadonlyMember: true,
                CreateAaOneLiner: false));
        }

        var constructor = new ConstructorParameters(
            DocumentationTags: null,
            DeclarationModifier: DeclarationModifiers.Public,
            GenericTypeName: null,
            TypeName: $"{operationName}Endpoint",
            InheritedClassTypeName: null,
            Parameters: constructorParams);

        // Build ExecuteAsync method parameters
        var methodParameters = new List<ParameterBaseParameters>();

        if (hasParameters)
        {
            methodParameters.Add(new ParameterBaseParameters(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: $"{operationName}Parameters",
                IsNullableType: false,
                IsReferenceType: true,
                Name: "parameters",
                DefaultValue: null));
        }

        methodParameters.Add(new ParameterBaseParameters(
            Attributes: null,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: "string",
            IsNullableType: false,
            IsReferenceType: true,
            Name: "httpClientName",
            DefaultValue: "Constants.HttpClientName"));

        methodParameters.Add(new ParameterBaseParameters(
            Attributes: null,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: "CancellationToken",
            IsNullableType: false,
            IsReferenceType: false,
            Name: "cancellationToken",
            DefaultValue: "default"));

        // Build method body content
        var methodBodyContent = BuildExecuteAsyncMethodBody(
            path,
            httpMethod,
            operation,
            pathLevelParameters,
            openApiDoc,
            operationName,
            responses,
            defaultResponse,
            isBinaryEndpoint,
            isAsyncEnumerable,
            streamingItemType,
            resilienceStamp,
            useServersBasePath);

        // Determine return type: BinaryEndpointResponse for binary, StreamingEndpointResponse for streaming, custom result class otherwise
        string returnTypeName;
        if (isBinaryEndpoint)
        {
            returnTypeName = "Task<BinaryEndpointResponse>";
        }
        else if (isAsyncEnumerable && !string.IsNullOrEmpty(streamingItemType))
        {
            returnTypeName = $"Task<StreamingEndpointResponse<{streamingItemType}>>";
        }
        else
        {
            returnTypeName = $"Task<{operationName}EndpointResult>";
        }

        var executeMethod = new MethodParameters(
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicAsync,
            ReturnGenericTypeName: null,
            ReturnTypeName: returnTypeName,
            Name: "ExecuteAsync",
            Parameters: methodParameters,
            AlwaysBreakDownParameters: true,
            UseExpressionBody: false,
            Content: methodBodyContent);

        // Build class documentation
        var docTags = new CodeDocumentationTags(
            $"Client Endpoint.\nDescription: {description}\nOperation: {operationName}.");

        // Build class parameters
        var classParams = new ClassParameters(
            HeaderContent: headerBuilder.ToString(),
            Namespace: namespaceValue,
            DocumentationTags: docTags,
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicSealedClass,
            ClassTypeName: $"{operationName}Endpoint",
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: $"I{operationName}Endpoint",
            Constructors: new List<ConstructorParameters> { constructor },
            Properties: null,
            Methods: new List<MethodParameters> { executeMethod },
            GenerateToStringMethod: false);

        // Generate content using the code generation library
        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var contentGenerator = new GenerateContentForClass(codeDocGenerator, classParams);
        return contentGenerator.Generate();
    }

    private static string BuildExecuteAsyncMethodBody(
        string path,
        string httpMethod,
        OpenApiOperation operation,
        IList<IOpenApiParameter>? pathLevelParameters,
        OpenApiDocument openApiDoc,
        string operationName,
        List<ResponseInfo> responses,
        ResponseInfo? defaultResponse,
        bool isBinaryEndpoint,
        bool isAsyncEnumerable,
        string? streamingItemType,
        string? resilienceStamp,
        bool useServersBasePath = true)
    {
        var sb = new StringBuilder();
        sb.AppendLine("var client = factory.CreateClient(httpClientName);");
        sb.AppendLine();

        // Get server base path if enabled (e.g., "/api/v1" from servers[0].url)
        var serverBasePath = useServersBasePath ? ServerUrlHelper.GetServersBasePath(openApiDoc) : null;

        // Build URL template with path parameters (optionally prepend server base path)
        var templatePath = serverBasePath is not null ? $"{serverBasePath}{path}" : path;
        sb.AppendLine($"var requestBuilder = httpMessageFactory.FromTemplate(\"{templatePath}\");");

        // Add path parameters
        var allParams = new List<(OpenApiParameter Param, string? ReferenceId)>();
        if (pathLevelParameters is not null)
        {
            foreach (var p in pathLevelParameters)
            {
                var resolved = p.Resolve();
                if (resolved.Parameter is not null)
                {
                    allParams.Add((resolved.Parameter, resolved.ReferenceId));
                }
            }
        }

        if (operation.Parameters is not null)
        {
            foreach (var p in operation.Parameters)
            {
                var resolved = p.Resolve();
                if (resolved.Parameter is not null)
                {
                    allParams.Add((resolved.Parameter, resolved.ReferenceId));
                }
            }
        }

        foreach (var (param, _) in allParams.Where(p => p.Param.In == ParameterLocation.Path))
        {
            var propName = param.Name!.ToPascalCaseForDotNet();
            sb.AppendLine($"requestBuilder.WithPathParameter(\"{param.Name}\", parameters.{propName});");
        }

        // Add query parameters
        foreach (var (param, _) in allParams.Where(p => p.Param.In == ParameterLocation.Query))
        {
            var propName = param.Name!.ToPascalCaseForDotNet();
            if (param.Required)
            {
                sb.AppendLine($"requestBuilder.WithQueryParameter(\"{param.Name}\", parameters.{propName});");
            }
            else
            {
                sb.AppendLine($"if (parameters.{propName} is not null)");
                sb.AppendLine("{");
                sb.AppendLine(4, $"requestBuilder.WithQueryParameter(\"{param.Name}\", parameters.{propName});");
                sb.AppendLine("}");
            }
        }

        // Add header parameters
        foreach (var (param, referenceId) in allParams.Where(p => p.Param.In == ParameterLocation.Header))
        {
            // For header parameters, use reference ID as property name if available,
            // otherwise strip x- prefix from header name
            var propName = !string.IsNullOrEmpty(referenceId)
                ? referenceId!.ToPascalCaseForDotNet()
                : param.Name!.ToHeaderPropertyName();
            var headerName = param.Name!;

            if (param.Required)
            {
                sb.AppendLine($"requestBuilder.WithHeaderParameter(\"{headerName}\", parameters.{propName});");
            }
            else
            {
                sb.AppendLine($"if (parameters.{propName} is not null)");
                sb.AppendLine("{");
                sb.AppendLine(4, $"requestBuilder.WithHeaderParameter(\"{headerName}\", parameters.{propName});");
                sb.AppendLine("}");
            }
        }

        // Add request body
        if (operation.RequestBody?.Content is not null)
        {
            // Check if this is a file upload
            var isFileUpload = operation.HasFileUpload();
            if (isFileUpload)
            {
                // For file uploads, check if it's single or multiple files
                var contentType = operation.GetFileUploadContentType();
                if (contentType is "application/octet-stream")
                {
                    // Single file upload via octet-stream - use WithBinaryBody with stream from IFileContent
                    sb.AppendLine("requestBuilder.WithBinaryBody(parameters.File!.OpenReadStream());");
                }
                else if (contentType is "multipart/form-data")
                {
                    // Check if it's a schema-based upload or raw file(s)
                    if (!operation.RequestBody.Content.TryGetValue("multipart/form-data", out var mediaType))
                    {
                        sb.AppendLine("requestBuilder.WithBody(parameters.File!.OpenReadStream());");
                    }
                    else if (mediaType.Schema is OpenApiSchemaReference schemaRef)
                    {
                        // Schema-based multipart - generate WithFile/WithFormField calls for each property
                        GenerateMultipartFormDataCode(sb, schemaRef, openApiDoc);
                    }
                    else if (mediaType.Schema is not null)
                    {
                        // Raw file(s) multipart - check if single or array
                        var (isFile, isCollection) = mediaType.Schema.GetFileUploadInfo();
                        if (isFile && isCollection)
                        {
                            // Array of files - iterate and add each with WithFile using IFileContent
                            sb.AppendLine("if (parameters.File is not null)");
                            sb.AppendLine("{");
                            sb.AppendLine(4, "for (var i = 0; i < parameters.File.Length; i++)");
                            sb.AppendLine(4, "{");
                            sb.AppendLine(8, "requestBuilder.WithFile(parameters.File[i].OpenReadStream(), \"files\", parameters.File[i].FileName);");
                            sb.AppendLine(4, "}");
                            sb.AppendLine("}");
                        }
                        else if (isFile)
                        {
                            // Single file - use WithFile with IFileContent metadata
                            sb.AppendLine("requestBuilder.WithFile(parameters.File!.OpenReadStream(), \"file\", parameters.File.FileName);");
                        }
                        else
                        {
                            // Fallback
                            sb.AppendLine("requestBuilder.WithBody(parameters.File!.OpenReadStream());");
                        }
                    }
                    else
                    {
                        // No schema - fallback
                        sb.AppendLine("requestBuilder.WithBody(parameters.File!.OpenReadStream());");
                    }
                }
                else
                {
                    // Other file upload content types
                    sb.AppendLine("requestBuilder.WithBody(parameters.File!.OpenReadStream());");
                }
            }
            else
            {
                // Regular JSON body
                sb.AppendLine("requestBuilder.WithBody(parameters.Request);");
            }
        }

        sb.AppendLine();

        // Build and send request. Standard verbs use the HttpMethod static property;
        // OpenAPI 3.2 custom additionalOperations verbs (e.g. LINK) are constructed by name.
        sb.AppendLine($"using var requestMessage = requestBuilder.Build({EndpointMapHelper.BuildHttpMethodExpression(httpMethod)});");
        if (resilienceStamp is not null)
        {
            sb.AppendLine(resilienceStamp);
        }

        // Use ResponseHeadersRead for streaming (IAsyncEnumerable) to enable true HTTP streaming
        // instead of buffering the entire response before iteration.
        // For streaming, don't use 'using' - StreamingEndpointResponse manages the response lifecycle.
        if (isAsyncEnumerable && !string.IsNullOrEmpty(streamingItemType))
        {
            sb.AppendLine("var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);");
        }
        else
        {
            sb.AppendLine("using var response = await client.SendAsync(requestMessage, cancellationToken);");
        }

        sb.AppendLine();

        // SSE and JSON Lines build the StreamingEndpointResponse<T> inline (their wire framing is
        // not a JSON array, so the JSON-based responseBuilder can't parse it), so only emit the
        // responseBuilder for the other paths.
        var streamFraming = operation.GetStreamingFraming();
        var usesStreamReaders = isAsyncEnumerable &&
                                !string.IsNullOrEmpty(streamingItemType) &&
                                UsesStreamReaders(streamFraming);

        if (!usesStreamReaders)
        {
            // Build response
            sb.AppendLine("var responseBuilder = httpMessageFactory.FromResponse(response);");
        }

        if (isBinaryEndpoint)
        {
            // For binary endpoints, use BuildBinaryResponseAsync directly
            sb.Append("return await responseBuilder.BuildBinaryResponseAsync(cancellationToken);");
        }
        else if (usesStreamReaders)
        {
            // SSE/JSON-Lines/JSON-seq/multipart framing is not a JSON array, so the Atc.Rest.Client
            // BuildStreamingEndpointResponseAsync<T> reader cannot parse it. Build the
            // StreamingEndpointResponse<T> inline using the emitted StreamReaders helper, which
            // deserializes each element via the DI-configured IContractSerializer. multipart/mixed
            // additionally extracts the boundary from the response Content-Type (the reader's extra arg).
            var readerMethod = ReaderMethodName(streamFraming);
            var isMultipart = streamFraming == StreamingFraming.MultipartMixed;
            sb.AppendLine("if (!response.IsSuccessStatusCode)");
            sb.AppendLine("{");
            sb.AppendLine(4, "var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);");
            sb.AppendLine(4, $"return new StreamingEndpointResponse<{streamingItemType}>(false, response.StatusCode, content: null, errorContent, response);");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("var stream = await response.Content.ReadAsStreamAsync(cancellationToken);");
            if (isMultipart)
            {
                sb.AppendLine("var boundary = response.Content.Headers.ContentType?.Parameters");
                sb.AppendLine(4, ".FirstOrDefault(p => string.Equals(p.Name, \"boundary\", StringComparison.OrdinalIgnoreCase))?.Value?.Trim('\"')");
                sb.AppendLine(4, $"?? \"{SequentialResultsExtractor.MultipartBoundaryValue}\";");
                sb.AppendLine($"var content = StreamReaders.{readerMethod}<{streamingItemType}>(stream, boundary, contractSerializer, cancellationToken);");
            }
            else
            {
                sb.AppendLine($"var content = StreamReaders.{readerMethod}<{streamingItemType}>(stream, contractSerializer, cancellationToken);");
            }

            sb.Append($"return new StreamingEndpointResponse<{streamingItemType}>(true, response.StatusCode, content, errorContent: null, response);");
        }
        else if (isAsyncEnumerable && !string.IsNullOrEmpty(streamingItemType))
        {
            // For streaming endpoints, use BuildStreamingEndpointResponseAsync directly.
            // This returns a StreamingEndpointResponse<T> that manages the response lifecycle
            // and allows proper streaming enumeration.
            sb.Append($"return await responseBuilder.BuildStreamingEndpointResponseAsync<{streamingItemType}>(cancellationToken);");
        }
        else
        {
            // A generator-added status that the default response shares: the framework's ProblemDetails is
            // sent as application/problem+json, the handler's default body is not.
            var sharedWithDefault = SharedWithDefault(responses, defaultResponse);
            var splitByMediaType = SplitsByMediaType(responses, defaultResponse);

            // A string error status whose handler sent no body gets the host wiring's ProblemDetails
            // (application/problem+json): read it as one, so the string accessor gives its message, not its JSON text.
            var hasStringErrors = responses.Any(r => !r.IsSuccess && !sharedWithDefault.Contains(r) && r.ContentType == "string");
            if (splitByMediaType || hasStringErrors)
            {
                sb.AppendLine("var isProblemJson = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() is \"application/problem+json\";");
            }

            foreach (var responseInfo in responses)
            {
                if (sharedWithDefault.Contains(responseInfo))
                {
                    continue;
                }

                if (!responseInfo.IsSuccess && responseInfo.ContentType == "string")
                {
                    sb.AppendLine("if (isProblemJson)");
                    sb.AppendLine("{");
                    sb.AppendLine(4, $"responseBuilder.AddErrorResponse<ProblemDetails>({responseInfo.StatusCodeExpression});");
                    sb.AppendLine("}");
                    sb.AppendLine("else");
                    sb.AppendLine("{");
                    sb.AppendLine(4, responseInfo.TextMediaType is not null
                        ? $"responseBuilder.AddErrorTextResponse({responseInfo.StatusCodeExpression});"
                        : $"responseBuilder.AddErrorResponse<string>({responseInfo.StatusCodeExpression});");
                    sb.AppendLine("}");
                    sb.AppendLine();
                    continue;
                }

                if (responseInfo.IsSuccess)
                {
                    if (responseInfo.TextMediaType is not null)
                    {
                        // Raw text body (text/plain, text/csv, ...) — bypass the JSON contract serializer.
                        sb.AppendLine($"responseBuilder.AddSuccessTextResponse({responseInfo.StatusCodeExpression});");
                    }
                    else if (responseInfo.ContentType is null)
                    {
                        // Empty response - use non-generic AddSuccessResponse
                        sb.AppendLine($"responseBuilder.AddSuccessResponse({responseInfo.StatusCodeExpression});");
                    }
                    else
                    {
                        sb.AppendLine($"responseBuilder.AddSuccessResponse<{responseInfo.ContentType}>({responseInfo.StatusCodeExpression});");
                    }
                }
                else if (responseInfo.TextMediaType is not null)
                {
                    // Raw text error body — same bypass for the error path.
                    sb.AppendLine($"responseBuilder.AddErrorTextResponse({responseInfo.StatusCodeExpression});");
                }
                else
                {
                    // Error responses always have a type (ProblemDetails is the fallback)
                    sb.AppendLine($"responseBuilder.AddErrorResponse<{responseInfo.ContentType}>({responseInfo.StatusCodeExpression});");
                }
            }

            if (splitByMediaType)
            {
                sb.AppendLine("if (isProblemJson)");
                sb.AppendLine("{");
                foreach (var responseInfo in sharedWithDefault)
                {
                    sb.AppendLine(4, $"responseBuilder.AddErrorResponse<{responseInfo.ContentType}>({responseInfo.StatusCodeExpression});");
                }

                sb.AppendLine("}");
                sb.AppendLine();
            }

            // `default`: any other error status is read as the default body, through the configured serializer.
            if (defaultResponse is not null)
            {
                sb.AppendLine($"if ({DefaultErrorCondition(responses, defaultResponse, "response.IsSuccessStatusCode", "response.StatusCode", splitByMediaType ? "!isProblemJson" : null)})");
                sb.AppendLine("{");
                sb.AppendLine(4, defaultResponse.TextMediaType is not null
                    ? "responseBuilder.AddErrorTextResponse(response.StatusCode);"
                    : $"responseBuilder.AddErrorResponse<{defaultResponse.ContentType}>(response.StatusCode);");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            sb.Append($"return await responseBuilder.BuildResponseAsync(x => new {operationName}EndpointResult(ProblemDetailsFactory.WithTextErrorBody(x)), cancellationToken);");
        }

        return sb.ToString();
    }

    private static string GenerateResultInterface(
        string projectName,
        string pathSegment,
        string operationName,
        string description,
        List<ResponseInfo> responses,
        ResponseInfo? defaultResponse,
        bool hasSegmentModels,
        bool hasSharedModels)
    {
        var sharedModelsNamespace = NamespaceBuilder.ForModels(projectName);
        var segmentModelsNamespace = NamespaceBuilder.ForModels(projectName, pathSegment);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using Atc.Rest.Client;");
        sb.AppendLine($"using {NamespaceBuilder.BuildBase(projectName)};");

        // Add shared Models using if there are shared models
        if (hasSharedModels)
        {
            sb.AppendLine($"using {sharedModelsNamespace};");
        }

        // Add segment-specific Models using if there are segment-specific models.
        // When the path segment resolves away both namespaces are identical, so skip the duplicate.
        if (hasSegmentModels &&
            !(hasSharedModels && string.Equals(segmentModelsNamespace, sharedModelsNamespace, StringComparison.Ordinal)))
        {
            sb.AppendLine($"using {segmentModelsNamespace};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {NamespaceBuilder.ForEndpointInterfaces(projectName, pathSegment)};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Interface for Client Endpoint Result.");
        sb.AppendLine($"/// Description: {description}");
        sb.AppendLine($"/// Operation: {operationName}.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"public interface I{operationName}EndpointResult : IEndpointResponse");
        sb.AppendLine("{");

        foreach (var response in responses)
        {
            sb.AppendLine(4, $"bool Is{response.PropertyName} {{ get; }}");
            sb.AppendLine();
        }

        if (defaultResponse is not null)
        {
            sb.AppendLine(4, $"bool Is{defaultResponse.PropertyName} {{ get; }}");
            sb.AppendLine();
        }

        // Only generate content properties for responses that have content
        foreach (var response in responses.Where(r => r.ContentType is not null))
        {
            sb.AppendLine(4, $"{response.ContentType} {response.PropertyName}Content {{ get; }}");
            sb.AppendLine();
        }

        if (defaultResponse?.ContentType is not null)
        {
            sb.AppendLine(4, $"{defaultResponse.ContentType} {defaultResponse.PropertyName}Content {{ get; }}");
            sb.AppendLine();
        }

        // Remove last empty line
        var content = sb.ToString();
        if (content.EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            content = content.Substring(0, content.Length - 2);
        }
        else if (content.EndsWith("\n\n", StringComparison.Ordinal))
        {
            content = content.Substring(0, content.Length - 1);
        }

        return content + "}\n";
    }

    private static string GenerateResultClass(
        string projectName,
        string pathSegment,
        string operationName,
        string description,
        List<ResponseInfo> responses,
        ResponseInfo? defaultResponse,
        bool hasSegmentModels,
        bool hasSharedModels,
        string? customErrorTypeName)
    {
        var sharedModelsNamespace = NamespaceBuilder.ForModels(projectName);
        var segmentModelsNamespace = NamespaceBuilder.ForModels(projectName, pathSegment);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Net;");
        sb.AppendLine("using Atc.Rest.Client;");
        sb.AppendLine($"using {NamespaceBuilder.BuildBase(projectName)};");
        sb.AppendLine($"using {NamespaceBuilder.ForEndpointInterfaces(projectName, pathSegment)};");

        // Add shared Models using if there are shared models
        if (hasSharedModels)
        {
            sb.AppendLine($"using {sharedModelsNamespace};");
        }

        // Add segment-specific Models using if there are segment-specific models.
        // When the path segment resolves away both namespaces are identical, so skip the duplicate.
        if (hasSegmentModels &&
            !(hasSharedModels && string.Equals(segmentModelsNamespace, sharedModelsNamespace, StringComparison.Ordinal)))
        {
            sb.AppendLine($"using {segmentModelsNamespace};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {NamespaceBuilder.ForEndpointResults(projectName, pathSegment)};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Client Endpoint result.");
        sb.AppendLine($"/// Description: {description}");
        sb.AppendLine($"/// Operation: {operationName}.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"public partial class {operationName}EndpointResult : EndpointResponse, I{operationName}EndpointResult");
        sb.AppendLine("{");
        sb.AppendLine(4, $"public {operationName}EndpointResult(EndpointResponse response)");
        sb.AppendLine(8, ": base(response)");
        sb.AppendLine(4, "{");
        sb.AppendLine(4, "}");
        sb.AppendLine();

        // Static factories so consumers can build results in tests without
        // constructing an EndpointResponse by hand.
        foreach (var response in responses
                     .GroupBy(r => r.PropertyName, StringComparer.Ordinal)
                     .Select(g => g.First()))
        {
            sb.AppendLine(4, "/// <summary>");
            sb.AppendLine(4, $"/// Creates a result representing HTTP {response.StatusEnumName}.");
            sb.AppendLine(4, "/// </summary>");

            if (response.ContentType is null)
            {
                sb.AppendLine(4, $"public static {operationName}EndpointResult {response.PropertyName}()");
                sb.AppendLine(8, $"=> Create({response.StatusCodeExpression}, contentObject: null);");
            }
            else
            {
                sb.AppendLine(4, $"public static {operationName}EndpointResult {response.PropertyName}({response.ContentType} content)");
                sb.AppendLine(8, $"=> Create({response.StatusCodeExpression}, content);");
            }

            sb.AppendLine();
        }

        if (defaultResponse?.ContentType is not null)
        {
            sb.AppendLine(4, "/// <summary>");
            sb.AppendLine(4, "/// Creates a result representing the default (any other) error response.");
            sb.AppendLine(4, "/// </summary>");
            sb.AppendLine(4, $"public static {operationName}EndpointResult {defaultResponse.PropertyName}(HttpStatusCode statusCode, {defaultResponse.ContentType} content)");
            sb.AppendLine(8, "=> Create(statusCode, content);");
            sb.AppendLine();
        }

        sb.AppendLine(4, $"private static {operationName}EndpointResult Create(");
        sb.AppendLine(8, "HttpStatusCode statusCode,");
        sb.AppendLine(8, "object? contentObject)");
        sb.AppendLine(8, "=> new(new EndpointResponse(");
        sb.AppendLine(12, "isSuccess: (int)statusCode is >= 200 and < 300,");
        sb.AppendLine(12, "statusCode: statusCode,");
        sb.AppendLine(12, "content: string.Empty,");
        sb.AppendLine(12, "contentObject: contentObject,");
        sb.AppendLine(12, "headers: new Dictionary<string, IEnumerable<string>>(StringComparer.Ordinal)));");
        sb.AppendLine();

        // Is{PropertyName} properties
        foreach (var response in responses)
        {
            sb.AppendLine(4, $"public bool Is{response.PropertyName}");
            sb.AppendLine(8, $"=> StatusCode == {response.StatusCodeExpression};");
            sb.AppendLine();
        }

        if (defaultResponse is not null)
        {
            // A model (or text) default is only the default error when the body was read as it: an HTML page from a
            // proxy is not, so IsDefaultError stays false and DefaultErrorContent never throws when it is true. A
            // ProblemDetails default reads any body, and a default without content has nothing to read.
            var readGuard = defaultResponse.ContentType is null or "ProblemDetails" or "ValidationProblemDetails"
                ? null
                : $"ContentObject is {defaultResponse.ContentType}";

            // The guard also tells a shared status's default body from the framework's ProblemDetails (the media type
            // split registers the default type only for a body that is not application/problem+json).
            var condition = DefaultErrorCondition(responses, defaultResponse, "IsSuccess", "StatusCode", sharedStatusExpression: null);

            sb.AppendLine(4, $"public bool Is{defaultResponse.PropertyName}");
            sb.AppendLine(8, readGuard is null ? $"=> {condition};" : $"=> {condition} && {readGuard};");
            sb.AppendLine();
        }

        // {PropertyName}Content properties - only generate for responses that have content
        foreach (var response in responses.Where(r => r.ContentType is not null))
        {
            sb.AppendLine(4, $"public {response.ContentType} {response.PropertyName}Content");
            sb.AppendLine(8, $"=> Is{response.PropertyName} && ContentObject is {response.ContentType} result");
            sb.AppendLine(12, "? result");

            // For ProblemDetails / ValidationProblemDetails, add fallback for plain text responses and null ContentObject
            if (response.ContentType is "ProblemDetails" or "ValidationProblemDetails")
            {
                var create = response.ContentType == "ProblemDetails" ? "Create" : "CreateValidation";
                var fromContent = response.ContentType == "ProblemDetails" ? "FromContent" : "ValidationFromContent";
                sb.AppendLine(12, $": Is{response.PropertyName} && ContentObject is string message");
                sb.AppendLine(16, $"? ProblemDetailsFactory.{create}({response.StatusCodeExpression}, message)");
                sb.AppendLine(16, $": Is{response.PropertyName} && ContentObject is null");
                sb.AppendLine(20, $"? ProblemDetailsFactory.{create}({response.StatusCodeExpression})");
                sb.AppendLine(20, $": Is{response.PropertyName}");
                sb.AppendLine(24, $"? ProblemDetailsFactory.{fromContent}({response.StatusCodeExpression}, Content)");
                sb.AppendLine(24, $": throw InvalidContentAccessException<{response.ContentType}>({response.StatusCodeExpression}, \"{response.PropertyName}Content\");");
            }
            else if (!response.IsSuccess && response.ContentType != "string")
            {
                // An error model - the declared one, or the custom one standing for bodies of an unknown shape: read the
                // raw body leniently rather than throw when it did not fit (a ProblemDetails' numeric status in a string
                // property, a proxy's page); the accessor of a status that is true never throws.
                sb.AppendLine(12, $": Is{response.PropertyName}");
                sb.AppendLine(16, $"? ProblemDetailsFactory.ModelFromContent<{response.ContentType}>(Content)");
                sb.AppendLine(16, $": throw InvalidContentAccessException<{response.ContentType}>({response.StatusCodeExpression}, \"{response.PropertyName}Content\");");
            }
            else if (!response.IsSuccess && response.ContentType == "string")
            {
                // An application/problem+json body (the host wiring's, when the handler sent none) was read as a
                // ProblemDetails: its message is the detail, else the title. Any other body is the raw text.
                sb.AppendLine(12, $": Is{response.PropertyName} && ContentObject is ProblemDetails problem");
                sb.AppendLine(16, "? string.IsNullOrEmpty(problem.Detail) ? problem.Title ?? string.Empty : problem.Detail");
                sb.AppendLine(16, $": Is{response.PropertyName}");
                sb.AppendLine(20, "? Content");
                sb.AppendLine(20, $": throw InvalidContentAccessException<string>({response.StatusCodeExpression}, \"{response.PropertyName}Content\");");
            }
            else
            {
                sb.AppendLine(12, $": throw InvalidContentAccessException<{response.ContentType}>({response.StatusCodeExpression}, \"{response.PropertyName}Content\");");
            }

            sb.AppendLine();
        }

        if (defaultResponse?.ContentType is { } defaultType)
        {
            var name = defaultResponse.PropertyName;
            sb.AppendLine(4, $"public {defaultType} {name}Content");
            sb.AppendLine(8, $"=> Is{name} && ContentObject is {defaultType} result");
            sb.AppendLine(12, "? result");

            if (defaultType is "ProblemDetails" or "ValidationProblemDetails")
            {
                var create = defaultType == "ProblemDetails" ? "Create" : "CreateValidation";
                sb.AppendLine(12, $": Is{name} && ContentObject is string message");
                sb.AppendLine(16, $"? ProblemDetailsFactory.{create}(StatusCode, message)");
                sb.AppendLine(16, $": Is{name} && ContentObject is null");
                sb.AppendLine(20, $"? ProblemDetailsFactory.{create}(StatusCode)");
                sb.AppendLine(20, $": Is{name}");
                sb.AppendLine(24, $"? ProblemDetailsFactory.{(defaultType == "ProblemDetails" ? "FromContent" : "ValidationFromContent")}(StatusCode, Content)");
                sb.AppendLine(24, $": throw InvalidContentAccessException<{defaultType}>(StatusCode, \"{name}Content\");");
            }
            else
            {
                sb.AppendLine(12, $": throw InvalidContentAccessException<{defaultType}>(StatusCode, \"{name}Content\");");
            }

            sb.AppendLine();
        }

        // Remove last empty line
        var content = sb.ToString();
        if (content.EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            content = content.Substring(0, content.Length - 2);
        }
        else if (content.EndsWith("\n\n", StringComparison.Ordinal))
        {
            content = content.Substring(0, content.Length - 1);
        }

        return content + "}\n";
    }

    /// <summary>
    /// Resolves the effective HTTP client name based on project name and optional configured name.
    /// </summary>
    /// <param name="projectName">The project/namespace name.</param>
    /// <param name="configuredName">Optional configured HTTP client name.</param>
    /// <returns>The resolved HTTP client name.</returns>
    /// <remarks>
    /// Resolution rules:
    /// - If configuredName is null/empty: strips ".ApiClient" or "ApiClient" suffix from projectName, appends "-ApiClient"
    /// - If configuredName contains '.' or '-': uses configuredName as-is (full name)
    /// - Otherwise: strips suffix from projectName, appends "-{configuredName}" (simple name)
    /// </remarks>
    internal static string GetEffectiveHttpClientName(
        string projectName,
        string? configuredName)
    {
        // Get base name by stripping common ApiClient suffixes
        var baseName = projectName;
        if (baseName.EndsWith(".ApiClient", StringComparison.OrdinalIgnoreCase))
        {
            baseName = baseName.Substring(0, baseName.Length - ".ApiClient".Length);
        }
        else if (baseName.EndsWith("ApiClient", StringComparison.OrdinalIgnoreCase))
        {
            baseName = baseName.Substring(0, baseName.Length - "ApiClient".Length);
        }

        // Replace dots with dashes for cleaner HTTP client names
        baseName = baseName.Replace('.', '-');

        // If no configured name, use default pattern
        if (string.IsNullOrWhiteSpace(configuredName))
        {
            return $"{baseName}-ApiClient";
        }

        // If full name (contains . or -), use as-is
        if (configuredName.Contains('.') || configuredName.Contains('-'))
        {
            return configuredName!;
        }

        // Simple name - combine with base
        return $"{baseName}-{configuredName}";
    }

    private static string GetSchemaTypeName(
        IOpenApiSchema? schema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        string? operationId,
        string? pathSegment,
        string? context,
        Dictionary<string, InlineSchemaInfo>? inlineSchemas,
        bool isAsyncEnumerable)
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

            if (openApiDoc.Components?.Schemas is not null &&
                openApiDoc.Components.Schemas.TryGetValue(refId!, out var resolvedSchema) &&
                resolvedSchema is OpenApiSchema { Type: JsonSchemaType.Array } arraySchema)
            {
                return GetArraySchemaType(arraySchema, openApiDoc, registry, operationId, pathSegment, inlineSchemas, isAsyncEnumerable);
            }

            return OpenApiSchemaExtensions.ResolveTypeName(refId!, registry);
        }

        if (schema is OpenApiSchema actualSchema)
        {
            // Handle array type specially (GetPrimitiveCSharpTypeName returns null for arrays)
            if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true)
            {
                return GetArraySchemaType(actualSchema, openApiDoc, registry, operationId, pathSegment, inlineSchemas, isAsyncEnumerable);
            }

            // Handle allOf composition - look for PaginatedResult pattern
            if (actualSchema.AllOf is { Count: > 0 })
            {
                var allOfType = GetAllOfSchemaTypeName(actualSchema.AllOf, openApiDoc, registry);
                if (isAsyncEnumerable && allOfType != "object")
                {
                    return $"IAsyncEnumerable<{allOfType}>";
                }

                return allOfType;
            }

            // Handle inline object schemas with properties
            if (InlineSchemaExtractor.IsInlineObjectSchema(actualSchema) &&
                !string.IsNullOrEmpty(operationId) &&
                pathSegment is not null &&
                !string.IsNullOrEmpty(context) &&
                inlineSchemas is not null)
            {
                var typeName = InlineSchemaExtractor.GenerateInlineTypeName(operationId!, context!);

                // Extract and register the inline schema if not already registered
                if (!inlineSchemas.ContainsKey(typeName))
                {
                    var recordParams = InlineSchemaExtractor.ExtractRecordFromInlineSchema(
                        actualSchema, typeName, registry);
                    inlineSchemas[typeName] = new InlineSchemaInfo(typeName, pathSegment!, recordParams);
                }

                return typeName;
            }

            // Use centralized primitive type mapping
            return actualSchema.Type.ToPrimitiveCSharpTypeName(actualSchema.Format) ?? "object";
        }

        return "object";
    }

    private static string GetArraySchemaType(
        OpenApiSchema schema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        string? operationId,
        string? pathSegment,
        Dictionary<string, InlineSchemaInfo>? inlineSchemas,
        bool isAsyncEnumerable)
    {
        if (schema.Items is null)
        {
            return isAsyncEnumerable ? "IAsyncEnumerable<object>" : "IEnumerable<object>";
        }

        // For array items, use "ResponseItem" context to distinguish from direct response objects
        var itemType = GetSchemaTypeName(
            schema.Items,
            openApiDoc,
            registry,
            operationId,
            pathSegment,
            "ResponseItem",
            inlineSchemas,
            isAsyncEnumerable: false); // Item types don't get wrapped in IAsyncEnumerable

        return isAsyncEnumerable
            ? $"IAsyncEnumerable<{itemType}>"
            : $"IEnumerable<{itemType}>";
    }

    /// <summary>
    /// Handles allOf composition schemas, specifically for PaginatedResult patterns.
    /// </summary>
    private static string GetAllOfSchemaTypeName(
        IList<IOpenApiSchema> allOfSchemas,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry)
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
                            itemType = GetArrayItemTypeName(arraySchema, openApiDoc, registry);
                        }
                        else if (propSchema is OpenApiSchemaReference propRef)
                        {
                            // Resolve the reference
                            var propRefId = propRef.Reference.Id;
                            if (!string.IsNullOrEmpty(propRefId) &&
                                openApiDoc.Components?.Schemas?.TryGetValue(propRefId!, out var resolvedSchema) == true &&
                                resolvedSchema is OpenApiSchema { Type: JsonSchemaType.Array } resolvedArray)
                            {
                                itemType = GetArrayItemTypeName(resolvedArray, openApiDoc, registry);
                            }
                        }

                        break;
                    }
                }
            }
        }

        // If we found PaginationResult<T> or PaginatedResult<T> pattern, return it
        if (baseType is not null && HttpClientExtractor.IsPaginationBaseType(baseType) && itemType is not null)
        {
            return $"{baseType}<{itemType}>";
        }

        // Return the base type if found
        return baseType ?? "object";
    }

    /// <summary>
    /// Gets the item type name from an array schema.
    /// </summary>
    private static string GetArrayItemTypeName(
        OpenApiSchema arraySchema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry)
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
                // Check if this is an array type alias
                if (openApiDoc.Components?.Schemas?.TryGetValue(itemRefId!, out var itemSchema) == true &&
                    itemSchema is OpenApiSchema { Type: JsonSchemaType.Array } innerArray)
                {
                    return GetArrayItemTypeName(innerArray, openApiDoc, registry);
                }

                return OpenApiSchemaExtensions.ResolveTypeName(itemRefId!, registry);
            }
        }

        if (arraySchema.Items is OpenApiSchema itemSchema2)
        {
            return itemSchema2.Type.ToPrimitiveCSharpTypeName(itemSchema2.Format) ?? "object";
        }

        return "object";
    }

    /// <summary>
    /// Normalizes HTTP status code names to proper PascalCase.
    /// HttpStatusCode.ToString() returns "OK" for 200, but we want "Ok" for proper PascalCase.
    /// </summary>
    private static string NormalizeStatusName(string statusName)
        => statusName switch
        {
            "OK" => "Ok",
            _ => statusName,
        };

    /// <summary>
    /// The condition under which a response is the spec's <c>default</c> response: not a success, and not
    /// one of the error statuses the operation lists (or the generator adds).
    /// </summary>
    /// <param name="responses">The operation's listed (and generator-added) responses.</param>
    /// <param name="defaultResponse">The <c>default</c> response.</param>
    /// <param name="isSuccessExpression">The expression for "the response is a success".</param>
    /// <param name="statusCodeExpression">The expression for the response status.</param>
    /// <param name="sharedStatusExpression">
    /// The expression that tells, for a status in <see cref="SharedWithDefault"/>, that the body is the default body.
    /// </param>
    private static string DefaultErrorCondition(
        List<ResponseInfo> responses,
        ResponseInfo defaultResponse,
        string isSuccessExpression,
        string statusCodeExpression,
        string? sharedStatusExpression)
    {
        var shared = SharedWithDefault(responses, defaultResponse);
        var listed = responses
            .Where(r => !r.IsSuccess && !shared.Contains(r))
            .Select(r => r.StatusCodeExpression)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var condition = listed.Count == 0
            ? $"!{isSuccessExpression}"
            : $"!{isSuccessExpression} && {statusCodeExpression} is not {OrPattern(listed)}";

        return shared.Count == 0 || sharedStatusExpression is null
            ? condition
            : $"{condition} && ({statusCodeExpression} is not {OrPattern(shared.Select(r => r.StatusCodeExpression).ToList())} || {sharedStatusExpression})";

        static string OrPattern(List<string> values)
            => values.Count == 1 ? values[0] : $"({string.Join(" or ", values)})";
    }

    /// <summary>
    /// The generator-added error responses (400 validation, 404, 409, 500, 504, …) whose type differs from the
    /// <c>default</c> response's. Such a status is written either by the framework (a ProblemDetails, sent as
    /// <c>application/problem+json</c>) or by the handler's <c>Error(…)</c> factory (the default body), so the
    /// client decides by the response media type. A <c>ValidationProblemDetails</c> under a <c>ProblemDetails</c>
    /// default is not shared: it is the richer ProblemDetails, so the 400 stays a <c>ValidationProblemDetails</c>.
    /// </summary>
    private static List<ResponseInfo> SharedWithDefault(
        List<ResponseInfo> responses,
        ResponseInfo? defaultResponse)
        => defaultResponse is null
            ? []
            : responses
                .Where(r => r.IsAutoAdded &&
                            r.ContentType is "ProblemDetails" or "ValidationProblemDetails" &&
                            !string.Equals(r.ContentType, defaultResponse.ContentType, StringComparison.Ordinal) &&
                            !(string.Equals(r.ContentType, "ValidationProblemDetails", StringComparison.Ordinal) &&
                              string.Equals(defaultResponse.ContentType, "ProblemDetails", StringComparison.Ordinal)))
                .ToList();

    /// <summary>
    /// Whether the generator-added statuses shared with the <c>default</c> response are told apart by the response
    /// media type: only when the default body is a plain JSON / text body. A default that is itself a problem (a
    /// ProblemDetails, or any model under <c>application/problem+json</c>) cannot be told apart from the framework's
    /// ProblemDetails, so those statuses are read as the default body - their ProblemDetails accessors read the raw
    /// body again (<c>ProblemDetailsFactory.FromContent</c>).
    /// </summary>
    private static bool SplitsByMediaType(
        List<ResponseInfo> responses,
        ResponseInfo? defaultResponse)
        => defaultResponse is not null &&
           SharedWithDefault(responses, defaultResponse).Count > 0 &&
           defaultResponse.ContentType is not ("ProblemDetails" or "ValidationProblemDetails") &&
           defaultResponse.ErrorMediaType?.Split(';')[0].Trim().EndsWith("problem+json", StringComparison.OrdinalIgnoreCase) != true;

    /// <summary>
    /// Extracts the item type from an IAsyncEnumerable type string.
    /// For example, "IAsyncEnumerable&lt;Account&gt;" returns "Account".
    /// </summary>
    private static string? ExtractStreamingItemType(
        List<ResponseInfo> responses)
    {
        // Find the success response with IAsyncEnumerable content type
        var successResponse = responses.FirstOrDefault(r => r.IsSuccess && r.ContentType is not null);
        if (successResponse?.ContentType is null)
        {
            return null;
        }

        var contentType = successResponse.ContentType;
        const string prefix = "IAsyncEnumerable<";

        if (!contentType.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        // Extract the type between IAsyncEnumerable< and >
        var startIndex = prefix.Length;
        var endIndex = contentType.LastIndexOf('>');

        if (endIndex <= startIndex)
        {
            return null;
        }

        return contentType.Substring(startIndex, endIndex - startIndex);
    }

    /// <summary>
    /// Generates code for multipart/form-data requests with schema-based content.
    /// Maps schema properties to WithFile() for binary properties and WithFormField() for others.
    /// </summary>
    private static void GenerateMultipartFormDataCode(
        StringBuilder sb,
        OpenApiSchemaReference schemaRef,
        OpenApiDocument openApiDoc)
    {
        // Resolve the schema from the document
        var schemaId = schemaRef.Reference?.Id;
        if (string.IsNullOrEmpty(schemaId))
        {
            // Fallback to simple body if schema can't be resolved
            sb.AppendLine("requestBuilder.WithBody(parameters.Request);");
            return;
        }

        if (openApiDoc.Components?.Schemas is null ||
            !openApiDoc.Components.Schemas.TryGetValue(schemaId!, out var schema))
        {
            // Fallback to simple body if schema can't be resolved
            sb.AppendLine("requestBuilder.WithBody(parameters.Request);");
            return;
        }

        var properties = schema.Properties?.ToList() ?? [];
        if (properties.Count == 0)
        {
            sb.AppendLine("requestBuilder.WithBody(parameters.Request);");
            return;
        }

        // Generate code for each property
        foreach (var prop in properties)
        {
            var propName = prop.Key;
            var propSchema = prop.Value;
            var pascalPropName = CasingHelper.ToPascalCase(propName);
            var isBinary = propSchema.Type?.HasFlag(JsonSchemaType.String) == true && propSchema.Format == "binary";
            var isArray = propSchema.Type?.HasFlag(JsonSchemaType.Array) == true;
            var isArrayOfBinary = isArray &&
                                  propSchema.Items?.Type?.HasFlag(JsonSchemaType.String) == true &&
                                  propSchema.Items?.Format == "binary";

            if (isBinary)
            {
                // Single file property - use WithFile with IFileContent
                sb.AppendLine($"if (parameters.Request?.{pascalPropName} is not null)");
                sb.AppendLine("{");
                sb.AppendLine(4, $"requestBuilder.WithFile(parameters.Request.{pascalPropName}.OpenReadStream(), \"{propName}\", parameters.Request.{pascalPropName}.FileName);");
                sb.AppendLine("}");
                sb.AppendLine();
            }
            else if (isArrayOfBinary)
            {
                // Array of files - use WithFile for each IFileContent
                sb.AppendLine($"if (parameters.Request?.{pascalPropName} is not null)");
                sb.AppendLine("{");
                sb.AppendLine(4, $"foreach (var (fileItem, index) in parameters.Request.{pascalPropName}.Select((f, i) => (f, i)))");
                sb.AppendLine(4, "{");
                sb.AppendLine(8, $"requestBuilder.WithFile(fileItem.OpenReadStream(), \"{propName}\", fileItem.FileName);");
                sb.AppendLine(4, "}");
                sb.AppendLine("}");
                sb.AppendLine();
            }
            else if (isArray)
            {
                // Array of non-binary values - serialize as JSON or comma-separated
                sb.AppendLine($"if (parameters.Request?.{pascalPropName} is not null)");
                sb.AppendLine("{");
                sb.AppendLine(4, $"foreach (var item in parameters.Request.{pascalPropName})");
                sb.AppendLine(4, "{");
                sb.AppendLine(8, $"requestBuilder.WithFormField(\"{propName}\", item?.ToString() ?? string.Empty);");
                sb.AppendLine(4, "}");
                sb.AppendLine("}");
                sb.AppendLine();
            }
            else
            {
                // Simple value - use WithFormField
                sb.AppendLine($"if (parameters.Request?.{pascalPropName} is not null)");
                sb.AppendLine("{");
                sb.AppendLine(4, $"requestBuilder.WithFormField(\"{propName}\", parameters.Request.{pascalPropName}.ToString()!);");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }
    }

    /// <summary>
    /// Determines whether a streaming operation reads its response via the emitted per-operation
    /// <c>StreamReaders</c> helper (rather than the JSON-array-based <c>BuildStreamingEndpointResponseAsync</c>).
    /// That is true for the wire framings whose bytes are not a JSON array: Server-Sent Events,
    /// JSON Lines, JSON Text Sequence and multipart/mixed.
    /// </summary>
    private static bool UsesStreamReaders(StreamingFraming framing)
        => framing is StreamingFraming.ServerSentEvents
            or StreamingFraming.JsonLines
            or StreamingFraming.JsonSequence
            or StreamingFraming.MultipartMixed;

    /// <summary>
    /// Maps a StreamReaders-based framing to the emitted per-operation reader method name. Throws
    /// for any framing not explicitly handled so a newly-added framing (e.g. json-seq in a later
    /// task) fails loudly here instead of silently routing through the JSON Lines reader.
    /// </summary>
    private static string ReaderMethodName(StreamingFraming framing)
        => framing switch
        {
            StreamingFraming.ServerSentEvents => "ReadServerSentEventsAsync",
            StreamingFraming.JsonLines => "ReadJsonLinesAsync",
            StreamingFraming.JsonSequence => "ReadJsonSequenceAsync",
            StreamingFraming.MultipartMixed => "ReadMultipartMixedAsync",
            _ => throw new InvalidOperationException(
                $"No per-operation StreamReaders method is defined for framing '{framing}'."),
        };
}