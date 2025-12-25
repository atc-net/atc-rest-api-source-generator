// ReSharper disable InvertIf
// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI operations and converts them to InterfaceParameters for handler interface generation.
/// </summary>
public static class HandlerExtractor
{
    /// <summary>
    /// Extracts handler interfaces from OpenAPI document paths and operations.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>List of InterfaceParameters for each operation handler.</returns>
    public static List<InterfaceParameters>? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated = false)
        => Extract(openApiDoc, projectName, pathSegment: null, systemTypeResolver, includeDeprecated: includeDeprecated);

    /// <summary>
    /// Extracts handler interfaces from OpenAPI document paths and operations filtered by path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets"). If null, extracts all handlers.</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>List of InterfaceParameters for each operation handler in the path segment.</returns>
    public static List<InterfaceParameters>? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated = false)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (openApiDoc.Paths == null || openApiDoc.Paths.Count == 0)
        {
            return null;
        }

        var interfacesList = new List<InterfaceParameters>();
        var namespaceValue = NamespaceBuilder.ForHandlers(projectName, pathSegment);

        foreach (var path in openApiDoc.Paths)
        {
            var pathKey = path.Key;
            var pathItemInterface = path.Value;

            // Apply path segment filter if provided
            if (pathKey.ShouldSkipForPathSegment(pathSegment))
            {
                continue;
            }

            if (pathItemInterface is not OpenApiPathItem pathItem)
            {
                continue;
            }

            // Get path-level parameters (inherited by all operations on this path)
            var pathParameters = pathItem.Parameters;

            // Process each HTTP method (GET, POST, PUT, DELETE, etc.)
            if (pathItem.Operations is not null)
            {
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

                    var interfaceParams = ExtractHandlerInterface(
                        pathKey,
                        httpMethod,
                        operation.Value,
                        pathParameters,
                        projectName,
                        namespaceValue,
                        systemTypeResolver);

                    if (interfaceParams != null)
                    {
                        interfacesList.Add(interfaceParams);
                    }
                }
            }
        }

        return interfacesList.Count > 0
            ? interfacesList
            : null;
    }

    /// <summary>
    /// Extracts a single handler interface from an OpenAPI operation.
    /// </summary>
    /// <param name="path">The API path.</param>
    /// <param name="httpMethod">The HTTP method.</param>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="pathParameters">Path-level parameters inherited by this operation.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="namespaceValue">The namespace for the generated interface.</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    private static InterfaceParameters? ExtractHandlerInterface(
        string path,
        string httpMethod,
        OpenApiOperation? operation,
        IList<IOpenApiParameter>? pathParameters,
        string projectName,
        string namespaceValue,
        SystemTypeConflictResolver systemTypeResolver)
    {
        if (operation == null)
        {
            return null;
        }

        var normalizedPath = path
            .Replace("/", "_")
            .Replace("{", string.Empty)
            .Replace("}", string.Empty);

        var operationId = operation.OperationId ?? $"{httpMethod}{normalizedPath}";
        var handlerName = $"I{operationId.ToPascalCaseForDotNet()}Handler";
        var resultClassName = $"{operationId.ToPascalCaseForDotNet()}Result";
        var summary = operation.Summary ?? $"Handler for operation: {operationId}";

        // Determine if operation has parameters (operation-level OR path-level)
        var hasOperationParams = operation.Parameters is { Count: > 0 };
        var hasPathParams = pathParameters is { Count: > 0 };
        var hasParameters = hasOperationParams || hasPathParams;
        var hasRequestBody = operation.HasRequestBody();

        // Build method parameters
        var methodParameters = new List<ParameterBaseParameters>();

        // Add Parameters class if operation has parameters OR request body
        if (hasParameters || hasRequestBody)
        {
            var parameterClassName = $"{operationId.ToPascalCaseForDotNet()}Parameters";
            methodParameters.Add(new ParameterBaseParameters(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: parameterClassName,
                IsNullableType: false,
                IsReferenceType: true,
                Name: "parameters",
                DefaultValue: null));
        }

        methodParameters.Add(new ParameterBaseParameters(
            Attributes: null,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: "CancellationToken",
            IsNullableType: false,
            IsReferenceType: false,
            Name: "cancellationToken",
            DefaultValue: "default"));

        var taskTypeName = systemTypeResolver.EnsureFullNamespaceIfNeeded(nameof(Task));

        var executeMethod = new MethodParameters(
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.None,
            ReturnGenericTypeName: null,
            ReturnTypeName: $"{taskTypeName}<{resultClassName}>",
            Name: "ExecuteAsync",
            Parameters: methodParameters,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: null);

        // Build documentation
        var docTags = new CodeDocumentationTags(summary);

        // Build interface with GeneratedCode attribute
        var attributes = new List<AttributeParameters>
        {
            new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
        };

        return new InterfaceParameters(
            HeaderContent: HeaderBuilder.Simple,
            Namespace: namespaceValue,
            DocumentationTags: docTags,
            Attributes: attributes,
            DeclarationModifier: DeclarationModifiers.PublicInterface,
            InterfaceTypeName: handlerName,
            InheritedInterfaceTypeName: null,
            Properties: null,
            Methods: new List<MethodParameters> { executeMethod });
    }
}