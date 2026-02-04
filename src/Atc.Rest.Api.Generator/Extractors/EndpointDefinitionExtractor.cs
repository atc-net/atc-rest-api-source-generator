// ReSharper disable InvertIf
// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable MergeIntoLogicalPattern
namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI paths and operations and converts them to endpoint definition classes grouped by strategy.
/// Generates IEndpointDefinition interface and per-group endpoint definition classes.
/// </summary>
public static class EndpointDefinitionExtractor
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
    /// Extracts endpoint definition interface and classes from OpenAPI document.
    /// Groups operations by the specified strategy (first path segment or OpenAPI tag).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    /// <param name="subFolderStrategy">The strategy for grouping operations. Default: FirstPathSegment.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <param name="useMinimalApiPackage">Whether to use IEndpointDefinition from Atc.Rest.MinimalApi package instead of generating it.</param>
    /// <param name="useValidationFilter">Whether to add ValidationFilter&lt;T&gt; to endpoints with parameters.</param>
    /// <returns>
    /// A tuple containing:
    /// - InterfaceParameters for IEndpointDefinition (null if useMinimalApiPackage is true)
    /// - List of ClassParameters for each endpoint definition class
    /// </returns>
    public static (InterfaceParameters? Interface, List<ClassParameters>? Classes) Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        SubFolderStrategyType subFolderStrategy = SubFolderStrategyType.FirstPathSegment,
        bool includeDeprecated = false,
        bool useMinimalApiPackage = false,
        bool useValidationFilter = false)
        => Extract(openApiDoc, projectName, pathSegment: null, registry: registry, systemTypeResolver: systemTypeResolver, subFolderStrategy: subFolderStrategy, includeDeprecated: includeDeprecated, useMinimalApiPackage: useMinimalApiPackage, useValidationFilter: useValidationFilter, versioningStrategy: VersioningStrategyType.None, defaultApiVersion: null, useServersBasePath: true);

    /// <summary>
    /// Extracts endpoint definition interface and classes from OpenAPI document filtered by path segment.
    /// Groups operations by the specified strategy (first path segment or OpenAPI tag).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets"). If null, extracts all endpoints.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    /// <param name="subFolderStrategy">The strategy for grouping operations. Default: FirstPathSegment.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <param name="useMinimalApiPackage">Whether to use IEndpointDefinition from Atc.Rest.MinimalApi package instead of generating it.</param>
    /// <param name="useValidationFilter">Whether to add ValidationFilter&lt;T&gt; to endpoints with parameters.</param>
    /// <param name="versioningStrategy">The API versioning strategy. Default: None.</param>
    /// <param name="defaultApiVersion">The default API version (e.g., "1.0"). Used when versioning is enabled.</param>
    /// <param name="useServersBasePath">Whether to prepend the base path from OpenAPI servers[0].url to routes. Default: true.</param>
    /// <returns>
    /// A tuple containing:
    /// - InterfaceParameters for IEndpointDefinition (null if useMinimalApiPackage is true)
    /// - List of ClassParameters for each endpoint definition class
    /// </returns>
    public static (InterfaceParameters? Interface, List<ClassParameters>? Classes) Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        SubFolderStrategyType subFolderStrategy = SubFolderStrategyType.FirstPathSegment,
        bool includeDeprecated = false,
        bool useMinimalApiPackage = false,
        bool useValidationFilter = false,
        VersioningStrategyType versioningStrategy = VersioningStrategyType.None,
        string? defaultApiVersion = null,
        bool useServersBasePath = true)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (openApiDoc.Paths == null || openApiDoc.Paths.Count == 0)
        {
            return (null, null);
        }

        // Group operations by strategy, optionally filtered by path segment
        var operationsByGroup = GroupOperationsByStrategy(openApiDoc, pathSegment, subFolderStrategy, includeDeprecated);

        if (operationsByGroup.Count == 0)
        {
            return (null, null);
        }

        // Generate IEndpointDefinition interface (only if not using package)
        InterfaceParameters? interfaceParams = null;
        if (!useMinimalApiPackage)
        {
            interfaceParams = GenerateInterface(projectName, pathSegment);
        }

        // Generate endpoint definition classes for each group
        var classes = new List<ClassParameters>();
        foreach (var kvp in operationsByGroup)
        {
            var groupName = kvp.Key;
            var operations = kvp.Value;
            var classParams = GenerateEndpointDefinitionClass(openApiDoc, projectName, pathSegment, groupName, operations, registry, systemTypeResolver, useMinimalApiPackage, useValidationFilter, versioningStrategy, defaultApiVersion, useServersBasePath);
            if (classParams != null)
            {
                classes.Add(classParams);
            }
        }

        return (interfaceParams, classes.Count > 0 ? classes : null);
    }

    private static Dictionary<string, List<(string Path, string Method, OpenApiOperation Operation, IList<IOpenApiParameter>? PathParameters)>> GroupOperationsByStrategy(
        OpenApiDocument openApiDoc,
        string? pathSegment,
        SubFolderStrategyType strategy,
        bool includeDeprecated)
    {
        var allOperations = strategy switch
        {
            SubFolderStrategyType.None => GroupAllOperations(openApiDoc, includeDeprecated),
            SubFolderStrategyType.FirstPathSegment => GroupOperationsByFirstPathSegment(openApiDoc, includeDeprecated),
            SubFolderStrategyType.OpenApiTag => GroupOperationsByOpenApiTag(openApiDoc, includeDeprecated),
            _ => GroupOperationsByFirstPathSegment(openApiDoc, includeDeprecated),
        };

        // Filter by path segment if specified
        if (!string.IsNullOrEmpty(pathSegment))
        {
            return allOperations
                .Where(kvp => kvp.Key.Equals(pathSegment, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

        return allOperations;
    }

    private static Dictionary<string, List<(string Path, string Method, OpenApiOperation Operation, IList<IOpenApiParameter>? PathParameters)>> GroupAllOperations(
        OpenApiDocument openApiDoc,
        bool includeDeprecated)
    {
        var groups = new Dictionary<string, List<(string Path, string Method, OpenApiOperation Operation, IList<IOpenApiParameter>? PathParameters)>>(StringComparer.OrdinalIgnoreCase);
        var operationList = new List<(string Path, string Method, OpenApiOperation Operation, IList<IOpenApiParameter>? PathParameters)>();

        foreach (var path in openApiDoc.Paths)
        {
            var pathKey = path.Key;
            var pathItemInterface = path.Value;

            if (pathItemInterface is not OpenApiPathItem pathItem || pathItem.Operations == null)
            {
                continue;
            }

            // Get path-level parameters
            var pathParameters = pathItem.Parameters;

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
                operationList.Add((pathKey, httpMethod, operation.Value!, pathParameters));
            }
        }

        if (operationList.Count > 0)
        {
            // Use "Api" as the default group name when strategy is None
            groups["Api"] = operationList;
        }

        return groups;
    }

    private static Dictionary<string, List<(string Path, string Method, OpenApiOperation Operation, IList<IOpenApiParameter>? PathParameters)>> GroupOperationsByFirstPathSegment(
        OpenApiDocument openApiDoc,
        bool includeDeprecated)
    {
        var groups = new Dictionary<string, List<(string Path, string Method, OpenApiOperation Operation, IList<IOpenApiParameter>? PathParameters)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in openApiDoc.Paths)
        {
            var pathKey = path.Key;
            var pathItemInterface = path.Value;

            if (pathItemInterface is not OpenApiPathItem pathItem || pathItem.Operations == null)
            {
                continue;
            }

            // Extract first path segment (e.g., "/pets/{id}" -> "Pets")
            // PathSegmentHelper returns PascalCase, pluralized name
            var segment = GetFirstPathSegment(pathKey);

            if (!groups.TryGetValue(segment, out var operationList))
            {
                operationList = [];
                groups[segment] = operationList;
            }

            // Get path-level parameters
            var pathParameters = pathItem.Parameters;

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

                operationList.Add((pathKey, httpMethod, operation.Value!, pathParameters));
            }
        }

        return groups;
    }

    private static Dictionary<string, List<(string Path, string Method, OpenApiOperation Operation, IList<IOpenApiParameter>? PathParameters)>> GroupOperationsByOpenApiTag(
        OpenApiDocument openApiDoc,
        bool includeDeprecated)
    {
        var groups = new Dictionary<string, List<(string Path, string Method, OpenApiOperation Operation, IList<IOpenApiParameter>? PathParameters)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in openApiDoc.Paths)
        {
            var pathKey = path.Key;
            var pathItemInterface = path.Value;

            if (pathItemInterface is not OpenApiPathItem pathItem || pathItem.Operations == null)
            {
                continue;
            }

            // Get path-level parameters
            var pathParameters = pathItem.Parameters;

            foreach (var operation in pathItem.Operations)
            {
                var op = operation.Value;

                // Skip deprecated operations if not including them
                if (!includeDeprecated && op?.Deprecated == true)
                {
                    continue;
                }

                var httpMethod = operation
                    .Key
                    .ToString()
                    .ToUpperInvariant();

                // Get group name from OpenAPI tag, or fall back to first path segment
                // PathSegmentHelper.GetFirstPathSegment returns PascalCase, pluralized name
                var groupName = op?.Tags?.FirstOrDefault()?.Name?.ToPascalCaseForDotNet();
                if (string.IsNullOrEmpty(groupName))
                {
                    // GetFirstPathSegment already returns properly formatted name from PathSegmentHelper
                    groupName = GetFirstPathSegment(pathKey);
                }

                if (groupName is null)
                {
                    continue;
                }

                if (!groups.TryGetValue(groupName, out var operationList))
                {
                    operationList = [];
                    groups[groupName] = operationList;
                }

                operationList.Add((pathKey, httpMethod, op!, pathParameters));
            }
        }

        return groups;
    }

    private static string GetFirstPathSegment(string path)
        => PathSegmentHelper.GetFirstPathSegment(path);

    private static InterfaceParameters GenerateInterface(
        string projectName,
        string? pathSegment)
    {
        var namespaceValue = NamespaceBuilder.ForEndpoints(projectName, pathSegment);

        var defineEndpointsMethod = new MethodParameters(
            DocumentationTags: new CodeDocumentationTags("Defines the endpoints for this definition."),
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.None,
            ReturnGenericTypeName: null,
            ReturnTypeName: "void",
            Name: "DefineEndpoints",
            Parameters: new List<ParameterBaseParameters>
            {
                new(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: "WebApplication",
                    IsNullableType: false,
                    IsReferenceType: true,
                    Name: "app",
                    DefaultValue: null),
            },
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: null);

        return new InterfaceParameters(
            HeaderContent: @"// <auto-generated />
#nullable enable

using Microsoft.AspNetCore.Builder;
",
            Namespace: namespaceValue,
            DocumentationTags: new CodeDocumentationTags("Defines a contract for endpoint definitions."),
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicInterface,
            InterfaceTypeName: "IEndpointDefinition",
            InheritedInterfaceTypeName: null,
            Properties: null,
            Methods: new List<MethodParameters> { defineEndpointsMethod });
    }

    private static ClassParameters GenerateEndpointDefinitionClass(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        string segment,
        List<(string Path, string Method, OpenApiOperation Operation, IList<IOpenApiParameter>? PathParameters)> operations,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool useMinimalApiPackage = false,
        bool useValidationFilter = false,
        VersioningStrategyType versioningStrategy = VersioningStrategyType.None,
        string? defaultApiVersion = null,
        bool useServersBasePath = true)
    {
        var className = $"{segment}EndpointDefinition";

        // Find the common base path for this segment (optionally prepended with server base path)
        // ApiRouteBase: Full path for MapGroup (e.g., "/api/v1/accounts")
        // OriginalPrefix: Original path for relative route calculation (e.g., "/accounts")
        var (apiRouteBase, originalPrefix) = DetermineApiRouteBase(openApiDoc, operations, segment, useServersBasePath);

        // Check if any operation in this group uses output caching
        // Use document-level check since using statements are needed if ANY output caching is used
        var hasOutputCaching = openApiDoc.HasOutputCaching();

        // Build methods list
        var methods = new List<MethodParameters>();

        // Add DefineEndpoints method
        var defineEndpointsContent = GenerateDefineEndpointsContent(openApiDoc, operations, projectName, segment, apiRouteBase, originalPrefix, systemTypeResolver, registry, useValidationFilter, versioningStrategy, defaultApiVersion);
        var defineEndpointsMethod = new MethodParameters(
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            ReturnGenericTypeName: null,
            ReturnTypeName: "void",
            Name: "DefineEndpoints",
            Parameters: new List<ParameterBaseParameters>
            {
                new(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: "WebApplication",
                    IsNullableType: false,
                    IsReferenceType: true,
                    Name: "app",
                    DefaultValue: null),
            },
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: defineEndpointsContent);
        methods.Add(defineEndpointsMethod);

        // Add internal endpoint methods for each operation
        foreach (var (path, httpMethod, operation, pathParameters) in operations)
        {
            var endpointMethod = GenerateEndpointMethod(path, httpMethod, operation, pathParameters, systemTypeResolver);
            if (endpointMethod != null)
            {
                methods.Add(endpointMethod);
            }
        }

        // Get namespace availability for this segment
        var namespaces = PathSegmentHelper.GetPathSegmentNamespaces(openApiDoc, segment);

        var headerContent = GenerateHeaderContent(projectName, pathSegment, useMinimalApiPackage, useValidationFilter, versioningStrategy, hasOutputCaching, namespaces);
        var namespaceValue = NamespaceBuilder.ForEndpoints(projectName, pathSegment);

        return new ClassParameters(
            HeaderContent: headerContent,
            Namespace: namespaceValue,
            DocumentationTags: new CodeDocumentationTags($"Endpoint definitions for {segment}."),
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicSealedClass,
            ClassTypeName: className,
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: "IEndpointDefinition",
            Constructors: null,
            Properties: null,
            Methods: methods,
            GenerateToStringMethod: false);
    }

    private static string GenerateHeaderContent(
        string projectName,
        string? pathSegment,
        bool useMinimalApiPackage = false,
        bool useValidationFilter = false,
        VersioningStrategyType versioningStrategy = VersioningStrategyType.None,
        bool hasOutputCaching = false,
        PathSegmentNamespaces? namespaces = null)
    {
        // Default to all namespaces available if not specified (backward compatibility)
        namespaces ??= new PathSegmentNamespaces(HasHandlers: true, HasResults: true, HasParameters: true, HasModels: true);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Add versioning namespace when versioning is enabled
        if (versioningStrategy != VersioningStrategyType.None)
        {
            sb.AppendLine("using Asp.Versioning;");
        }

        // Add package namespace when using Atc.Rest.MinimalApi package
        if (useMinimalApiPackage)
        {
            sb.AppendLine("using Atc.Rest.MinimalApi.Abstractions;");
        }

        // Add ValidationFilter namespace when using validation
        if (useValidationFilter)
        {
            sb.AppendLine("using Atc.Rest.MinimalApi.Filters.Endpoints;");
        }

        // Add OutputCaching namespace when output caching is used
        if (hasOutputCaching)
        {
            sb.AppendLine("using Microsoft.AspNetCore.OutputCaching;");
        }

        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using Microsoft.AspNetCore.Builder;");
        sb.AppendLine("using Microsoft.AspNetCore.Http;");
        sb.AppendLine("using Microsoft.AspNetCore.Mvc;");

        // Add conditional segment namespace usings
        sb.AppendSegmentUsings(projectName, pathSegment, namespaces);

        // Add Caching namespace only when output caching is used
        if (hasOutputCaching)
        {
            sb.AppendLine($"using {projectName}.Generated.Caching;");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Determines the API route base for the MapGroup and the original prefix for relative path calculations.
    /// </summary>
    /// <returns>
    /// A tuple containing:
    /// - ApiRouteBase: The full route base including server base path (e.g., "/api/v1/accounts") for MapGroup
    /// - OriginalPrefix: The original prefix without server base path (e.g., "/accounts") for relative path calculations
    /// </returns>
    private static (string ApiRouteBase, string OriginalPrefix) DetermineApiRouteBase(
        OpenApiDocument openApiDoc,
        List<(string Path, string Method, OpenApiOperation Operation, IList<IOpenApiParameter>? PathParameters)> operations,
        string segment,
        bool useServersBasePath)
    {
        // Get server base path if enabled (e.g., "/api/v1" from servers[0].url)
        string? serverBasePath = null;
        if (useServersBasePath)
        {
            serverBasePath = ServerUrlHelper.GetServersBasePath(openApiDoc);
        }

        // Find the common prefix path for all operations in this segment
        if (operations.Count == 0)
        {
            var segmentPath = $"/{segment.ToLowerInvariant()}";
            var fullPath = serverBasePath != null ? $"{serverBasePath}{segmentPath}" : segmentPath;
            return (fullPath, segmentPath);
        }

        // Get all paths and find common prefix
        var paths = operations
            .Select(o => o.Path)
            .ToList();

        var commonPrefix = GetCommonPathPrefix(paths);

        // If no common prefix found, use the segment name
        if (string.IsNullOrEmpty(commonPrefix) || commonPrefix == "/")
        {
            var segmentPath = $"/{segment.ToLowerInvariant()}";
            var fullPath = serverBasePath != null ? $"{serverBasePath}{segmentPath}" : segmentPath;
            return (fullPath, segmentPath);
        }

        var routeBase = serverBasePath != null ? $"{serverBasePath}{commonPrefix}" : commonPrefix;
        return (routeBase, commonPrefix);
    }

    private static string GetCommonPathPrefix(List<string> paths)
    {
        if (paths.Count == 0)
        {
            return string.Empty;
        }

        if (paths.Count == 1)
        {
            // For single path, get the base path without parameters
            var path = paths[0];
            var segments = path
                .Split('/')
                .Where(s => !string.IsNullOrEmpty(s) && !s.StartsWith("{", StringComparison.Ordinal))
                .ToList();

            return "/" + string.Join("/", segments);
        }

        // Find common prefix among all paths
        var firstPath = paths[0];
        var firstSegments = firstPath
            .Split('/')
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();

        var commonSegments = new List<string>();
        for (var i = 0; i < firstSegments.Length; i++)
        {
            var segment = firstSegments[i];

            // Skip path parameters
            if (segment.StartsWith("{", StringComparison.Ordinal))
            {
                break;
            }

            // Check if this segment exists in all other paths at the same position
            var allMatch = true;
            foreach (var path in paths.Skip(1))
            {
                var pathSegments = path
                    .Split('/')
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToArray();

                if (i >= pathSegments.Length || !pathSegments[i].Equals(segment, StringComparison.Ordinal))
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
            {
                commonSegments.Add(segment);
            }
            else
            {
                break;
            }
        }

        return commonSegments.Count > 0 ? "/" + string.Join("/", commonSegments) : "/";
    }

    private static string GenerateDefineEndpointsContent(
        OpenApiDocument openApiDoc,
        List<(string Path, string Method, OpenApiOperation Operation, IList<IOpenApiParameter>? PathParameters)> operations,
        string projectName,
        string segment,
        string apiRouteBase,
        string originalPrefix,
        SystemTypeConflictResolver systemTypeResolver,
        TypeConflictRegistry? registry = null,
        bool useValidationFilter = false,
        VersioningStrategyType versioningStrategy = VersioningStrategyType.None,
        string? defaultApiVersion = null)
    {
        var builder = new StringBuilder();
        var hasVersioning = versioningStrategy != VersioningStrategyType.None;

        // For URL segment versioning, modify the route base to include version pattern
        var effectiveRouteBase = apiRouteBase;
        if (versioningStrategy == VersioningStrategyType.UrlSegment &&
            effectiveRouteBase.StartsWith("/", StringComparison.Ordinal) &&
            effectiveRouteBase.IndexOf("{version:", StringComparison.OrdinalIgnoreCase) < 0)
        {
            // Insert version segment after the first slash if not already present
            // e.g., "/pets" becomes "/v{version:apiVersion}/pets"
            effectiveRouteBase = "/v{version:apiVersion}" + effectiveRouteBase;
        }

        // Create versioned API group if versioning is enabled
        if (hasVersioning)
        {
            // Parse version
            var versionParts = (defaultApiVersion ?? "1.0").Split('.');
            var majorVersion = versionParts.Length > 0 ? versionParts[0] : "1";
            var minorVersion = versionParts.Length > 1 ? versionParts[1] : "0";

            builder.AppendLine($"var versionSet = app.NewApiVersionSet()");
            builder.AppendLine(4, $".HasApiVersion(new ApiVersion({majorVersion}, {minorVersion}))");
            builder.AppendLine(4, ".ReportApiVersions()");
            builder.AppendLine(4, ".Build();");
            builder.AppendLine();
        }

        // Create the route group with inline route base
        builder.AppendLine($"var {segment.ToLowerInvariant()} = app");
        builder.AppendLine(4, $".MapGroup(\"{effectiveRouteBase}\")");

        // Add versioning to the group if enabled
        if (hasVersioning)
        {
            builder.AppendLine(4, ".WithApiVersionSet(versionSet)");
        }

        builder.Append(4, $".WithTags(\"{segment}\")");

        // Check for group-level security (common to all operations in this segment)
        var groupSecurity = GetGroupLevelSecurity(openApiDoc, operations);
        if (groupSecurity != null && groupSecurity.AuthenticationRequired && !groupSecurity.AllowAnonymous)
        {
            builder.AppendLine();
            builder.Append(4, ".RequireAuthorization()");
        }

        // Check for group-level rate limiting (common to all operations in this segment)
        var groupRateLimit = GetGroupLevelRateLimiting(openApiDoc, operations);
        if (groupRateLimit != null && groupRateLimit.Enabled && !string.IsNullOrEmpty(groupRateLimit.Policy))
        {
            builder.AppendLine();
            builder.Append(4, $".RequireRateLimiting(\"{groupRateLimit.Policy}\")");
        }

        // Check for group-level output caching (common to all operations in this segment)
        var groupOutputCache = GetGroupLevelOutputCaching(openApiDoc, operations);
        if (groupOutputCache != null && groupOutputCache.Enabled && !string.IsNullOrEmpty(groupOutputCache.Policy))
        {
            builder.AppendLine();
            builder.Append(4, $".CacheOutput(OutputCachePolicies.{OutputCachePoliciesExtractor.GenerateConstantName(groupOutputCache.Policy!)})");
        }

        builder.AppendLine(";");

        foreach (var (path, httpMethod, operation, _) in operations)
        {
            builder.AppendLine();

            // Find the PathItem for this path
            OpenApiPathItem? pathItem = null;
            if (openApiDoc.Paths.TryGetValue(path, out var pathItemInterface) && pathItemInterface is OpenApiPathItem item)
            {
                pathItem = item;
            }

            GenerateEndpointRegistration(builder, openApiDoc, path, httpMethod, operation, pathItem, projectName, segment, originalPrefix, systemTypeResolver, groupSecurity, groupRateLimit, groupOutputCache, registry, useValidationFilter);
        }

        return builder
            .ToString()
            .TrimEnd();
    }

    private static UnifiedSecurityConfig? GetGroupLevelSecurity(
        OpenApiDocument openApiDoc,
        List<(string Path, string Method, OpenApiOperation Operation, IList<IOpenApiParameter>? PathParameters)> operations)
    {
        if (operations.Count == 0)
        {
            return null;
        }

        // Get security config for first operation to check if all operations share the same security
        var firstOp = operations[0];
        OpenApiPathItem? firstPathItem = null;
        if (openApiDoc.Paths.TryGetValue(firstOp.Path, out var firstPathItemInterface) && firstPathItemInterface is OpenApiPathItem item)
        {
            firstPathItem = item;
        }

        if (firstPathItem is null)
        {
            return null;
        }

        var firstSecurity = firstOp.Operation.ExtractUnifiedSecurityConfiguration(firstPathItem, openApiDoc);

        // If first operation doesn't require auth, no group-level security
        if (firstSecurity.Source == SecuritySource.None || !firstSecurity.AuthenticationRequired)
        {
            return null;
        }

        // Check if all operations have the same base security requirement (authentication required, no allow anonymous)
        foreach (var (path, _, operation, _) in operations.Skip(1))
        {
            OpenApiPathItem? pathItem = null;
            if (openApiDoc.Paths.TryGetValue(path, out var pathItemInterface) && pathItemInterface is OpenApiPathItem pi)
            {
                pathItem = pi;
            }

            if (pathItem is null)
            {
                return null;
            }

            var opSecurity = operation.ExtractUnifiedSecurityConfiguration(pathItem, openApiDoc);

            // If any operation has different base security, can't use group-level
            if (opSecurity.Source == SecuritySource.None ||
                !opSecurity.AuthenticationRequired ||
                opSecurity.AllowAnonymous)
            {
                return null;
            }
        }

        // All operations require auth, return the base security config
        return firstSecurity;
    }

    private static RateLimitConfiguration? GetGroupLevelRateLimiting(
        OpenApiDocument openApiDoc,
        List<(string Path, string Method, OpenApiOperation Operation, IList<IOpenApiParameter>? PathParameters)> operations)
    {
        if (operations.Count == 0)
        {
            return null;
        }

        // Get rate limit config for first operation to check if all operations share the same policy
        var firstOp = operations[0];
        OpenApiPathItem? firstPathItem = null;
        if (openApiDoc.Paths.TryGetValue(firstOp.Path, out var firstPathItemInterface) && firstPathItemInterface is OpenApiPathItem item)
        {
            firstPathItem = item;
        }

        if (firstPathItem is null)
        {
            return null;
        }

        var firstRateLimit = firstOp.Operation.ExtractRateLimitConfiguration(firstPathItem, openApiDoc);

        // If first operation doesn't have rate limiting, no group-level rate limiting
        if (firstRateLimit == null || !firstRateLimit.Enabled || string.IsNullOrEmpty(firstRateLimit.Policy))
        {
            return null;
        }

        // Check if all operations have the same rate limit policy
        foreach (var (path, _, operation, _) in operations.Skip(1))
        {
            OpenApiPathItem? pathItem = null;
            if (openApiDoc.Paths.TryGetValue(path, out var pathItemInterface) && pathItemInterface is OpenApiPathItem pi)
            {
                pathItem = pi;
            }

            if (pathItem is null)
            {
                return null;
            }

            var opRateLimit = operation.ExtractRateLimitConfiguration(pathItem, openApiDoc);

            // If any operation has different rate limit policy, can't use group-level
            if (opRateLimit == null ||
                !opRateLimit.Enabled ||
                string.IsNullOrEmpty(opRateLimit.Policy) ||
                !string.Equals(opRateLimit.Policy, firstRateLimit.Policy, StringComparison.Ordinal))
            {
                return null;
            }
        }

        // All operations have the same policy, return it
        return firstRateLimit;
    }

    private static CacheConfiguration? GetGroupLevelOutputCaching(
        OpenApiDocument openApiDoc,
        List<(string Path, string Method, OpenApiOperation Operation, IList<IOpenApiParameter>? PathParameters)> operations)
    {
        if (operations.Count == 0)
        {
            return null;
        }

        // Filter to only GET operations (output caching only applies to read operations)
        var getOperations = operations
            .Where(o => o.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (getOperations.Count == 0)
        {
            return null;
        }

        // Get output cache config for first GET operation to check if all GET operations share the same policy
        var firstOp = getOperations[0];
        OpenApiPathItem? firstPathItem = null;
        if (openApiDoc.Paths.TryGetValue(firstOp.Path, out var firstPathItemInterface) && firstPathItemInterface is OpenApiPathItem item)
        {
            firstPathItem = item;
        }

        if (firstPathItem is null)
        {
            return null;
        }

        var firstOutputCache = firstOp.Operation.ExtractCacheConfiguration(firstPathItem, openApiDoc);

        // If first operation doesn't have output caching enabled, no group-level caching
        if (firstOutputCache == null || !firstOutputCache.Enabled || string.IsNullOrEmpty(firstOutputCache.Policy))
        {
            return null;
        }

        // Only process Output Cache type (not HybridCache)
        if (firstOutputCache.Type != CacheType.Output)
        {
            return null;
        }

        // Check if all GET operations have the same output cache policy
        foreach (var (path, _, operation, _) in getOperations.Skip(1))
        {
            OpenApiPathItem? pathItem = null;
            if (openApiDoc.Paths.TryGetValue(path, out var pathItemInterface) && pathItemInterface is OpenApiPathItem pi)
            {
                pathItem = pi;
            }

            if (pathItem is null)
            {
                return null;
            }

            var opOutputCache = operation.ExtractCacheConfiguration(pathItem, openApiDoc);

            // If any GET operation has different output cache policy, can't use group-level
            if (opOutputCache == null ||
                !opOutputCache.Enabled ||
                opOutputCache.Type != CacheType.Output ||
                string.IsNullOrEmpty(opOutputCache.Policy) ||
                !string.Equals(opOutputCache.Policy, firstOutputCache.Policy, StringComparison.Ordinal))
            {
                return null;
            }
        }

        // All GET operations have the same output cache policy, return it
        return firstOutputCache;
    }

    private static void GenerateEndpointRegistration(
        StringBuilder builder,
        OpenApiDocument openApiDoc,
        string path,
        string httpMethod,
        OpenApiOperation operation,
        OpenApiPathItem? pathItem,
        string projectName,
        string segment,
        string originalPrefix,
        SystemTypeConflictResolver systemTypeResolver,
        UnifiedSecurityConfig? groupSecurity,
        RateLimitConfiguration? groupRateLimit,
        CacheConfiguration? groupOutputCache,
        TypeConflictRegistry? registry = null,
        bool useValidationFilter = false)
    {
        var normalizedPath = path
            .Replace("/", "_")
            .Replace("{", string.Empty)
            .Replace("}", string.Empty);

        var operationId = operation.OperationId ?? $"{httpMethod}{normalizedPath}";
        var methodName = operationId.ToPascalCaseForDotNet();

        // Convert HTTP method to Pascal case
        var httpMethodPascal = char.ToUpperInvariant(
            httpMethod[0]) + httpMethod
            .Substring(1)
            .ToLowerInvariant();

        // Calculate relative path from original prefix (without server base path)
        var relativePath = GetRelativePath(path, originalPrefix);

        builder.Append(segment.ToLowerInvariant());
        builder.AppendLine();
        builder.Append($"    .Map{httpMethodPascal}(\"{relativePath}\", {methodName})");
        builder.AppendLine();
        builder.Append($"    .WithName(\"{methodName}\")");

        // Add summary if present
        if (!string.IsNullOrEmpty(operation.Summary))
        {
            builder.AppendLine();
            builder.Append($"    .WithSummary(\"{operation.Summary!.Replace("\"", "\\\"")}\")");
        }

        // Add Produces metadata based on responses (includes auto-apply for common errors)
        GenerateProducesMetadata(builder, openApiDoc, operation, pathItem!, httpMethod, projectName, segment, systemTypeResolver, registry);

        // Add ValidationFilter if enabled and operation has parameters or request body
        if (useValidationFilter)
        {
            GenerateValidationFilterMetadata(builder, operation, pathItem, methodName);
        }

        // Add security metadata if needed (only if different from group-level)
        if (pathItem != null)
        {
            GenerateSecurityMetadata(builder, operation, pathItem, openApiDoc, groupSecurity);
        }

        // Add rate limiting metadata if needed (only if different from group-level)
        if (pathItem != null)
        {
            GenerateRateLimitingMetadata(builder, operation, pathItem, openApiDoc, groupRateLimit);
        }

        // Add output caching metadata if needed (only if different from group-level)
        if (pathItem != null)
        {
            GenerateOutputCachingMetadata(builder, httpMethod, operation, pathItem, openApiDoc, groupOutputCache);
        }

        // Add Accepts and DisableAntiforgery for file upload endpoints
        if (operation.HasFileUpload())
        {
            var contentType = operation.GetFileUploadContentType();
            builder.AppendLine();
            if (contentType is "application/octet-stream")
            {
                builder.Append("    .Accepts<Stream>(\"application/octet-stream\")");
            }
            else
            {
                builder.Append("    .Accepts<IFormFile>(\"multipart/form-data\")");
            }

            builder.AppendLine();
            builder.Append("    .DisableAntiforgery()");
        }

        builder.AppendLine(";");
    }

    private static void GenerateSecurityMetadata(
        StringBuilder builder,
        OpenApiOperation operation,
        OpenApiPathItem pathItem,
        OpenApiDocument openApiDoc,
        UnifiedSecurityConfig? groupSecurity)
    {
        var security = operation.ExtractUnifiedSecurityConfiguration(pathItem, openApiDoc);

        // If no security configured, nothing to generate
        if (security.Source == SecuritySource.None)
        {
            return;
        }

        // If AllowAnonymous is set, generate it (this overrides any group-level security)
        if (security.AllowAnonymous)
        {
            builder.AppendLine();
            builder.Append("    .AllowAnonymous()");
            return;
        }

        // If group-level security is already applied, only generate operation-specific overrides
        if (groupSecurity != null && groupSecurity.AuthenticationRequired && !groupSecurity.AllowAnonymous)
        {
            // Only generate if this operation has additional requirements (roles, schemes, policies)
            var hasRoles = security.Roles.Count > 0;
            var hasSchemes = security.Schemes.Count > 0;
            var hasPolicies = security.Policies.Count > 0;

            if (!hasRoles && !hasSchemes && !hasPolicies)
            {
                // No additional requirements, group-level is sufficient
                return;
            }

            // Generate operation-specific authorization
            GenerateRequireAuthorization(builder, security);
            return;
        }

        // No group-level security, generate full security config
        if (!security.AuthenticationRequired)
        {
            return;
        }

        GenerateRequireAuthorization(builder, security);
    }

    private static void GenerateRateLimitingMetadata(
        StringBuilder builder,
        OpenApiOperation operation,
        OpenApiPathItem pathItem,
        OpenApiDocument openApiDoc,
        RateLimitConfiguration? groupRateLimit)
    {
        var rateLimit = operation.ExtractRateLimitConfiguration(pathItem, openApiDoc);

        // If no rate limiting configured, nothing to generate
        if (rateLimit == null)
        {
            return;
        }

        // If rate limiting is explicitly disabled, generate DisableRateLimiting
        if (!rateLimit.Enabled)
        {
            builder.AppendLine();
            builder.Append("    .DisableRateLimiting()");
            return;
        }

        // If group-level rate limiting is already applied with the same policy, skip
        if (groupRateLimit != null &&
            groupRateLimit.Enabled &&
            string.Equals(groupRateLimit.Policy, rateLimit.Policy, StringComparison.Ordinal))
        {
            return;
        }

        // Generate operation-specific rate limiting
        if (!string.IsNullOrEmpty(rateLimit.Policy))
        {
            builder.AppendLine();
            builder.Append($"    .RequireRateLimiting(\"{rateLimit.Policy}\")");
        }
    }

    private static void GenerateOutputCachingMetadata(
        StringBuilder builder,
        string httpMethod,
        OpenApiOperation operation,
        OpenApiPathItem pathItem,
        OpenApiDocument openApiDoc,
        CacheConfiguration? groupOutputCache)
    {
        // Output caching only applies to GET requests (read operations)
        if (!httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var cacheConfig = operation.ExtractCacheConfiguration(pathItem, openApiDoc);

        // If no caching configured, nothing to generate
        if (cacheConfig == null)
        {
            return;
        }

        // Only process Output Cache type (not HybridCache)
        if (cacheConfig.Type != CacheType.Output)
        {
            return;
        }

        // If caching is explicitly disabled, skip (no .CacheOutput() call)
        if (!cacheConfig.Enabled)
        {
            return;
        }

        // If group-level output caching is already applied with the same policy, skip
        if (groupOutputCache != null &&
            groupOutputCache.Enabled &&
            groupOutputCache.Type == CacheType.Output &&
            string.Equals(groupOutputCache.Policy, cacheConfig.Policy, StringComparison.Ordinal))
        {
            return;
        }

        // Generate operation-specific output caching
        if (!string.IsNullOrEmpty(cacheConfig.Policy))
        {
            var constantName = OutputCachePoliciesExtractor.GenerateConstantName(cacheConfig.Policy!);
            builder.AppendLine();
            builder.Append($"    .CacheOutput(OutputCachePolicies.{constantName})");
        }
    }

    private static void GenerateValidationFilterMetadata(
        StringBuilder builder,
        OpenApiOperation operation,
        OpenApiPathItem? pathItem,
        string methodName)
    {
        // Skip ValidationFilter for endpoints that use form binding flattening
        // (they don't use [AsParameters] so the filter can't validate them)
        if (operation.RequiresFormBindingFlattening())
        {
            return;
        }

        // Determine if operation has parameters (operation-level OR path-level)
        var hasOperationParams = operation.Parameters != null && operation.Parameters.Count > 0;
        var hasPathParams = pathItem?.Parameters != null && pathItem.Parameters.Count > 0;
        var hasParameters = hasOperationParams || hasPathParams;
        var hasRequestBody = operation.HasRequestBody();

        // Only add ValidationFilter if operation has parameters or request body
        if (!hasParameters && !hasRequestBody)
        {
            return;
        }

        var parameterClassName = $"{methodName}Parameters";

        builder.AppendLine();
        builder.Append($"    .AddEndpointFilter<ValidationFilter<{parameterClassName}>>()");
    }

    private static void GenerateRequireAuthorization(
        StringBuilder builder,
        UnifiedSecurityConfig security)
    {
        var hasRoles = security.Roles.Count > 0;
        var hasSchemes = security.Schemes.Count > 0;
        var hasPolicies = security.Policies.Count > 0;

        builder.AppendLine();

        if (hasRoles && hasSchemes)
        {
            var rolesString = string.Join("\", \"", security.Roles);
            var schemesString = string.Join("\", \"", security.Schemes);
            builder.AppendLine(4, ".RequireAuthorization(policy => policy");
            builder.AppendLine(8, $".RequireRole(\"{rolesString}\")");
            builder.Append(8, $".AddAuthenticationSchemes(\"{schemesString}\"))");
        }
        else if (hasRoles)
        {
            var rolesString = string.Join("\", \"", security.Roles);
            builder.Append(4, $".RequireAuthorization(policy => policy.RequireRole(\"{rolesString}\"))");
        }
        else if (hasSchemes)
        {
            var schemesString = string.Join("\", \"", security.Schemes);
            builder.AppendLine(4, ".RequireAuthorization(policy => policy");
            builder.AppendLine(8, $".AddAuthenticationSchemes(\"{schemesString}\")");
            builder.Append(8, ".RequireAuthenticatedUser())");
        }
        else if (hasPolicies)
        {
            var policiesString = string.Join("\", \"", security.Policies);
            builder.Append(4, $".RequireAuthorization(\"{policiesString}\")");
        }
        else
        {
            builder.Append(4, ".RequireAuthorization()");
        }
    }

    private static string GetRelativePath(
        string fullPath,
        string apiRouteBase)
    {
        // Remove the apiRouteBase prefix from the path
        if (fullPath.StartsWith(apiRouteBase, StringComparison.OrdinalIgnoreCase))
        {
            var remaining = fullPath
                .Substring(apiRouteBase.Length)
                .TrimStart('/');
            return string.IsNullOrEmpty(remaining) ? "/" : remaining;
        }

        // If path doesn't match apiRouteBase, return the full path
        return fullPath;
    }

    private static void GenerateProducesMetadata(
        StringBuilder builder,
        OpenApiDocument openApiDoc,
        OpenApiOperation operation,
        OpenApiPathItem pathItem,
        string httpMethod,
        string projectName,
        string segment,
        SystemTypeConflictResolver systemTypeResolver,
        TypeConflictRegistry? registry = null)
    {
        // Track which response codes are defined in the spec
        var definedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (operation.Responses != null)
        {
            foreach (var response in operation.Responses)
            {
                if (response.Value is not OpenApiResponse openApiResponse)
                {
                    continue;
                }

                var statusCode = response.Key;
                definedCodes.Add(statusCode);

                // Handle "default" response
                if (statusCode.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    builder.AppendLine();
                    builder.Append("    .ProducesValidationProblem()");
                    continue;
                }

                // Get the StatusCodes constant name
                if (!HttpStatusCodeMap.TryGetValue(statusCode, out var statusCodeConstant))
                {
                    continue;
                }

                // Get response content type - handle inline schemas properly
                string? contentType = null;
                if (openApiResponse.Content != null && openApiResponse.Content.TryGetValue("application/json", out var mediaType))
                {
                    contentType = GetResponseContentType(mediaType.Schema, openApiDoc, operation, projectName, segment, systemTypeResolver, registry);
                }

                // Parse the status code to determine the category
                if (!int.TryParse(statusCode, out var statusCodeInt))
                {
                    continue;
                }

                builder.AppendLine();

                switch (statusCodeInt)
                {
                    // 4xx and 5xx use ProducesProblem
                    case >= 400:
                        builder.Append($"    .ProducesProblem(StatusCodes.{statusCodeConstant})");
                        break;

                    // 2xx success codes: 204 and 205 never have content
                    case >= 200 and < 300 when statusCodeInt == 204 || statusCodeInt == 205:
                        builder.Append($"    .Produces(StatusCodes.{statusCodeConstant})");
                        break;
                    case >= 200 and < 300 when statusCodeInt == 200:
                    {
                        // 200 is the default status code for Produces<T>()
                        builder.Append(string.IsNullOrEmpty(contentType)
                            ? $"    .Produces(StatusCodes.{statusCodeConstant})"
                            : $"    .Produces<{contentType}>()");

                        break;
                    }

                    // Other 2xx codes need explicit status code
                    case >= 200 and < 300 when !string.IsNullOrEmpty(contentType):
                        builder.Append($"    .Produces<{contentType}>(StatusCodes.{statusCodeConstant})");
                        break;
                    case >= 200 and < 300:
                        builder.Append($"    .Produces(StatusCodes.{statusCodeConstant})");
                        break;
                    default:
                        // 1xx and 3xx use simple Produces
                        builder.Append($"    .Produces(StatusCodes.{statusCodeConstant})");
                        break;
                }
            }
        }

        // Auto-apply .ProducesProblem for responses NOT defined in spec
        // This ensures Swagger documentation shows all possible responses
        var features = OperationFeaturesHelper.DetectOperationFeatures(operation, pathItem, openApiDoc, httpMethod);

        // 400 - Has parameters but no 400 defined
        if (features.HasParameters && !definedCodes.Contains("400"))
        {
            builder.AppendLine();
            builder.Append("    .ProducesValidationProblem()");
        }

        // 401 - Has security but no 401 defined
        if (features.HasSecurity && !definedCodes.Contains("401"))
        {
            builder.AppendLine();
            builder.Append("    .ProducesProblem(StatusCodes.Status401Unauthorized)");
        }

        // 403 - Has roles/policies but no 403 defined
        if (features.HasRolesOrPolicies && !definedCodes.Contains("403"))
        {
            builder.AppendLine();
            builder.Append("    .ProducesProblem(StatusCodes.Status403Forbidden)");
        }

        // 404 - GET/PUT/DELETE/PATCH with path params but no 404 defined
        if (features.HasPathParameters &&
            features.HttpMethod is "GET" or "PUT" or "DELETE" or "PATCH" &&
            !definedCodes.Contains("404"))
        {
            builder.AppendLine();
            builder.Append("    .ProducesProblem(StatusCodes.Status404NotFound)");
        }

        // 409 - POST/PUT but no 409 defined
        if (features.HttpMethod is "POST" or "PUT" && !definedCodes.Contains("409"))
        {
            builder.AppendLine();
            builder.Append("    .ProducesProblem(StatusCodes.Status409Conflict)");
        }

        // 429 - Has rate limiting but no 429 defined
        if (features.HasRateLimiting && !definedCodes.Contains("429"))
        {
            builder.AppendLine();
            builder.Append("    .ProducesProblem(StatusCodes.Status429TooManyRequests)");
        }

        // 500 - Always add if not defined (GlobalErrorHandler)
        if (!definedCodes.Contains("500") && !definedCodes.Contains("default"))
        {
            builder.AppendLine();
            builder.Append("    .ProducesProblem(StatusCodes.Status500InternalServerError)");
        }
    }

    private static MethodParameters GenerateEndpointMethod(
        string path,
        string httpMethod,
        OpenApiOperation operation,
        IList<IOpenApiParameter>? pathParameters,
        SystemTypeConflictResolver systemTypeResolver)
    {
        var normalizedPath = path
            .Replace("/", "_")
            .Replace("{", string.Empty)
            .Replace("}", string.Empty);

        var operationId = operation.OperationId ?? $"{httpMethod}{normalizedPath}";
        var methodName = operationId.ToPascalCaseForDotNet();
        var resultClassName = $"{methodName}Result";

        // Determine if operation has parameters (operation-level OR path-level)
        var hasOperationParams = operation.Parameters != null && operation.Parameters.Count > 0;
        var hasPathParams = pathParameters != null && pathParameters.Count > 0;
        var hasParameters = hasOperationParams || hasPathParams;
        var hasRequestBody = operation.HasRequestBody();

        // Check if this is a multipart/form-data with complex schema that needs flattening
        var requiresFormFlattening = operation.RequiresFormBindingFlattening();

        // Build method parameters
        var methodParameters = new List<ParameterBaseParameters>();

        // Add handler parameter with [FromServices]
        methodParameters.Add(new ParameterBaseParameters(
            Attributes: new List<AttributeParameters> { new("FromServices", null) },
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: $"I{methodName}Handler",
            IsNullableType: false,
            IsReferenceType: true,
            Name: "handler",
            DefaultValue: null));

        // For form flattening, add individual form parameters instead of [AsParameters]
        var formParameterNames = new List<(string ParamName, string PropName, string TypeName)>();
        if (requiresFormFlattening)
        {
            var (schemaName, properties) = operation.GetMultipartFormDataSchemaInfo();
            if (properties != null)
            {
                foreach (var kvp in properties)
                {
                    var propName = kvp.Key;
                    var propSchema = kvp.Value;
                    var pascalPropName = propName.ToPascalCaseForDotNet();

                    // Convert to camelCase for parameter name
                    var paramName = char.ToLowerInvariant(pascalPropName[0]) + pascalPropName.Substring(1);
                    var (isFile, isCollection) = propSchema.GetFileUploadInfo();

                    string typeName;
                    List<AttributeParameters>? attributes = null;

                    if (isFile)
                    {
                        // File property - use IFormFile (no attribute needed, ASP.NET Core binds it automatically)
                        typeName = isCollection ? "IFormFileCollection" : "IFormFile";

                        // Make nullable since file is often optional
                        typeName += "?";
                    }
                    else
                    {
                        // Non-file property - needs [FromForm] attribute
                        // Use ToCSharpType for proper type mapping (handles format, nullable, etc.)
                        typeName = propSchema.ToCSharpType(isRequired: false, registry: null);

                        attributes = new List<AttributeParameters>
                        {
                            new("FromForm", $"Name = \"{propName}\""),
                        };
                    }

                    methodParameters.Add(new ParameterBaseParameters(
                        Attributes: attributes,
                        GenericTypeName: null,
                        IsGenericListType: typeName.StartsWith("List<", StringComparison.Ordinal),
                        TypeName: typeName.TrimEnd('?'),
                        IsNullableType: typeName.EndsWith("?", StringComparison.Ordinal),
                        IsReferenceType: true,
                        Name: paramName,
                        DefaultValue: null));

                    formParameterNames.Add((paramName, pascalPropName, typeName));
                }
            }
        }
        else if (hasParameters || hasRequestBody)
        {
            // Standard case: use [AsParameters] with the parameters class
            var parameterClassName = $"{methodName}Parameters";
            methodParameters.Add(new ParameterBaseParameters(
                Attributes: new List<AttributeParameters> { new("AsParameters", null) },
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: parameterClassName,
                IsNullableType: false,
                IsReferenceType: true,
                Name: "parameters",
                DefaultValue: null));
        }

        // Add CancellationToken
        methodParameters.Add(new ParameterBaseParameters(
            Attributes: null,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: "CancellationToken",
            IsNullableType: false,
            IsReferenceType: false,
            Name: "cancellationToken",
            DefaultValue: null));

        // Build method content
        string content;
        if (requiresFormFlattening)
        {
            // For form flattening, construct the request object and parameters manually
            var (schemaName, _) = operation.GetMultipartFormDataSchemaInfo();
            var requestTypeName = schemaName?.ToPascalCaseForDotNet() ?? "Request";
            var parameterClassName = $"{methodName}Parameters";

            // Build constructor arguments for the request object
            // Add null-coalescing for required parameters to handle nullable form binding
            var requestArgs = string.Join(", ", formParameterNames.Select(p =>
            {
                var cleanTypeName = p.TypeName.TrimEnd('?');

                // IFormFile types - handle collection conversion if needed
                if (cleanTypeName.Contains("IFormFile", StringComparison.Ordinal))
                {
                    // IFormFileCollection needs conversion to List<IFormFile> for model compatibility
                    // The model uses List<IFormFile> but ASP.NET Core binds to IFormFileCollection
                    if (cleanTypeName == "IFormFileCollection")
                    {
                        return $"{p.ParamName}?.ToList() ?? new List<IFormFile>()";
                    }

                    return p.ParamName;
                }

                // String parameters - use null-coalescing with empty string
                if (cleanTypeName.Equals("string", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{p.ParamName} ?? string.Empty";
                }

                // List parameters - use null-coalescing with empty list
                if (cleanTypeName.StartsWith("List<", StringComparison.Ordinal))
                {
                    return $"{p.ParamName} ?? new {cleanTypeName}()";
                }

                return p.ParamName;
            }));

            // Use block body since we need to construct objects
            // Note: Content should NOT include braces - the code generator adds them
            content = $@"var request = new {requestTypeName}({requestArgs});
        var parameters = new {parameterClassName}(request);
        return {resultClassName}.ToIResult(
            await handler.ExecuteAsync(
                parameters,
                cancellationToken));";

            return new MethodParameters(
                DocumentationTags: null,
                Attributes: null,
                DeclarationModifier: DeclarationModifiers.InternalAsync,
                ReturnGenericTypeName: systemTypeResolver.EnsureFullNamespaceIfNeeded(nameof(Task)),
                ReturnTypeName: "IResult",
                Name: methodName,
                Parameters: methodParameters,
                AlwaysBreakDownParameters: true,
                UseExpressionBody: false,
                Content: content);
        }

        if (hasParameters || hasRequestBody)
        {
            content = $@"{resultClassName}.ToIResult(
            await handler.ExecuteAsync(
                parameters,
                cancellationToken))";
        }
        else
        {
            content = $@"{resultClassName}.ToIResult(
            await handler.ExecuteAsync(
                cancellationToken))";
        }

        return new MethodParameters(
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.InternalAsync,
            ReturnGenericTypeName: systemTypeResolver.EnsureFullNamespaceIfNeeded(nameof(Task)),
            ReturnTypeName: "IResult",
            Name: methodName,
            Parameters: methodParameters,
            AlwaysBreakDownParameters: true,
            UseExpressionBody: true,
            Content: content);
    }

    /// <summary>
    /// Gets the C# type for a response schema, handling inline object schemas properly.
    /// </summary>
    private static string? GetResponseContentType(
        IOpenApiSchema? schema,
        OpenApiDocument openApiDoc,
        OpenApiOperation operation,
        string projectName,
        string segment,
        SystemTypeConflictResolver systemTypeResolver,
        TypeConflictRegistry? registry)
    {
        if (schema == null)
        {
            return null;
        }

        var operationId = operation.OperationId ?? "Unknown";
        var schemaType = schema.GetSchemaType();

        // Handle array types
        if (schemaType == "array" && schema.Items != null)
        {
            var itemType = GetSchemaItemType(schema.Items, openApiDoc, operationId, projectName, segment, systemTypeResolver, registry);
            return $"List<{itemType}>";
        }

        // Handle schema reference ($ref)
        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refName = schemaRef.Reference?.Id ?? schemaRef.Id;
            if (!string.IsNullOrEmpty(refName))
            {
                var typeName = refName!.ToPascalCaseForDotNet();
                return ResolveModelTypeConflict(typeName, projectName, segment, systemTypeResolver);
            }
        }

        // Handle inline object schema
        if (schemaType == "object" && schema.Properties != null && schema.Properties.Count > 0)
        {
            // Generate inline type name using the same pattern as ResultClassExtractor
            return InlineSchemaExtractor.GenerateInlineTypeName(operationId, "Response");
        }

        // Fall back to ToCSharpTypeWithGenericSupport for other cases (allOf, primitives, etc.)
        var fallbackType = schema.ToCSharpTypeWithGenericSupport(openApiDoc, isRequired: true, registry);
        if (fallbackType != null)
        {
            return ResolveModelTypeConflict(fallbackType, projectName, segment, systemTypeResolver);
        }

        return fallbackType;
    }

    /// <summary>
    /// Resolves model type conflicts with System types by using full namespace.
    /// </summary>
    private static string ResolveModelTypeConflict(
        string typeName,
        string projectName,
        string segment,
        SystemTypeConflictResolver systemTypeResolver)
    {
        // Check if this model name conflicts with a System type
        if (systemTypeResolver.HasConflict(typeName))
        {
            // Use full namespace for the model type to avoid ambiguity
            // e.g., "Task" becomes "Showcase.Generated.Tasks.Models.Task"
            return $"{projectName}.Generated.{segment}.Models.{typeName}";
        }

        return typeName;
    }

    /// <summary>
    /// Gets the C# type for array item schema.
    /// </summary>
    private static string GetSchemaItemType(
        IOpenApiSchema itemSchema,
        OpenApiDocument openApiDoc,
        string operationId,
        string projectName,
        string segment,
        SystemTypeConflictResolver systemTypeResolver,
        TypeConflictRegistry? registry)
    {
        // Handle reference
        if (itemSchema is OpenApiSchemaReference itemRef)
        {
            var refName = itemRef.Reference?.Id ?? itemRef.Id;
            if (!string.IsNullOrEmpty(refName))
            {
                var typeName = refName!.ToPascalCaseForDotNet();
                return ResolveModelTypeConflict(typeName, projectName, segment, systemTypeResolver);
            }
        }

        var itemSchemaType = itemSchema.GetSchemaType();

        // Handle inline object - use "ResponseItem" suffix for array items
        if (itemSchemaType == "object" && itemSchema.Properties != null && itemSchema.Properties.Count > 0)
        {
            return InlineSchemaExtractor.GenerateInlineTypeName(operationId, "ResponseItem");
        }

        // Try to find schema by Title property in components
        if (itemSchema is OpenApiSchema openApiSchema && !string.IsNullOrEmpty(openApiSchema.Title))
        {
            var schemas = openApiDoc.Components?.Schemas;
            if (schemas != null)
            {
                foreach (var kvp in schemas)
                {
                    if (kvp.Value is OpenApiSchema componentSchema &&
                        string.Equals(componentSchema.Title, openApiSchema.Title, StringComparison.Ordinal))
                    {
                        var typeName = kvp.Key.ToPascalCaseForDotNet();
                        return ResolveModelTypeConflict(typeName, projectName, segment, systemTypeResolver);
                    }
                }
            }
        }

        // Fall back to ToCSharpType for primitives
        return itemSchema.ToCSharpType(isRequired: true, registry);
    }
}