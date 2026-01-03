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
    /// <param name="customErrorTypeName">Optional custom error type name (replaces ProblemDetails).</param>
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
    /// <param name="customErrorTypeName">Optional custom error type name (replaces ProblemDetails).</param>
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

        var httpClientName = customHttpClientName ?? $"{projectName}-ApiClient";

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

        // Extract response information from OpenAPI spec
        var responses = ExtractResponses(
            operation,
            openApiDoc,
            registry,
            customErrorTypeName,
            operationId,
            pathSegment,
            inlineSchemas);

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

        // Generate endpoint interface
        var endpointInterfaceContent = GenerateEndpointInterface(
            projectName,
            pathSegment,
            operationName,
            description,
            hasParameters,
            httpClientName,
            isBinaryEndpoint,
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
            hasSegmentModels,
            hasSharedModels,
            useServersBasePath);

        // For binary endpoints, skip generating result interface/class (use BinaryEndpointResponse directly)
        string? resultInterfaceFileName = null;
        string? resultInterfaceContent = null;
        string? resultClassFileName = null;
        string? resultClassContent = null;

        if (!isBinaryEndpoint)
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
        string? customErrorTypeName,
        string? operationId,
        string? pathSegment,
        Dictionary<string, InlineSchemaInfo>? inlineSchemas)
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
                contentType = GetSchemaTypeName(
                    mediaType.Schema,
                    openApiDoc,
                    registry,
                    operationId,
                    pathSegment,
                    isSuccess ? "Response" : null, // Only generate inline types for success responses
                    inlineSchemas);
            }

            // Use custom error type if provided, otherwise use ValidationProblemDetails for 400 Bad Request, ProblemDetails for other errors
            if (!isSuccess && contentType == null)
            {
                if (!string.IsNullOrEmpty(customErrorTypeName))
                {
                    contentType = customErrorTypeName;
                }
                else
                {
                    contentType = statusCodeStr == "400"
                        ? "ValidationProblemDetails"
                        : "ProblemDetails";
                }
            }

            responses.Add(new ResponseInfo(statusCodeStr, statusEnumName, propertyName, contentType, isSuccess));
        }

        return responses;
    }

    private static string GenerateEndpointInterface(
        string projectName,
        string pathSegment,
        string operationName,
        string description,
        bool hasParameters,
        string httpClientName,
        bool isBinaryEndpoint,
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

        if (isBinaryEndpoint)
        {
            // Add using for BinaryEndpointResponse
            headerBuilder.AppendLine("using Atc.Rest.Client;");
        }

        if (hasParameters)
        {
            headerBuilder.AppendLine($"using {projectName}.Generated.{pathSegment}.Client;");
        }

        // Add using for result type (in Endpoints.Results namespace) - only for non-binary endpoints
        if (!isBinaryEndpoint)
        {
            headerBuilder.AppendLine($"using {projectName}.Generated.{pathSegment}.Endpoints.Results;");
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

        // Determine return type: BinaryEndpointResponse for binary endpoints, custom result class otherwise
        var returnTypeName = isBinaryEndpoint
            ? "Task<BinaryEndpointResponse>"
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

        if (isBinaryEndpoint)
        {
            // Add using for BinaryEndpointResponse
            headerBuilder.AppendLine("using Atc.Rest.Client;");
        }

        headerBuilder.AppendLine("using Atc.Rest.Client.Builder;");
        headerBuilder.AppendLine("using Microsoft.Extensions.Http;");
        headerBuilder.AppendLine($"using {projectName}.Generated;");
        headerBuilder.AppendLine($"using {projectName}.Generated.{pathSegment}.Endpoints.Interfaces;");

        // Add using for result types (in Results namespace) - only for non-binary endpoints
        if (!isBinaryEndpoint)
        {
            headerBuilder.AppendLine($"using {projectName}.Generated.{pathSegment}.Endpoints.Results;");
        }

        // Only add Models using for non-binary endpoints (they use ProblemDetails etc.)
        if (!isBinaryEndpoint)
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
            useServersBasePath);

        // Determine return type: BinaryEndpointResponse for binary endpoints, custom result class otherwise
        var returnTypeName = isBinaryEndpoint
            ? "Task<BinaryEndpointResponse>"
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
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicClass,
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
                    // Single file upload via octet-stream
                    sb.AppendLine("requestBuilder.WithBody(parameters.File);");
                }
                else if (contentType is "multipart/form-data")
                {
                    // Check if it's a schema-based upload or raw file(s)
                    var schemaRef = operation.RequestBody.Content.TryGetValue("multipart/form-data", out var mediaType)
                        ? mediaType.Schema as OpenApiSchemaReference
                        : null;
                    if (schemaRef != null)
                    {
                        // Schema-based multipart - use full object (properties mapped to form fields)
                        sb.AppendLine("requestBuilder.WithBody(parameters);");
                    }
                    else
                    {
                        // Raw file(s) multipart
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
        sb.AppendLine("using var response = await client.SendAsync(requestMessage, cancellationToken);");
        sb.AppendLine();

        // Build response
        sb.AppendLine("var responseBuilder = httpMessageFactory.FromResponse(response);");

        if (isBinaryEndpoint)
        {
            // For binary endpoints, use BuildBinaryResponseAsync directly
            sb.Append("return await responseBuilder.BuildBinaryResponseAsync(cancellationToken);");
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
        sb.AppendLine($"public class {operationName}EndpointResult : EndpointResponse, I{operationName}EndpointResult");
        sb.AppendLine("{");
        sb.AppendLine(4, $"public {operationName}EndpointResult(EndpointResponse response)");
        sb.AppendLine(8, ": base(response)");
        sb.AppendLine(4, "{");
        sb.AppendLine(4, "}");
        sb.AppendLine();

        // Is{PropertyName} properties
        foreach (var response in responses)
        {
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
            sb.AppendLine(12, $": throw InvalidContentAccessException<{response.ContentType}>(HttpStatusCode.{response.StatusEnumName}, \"{response.PropertyName}Content\");");
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

    private static string GetSchemaTypeName(
        IOpenApiSchema? schema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        string? operationId,
        string? pathSegment,
        string? context,
        Dictionary<string, InlineSchemaInfo>? inlineSchemas)
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
                return GetArraySchemaType(arraySchema, openApiDoc, registry, operationId, pathSegment, inlineSchemas);
            }

            return OpenApiSchemaExtensions.ResolveTypeName(refId!, registry);
        }

        if (schema is OpenApiSchema actualSchema)
        {
            // Handle array type specially (GetPrimitiveCSharpTypeName returns null for arrays)
            if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true)
            {
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
        Dictionary<string, InlineSchemaInfo>? inlineSchemas)
    {
        if (schema.Items == null)
        {
            return "object[]";
        }

        // For array items, use "ResponseItem" context to distinguish from direct response objects
        var itemType = GetSchemaTypeName(
            schema.Items,
            openApiDoc,
            registry,
            operationId,
            pathSegment,
            "ResponseItem",
            inlineSchemas);

        return $"IEnumerable<{itemType}>";
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
}