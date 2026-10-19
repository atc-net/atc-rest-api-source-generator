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
        string? ResultClassContent,
        IReadOnlyList<(string FileName, string Content)>? HeaderEnumFiles = null);

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
    /// <param name="preferXml">Whether a body offered as JSON and XML is sent and read as XML.</param>
    /// <param name="extensibleEnums">Whether the client's string enums are extensible (a response header keeps a value the specification does not list).</param>
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
        string? namespaceSegment = null,
        bool preferXml = false,
        bool extensibleEnums = true)
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
                    useServersBasePath,
                    preferXml,
                    extensibleEnums);

                if (files is not null)
                {
                    result.Add(files);
                }
            }
        }

        return (result, inlineSchemas);
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

    /// <summary>
    /// Checks if an operation returns binary content (application/octet-stream).
    /// </summary>
    /// <remarks>
    /// Binary endpoints use BinaryEndpointResponse from Atc.Rest.Client instead of
    /// generating custom result classes.
    /// </remarks>
    private static bool IsBinaryEndpoint(OpenApiOperation operation)
        => operation.IsStreamedDownloadOperation();

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
    /// <param name="IsFile">Whether the body is a file (a DownloadedFile).</param>
    /// <param name="Xml">The XML body, or <see langword="null"/>.</param>
    /// <param name="FileMediaTypes">A model or a file: the file media types (the file is read by <c>{PropertyName}File</c>), else <see langword="null"/>.</param>
    private sealed record ResponseInfo(string StatusCode, string StatusEnumName, string PropertyName, string? ContentType, bool IsSuccess, string? TextMediaType = null, bool IsAutoAdded = false, string? ErrorMediaType = null, bool IsFile = false, XmlBody? Xml = null, IReadOnlyList<string>? FileMediaTypes = null)
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
        bool useServersBasePath = true,
        bool preferXml = false,
        bool extensibleEnums = true)
    {
        var operationId = OperationIdHelper.Effective(operation, httpMethod, path);
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
            hasSharedModels,
            operation.Deprecated);

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
            useServersBasePath,
            preferXml);

        // A binary endpoint has no result interface/class (it returns StreamBinaryEndpointResponse). A streamed one
        // has: the items are the response's Content, so its success responses carry no content of their own.
        string? resultInterfaceFileName = null;
        string? resultInterfaceContent = null;
        string? resultClassFileName = null;
        string? resultClassContent = null;
        var headerEnumFiles = new List<(string FileName, string Content)>();
        var streamedItemType = isAsyncEnumerable && !string.IsNullOrEmpty(streamingItemType) ? streamingItemType : null;

        if (!isBinaryEndpoint && (!isAsyncEnumerable || streamedItemType is not null))
        {
            var resultResponses = streamedItemType is null
                ? responses
                : responses.ConvertAll(r => r.IsSuccess ? r with { ContentType = null, Xml = null, IsFile = false, FileMediaTypes = null, TextMediaType = null } : r);

            // Generate result interface
            // The headers the responses declare: typed properties of the result. An enum a header declares inline is
            // a type next to the result's interface, which the result class imports too.
            var componentTypeNames = TypeConflictRegistry.ComponentTypeNamesOf(openApiDoc);
            var declaredHeaders = ResponseHeaderFactoryHelper.DeclaredBy(operation, registry, operationName, componentTypeNames);
            var interfacesNamespace = NamespaceBuilder.ForEndpointInterfaces(projectName, pathSegment);
            foreach (var (typeName, schema) in ResponseHeaderFactoryHelper.InlineEnumsOf(operation, operationName, componentTypeNames))
            {
                if (InlineEnumExtractor.ExtractEnumFromInlineSchema(schema, typeName, interfacesNamespace) is { } enumParameters)
                {
                    headerEnumFiles.Add((
                        NamespaceBuilder.ToFileName(interfacesNamespace, typeName),
                        InlineEnumExtractor.GenerateEnumContent(enumParameters, extensible: extensibleEnums)));
                }
            }

            resultInterfaceContent = GenerateResultInterface(
                projectName,
                pathSegment,
                operationName,
                description,
                resultResponses,
                defaultResponse,
                hasSegmentModels,
                hasSharedModels,
                declaredHeaders,
                streamedItemType,
                isEventStream: streamedItemType is not null && operation.GetStreamingFraming() == StreamingFraming.ServerSentEvents);
            resultInterfaceFileName = NamespaceBuilder.ToFileName(
                NamespaceBuilder.ForEndpointInterfaces(projectName, pathSegment),
                $"I{operationName}EndpointResult");

            // Generate result class
            resultClassContent = GenerateResultClass(
                projectName,
                pathSegment,
                operationName,
                description,
                resultResponses,
                defaultResponse,
                hasSegmentModels,
                hasSharedModels,
                customErrorTypeName,
                declaredHeaders,
                streamedItemType,
                extensibleEnums);
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
            ResultClassContent: resultClassContent,
            HeaderEnumFiles: headerEnumFiles);
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

            // An informational (1xx) status is never a final response: nothing to read (ATC_API_OPR030).
            if (!isDefault && statusCodeInt < 200)
            {
                continue;
            }

            // A 3xx is no error: its declared body (e.g. 300) is read as declared, and it is never IsSuccess.
            var isRedirection = statusCodeInt is >= 300 and < 400;
            if (isDefault)
            {
                statusCodeStr = "default";
            }

            // Determine content type from response body (headers like Location are metadata, not content)
            string? contentType = null;
            string? textMediaType = null;

            // Error responses: the same resolution the server uses for its factories, so a client generated
            // from a spec parses what a server generated from that spec writes (+json media types included).
            if (!isSuccess && !isRedirection)
            {
                var shape = ErrorResponseShapeResolver.Resolve(response.Value);
                (contentType, textMediaType) = shape.Kind switch
                {
                    ErrorResponseKind.Problem when shape.IsDeclared => ("ProblemDetails", null),
                    ErrorResponseKind.ValidationProblem => ("ValidationProblemDetails", null),
                    ErrorResponseKind.JsonObject or ErrorResponseKind.XmlObject => (GetSchemaTypeName(shape.Schema, openApiDoc, registry, operationId, pathSegment, ErrorResponseShapeResolver.InlineTypeContext(statusCodeStr), inlineSchemas, isAsyncEnumerable: false), null),
                    ErrorResponseKind.JsonString => ("string", null),
                    ErrorResponseKind.Text => ("string", shape.MediaType),
                    _ => (GetErrorContentType(errorResponseFormat, customErrorTypeName, statusCodeStr), (string?)null),
                };

                // A declared XML error model: read by the model's XML mapping (the accessor reads the error text)
                var errorXml = shape.Kind == ErrorResponseKind.XmlObject ? new XmlBody(shape.MediaType!, IsList: false, ListRootName: null) : null;
                responses.Add(new ResponseInfo(statusCodeStr, statusEnumName, propertyName, contentType, isSuccess, textMediaType, ErrorMediaType: shape.MediaType, Xml: errorXml));
                continue;
            }

            // A file download (the primary body is binary - ResponseBodyClassifier): the bytes, content type and file
            // name as a DownloadedFile, read from the response by OkContent
            var isFile = isSuccess && ResponseBodyClassifier.PrimaryBody(response.Value?.Content)?.Kind == ResponseBodyKind.Binary;
            XmlBody? xmlBody = null;
            if (isFile)
            {
                contentType = "DownloadedFile";
            }
            else if (isSuccess && XmlBodyHelper.FindXmlOnlyBody(response.Value?.Content) is { } xml)
            {
                // An XML-only model body: Atc.Rest.Client keeps it (bytes, or text for text/xml), OkContent reads the XML
                contentType = GetSchemaTypeName(response.Value!.Content![xml.MediaType].Schema, openApiDoc, registry, operationId, pathSegment, "Response", inlineSchemas, isAsyncEnumerable: false);
                xmlBody = xml;
            }
            else if (response.Value?.Content is not null &&
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

                // A stream of a schema that is not an array: a sequence of that schema, as the server writes it
                if (isAsyncEnumerable && isSuccess && !contentType.StartsWith("IAsyncEnumerable<", StringComparison.Ordinal))
                {
                    contentType = $"IAsyncEnumerable<{contentType}>";
                }

                // Offered as XML too: read by the response's Content-Type
                if (isSuccess && !isAsyncEnumerable)
                {
                    xmlBody = XmlBodyHelper.FindXmlAlternative(response.Value.Content);
                }
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
            if (!isSuccess && !isRedirection && contentType is null)
            {
                contentType = GetErrorContentType(errorResponseFormat, customErrorTypeName, statusCodeStr);
            }

            // A model or a file (a 200 that declares both): the file is read by {PropertyName}File
            var fileMediaTypes = isSuccess && contentType is not null && statusCodeStr == "200"
                ? ResponseBodyClassifier.FileAlternatives(response.Value?.Content)
                : [];
            responses.Add(new ResponseInfo(statusCodeStr, statusEnumName, propertyName, contentType, isSuccess, textMediaType, IsFile: isFile, Xml: xmlBody, FileMediaTypes: fileMediaTypes.Count > 0 ? fileMediaTypes : null));
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
        bool hasSharedModels,
        bool isDeprecated)
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

        // Add using for result type (in Endpoints.Results namespace) - a binary endpoint has none
        if (!isBinaryEndpoint)
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
        var methodParameters = ExecuteAsyncParameters(operationName, hasParameters, isAsyncEnumerable && !string.IsNullOrEmpty(streamingItemType));

        // Determine return type: StreamBinaryEndpointResponse for binary, the result class otherwise (for a streamed
        // operation it derives from StreamingEndpointResponse<T>)
        var returnTypeName = isBinaryEndpoint
            ? "Task<StreamBinaryEndpointResponse>"
            : $"Task<{operationName}EndpointResult>";

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
            Attributes: EndpointTypeAttributes(isDeprecated),
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
        bool useServersBasePath = true,
        bool preferXml = false)
    {
        var namespaceValue = NamespaceBuilder.ForEndpoints(projectName, pathSegment);
        var sharedModelsNamespace = NamespaceBuilder.ForModels(projectName);
        var segmentModelsNamespace = NamespaceBuilder.ForModels(projectName, pathSegment);

        // Per-operation retry (x-retry-*): tells the API resilience handler how to run this request
        // when it differs from the handler default (its policy for safe methods, no retry for POST/PATCH).
        // A response that is not streamed is also read inside the handler's attempt, so its timeout and retry cover the body.
        var isStreamedResponse = (isAsyncEnumerable && !string.IsNullOrEmpty(streamingItemType)) || isBinaryEndpoint;
        var resilienceStatements = new[]
            {
                ResilienceRequestHelper.BuildStampStatement(operation, pathItem, openApiDoc, httpMethod, "requestMessage"),
                isStreamedResponse ? null : ResilienceRequestHelper.BuildBufferStatement(openApiDoc, "requestMessage"),
            }
            .Where(s => s is not null)
            .ToList();
        var resilienceStamp = resilienceStatements.Count == 0
            ? null
            : string.Join(Environment.NewLine, resilienceStatements);

        // Build header with usings
        var headerBuilder = new StringBuilder();
        headerBuilder.AppendLine("// <auto-generated />");
        headerBuilder.AppendLine("#nullable enable");
        headerBuilder.AppendLine();
        headerBuilder.AppendLine("using System.CodeDom.Compiler;");
        headerBuilder.AppendLine("using System.Collections.Generic;");
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
        headerBuilder.AppendLine("using Microsoft.Extensions.Http;");
        headerBuilder.AppendLine($"using {projectName}.Generated;");
        if (resilienceStamp is not null)
        {
            headerBuilder.AppendLine($"using {projectName}.Generated.Resilience;");
        }

        headerBuilder.AppendLine($"using {NamespaceBuilder.ForEndpointInterfaces(projectName, pathSegment)};");

        // Add using for result types (in Results namespace) - a binary endpoint has none
        if (!isBinaryEndpoint)
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

        var constructor = new ConstructorParameters(
            DocumentationTags: null,
            DeclarationModifier: DeclarationModifiers.Public,
            GenericTypeName: null,
            TypeName: $"{operationName}Endpoint",
            InheritedClassTypeName: null,
            Parameters: constructorParams);

        // Build ExecuteAsync method parameters
        var methodParameters = ExecuteAsyncParameters(operationName, hasParameters, isAsyncEnumerable && !string.IsNullOrEmpty(streamingItemType));

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
            useServersBasePath,
            preferXml);

        // Determine return type: StreamBinaryEndpointResponse for binary, the result class otherwise (for a streamed
        // operation it derives from StreamingEndpointResponse<T>)
        var returnTypeName = isBinaryEndpoint
            ? "Task<StreamBinaryEndpointResponse>"
            : $"Task<{operationName}EndpointResult>";

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
            Attributes: EndpointTypeAttributes(operation.Deprecated),
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

    /// <summary>
    /// The value handed to the request builder: a date, a date-time or a decimal number as its wire text
    /// (the builder would format it with the machine's culture), anything else as it is.
    /// </summary>
    /// <summary>
    /// Whether the endpoint serializes its JSON body itself: a body under a JSON media type other than
    /// <c>application/json</c>, sent with that media type.
    /// </summary>
    /// <summary>
    /// The attributes of an endpoint interface and class: [GeneratedCode], and [Obsolete] for a deprecated operation, so
    /// the code that injects or calls the endpoint is warned.
    /// </summary>
    private static List<AttributeParameters> EndpointTypeAttributes(
        bool isDeprecated)
    {
        var attributes = new List<AttributeParameters>
        {
            new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
        };

        if (isDeprecated)
        {
            attributes.Add(ObsoleteAttributeHelper.Attribute);
        }

        return attributes;
    }

    private static string WireValue(
        OpenApiParameter param,
        string propName)
    {
        // An array whose items are one value (form with explode: false, pipeDelimited, spaceDelimited): the items
        // joined on the declared delimiter, each as the specification writes it - one string, which the builder
        // percent-encodes (a list would be sent as repeated keys)
        if (param.GetArrayDelimiter() is { } delimiter)
        {
            var itemsSchema = (param.Schema is OpenApiSchemaReference reference ? reference.Target : param.Schema)?.Items;
            var itemText = WireValueHelper.IsStringEnum(itemsSchema)
                ? "item.ToWireValue()"
                : WireValueHelper.TryBuildWireExpression("item", itemsSchema) is { } formatted
                    ? formatted + "!"
                    : "global::System.FormattableString.Invariant($\"{item}\")";
            return $"string.Join({CSharpLiteralHelper.ToStringLiteral(delimiter.ToString())}, global::System.Linq.Enumerable.Select(parameters.{propName}, item => {itemText}))";
        }

        return WireValueHelper.IsStringEnum(param.Schema) && !param.Schema!.IsOfType(JsonSchemaType.Array)
            ? $"parameters.{propName}.ToWireValue()"
            : WireValueHelper.TryBuildWireExpression($"parameters.{propName}", param.Schema) ?? $"parameters.{propName}";
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
        bool useServersBasePath = true,
        bool preferXml = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("var client = factory.CreateClient(httpClientName);");
        sb.AppendLine();

        // Get server base path if enabled (e.g., "/api/v1" from servers[0].url)
        var serverBasePath = useServersBasePath ? ServerUrlHelper.GetServersBasePath(openApiDoc) : null;

        // Build URL template with path parameters (optionally prepend server base path)
        var templatePath = serverBasePath is not null ? $"{serverBasePath}{path}" : path;
        var propertyNames = ParameterPropertyNames.For(operation, pathLevelParameters);

        // OAS 3.2 in: querystring is the whole query string, as given (an operation that has one has no query
        // parameters, so the builder adds none after it)
        var queryStringParameter = (pathLevelParameters ?? []).Concat(operation.Parameters ?? [])
            .Select(p => p.Resolve())
            .FirstOrDefault(r => r?.Parameter is { In: ParameterLocation.QueryString });
        if (queryStringParameter?.Parameter is { } queryString)
        {
            var queryStringProperty = propertyNames.Get(queryString, queryStringParameter.ReferenceId);
            sb.AppendLine($"var requestBuilder = httpMessageFactory.FromTemplate(\"{templatePath}\" + (parameters.{queryStringProperty} is {{ Length: > 0 }} queryString ? \"?\" + queryString : string.Empty));");
        }
        else
        {
            sb.AppendLine($"var requestBuilder = httpMessageFactory.FromTemplate(\"{templatePath}\");");
        }

        // Add path parameters
        var allParams = new List<(OpenApiParameter Param, string? ReferenceId)>();
        if (pathLevelParameters is not null)
        {
            foreach (var p in pathLevelParameters)
            {
                var resolved = p.Resolve();

                // A value the client cannot send is left out (ATC_API_OPR037 warned)
                if (resolved.Parameter is not null && !resolved.Parameter.IsUnbindable())
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

                // A value the client cannot send is left out (ATC_API_OPR037 warned)
                if (resolved.Parameter is not null && !resolved.Parameter.IsUnbindable())
                {
                    allParams.Add((resolved.Parameter, resolved.ReferenceId));
                }
            }
        }

        foreach (var (param, referenceId) in allParams.Where(p => p.Param.In == ParameterLocation.Path))
        {
            var propName = propertyNames.Get(param, referenceId);
            sb.AppendLine($"requestBuilder.WithPathParameter({CSharpLiteralHelper.ToStringLiteral(param.Name!)}, {WireValue(param, propName)});");
        }

        // Add query parameters
        foreach (var (param, referenceId) in allParams.Where(p => p.Param.In == ParameterLocation.Query))
        {
            var propName = propertyNames.Get(param, referenceId);

            // An object: each property that is set, by its key - filter[name] (deepObject) or name (form, explode)
            if (param.GetQueryObject() is { } queryObject)
            {
                var local = "queryObject" + propName;
                sb.AppendLine($"if (parameters.{propName} is {{ }} {local})");
                sb.AppendLine("{");
                foreach (var property in queryObject.Properties)
                {
                    var access = $"{local}.{property.WireName.ToPascalCaseForDotNet()}";
                    var schema = (IOpenApiSchema?)property.Schema.WrappedEnum() ?? property.Schema;
                    var value = WireValueHelper.IsStringEnum(schema)
                        ? $"{access}.ToWireValue()"
                        : WireValueHelper.TryBuildWireExpression(access, schema) ?? access;
                    var add = $"requestBuilder.WithQueryParameter({CSharpLiteralHelper.ToStringLiteral(queryObject.KeyOf(property))}, {value});";
                    if (property.Schema.CanBeNullInQueryObject(property.Required))
                    {
                        sb.AppendLine(4, $"if ({access} is not null)");
                        sb.AppendLine(4, "{");
                        sb.AppendLine(8, add);
                        sb.AppendLine(4, "}");
                    }
                    else
                    {
                        sb.AppendLine(4, add);
                    }
                }

                sb.AppendLine("}");
                continue;
            }

            if (param.Required)
            {
                sb.AppendLine($"requestBuilder.WithQueryParameter({CSharpLiteralHelper.ToStringLiteral(param.Name!)}, {WireValue(param, propName)});");
            }
            else
            {
                sb.AppendLine($"if (parameters.{propName} is not null)");
                sb.AppendLine("{");
                sb.AppendLine(4, $"requestBuilder.WithQueryParameter({CSharpLiteralHelper.ToStringLiteral(param.Name!)}, {WireValue(param, propName)});");
                sb.AppendLine("}");
            }
        }

        // Add header parameters
        foreach (var (param, referenceId) in allParams.Where(p => p.Param.In == ParameterLocation.Header))
        {
            var propName = propertyNames.Get(param, referenceId);
            var headerName = CSharpLiteralHelper.ToStringLiteral(param.Name!);

            if (param.Required)
            {
                sb.AppendLine($"requestBuilder.WithHeaderParameter({headerName}, {WireValue(param, propName)});");
            }
            else
            {
                sb.AppendLine($"if (parameters.{propName} is not null)");
                sb.AppendLine("{");
                sb.AppendLine(4, $"requestBuilder.WithHeaderParameter({headerName}, {WireValue(param, propName)});");
                sb.AppendLine("}");
            }
        }

        // Cookie parameters: one RFC 6265 Cookie header, each value percent-encoded (the server decodes it)
        var cookieParams = allParams.Where(p => p.Param.In == ParameterLocation.Cookie).ToList();
        if (cookieParams.Count > 0)
        {
            sb.AppendLine("var cookieParts = new List<string>();");
            foreach (var (param, referenceId) in cookieParams)
            {
                var propName = propertyNames.Get(param, referenceId);
                var isBoolean = (param.Schema is OpenApiSchemaReference { Target: { } target } ? target : param.Schema)?.IsOfType(JsonSchemaType.Boolean) == true;
                var text = isBoolean
                    ? $"(parameters.{propName} == true ? \"true\" : \"false\")"
                    : $"global::System.Convert.ToString({WireValue(param, propName)}, global::System.Globalization.CultureInfo.InvariantCulture)!";
                var add = $"cookieParts.Add(\"{param.Name}=\" + {(param.Style == ParameterStyle.Cookie ? text : $"Uri.EscapeDataString({text})")});";
                if (param.Required)
                {
                    sb.AppendLine(add);
                }
                else
                {
                    sb.AppendLine($"if (parameters.{propName} is not null)");
                    sb.AppendLine("{");
                    sb.AppendLine(4, add);
                    sb.AppendLine("}");
                }

                sb.AppendLine();
            }

            sb.AppendLine("if (cookieParts.Count > 0)");
            sb.AppendLine("{");
            sb.AppendLine(4, "requestBuilder.WithHeaderParameter(\"Cookie\", string.Join(\"; \", cookieParts));");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // An XML answer: ask for its media type (the request builder asks for application/json by default, under
        // the key "accept"), and for the errors as they are read: the declared error media types, else a ProblemDetails
        // (an Accept without it lets a host decline to write the problem, e.g. for an empty-body status)
        // A model or a file: ask for the file media types too
        if (XmlBodyHelper.AcceptFor(responses.Where(r => r.IsSuccess && r.Xml is not null).Select(r => r.Xml!).ToList(), XmlBodyHelper.ErrorMediaTypes(operation), preferXml, responses.FirstOrDefault(r => r.FileMediaTypes is not null)?.FileMediaTypes) is { } accept)
        {
            sb.AppendLine($"requestBuilder.WithHeaderParameter(\"accept\", \"{accept}\");");
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
                if (OpenApiOperationExtensions.IsRawFileUploadContentType(contentType))
                {
                    // A raw file body (octet-stream, PDF, PNG, an Office document, ...): the file's bytes, as the one media
                    // type the operation declares - else as the file's own of the several it declares
                    sb.AppendLine($"requestBuilder.WithBinaryBody(parameters.File!.OpenReadStream(), {RawFileBodyHelper.ContentTypeExpression(operation, "parameters.File", contentType!)});");
                }
                else if (operation.RequestBody.Content[contentType!].Schema is { } multipartSchema)
                {
                    // multipart/form-data (the declared key, with any parameter, in any case): a model's fields, or the
                    // raw file(s) - never a stream through WithBody, which serializes it as JSON
                    var (isFile, isCollection) = multipartSchema.GetFileUploadInfo();
                    if (isFile && isCollection)
                    {
                        // Array of files - iterate and add each with WithFile using IFileContent
                        sb.AppendLine("if (parameters.File is not null)");
                        sb.AppendLine("{");
                        sb.AppendLine(4, "for (var i = 0; i < parameters.File.Length; i++)");
                        sb.AppendLine(4, "{");
                        sb.AppendLine(8, "requestBuilder.WithFile(parameters.File[i].OpenReadStream(), \"files\", parameters.File[i].FileName, parameters.File[i].ContentType);");
                        sb.AppendLine(4, "}");
                        sb.AppendLine("}");
                    }
                    else if (isFile)
                    {
                        // Single file - use WithFile with IFileContent metadata
                        sb.AppendLine("requestBuilder.WithFile(parameters.File!.OpenReadStream(), \"file\", parameters.File.FileName, parameters.File.ContentType);");
                    }
                    else
                    {
                        // A model - WithFile/WithFormField calls for each property
                        GenerateMultipartFormDataCode(sb, multipartSchema, operation.RequestBody.Content[contentType!], openApiDoc);
                    }
                }
            }
            else if (operation.GetJsonRequestBodyMediaType() is null &&
                     operation.GetFormBodyMediaType() is { } formMediaType &&
                     FormBodyHelper.ResolveModelSchema(operation.RequestBody.Content[formMediaType].Schema, openApiDoc) is { } formSchema)
            {
                // A URL-encoded form: the model's fields, each as its wire text, as the request builder's URL-encoded
                // form - which is sent without a field too
                sb.AppendLine("var fields = new List<KeyValuePair<string, string>>();");
                FormBodyHelper.AppendUrlEncodedFields(sb, formSchema, "parameters.Request", "fields");
                sb.AppendLine("requestBuilder.WithUrlEncodedForm(fields);");
            }
            else if (XmlBodyHelper.ClientRequestBody(operation, preferXml) is { } xmlRequestBody)
            {
                // An XML body (XML-only, or with preferXml the XML alternative): the model's XML with its media type
                var bytes = XmlBodyHelper.ToUtf8BytesExpression(XmlBodyHelper.ModelTypeName(operation.RequestBody.Content, xmlRequestBody), xmlRequestBody, "parameters.Request");
                sb.AppendLine($"requestBuilder.WithBinaryBody(new global::System.IO.MemoryStream({bytes}), \"{xmlRequestBody.MediaType}; charset=utf-8\");");
            }
            else if (operation.GetJsonRequestBodyMediaType() is { } jsonMediaType && !string.Equals(jsonMediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                // A JSON body under another JSON media type (merge-patch+json, vnd.x+json): serialized by the request
                // builder and sent with the declared media type
                sb.AppendLine($"requestBuilder.WithBody(parameters.Request, {CSharpLiteralHelper.ToStringLiteral(jsonMediaType)});");
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
        // A streamed download (x-return-stream) hands the open content stream to the caller, so the response is not
        // disposed here either (StreamBinaryEndpointResponse disposes the stream).
        if ((isAsyncEnumerable && !string.IsNullOrEmpty(streamingItemType)) || isBinaryEndpoint)
        {
            sb.AppendLine("var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);");
        }
        else
        {
            sb.AppendLine("using var response = await client.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);");
        }

        sb.AppendLine();

        // Build response
        sb.AppendLine("var responseBuilder = httpMessageFactory.FromResponse(response);");

        if (isBinaryEndpoint)
        {
            // A streamed download: the open content stream on success; on an error the body is read as text (typed as
            // the registered error in ErrorContentObject), so the response is no longer needed and is disposed - the
            // builder does not own it. A build that throws disposes it too.
            AppendResponseRegistrations(sb, responses, defaultResponse);
            sb.AppendLine("try");
            sb.AppendLine("{");
            sb.AppendLine(4, "var streamed = await responseBuilder.BuildStreamBinaryResponseAsync(cancellationToken).ConfigureAwait(false);");
            sb.AppendLine(4, "if (!streamed.IsSuccess)");
            sb.AppendLine(4, "{");
            sb.AppendLine(8, "response.Dispose();");
            sb.AppendLine(4, "}");
            sb.AppendLine();
            sb.AppendLine(4, "return streamed;");
            sb.AppendLine("}");
            sb.AppendLine("catch");
            sb.AppendLine("{");
            sb.AppendLine(4, "response.Dispose();");
            sb.AppendLine(4, "throw;");
            sb.Append('}');
        }
        else if (isAsyncEnumerable && !string.IsNullOrEmpty(streamingItemType))
        {
            // A streamed response: Atc.Rest.Client reads the items in the operation's framing as they arrive, an
            // error body as the type registered for its status, and the headers. The result owns the response - the
            // builder disposes it when no result can be built (a multipart answer without a boundary).
            AppendResponseRegistrations(sb, responses.Where(r => !r.IsSuccess).ToList(), defaultResponse);

            // x-sse-item-events: the named events of an event stream that are items too
            var itemEvents = operation.GetStreamingFraming() == StreamingFraming.ServerSentEvents
                ? operation.GetServerSentEventItemNames()
                : [];

            // The caller's options for reading the stream; the item events of the specification apply unless the
            // caller names its own
            var readOptions = "streamReadOptions";
            if (itemEvents.Count > 0)
            {
                sb.AppendLine("var readOptions = new StreamReadOptions");
                sb.AppendLine("{");
                sb.AppendLine(4, "MaxRecordLength = streamReadOptions?.MaxRecordLength,");
                sb.AppendLine(4, $"ItemEventNames = streamReadOptions?.ItemEventNames ?? new[] {{ {string.Join(", ", itemEvents.Select(CSharpLiteralHelper.ToStringLiteral))} }},");
                sb.AppendLine(4, "OnEvent = streamReadOptions?.OnEvent,");
                sb.AppendLine("};");
                readOptions = "readOptions";
            }

            sb.Append($"return await responseBuilder.BuildStreamingEndpointResponseAsync<{streamingItemType}, {operationName}EndpointResult>(x => new {operationName}EndpointResult(x), StreamFraming.{operation.GetStreamingFraming()}, {readOptions}, cancellationToken).ConfigureAwait(false);");
        }
        else
        {
            AppendResponseRegistrations(sb, responses, defaultResponse);

            // An operation with an XML answer may get its errors as application/problem+xml: read into the typed problem
            sb.Append(responses.Any(r => r.Xml is not null)
                ? $"return await responseBuilder.BuildResponseAsync(x => new {operationName}EndpointResult(ProblemDetailsFactory.WithXmlProblemBody(x)), cancellationToken).ConfigureAwait(false);"
                : $"return await responseBuilder.BuildResponseAsync(x => new {operationName}EndpointResult(x), cancellationToken).ConfigureAwait(false);");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Registers a success response's body type on the response builder.
    /// </summary>
    private static void AppendSuccessRegistration(
        StringBuilder sb,
        ResponseInfo responseInfo)
    {
        if (responseInfo.Xml is { IsAlternative: true })
        {
            // Offered as JSON and XML: the JSON type, unless the response is XML (read by the accessor)
            sb.AppendLine("if (XmlModelSupport.IsXmlMediaType(response.Content.Headers.ContentType?.MediaType))");
            sb.AppendLine("{");
            sb.AppendLine(4, $"responseBuilder.AddSuccessResponse({responseInfo.StatusCodeExpression});");
            sb.AppendLine("}");
            sb.AppendLine("else");
            sb.AppendLine("{");
            sb.AppendLine(4, $"responseBuilder.AddSuccessResponse<{responseInfo.ContentType}>({responseInfo.StatusCodeExpression});");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        else if (responseInfo.IsFile || responseInfo.Xml is not null)
        {
            // A file or an XML body - no JSON type: Atc.Rest.Client keeps a non-text body as bytes (text/xml as
            // text), and the accessor reads the file or the XML
            sb.AppendLine($"responseBuilder.AddSuccessResponse({responseInfo.StatusCodeExpression});");
        }
        else if (responseInfo.TextMediaType is not null)
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

    /// <summary>
    /// Registers the operation's responses on the response builder: the success types, and the error types a typed
    /// error body is read as (<c>BuildResponseAsync</c>'s error content, a streamed download's <c>ErrorContentObject</c>).
    /// </summary>
    private static void AppendResponseRegistrations(
        StringBuilder sb,
        List<ResponseInfo> responses,
        ResponseInfo? defaultResponse)
    {
        // A generator-added status that the default response shares: the framework's ProblemDetails is
        // sent as application/problem+json, the handler's default body is not.
        var sharedWithDefault = SharedWithDefault(responses, defaultResponse);
        var splitByMediaType = SplitsByMediaType(responses, defaultResponse);

        foreach (var responseInfo in responses)
        {
            if (sharedWithDefault.Contains(responseInfo))
            {
                continue;
            }

            if (!responseInfo.IsSuccess && responseInfo.ContentType == "string")
            {
                // A string error status whose handler sent no body gets the host wiring's ProblemDetails
                // (application/problem+json): read it as one, so the string accessor gives its message, not its JSON
                // text. Any other body is the text.
                sb.AppendLine(responseInfo.TextMediaType is not null
                    ? $"responseBuilder.AddErrorTextResponse({responseInfo.StatusCodeExpression});"
                    : $"responseBuilder.AddErrorResponse<string>({responseInfo.StatusCodeExpression});");
                sb.AppendLine($"responseBuilder.AddErrorResponse<ProblemDetails>({responseInfo.StatusCodeExpression}, \"application/problem+json\");");
                continue;
            }

            if (responseInfo.IsSuccess)
            {
                if (responseInfo.FileMediaTypes is not null)
                {
                    // A model or a file: told apart by the answer's Content-Type - a file is kept as bytes
                    sb.AppendLine("if (DownloadedFile.IsFileMediaType(response.Content.Headers.ContentType?.MediaType))");
                    sb.AppendLine("{");
                    sb.AppendLine(4, $"responseBuilder.AddSuccessResponse({responseInfo.StatusCodeExpression});");
                    sb.AppendLine("}");
                    sb.AppendLine("else");
                    sb.AppendLine("{");
                    var model = new StringBuilder();
                    AppendSuccessRegistration(model, responseInfo);
                    foreach (var line in model.ToString().TrimEnd().Split('\n'))
                    {
                        var text = line.TrimEnd('\r');
                        if (text.Length == 0)
                        {
                            sb.AppendLine();
                        }
                        else
                        {
                            sb.AppendLine(4, text);
                        }
                    }

                    sb.AppendLine("}");
                    sb.AppendLine();
                }
                else
                {
                    AppendSuccessRegistration(sb, responseInfo);
                }
            }
            else if (responseInfo.ContentType is null)
            {
                // A 3xx without a body (304, a redirect that was not followed): nothing to read. Registered as that
                // where the operation has a default, which would otherwise take the status for one of its own.
                if (defaultResponse is not null)
                {
                    sb.AppendLine($"responseBuilder.AddErrorResponse({responseInfo.StatusCodeExpression});");
                }
            }
            else if (responseInfo.TextMediaType is not null)
            {
                // Raw text error body — same bypass for the error path.
                sb.AppendLine($"responseBuilder.AddErrorTextResponse({responseInfo.StatusCodeExpression});");
            }
            else if (responseInfo.Xml is not null)
            {
                // A declared XML error model - no JSON type: the accessor reads the error text by the model's XML mapping
                sb.AppendLine($"responseBuilder.AddErrorResponse({responseInfo.StatusCodeExpression});");
            }
            else
            {
                // Error responses always have a type (ProblemDetails is the fallback). A declared ProblemDetails 400
                // is read as its ValidationProblemDetails subtype: the ValidationFilter's field errors stay typed.
                var registeredType = responseInfo.StatusCode == "400" && responseInfo.ContentType == "ProblemDetails"
                    ? "ValidationProblemDetails"
                    : responseInfo.ContentType;
                sb.AppendLine($"responseBuilder.AddErrorResponse<{registeredType}>({responseInfo.StatusCodeExpression});");
            }
        }

        if (splitByMediaType)
        {
            // A status the default shares: its own type only for the framework's problem; any other body of it is the
            // default's
            foreach (var responseInfo in sharedWithDefault)
            {
                sb.AppendLine($"responseBuilder.AddErrorResponse<{responseInfo.ContentType}>({responseInfo.StatusCodeExpression}, \"application/problem+json\");");
            }
        }

        // `default`: any other error status is read as the default body, through the configured serializer -
        // Atc.Rest.Client's default registration, which applies to every error status without one of its own.
        // A declared XML model has no type the serializer reads: the default without a type keeps the text, and the
        // result reads the model from it.
        if (defaultResponse is not null)
        {
            sb.AppendLine(defaultResponse.TextMediaType is not null
                ? "responseBuilder.AddDefaultErrorTextResponse();"
                : defaultResponse.Xml is not null
                    ? "responseBuilder.AddDefaultErrorResponse();"
                    : $"responseBuilder.AddDefaultErrorResponse<{defaultResponse.ContentType}>();");
        }
    }

    private static string GenerateResultInterface(
        string projectName,
        string pathSegment,
        string operationName,
        string description,
        List<ResponseInfo> responses,
        ResponseInfo? defaultResponse,
        bool hasSegmentModels,
        bool hasSharedModels,
        List<DeclaredResponseHeader> declaredHeaders,
        string? streamedItemType,
        bool isEventStream)
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
        sb.AppendLine($"/// Description: {CSharpLiteralHelper.ToXmlDocText(description)}");
        sb.AppendLine($"/// Operation: {operationName}.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine(streamedItemType is null
            ? $"public interface I{operationName}EndpointResult : IEndpointResponse, IEndpointResult"
            : $"public interface I{operationName}EndpointResult : IStreamingEndpointResponse<{streamedItemType}>, IEndpointResult");
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

        // A model or a file: the file
        foreach (var response in responses.Where(r => r.FileMediaTypes is not null))
        {
            sb.AppendLine(4, $"bool Is{response.PropertyName}File {{ get; }}");
            sb.AppendLine();
            sb.AppendLine(4, $"DownloadedFile {response.PropertyName}File {{ get; }}");
            sb.AppendLine();
        }

        if (defaultResponse?.ContentType is not null)
        {
            sb.AppendLine(4, $"{defaultResponse.ContentType} {defaultResponse.PropertyName}Content {{ get; }}");
            sb.AppendLine();
        }

        // Problem / ValidationProblem: from IEndpointResult, which every result interface shares

        // The headers the responses declare
        foreach (var property in ResponseHeaderFactoryHelper.ClientInterfaceProperties(declaredHeaders))
        {
            sb.AppendLine(4, property);
            sb.AppendLine();
        }

        // An event stream: the id of the last event read so far (StreamingEndpointResponse<T> has it), what a caller
        // sends as Last-Event-ID to continue the stream after it
        if (isEventStream)
        {
            sb.AppendLine(4, "/// <summary>");
            sb.AppendLine(4, "/// The id of the last server-sent event read so far - what to send as Last-Event-ID to continue the stream");
            sb.AppendLine(4, "/// after it. Null when no event has had an id; empty when the server has reset it.");
            sb.AppendLine(4, "/// </summary>");
            sb.AppendLine(4, "string? LastEventId { get; }");
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
        string? customErrorTypeName,
        List<DeclaredResponseHeader> declaredHeaders,
        string? streamedItemType,
        bool extensibleEnums)
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
        sb.AppendLine($"/// Description: {CSharpLiteralHelper.ToXmlDocText(description)}");
        sb.AppendLine($"/// Operation: {operationName}.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        var baseType = streamedItemType is null ? "EndpointResponse" : $"StreamingEndpointResponse<{streamedItemType}>";
        sb.AppendLine($"public partial class {operationName}EndpointResult : {baseType}, I{operationName}EndpointResult");
        sb.AppendLine("{");
        sb.AppendLine(4, $"public {operationName}EndpointResult({baseType} response)");
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

            if (streamedItemType is not null && response.IsSuccess)
            {
                // A streamed success: the items it yields
                sb.AppendLine(4, $"public static {operationName}EndpointResult {response.PropertyName}(IAsyncEnumerable<{streamedItemType}?> items)");
                sb.AppendLine(8, $"=> Create({response.StatusCodeExpression}, contentObject: null, items);");
            }
            else if (response.ContentType is null)
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

        foreach (var response in responses.Where(r => r.FileMediaTypes is not null))
        {
            sb.AppendLine(4, "/// <summary>");
            sb.AppendLine(4, $"/// Creates a result representing HTTP {response.StatusEnumName} answered with a file.");
            sb.AppendLine(4, "/// </summary>");
            sb.AppendLine(4, $"public static {operationName}EndpointResult {response.PropertyName}(DownloadedFile file)");
            sb.AppendLine(8, "=> new(new EndpointResponse(");
            sb.AppendLine(12, "isSuccess: true,");
            sb.AppendLine(12, $"statusCode: {response.StatusCodeExpression},");
            sb.AppendLine(12, "content: string.Empty,");
            sb.AppendLine(12, "contentObject: file.Content,");
            sb.AppendLine(12, "headers: new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine(12, "{");
            sb.AppendLine(16, $"[\"Content-Type\"] = new[] {{ file.ContentType ?? \"{response.FileMediaTypes![0]}\" }},");
            sb.AppendLine(16, "[\"Content-Disposition\"] = file.FileName is null ? Array.Empty<string>() : new[] { new System.Net.Http.Headers.ContentDispositionHeaderValue(\"attachment\") { FileNameStar = file.FileName }.ToString() },");
            sb.AppendLine(12, "}));");
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

        if (streamedItemType is null)
        {
            sb.AppendLine(4, $"private static {operationName}EndpointResult Create(");
            sb.AppendLine(8, "HttpStatusCode statusCode,");
            sb.AppendLine(8, "object? contentObject)");
            sb.AppendLine(8, "=> new(new EndpointResponse(");
            sb.AppendLine(12, "isSuccess: (int)statusCode is >= 200 and < 300,");
            sb.AppendLine(12, "statusCode: statusCode,");
            sb.AppendLine(12, "content: string.Empty,");
            sb.AppendLine(12, "contentObject: contentObject,");
            sb.AppendLine(12, "headers: new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase)));");
        }
        else
        {
            sb.AppendLine(4, $"private static {operationName}EndpointResult Create(");
            sb.AppendLine(8, "HttpStatusCode statusCode,");
            sb.AppendLine(8, "object? contentObject,");
            sb.AppendLine(8, $"IAsyncEnumerable<{streamedItemType}?>? items = null)");
            sb.AppendLine(8, $"=> new(new StreamingEndpointResponse<{streamedItemType}>(");
            sb.AppendLine(12, "isSuccess: (int)statusCode is >= 200 and < 300,");
            sb.AppendLine(12, "statusCode: statusCode,");
            sb.AppendLine(12, "content: items,");
            sb.AppendLine(12, "errorContent: contentObject as string,");
            sb.AppendLine(12, "httpResponse: null,");
            sb.AppendLine(12, "errorContentObject: contentObject,");
            sb.AppendLine(12, "headers: new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase)));");
            sb.AppendLine();

            // The accessors below read what an EndpointResponse calls Content and ContentObject: the error text, and the
            // error read as the registered type - or, as there, the text when it could not be read as it
            sb.AppendLine(4, "private string ErrorText");
            sb.AppendLine(8, "=> ErrorContent ?? string.Empty;");
            sb.AppendLine();
            sb.AppendLine(4, "private object? ErrorObject");
            sb.AppendLine(8, "=> ErrorContentObject ?? ErrorContent;");
        }

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
            // A declared XML model: the body reads as the model (a proxy's page does not)
            var readGuard = defaultResponse.ContentType is null or "ProblemDetails" or "ValidationProblemDetails"
                ? null
                : defaultResponse.Xml is not null
                    ? $"XmlModelSupport.TryRead(Content, {XmlBodyHelper.ReadMethod(defaultResponse.ContentType, defaultResponse.Xml)}, out _)"
                    : $"ContentObject is {defaultResponse.ContentType}";

            // The guard also tells a shared status's default body from the framework's ProblemDetails (the media type
            // split registers the default type only for a body that is not application/problem+json).
            var condition = DefaultErrorCondition(responses, defaultResponse, "IsSuccess", "StatusCode", sharedStatusExpression: null);

            sb.AppendLine(4, $"public bool Is{defaultResponse.PropertyName}");
            sb.AppendLine(8, readGuard is null ? $"=> {condition};" : $"=> {condition} && {readGuard};");
            sb.AppendLine();
        }

        // A model or a file: the file, told apart by the answer's Content-Type (the model is {PropertyName}Content)
        foreach (var response in responses.Where(r => r.FileMediaTypes is not null))
        {
            sb.AppendLine(4, $"public bool Is{response.PropertyName}File");
            sb.AppendLine(8, $"=> Is{response.PropertyName} && DownloadedFile.IsFileResponse(this);");
            sb.AppendLine();
            sb.AppendLine(4, $"public DownloadedFile {response.PropertyName}File");
            sb.AppendLine(8, $"=> Is{response.PropertyName}File");
            sb.AppendLine(12, "? DownloadedFile.FromResponse(this)");
            sb.AppendLine(12, $": throw InvalidContentAccessException<DownloadedFile>({response.StatusCodeExpression}, \"{response.PropertyName}File\");");
            sb.AppendLine();
        }

        // {PropertyName}Content properties - only generate for responses that have content
        foreach (var response in responses.Where(r => r.ContentType is not null))
        {
            // A model or a file: the model only when the answer is not the file
            var isModel = response.FileMediaTypes is null ? $"Is{response.PropertyName}" : $"Is{response.PropertyName} && !Is{response.PropertyName}File";
            if (response.IsFile)
            {
                sb.AppendLine(4, $"public {response.ContentType} {response.PropertyName}Content");
                sb.AppendLine(8, $"=> Is{response.PropertyName}");
                sb.AppendLine(12, "? DownloadedFile.FromResponse(this)");
                sb.AppendLine(12, $": throw InvalidContentAccessException<{response.ContentType}>({response.StatusCodeExpression}, \"{response.PropertyName}Content\");");
                sb.AppendLine();
                continue;
            }

            if (response.Xml is { IsAlternative: true })
            {
                // Offered as JSON and XML: the JSON model, else the model read from the XML body
                sb.AppendLine(4, $"public {response.ContentType} {response.PropertyName}Content");
                sb.AppendLine(8, $"=> Is{response.PropertyName} && ContentObject is {response.ContentType} result");
                sb.AppendLine(12, "? result");
                sb.AppendLine(12, $": {isModel}");
                sb.AppendLine(16, $"? {XmlBodyHelper.ReadMethod(response.ContentType!, response.Xml)}(XmlModelSupport.ParseContent(ContentObject ?? Content))");
                sb.AppendLine(16, $": throw InvalidContentAccessException<{response.ContentType}>({response.StatusCodeExpression}, \"{response.PropertyName}Content\");");
                sb.AppendLine();
                continue;
            }

            if (response.Xml is not null)
            {
                // The model read from the XML body: the bytes (ContentObject), or for text/xml the text (Content)
                sb.AppendLine(4, $"public {response.ContentType} {response.PropertyName}Content");
                sb.AppendLine(8, $"=> {isModel}");
                sb.AppendLine(12, $"? {XmlBodyHelper.ReadMethod(response.ContentType!, response.Xml)}(XmlModelSupport.ParseContent(ContentObject ?? Content))");
                sb.AppendLine(12, $": throw InvalidContentAccessException<{response.ContentType}>({response.StatusCodeExpression}, \"{response.PropertyName}Content\");");
                sb.AppendLine();
                continue;
            }

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

        // A 400 read as a model that cannot hold field errors (a declared model, or the custom one): the generated
        // ValidationFilter still writes a ValidationProblemDetails, so its errors are read from the raw body.
        if (responses.Find(r => r.StatusCode == "400") is { ContentType: not (null or "string" or "ProblemDetails" or "ValidationProblemDetails") } badRequest)
        {
            sb.AppendLine(4, $"public System.Collections.Generic.Dictionary<string, string[]>? {badRequest.PropertyName}ValidationErrors");
            sb.AppendLine(8, $"=> Is{badRequest.PropertyName} ? ProblemDetailsFactory.ValidationErrorsFromContent(Content) : null;");
            sb.AppendLine();
        }

        if (defaultResponse is { ContentType: { } xmlDefaultType, Xml: { } defaultXml })
        {
            // A declared XML model: read from the error text by the model's XML mapping
            sb.AppendLine(4, $"public {xmlDefaultType} {defaultResponse.PropertyName}Content");
            sb.AppendLine(8, $"=> Is{defaultResponse.PropertyName}");
            sb.AppendLine(12, $"? {XmlBodyHelper.ReadMethod(xmlDefaultType, defaultXml)}(XmlModelSupport.ParseContent(Content))");
            sb.AppendLine(12, $": throw InvalidContentAccessException<{xmlDefaultType}>(StatusCode, \"{defaultResponse.PropertyName}Content\");");
            sb.AppendLine();
        }
        else if (defaultResponse?.ContentType is { } defaultType)
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

        // The reason for any refusal, whatever its status: what the per-status accessors read, for every status
        sb.AppendLine(4, "public ProblemDetails? Problem");
        sb.AppendLine(8, "=> IsSuccess");
        sb.AppendLine(12, "? null");
        sb.AppendLine(12, ": ContentObject switch");
        sb.AppendLine(12, "{");
        sb.AppendLine(16, "ProblemDetails problem => problem,");
        sb.AppendLine(16, "string message => ProblemDetailsFactory.Create(StatusCode, message),");
        sb.AppendLine(16, "null => ProblemDetailsFactory.ValidationErrorsFromContent(Content) is { Count: > 0 }");
        sb.AppendLine(20, "? ProblemDetailsFactory.ValidationFromContent(StatusCode, Content)");
        sb.AppendLine(20, ": ProblemDetailsFactory.FromContent(StatusCode, Content),");
        sb.AppendLine(16, "_ => null,");
        sb.AppendLine(12, "};");
        sb.AppendLine();
        sb.AppendLine(4, "public ValidationProblemDetails? ValidationProblem");
        sb.AppendLine(8, "=> Problem as ValidationProblemDetails;");
        sb.AppendLine();

        // The headers the responses declare
        foreach (var line in ResponseHeaderFactoryHelper.ClientProperties(declaredHeaders, extensibleEnums))
        {
            if (line.Length == 0)
            {
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine(4, line);
            }
        }

        if (declaredHeaders.Count > 0)
        {
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

        return streamedItemType is null
            ? content + "}\n"
            : StreamedResultText(content + "}\n");
    }

    /// <summary>
    /// The result class of a streamed operation, from the text written for a response read whole: the base class is
    /// <c>StreamingEndpointResponse&lt;T&gt;</c>, where the error body is <c>ErrorContentObject</c> and its text
    /// <c>ErrorContent</c> (<c>Content</c> is the items) - read through the class's <c>ErrorObject</c> and
    /// <c>ErrorText</c> - and the exception of a wrong access is not generic.
    /// </summary>
    private static string StreamedResultText(string text)
    {
        var timeout = TimeSpan.FromSeconds(5);
        text = Regex.Replace(text, @"(?<![\w.])ContentObject\b", "ErrorObject", RegexOptions.None, timeout);
        text = Regex.Replace(text, @"(?<![\w.""])Content\b(?![""-])", "ErrorText", RegexOptions.None, timeout);
        return Regex.Replace(text, @"InvalidContentAccessException<.*?>\(", "InvalidContentAccessException(", RegexOptions.None, timeout);
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
                resolvedSchema is OpenApiSchema arraySchema && arraySchema.IsOfType(JsonSchemaType.Array))
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
                var allOfType = PaginationTypeResolver.GetAllOfSchemaTypeName(actualSchema.AllOf, openApiDoc, registry, item => item.Type.ToPrimitiveCSharpTypeName(item.Format) ?? "object");
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
    /// client decides by the response media type. That holds for every <c>errorResponseFormat</c>: an added status
    /// is a ProblemDetails, a ValidationProblemDetails or - with Custom - the custom model, and a body that is not
    /// <c>application/problem+json</c> is the declared default's either way. A <c>ValidationProblemDetails</c> under
    /// a <c>ProblemDetails</c> default is not shared: it is the richer ProblemDetails, so the 400 stays a
    /// <c>ValidationProblemDetails</c>.
    /// </summary>
    private static List<ResponseInfo> SharedWithDefault(
        List<ResponseInfo> responses,
        ResponseInfo? defaultResponse)
        => defaultResponse is null
            ? []
            : responses
                .Where(r => r.IsAutoAdded &&
                            r.ContentType is not null &&
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
    /// The parameters of the generated <c>ExecuteAsync</c> - the operation's parameters record when it has one, the
    /// HTTP client name and the cancellation token - the same on the endpoint interface and its class. A streamed
    /// operation takes the options for reading its stream as well, last and optional: the limit on one record, and
    /// for an event stream what is told per event (its id, its name, the retry time the server asks for).
    /// </summary>
    private static List<ParameterBaseParameters> ExecuteAsyncParameters(
        string operationName,
        bool hasParameters,
        bool isStreamed)
    {
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

        if (isStreamed)
        {
            methodParameters.Add(new ParameterBaseParameters(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: "StreamReadOptions",
                IsNullableType: true,
                IsReferenceType: true,
                Name: "streamReadOptions",
                DefaultValue: "null"));
        }

        return methodParameters;
    }

    /// <summary>
    /// Generates code for multipart/form-data requests with schema-based content.
    /// Maps schema properties to WithFile() for binary properties, WithFormJsonField() for an object, a map or a
    /// composition, and WithFormField() for others.
    /// </summary>
    private static void GenerateMultipartFormDataCode(
        StringBuilder sb,
        IOpenApiSchema modelSchema,
        IOpenApiMediaType multipart,
        OpenApiDocument openApiDoc)
    {
        // A model without properties, or one the document does not have: the whole model as the body
        var schema = FormBodyHelper.ResolveModelSchema(modelSchema, openApiDoc);
        if (schema?.Properties is not { Count: > 0 })
        {
            sb.AppendLine("requestBuilder.WithBody(parameters.Request);");
            return;
        }

        // A file through WithFile (with its metadata), a text field through WithFormField, an object through
        // WithFormJsonField (an application/json part written by the request builder's serializer, as a JSON body is)
        FormBodyHelper.AppendMultipartFields(
            sb,
            schema,
            multipart,
            "parameters.Request",
            new MultipartPartWriter(
                File: (b, indent, name, file) => b.AppendLine(indent, $"requestBuilder.WithFile({file}.OpenReadStream(), {CSharpLiteralHelper.ToStringLiteral(name)}, {file}.FileName, {file}.ContentType);"),
                Text: (b, indent, name, text) => b.AppendLine(indent, $"requestBuilder.WithFormField({CSharpLiteralHelper.ToStringLiteral(name)}, {text});"),
                Json: (b, indent, name, value) => b.AppendLine(indent, $"requestBuilder.WithFormJsonField({CSharpLiteralHelper.ToStringLiteral(name)}, {value});")));
    }
}