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
        if (operation.Responses == null)
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

            if (response.Value?.Content != null &&
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
        bool useServersBasePath = true)
    {
        var result = new List<OperationFiles>();
        var inlineSchemas = new Dictionary<string, InlineSchemaInfo>(StringComparer.Ordinal);

        if (openApiDoc.Paths == null || openApiDoc.Paths.Count == 0)
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

            if (pathItemInterface is not OpenApiPathItem pathItem)
            {
                continue;
            }

            if (pathItem.Operations == null)
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

                if (operationValue == null)
                {
                    continue;
                }

                var files = ExtractOperationFiles(
                    projectName,
                    pathSegment,
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

                if (files != null)
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
        OpenApiPathItem pathItem,
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

        // Check if this operation returns an async enumerable (streaming response)
        var isAsyncEnumerable = operation.IsAsyncEnumerableOperation();

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

        // Auto-apply responses based on detected features (same as server-side .Produces() metadata)
        // This ensures client is prepared to handle all responses the server may return
        var definedCodes = new HashSet<string>(responses.Select(r => r.StatusCode), StringComparer.Ordinal);
        var features = Helpers.OperationFeaturesHelper.DetectOperationFeatures(operation, pathItem, openApiDoc, httpMethod);
        var errorContentType = string.IsNullOrEmpty(customErrorTypeName) ? "ProblemDetails" : customErrorTypeName;

        // 400 - Has parameters but no 400 defined
        if (features.HasParameters && !definedCodes.Contains("400"))
        {
            var validationContentType = string.IsNullOrEmpty(customErrorTypeName) ? "ValidationProblemDetails" : customErrorTypeName;
            responses.Add(new ResponseInfo("400", "BadRequest", "BadRequest", validationContentType, false));
        }

        // 401 - Has security but no 401 defined
        if (features.HasSecurity && !definedCodes.Contains("401"))
        {
            responses.Add(new ResponseInfo("401", "Unauthorized", "Unauthorized", errorContentType, false));
        }

        // 403 - Has roles/policies but no 403 defined
        if (features.HasRolesOrPolicies && !definedCodes.Contains("403"))
        {
            responses.Add(new ResponseInfo("403", "Forbidden", "Forbidden", errorContentType, false));
        }

        // 404 - GET/PUT/DELETE/PATCH with path params but no 404 defined
        if (features.HasPathParameters &&
            features.HttpMethod is "GET" or "PUT" or "DELETE" or "PATCH" &&
            !definedCodes.Contains("404"))
        {
            responses.Add(new ResponseInfo("404", "NotFound", "NotFound", errorContentType, false));
        }

        // 409 - POST/PUT but no 409 defined
        if (features.HttpMethod is "POST" or "PUT" && !definedCodes.Contains("409"))
        {
            responses.Add(new ResponseInfo("409", "Conflict", "Conflict", errorContentType, false));
        }

        // 429 - Has rate limiting but no 429 defined
        if (features.HasRateLimiting && !definedCodes.Contains("429"))
        {
            responses.Add(new ResponseInfo("429", "TooManyRequests", "TooManyRequests", errorContentType, false));
        }

        // 500 - Always add if not defined (global error handler)
        if (!definedCodes.Contains("500") && !definedCodes.Contains("default"))
        {
            responses.Add(new ResponseInfo("500", "InternalServerError", "InternalServerError", errorContentType, false));
        }

        // Check if operation has parameters
        var hasQueryRouteParams = operation.Parameters is { Count: > 0 } || pathLevelParameters is { Count: > 0 };
        var hasRequestBody = operation.RequestBody is { Content: not null };
        var hasParameters = hasQueryRouteParams || hasRequestBody;

        // Extract streaming item type for IAsyncEnumerable endpoints (e.g., "Account" from "IAsyncEnumerable<Account>")
        var streamingItemType = isAsyncEnumerable ? ExtractStreamingItemType(responses) : null;

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
            pathLevelParameters,
            openApiDoc,
            hasParameters,
            httpClientName,
            responses,
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
                hasSegmentModels,
                hasSharedModels);
            resultInterfaceFileName = $"{projectName}.{pathSegment}.Endpoints.Interfaces.I{operationName}EndpointResult.g.cs";

            // Generate result class
            resultClassContent = GenerateResultClass(
                projectName,
                pathSegment,
                operationName,
                description,
                responses,
                hasSegmentModels,
                hasSharedModels);
            resultClassFileName = $"{projectName}.{pathSegment}.Endpoints.Results.{operationName}EndpointResult.g.cs";
        }

        return new OperationFiles(
            OperationName: operationName,
            PathSegment: pathSegment,
            EndpointInterfaceFileName: $"{projectName}.{pathSegment}.Endpoints.Interfaces.I{operationName}Endpoint.g.cs",
            EndpointInterfaceContent: endpointInterfaceContent,
            EndpointClassFileName: $"{projectName}.{pathSegment}.Endpoints.{operationName}Endpoint.g.cs",
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
    private sealed record ResponseInfo(string StatusCode, string StatusEnumName, string PropertyName, string? ContentType, bool IsSuccess);

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

        if (operation.Responses == null)
        {
            return responses;
        }

        foreach (var response in operation.Responses)
        {
            var statusCodeStr = response.Key;
            if (!int.TryParse(statusCodeStr, out var statusCodeInt))
            {
                continue;
            }

            var statusCode = (System.Net.HttpStatusCode)statusCodeInt;
            var statusEnumName = statusCode.ToString();
            var propertyName = NormalizeStatusName(statusEnumName);
            var isSuccess = statusCodeInt >= 200 && statusCodeInt < 300;

            // Determine content type from response body (headers like Location are metadata, not content)
            string? contentType = null;

            // Check for JSON content
            // Note: Binary/octet-stream endpoints are handled by IsBinaryEndpoint() and skip result class generation
            if (response.Value?.Content != null &&
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

            // Determine error content type based on ErrorResponseFormatType
            if (!isSuccess && contentType == null)
            {
                contentType = GetErrorContentType(errorResponseFormat, customErrorTypeName, statusCodeStr);
            }

            responses.Add(new ResponseInfo(statusCodeStr, statusEnumName, propertyName, contentType, isSuccess));
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
        var namespaceValue = $"{projectName}.Generated.{pathSegment}.Endpoints.Interfaces";

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
            headerBuilder.AppendLine($"using {projectName}.Generated.{pathSegment}.Client;");
        }

        // Add using for result type (in Endpoints.Results namespace) - only for non-binary and non-streaming endpoints
        if (!isBinaryEndpoint && !isAsyncEnumerable)
        {
            headerBuilder.AppendLine($"using {projectName}.Generated.{pathSegment}.Endpoints.Results;");
        }

        // For streaming endpoints, add model usings for the item type
        if (isAsyncEnumerable)
        {
            if (hasSharedModels)
            {
                headerBuilder.AppendLine($"using {projectName}.Generated.Models;");
            }

            if (hasSegmentModels)
            {
                headerBuilder.AppendLine($"using {projectName}.Generated.{pathSegment}.Models;");
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
            DefaultValue: httpClientName));

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
        IList<IOpenApiParameter>? pathLevelParameters,
        OpenApiDocument openApiDoc,
        bool hasParameters,
        string httpClientName,
        List<ResponseInfo> responses,
        bool isBinaryEndpoint,
        bool isAsyncEnumerable,
        string? streamingItemType,
        bool hasSegmentModels,
        bool hasSharedModels,
        bool useServersBasePath = true)
    {
        var namespaceValue = $"{projectName}.Generated.{pathSegment}.Endpoints";

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
        headerBuilder.AppendLine($"using {projectName}.Generated.{pathSegment}.Endpoints.Interfaces;");

        // Add using for result types (in Results namespace) - only for non-binary and non-streaming endpoints
        if (!isBinaryEndpoint && !isAsyncEnumerable)
        {
            headerBuilder.AppendLine($"using {projectName}.Generated.{pathSegment}.Endpoints.Results;");
        }

        // Add Models usings for non-binary endpoints (they use ProblemDetails etc.) or streaming endpoints (for item type)
        if (!isBinaryEndpoint || isAsyncEnumerable)
        {
            // Add shared Models using if there are shared models
            if (hasSharedModels)
            {
                headerBuilder.AppendLine($"using {projectName}.Generated.Models;");
            }

            // Add segment-specific Models using if there are segment-specific models
            if (hasSegmentModels)
            {
                headerBuilder.AppendLine($"using {projectName}.Generated.{pathSegment}.Models;");
            }
        }

        if (hasParameters)
        {
            headerBuilder.AppendLine($"using {projectName}.Generated.{pathSegment}.Client;");
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
            DefaultValue: httpClientName));

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
            isBinaryEndpoint,
            isAsyncEnumerable,
            streamingItemType,
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
        bool isBinaryEndpoint,
        bool isAsyncEnumerable,
        string? streamingItemType,
        bool useServersBasePath = true)
    {
        var sb = new StringBuilder();
        sb.AppendLine("var client = factory.CreateClient(httpClientName);");
        sb.AppendLine();

        // Get server base path if enabled (e.g., "/api/v1" from servers[0].url)
        var serverBasePath = useServersBasePath ? ServerUrlHelper.GetServersBasePath(openApiDoc) : null;

        // Build URL template with path parameters (optionally prepend server base path)
        var templatePath = serverBasePath != null ? $"{serverBasePath}{path}" : path;
        sb.AppendLine($"var requestBuilder = httpMessageFactory.FromTemplate(\"{templatePath}\");");

        // Add path parameters
        var allParams = new List<(OpenApiParameter Param, string? ReferenceId)>();
        if (pathLevelParameters != null)
        {
            foreach (var p in pathLevelParameters)
            {
                var resolved = p.Resolve();
                if (resolved.Parameter != null)
                {
                    allParams.Add((resolved.Parameter, resolved.ReferenceId));
                }
            }
        }

        if (operation.Parameters != null)
        {
            foreach (var p in operation.Parameters)
            {
                var resolved = p.Resolve();
                if (resolved.Parameter != null)
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
                sb.AppendLine($"if (parameters.{propName} != null)");
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
                sb.AppendLine($"if (parameters.{propName} != null)");
                sb.AppendLine("{");
                sb.AppendLine(4, $"requestBuilder.WithHeaderParameter(\"{headerName}\", parameters.{propName});");
                sb.AppendLine("}");
            }
        }

        // Add request body
        if (operation.RequestBody?.Content != null)
        {
            // Check if this is a file upload
            var isFileUpload = operation.HasFileUpload();
            if (isFileUpload)
            {
                // For file uploads, check if it's single or multiple files
                var contentType = operation.GetFileUploadContentType();
                if (contentType is "application/octet-stream")
                {
                    // Single file upload via octet-stream - use WithBinaryBody for raw stream upload
                    sb.AppendLine("requestBuilder.WithBinaryBody(parameters.File);");
                }
                else if (contentType is "multipart/form-data")
                {
                    // Check if it's a schema-based upload or raw file(s)
                    if (!operation.RequestBody.Content.TryGetValue("multipart/form-data", out var mediaType))
                    {
                        sb.AppendLine("requestBuilder.WithBody(parameters.File);");
                    }
                    else if (mediaType.Schema is OpenApiSchemaReference schemaRef)
                    {
                        // Schema-based multipart - generate WithFile/WithFormField calls for each property
                        GenerateMultipartFormDataCode(sb, schemaRef, openApiDoc);
                    }
                    else if (mediaType.Schema != null)
                    {
                        // Raw file(s) multipart - check if single or array
                        var (isFile, isCollection) = mediaType.Schema.GetFileUploadInfo();
                        if (isFile && isCollection)
                        {
                            // Array of files - iterate and add each with WithFile
                            sb.AppendLine("if (parameters.File != null)");
                            sb.AppendLine("{");
                            sb.AppendLine(4, "for (var i = 0; i < parameters.File.Length; i++)");
                            sb.AppendLine(4, "{");
                            sb.AppendLine(8, "requestBuilder.WithFile(parameters.File[i], \"files\", $\"file{i}\");");
                            sb.AppendLine(4, "}");
                            sb.AppendLine("}");
                        }
                        else if (isFile)
                        {
                            // Single file - use WithFile
                            sb.AppendLine("requestBuilder.WithFile(parameters.File, \"file\", \"file\");");
                        }
                        else
                        {
                            // Fallback
                            sb.AppendLine("requestBuilder.WithBody(parameters.File);");
                        }
                    }
                    else
                    {
                        // No schema - fallback
                        sb.AppendLine("requestBuilder.WithBody(parameters.File);");
                    }
                }
                else
                {
                    // Other file upload content types
                    sb.AppendLine("requestBuilder.WithBody(parameters.File);");
                }
            }
            else
            {
                // Regular JSON body
                sb.AppendLine("requestBuilder.WithBody(parameters.Request);");
            }
        }

        sb.AppendLine();

        // Build and send request - Convert HTTP method to PascalCase for System.Net.Http.HttpMethod (e.g., GET → Get, POST → Post)
        var httpMethodPascal = char.ToUpperInvariant(
            httpMethod[0]) + httpMethod
            .Substring(1)
            .ToLowerInvariant();
        sb.AppendLine($"using var requestMessage = requestBuilder.Build(HttpMethod.{httpMethodPascal});");

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

        // Build response
        sb.AppendLine("var responseBuilder = httpMessageFactory.FromResponse(response);");

        if (isBinaryEndpoint)
        {
            // For binary endpoints, use BuildBinaryResponseAsync directly
            sb.Append("return await responseBuilder.BuildBinaryResponseAsync(cancellationToken);");
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
            foreach (var responseInfo in responses)
            {
                if (responseInfo.IsSuccess)
                {
                    if (responseInfo.ContentType == null)
                    {
                        // Empty response - use non-generic AddSuccessResponse
                        sb.AppendLine($"responseBuilder.AddSuccessResponse(HttpStatusCode.{responseInfo.StatusEnumName});");
                    }
                    else
                    {
                        sb.AppendLine($"responseBuilder.AddSuccessResponse<{responseInfo.ContentType}>(HttpStatusCode.{responseInfo.StatusEnumName});");
                    }
                }
                else
                {
                    // Error responses always have a type (ProblemDetails is the fallback)
                    sb.AppendLine($"responseBuilder.AddErrorResponse<{responseInfo.ContentType}>(HttpStatusCode.{responseInfo.StatusEnumName});");
                }
            }

            sb.Append($"return await responseBuilder.BuildResponseAsync(x => new {operationName}EndpointResult(x), cancellationToken);");
        }

        return sb.ToString();
    }

    private static string GenerateResultInterface(
        string projectName,
        string pathSegment,
        string operationName,
        string description,
        List<ResponseInfo> responses,
        bool hasSegmentModels,
        bool hasSharedModels)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using Atc.Rest.Client;");
        sb.AppendLine($"using {projectName}.Generated;");

        // Add shared Models using if there are shared models
        if (hasSharedModels)
        {
            sb.AppendLine($"using {projectName}.Generated.Models;");
        }

        // Add segment-specific Models using if there are segment-specific models
        if (hasSegmentModels)
        {
            sb.AppendLine($"using {projectName}.Generated.{pathSegment}.Models;");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Generated.{pathSegment}.Endpoints.Interfaces;");
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
            // Skip IsOk - already provided by IEndpointResponse base interface
            if (response.PropertyName == "Ok")
            {
                continue;
            }

            sb.AppendLine(4, $"bool Is{response.PropertyName} {{ get; }}");
            sb.AppendLine();
        }

        // Only generate content properties for responses that have content
        foreach (var response in responses.Where(r => r.ContentType != null))
        {
            sb.AppendLine(4, $"{response.ContentType} {response.PropertyName}Content {{ get; }}");
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
        bool hasSegmentModels,
        bool hasSharedModels)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using System.Net;");
        sb.AppendLine("using Atc.Rest.Client;");
        sb.AppendLine($"using {projectName}.Generated;");
        sb.AppendLine($"using {projectName}.Generated.{pathSegment}.Endpoints.Interfaces;");

        // Add shared Models using if there are shared models
        if (hasSharedModels)
        {
            sb.AppendLine($"using {projectName}.Generated.Models;");
        }

        // Add segment-specific Models using if there are segment-specific models
        if (hasSegmentModels)
        {
            sb.AppendLine($"using {projectName}.Generated.{pathSegment}.Models;");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Generated.{pathSegment}.Endpoints.Results;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Client Endpoint result.");
        sb.AppendLine($"/// Description: {description}");
        sb.AppendLine($"/// Operation: {operationName}.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"public sealed class {operationName}EndpointResult : EndpointResponse, I{operationName}EndpointResult");
        sb.AppendLine("{");
        sb.AppendLine(4, $"public {operationName}EndpointResult(EndpointResponse response)");
        sb.AppendLine(8, ": base(response)");
        sb.AppendLine(4, "{");
        sb.AppendLine(4, "}");
        sb.AppendLine();

        // Is{PropertyName} properties
        foreach (var response in responses)
        {
            // Skip IsOk - already provided by EndpointResponse base class
            if (response.PropertyName == "Ok")
            {
                continue;
            }

            sb.AppendLine(4, $"public bool Is{response.PropertyName}");
            sb.AppendLine(8, $"=> StatusCode == HttpStatusCode.{response.StatusEnumName};");
            sb.AppendLine();
        }

        // {PropertyName}Content properties - only generate for responses that have content
        foreach (var response in responses.Where(r => r.ContentType != null))
        {
            sb.AppendLine(4, $"public {response.ContentType} {response.PropertyName}Content");
            sb.AppendLine(8, $"=> Is{response.PropertyName} && ContentObject is {response.ContentType} result");
            sb.AppendLine(12, "? result");

            // For 401/403 with ProblemDetails, add fallback for plain text responses
            if ((response.StatusCode == "401" || response.StatusCode == "403") && response.ContentType == "ProblemDetails")
            {
                sb.AppendLine(12, $": Is{response.PropertyName} && ContentObject is string message");
                sb.AppendLine(16, $"? ProblemDetailsFactory.Create(HttpStatusCode.{response.StatusEnumName}, message)");
                sb.AppendLine(16, $": throw InvalidContentAccessException<{response.ContentType}>(HttpStatusCode.{response.StatusEnumName}, \"{response.PropertyName}Content\");");
            }
            else
            {
                sb.AppendLine(12, $": throw InvalidContentAccessException<{response.ContentType}>(HttpStatusCode.{response.StatusEnumName}, \"{response.PropertyName}Content\");");
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

            if (openApiDoc.Components?.Schemas != null &&
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
                !string.IsNullOrEmpty(pathSegment) &&
                !string.IsNullOrEmpty(context) &&
                inlineSchemas != null)
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
        if (schema.Items == null)
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
        if (baseType != null && HttpClientExtractor.IsPaginationBaseType(baseType) && itemType != null)
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
        if (arraySchema.Items == null)
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
    /// Extracts the item type from an IAsyncEnumerable type string.
    /// For example, "IAsyncEnumerable&lt;Account&gt;" returns "Account".
    /// </summary>
    private static string? ExtractStreamingItemType(
        List<ResponseInfo> responses)
    {
        // Find the success response with IAsyncEnumerable content type
        var successResponse = responses.FirstOrDefault(r => r.IsSuccess && r.ContentType != null);
        if (successResponse?.ContentType == null)
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

        if (openApiDoc.Components?.Schemas == null ||
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
                // Single file property - use WithFile
                sb.AppendLine($"if (parameters.Request?.{pascalPropName} != null)");
                sb.AppendLine("{");
                sb.AppendLine(4, $"requestBuilder.WithFile(parameters.Request.{pascalPropName}, \"{propName}\", \"{propName}\");");
                sb.AppendLine("}");
                sb.AppendLine();
            }
            else if (isArrayOfBinary)
            {
                // Array of files - use WithFiles
                sb.AppendLine($"if (parameters.Request?.{pascalPropName} != null)");
                sb.AppendLine("{");
                sb.AppendLine(4, $"foreach (var (stream, index) in parameters.Request.{pascalPropName}.Select((s, i) => (s, i)))");
                sb.AppendLine(4, "{");
                sb.AppendLine(8, $"requestBuilder.WithFile(stream, \"{propName}\", $\"{propName}_{{index}}\");");
                sb.AppendLine(4, "}");
                sb.AppendLine("}");
                sb.AppendLine();
            }
            else if (isArray)
            {
                // Array of non-binary values - serialize as JSON or comma-separated
                sb.AppendLine($"if (parameters.Request?.{pascalPropName} != null)");
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
                sb.AppendLine($"if (parameters.Request?.{pascalPropName} != null)");
                sb.AppendLine("{");
                sb.AppendLine(4, $"requestBuilder.WithFormField(\"{propName}\", parameters.Request.{pascalPropName}.ToString()!);");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }
    }
}