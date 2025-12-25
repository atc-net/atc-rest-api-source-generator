namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI paths and operations and converts them to ClassParameters for endpoint registration class generation.
/// Also provides extraction for endpoint definition mapping extension methods.
/// </summary>
public static class EndpointRegistrationExtractor
{
    /// <summary>
    /// Maps HTTP status code strings to their ASP.NET Core StatusCodes constant names.
    /// </summary>
    private static readonly Dictionary<string, string> HttpStatusCodeMap = new(StringComparer.Ordinal)
    {
        // 1xx Informational
        ["100"] = "Status100Continue",
        ["101"] = "Status101SwitchingProtocols",
        ["102"] = "Status102Processing",
        ["103"] = "Status103EarlyHints",

        // 2xx Success
        ["200"] = "Status200OK",
        ["201"] = "Status201Created",
        ["202"] = "Status202Accepted",
        ["203"] = "Status203NonAuthoritative",
        ["204"] = "Status204NoContent",
        ["205"] = "Status205ResetContent",
        ["206"] = "Status206PartialContent",
        ["207"] = "Status207MultiStatus",
        ["208"] = "Status208AlreadyReported",
        ["226"] = "Status226IMUsed",

        // 3xx Redirection
        ["300"] = "Status300MultipleChoices",
        ["301"] = "Status301MovedPermanently",
        ["302"] = "Status302Found",
        ["303"] = "Status303SeeOther",
        ["304"] = "Status304NotModified",
        ["305"] = "Status305UseProxy",
        ["306"] = "Status306SwitchProxy",
        ["307"] = "Status307TemporaryRedirect",
        ["308"] = "Status308PermanentRedirect",

        // 4xx Client Error
        ["400"] = "Status400BadRequest",
        ["401"] = "Status401Unauthorized",
        ["402"] = "Status402PaymentRequired",
        ["403"] = "Status403Forbidden",
        ["404"] = "Status404NotFound",
        ["405"] = "Status405MethodNotAllowed",
        ["406"] = "Status406NotAcceptable",
        ["407"] = "Status407ProxyAuthenticationRequired",
        ["408"] = "Status408RequestTimeout",
        ["409"] = "Status409Conflict",
        ["410"] = "Status410Gone",
        ["411"] = "Status411LengthRequired",
        ["412"] = "Status412PreconditionFailed",
        ["413"] = "Status413PayloadTooLarge",
        ["414"] = "Status414UriTooLong",
        ["415"] = "Status415UnsupportedMediaType",
        ["416"] = "Status416RangeNotSatisfiable",
        ["417"] = "Status417ExpectationFailed",
        ["418"] = "Status418ImATeapot",
        ["421"] = "Status421MisdirectedRequest",
        ["422"] = "Status422UnprocessableEntity",
        ["423"] = "Status423Locked",
        ["424"] = "Status424FailedDependency",
        ["425"] = "Status425TooEarly",
        ["426"] = "Status426UpgradeRequired",
        ["428"] = "Status428PreconditionRequired",
        ["429"] = "Status429TooManyRequests",
        ["431"] = "Status431RequestHeaderFieldsTooLarge",
        ["451"] = "Status451UnavailableForLegalReasons",

        // 5xx Server Error
        ["500"] = "Status500InternalServerError",
        ["501"] = "Status501NotImplemented",
        ["502"] = "Status502BadGateway",
        ["503"] = "Status503ServiceUnavailable",
        ["504"] = "Status504GatewayTimeout",
        ["505"] = "Status505HttpVersionNotsupported",
        ["506"] = "Status506VariantAlsoNegotiates",
        ["507"] = "Status507InsufficientStorage",
        ["508"] = "Status508LoopDetected",
        ["510"] = "Status510NotExtended",
        ["511"] = "Status511NetworkAuthenticationRequired",
    };

    /// <summary>
    /// Extracts endpoint definition mapping extension class parameters from endpoint definition class names.
    /// Generates an extension method on WebApplication that calls DefineEndpoints on all endpoint definitions.
    /// </summary>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="endpointDefinitionClassNames">List of endpoint definition class names (e.g., "PetEndpointDefinition").</param>
    /// <returns>ClassParameters for the endpoint mapping extension class.</returns>
    public static ClassParameters? ExtractEndpointMappingExtension(
        string projectName,
        List<string> endpointDefinitionClassNames)
        => ExtractEndpointMappingExtension(projectName, pathSegment: null, endpointDefinitionClassNames);

    /// <summary>
    /// Extracts endpoint definition mapping extension class parameters from endpoint definition class names.
    /// Generates an extension method on WebApplication that calls DefineEndpoints on all endpoint definitions.
    /// </summary>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="pathSegment">The path segment for namespace (e.g., "Pets"). If null, uses root namespace.</param>
    /// <param name="endpointDefinitionClassNames">List of endpoint definition class names (e.g., "PetEndpointDefinition").</param>
    /// <returns>ClassParameters for the endpoint mapping extension class.</returns>
    public static ClassParameters? ExtractEndpointMappingExtension(
        string projectName,
        string? pathSegment,
        List<string> endpointDefinitionClassNames)
    {
        if (endpointDefinitionClassNames == null || endpointDefinitionClassNames.Count == 0)
        {
            return null;
        }

        var namespaceValue = NamespaceBuilder.ForEndpoints(projectName, pathSegment);

        var methodName = string.IsNullOrEmpty(pathSegment)
            ? "MapApiEndpoints"
            : $"Map{pathSegment}Endpoints";

        // Build method content
        var methodContent = GenerateEndpointMappingMethodContent(endpointDefinitionClassNames);

        // Build method parameters
        var (methodParams, _) = MethodParameterBuilder.BuildWebApplicationExtensionParameters();

        var methodDocParams = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "app", "The web application." },
        };

        var method = new MethodParameters(
            DocumentationTags: new CodeDocumentationTags(
                summary: "Maps all API endpoints from the generated endpoint definitions.",
                parameters: methodDocParams,
                remark: null,
                code: null,
                example: null,
                exceptions: null,
                @return: "The web application for method chaining."),
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: "WebApplication",
            Name: methodName,
            Parameters: methodParams,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: methodContent);

        return new ClassParameters(
            HeaderContent: null,
            Namespace: namespaceValue,
            DocumentationTags: new CodeDocumentationTags("Extension methods for mapping API endpoints."),
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicStaticClass,
            ClassTypeName: "EndpointDefinitionExtensions",
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties: null,
            Methods: new List<MethodParameters> { method },
            GenerateToStringMethod: false);
    }

    private static string GenerateEndpointMappingMethodContent(
        List<string> endpointDefinitionClassNames)
    {
        var builder = new StringBuilder();

        // Generate instantiation and DefineEndpoints call for each endpoint definition (sorted alphabetically)
        foreach (var className in endpointDefinitionClassNames.OrderBy(x => x, StringComparer.Ordinal))
        {
            builder.AppendLine($"new {className}().DefineEndpoints(app);");
        }

        builder.AppendLine();
        builder.Append("return app;");

        return builder.ToString();
    }

    /// <summary>
    /// Extracts endpoint registration class parameters from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <returns>ClassParameters for the endpoint registration class, or null if no paths exist.</returns>
    public static ClassParameters? Extract(
        OpenApiDocument openApiDoc,
        string projectName)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (openApiDoc.Paths == null || openApiDoc.Paths.Count == 0)
        {
            return null;
        }

        // Generate method content
        var methodContent = GenerateMethodContent(openApiDoc);

        // Build method parameters
        var methodParams = new List<ParameterBaseParameters>
        {
            new(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: "this IEndpointRouteBuilder",
                IsNullableType: false,
                IsReferenceType: true,
                Name: "endpoints",
                DefaultValue: null),
        };

        var method = new MethodParameters(
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: "IEndpointRouteBuilder",
            Name: $"Map{projectName}Endpoints",
            Parameters: methodParams,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: methodContent);

        var headerContent = $@"// <auto-generated />
#nullable enable

using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using {projectName}.Generated.Handlers;
using {projectName}.Generated.Models;
using {projectName}.Generated.Parameters;
";

        return new ClassParameters(
            HeaderContent: headerContent,
            Namespace: NamespaceBuilder.ForEndpoints(projectName),
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStaticClass,
            ClassTypeName: "EndpointRegistration",
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties: null,
            Methods: new List<MethodParameters> { method },
            GenerateToStringMethod: false);
    }

    private static string GenerateMethodContent(OpenApiDocument openApiDoc)
    {
        var builder = new StringBuilder();
        var isFirst = true;

        foreach (var path in openApiDoc.Paths)
        {
            var pathKey = path.Key;
            var pathItemInterface = path.Value;

            if (pathItemInterface is not OpenApiPathItem pathItem)
            {
                continue;
            }

            if (pathItem.Operations != null)
            {
                foreach (var operation in pathItem.Operations)
                {
                    var httpMethod = operation
                        .Key
                        .ToString()
                        .ToUpperInvariant();
                    GenerateEndpointMapping(builder, openApiDoc, pathItem, pathKey, httpMethod, operation.Value, isFirst);
                    isFirst = false;
                }
            }
        }

        builder.AppendLine();
        builder.Append("return endpoints;");

        return builder.ToString();
    }

    private static void GenerateEndpointMapping(
        StringBuilder builder,
        OpenApiDocument openApiDoc,
        OpenApiPathItem pathItem,
        string path,
        string httpMethod,
        OpenApiOperation? operation,
        bool isFirst)
    {
        if (operation == null)
        {
            return;
        }

        var normalizedPath = path
            .Replace("/", "_")
            .Replace("{", string.Empty)
            .Replace("}", string.Empty);
        var operationId = operation.OperationId ?? $"{httpMethod}{normalizedPath}";
        var handlerName = $"I{operationId.ToPascalCaseForDotNet()}Handler";

        // Convert HTTP method to Pascal case (e.g., GET -> Get, POST -> Post)
        var methodName = char.ToUpperInvariant(
            httpMethod[0]) + httpMethod
            .Substring(1)
            .ToLowerInvariant();

        // Add blank line before each endpoint except the first one
        if (!isFirst)
        {
            builder.AppendLine();
        }

        // Generate parameters for the lambda
        var parameters = new List<string>();
        var hasParameters = operation.Parameters is { Count: > 0 };
        var hasRequestBody = operation.RequestBody is { Content: not null } &&
                             operation.RequestBody.Content.ContainsKey("application/json");

        // Add Parameters class if operation has parameters OR request body
        if (hasParameters || hasRequestBody)
        {
            // Use [AsParameters] for parameter DTO
            var parameterClassName = $"{operationId.ToPascalCaseForDotNet()}Parameters";
            parameters.Add($"[AsParameters] {parameterClassName} parameters");
        }

        // Add handler and cancellation token
        parameters.Add($"{handlerName} handler");
        parameters.Add("CancellationToken cancellationToken");

        // Build handler call parameters
        var handlerParams = new List<string>();
        if (hasParameters || hasRequestBody)
        {
            handlerParams.Add("parameters");
        }

        handlerParams.Add("cancellationToken");

        // Start endpoint mapping with expanded formatting
        builder.AppendLine("endpoints");
        builder.AppendLine(4, $".Map{methodName}(");
        builder.AppendLine(8, $"\"{path}\",");
        builder.AppendLine(8, "async (");

        // Output each parameter on its own line
        for (var i = 0; i < parameters.Count; i++)
        {
            var separator = i < parameters.Count - 1 ? "," : string.Empty;
            builder.AppendLine(12, $"{parameters[i]}{separator}");
        }

        builder.AppendLine(8, ") =>");
        builder.AppendLine(8, "{");

        // Build the handler call with parameters on separate lines
        builder.AppendLine(12, "return await handler.ExecuteAsync(");
        for (var i = 0; i < handlerParams.Count; i++)
        {
            var separator = i < handlerParams.Count - 1 ? "," : ");";
            builder.AppendLine(16, $"{handlerParams[i]}{separator}");
        }

        builder.AppendLine(8, "})");

        // Add operation metadata with proper indentation
        var hasSummary = !string.IsNullOrEmpty(operation.Summary);
        var hasOperationId = !string.IsNullOrEmpty(operation.OperationId);
        var hasTags = operation.Tags is { Count: > 0 };

        if (hasSummary)
        {
            builder.AppendLine(4, $".WithSummary(\"{operation.Summary!.Replace("\"", "\\\"")}\")");
        }

        if (hasOperationId)
        {
            builder.AppendLine(4, $".WithName(\"{operation.OperationId}\")");
        }

        if (hasTags)
        {
            var tags = string.Join("\", \"", operation.Tags!.Select(t => t.Name));
            builder.AppendLine(4, $".WithTags(\"{tags}\")");
        }

        // Extract unified security configuration (supports both ATC extensions and standard OpenAPI security)
        var security = operation.ExtractUnifiedSecurityConfiguration(pathItem, openApiDoc);

        // Add security metadata (RequireAuthorization/AllowAnonymous)
        GenerateSecurityMetadata(builder, security);

        // Add Produces metadata based on responses (with auto-append for 401/403)
        GenerateProducesMetadata(builder, operation, security);

        // Add final semicolon with newline
        builder.AppendLine(";");
    }

    /// <summary>
    /// Generates Produces metadata for an endpoint based on OpenAPI response definitions.
    /// Auto-appends 401 Unauthorized when auth required and 403 Forbidden when roles specified.
    /// </summary>
    private static void GenerateProducesMetadata(
        StringBuilder builder,
        OpenApiOperation operation,
        UnifiedSecurityConfig security)
    {
        // Collect all Produces metadata lines first (with indentation)
        var producesLines = new List<string>();

        // Track which status codes are already defined
        var definedStatusCodes = new HashSet<string>(StringComparer.Ordinal);

        if (operation.Responses != null)
        {
            foreach (var response in operation.Responses)
            {
                definedStatusCodes.Add(response.Key);
            }
        }

        // Process explicitly defined responses
        if (operation.Responses != null)
        {
            foreach (var response in operation.Responses)
            {
                var statusCode = response.Key;

                // Handle "default" response
                if (statusCode.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    producesLines.Add("    .ProducesValidationProblem()");
                    continue;
                }

                // Skip if status code is not in our map
                if (!HttpStatusCodeMap.TryGetValue(statusCode, out var statusConstant))
                {
                    continue;
                }

                // Get response content type
                string? contentType = null;
                if (response.Value is OpenApiResponse openApiResponse &&
                    openApiResponse.Content?.TryGetValue("application/json", out var mediaType) == true &&
                    mediaType.Schema != null)
                {
                    // Use ToCSharpType directly on the schema - it handles schema references
                    // correctly and returns the reference name (e.g., "Pet") for schema references
                    contentType = mediaType.Schema.ToCSharpType(true);
                }

                // Parse status code for category
                if (!int.TryParse(statusCode, out var code))
                {
                    continue;
                }

                // Generate appropriate Produces call based on status code category
                switch (code)
                {
                    // 4xx and 5xx use ProducesProblem
                    case >= 400:
                        producesLines.Add($"    .ProducesProblem(StatusCodes.{statusConstant})");
                        break;

                    // 204 and 205 never have content
                    case 204 or 205:
                        producesLines.Add($"    .Produces(StatusCodes.{statusConstant})");
                        break;

                    // 200 is the default status code for Produces<T>()
                    case 200 when !string.IsNullOrEmpty(contentType):
                        producesLines.Add($"    .Produces<{contentType}>()");
                        break;

                    // Other 2xx codes need explicit status code
                    case >= 200 and < 300 when !string.IsNullOrEmpty(contentType):
                        producesLines.Add($"    .Produces<{contentType}>(StatusCodes.{statusConstant})");
                        break;

                    // Fallback for any other codes
                    default:
                        producesLines.Add($"    .Produces(StatusCodes.{statusConstant})");
                        break;
                }
            }
        }

        // Auto-append 401 Unauthorized when auth required and not AllowAnonymous
        if (security.AuthenticationRequired &&
            !security.AllowAnonymous &&
            !definedStatusCodes.Contains("401"))
        {
            producesLines.Add("    .ProducesProblem(StatusCodes.Status401Unauthorized)");
        }

        // Auto-append 403 Forbidden when roles or scopes are specified
        if ((security.Roles.Count > 0 || security.Scopes.Count > 0) &&
            !security.AllowAnonymous &&
            !definedStatusCodes.Contains("403"))
        {
            producesLines.Add("    .ProducesProblem(StatusCodes.Status403Forbidden)");
        }

        // Output all lines - use AppendLine for all except the last one
        for (var i = 0; i < producesLines.Count; i++)
        {
            if (i < producesLines.Count - 1)
            {
                builder.AppendLine(producesLines[i]);
            }
            else
            {
                // Last line - no newline so semicolon stays on same line
                builder.Append(producesLines[i]);
            }
        }
    }

    /// <summary>
    /// Generates security metadata for an endpoint based on OpenAPI security configuration.
    /// Supports both ATC extensions (x-authentication-*, x-authorize-roles) and standard OpenAPI security.
    /// </summary>
    private static void GenerateSecurityMetadata(
        StringBuilder builder,
        UnifiedSecurityConfig security)
    {
        if (security.Source == SecuritySource.None)
        {
            // No security configuration - endpoint is public
            return;
        }

        // AllowAnonymous override takes precedence
        if (security.AllowAnonymous)
        {
            builder.AppendLine(4, ".AllowAnonymous()");
            return;
        }

        // Check what security components we have
        var hasRoles = security.Roles.Count > 0;
        var hasSchemes = security.Schemes.Count > 0;

        // Build the RequireAuthorization call based on what we have
        if (hasRoles && hasSchemes)
        {
            // Roles + Schemes (ATC extensions or combined)
            var rolesString = string.Join("\", \"", security.Roles);
            var schemesString = string.Join("\", \"", security.Schemes);
            builder.AppendLine(4, ".RequireAuthorization(policy => policy");
            builder.AppendLine(8, $".RequireRole(\"{rolesString}\")");
            builder.AppendLine(8, $".AddAuthenticationSchemes(\"{schemesString}\"))");
        }
        else if (hasRoles)
        {
            // Only roles (from ATC extensions)
            var rolesString = string.Join("\", \"", security.Roles);
            builder.AppendLine(4, $".RequireAuthorization(policy => policy.RequireRole(\"{rolesString}\"))");
        }
        else if (hasSchemes)
        {
            // Only schemes (API Key, Bearer, or OAuth2 standard security)
            var schemesString = string.Join("\", \"", security.Schemes);
            builder.AppendLine(4, ".RequireAuthorization(policy => policy");
            builder.AppendLine(8, $".AddAuthenticationSchemes(\"{schemesString}\")");
            builder.AppendLine(8, ".RequireAuthenticatedUser())");
        }
        else if (security.AuthenticationRequired)
        {
            // Just require authorization (no specific roles or schemes)
            builder.AppendLine(4, ".RequireAuthorization()");
        }
    }
}