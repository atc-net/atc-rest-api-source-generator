namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI operations and converts them to ClassParameters for server DI registration class generation.
/// </summary>
public static class ServerDependencyInjectionExtractor
{
    /// <summary>
    /// Extracts server dependency injection class parameters from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>ClassParameters for the server DI registration class, or null if no paths exist.</returns>
    public static ClassParameters? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
        => Extract(openApiDoc, projectName, pathSegments: null, includeDeprecated: includeDeprecated);

    /// <summary>
    /// Extracts server dependency injection class parameters from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="pathSegments">List of path segments used for generating usings.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>ClassParameters for the server DI registration class, or null if no paths exist.</returns>
    public static ClassParameters? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        List<string>? pathSegments,
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

        // Collect all handler interfaces
        var handlerInterfaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in openApiDoc.Paths)
        {
            var pathItemInterface = path.Value;
            if (pathItemInterface is not OpenApiPathItem pathItem || pathItem.Operations == null)
            {
                continue;
            }

            foreach (var operation in pathItem.Operations)
            {
                var operationValue = operation.Value;
                if (operationValue == null)
                {
                    continue;
                }

                // Skip deprecated operations if not including them
                if (!includeDeprecated && operationValue.Deprecated)
                {
                    continue;
                }

                var httpMethod = operation
                    .Key
                    .ToString()
                    .ToUpperInvariant();

                var normalizedPath = path
                    .Key
                    .Replace("/", "_")
                    .Replace("{", string.Empty)
                    .Replace("}", string.Empty);

                var operationId = operationValue.OperationId ?? $"{httpMethod}{normalizedPath}";
                var handlerName = $"I{operationId.ToPascalCaseForDotNet()}Handler";
                handlerInterfaces.Add(handlerName);
            }
        }

        // Generate method content
        var methodContent = GenerateMethodContent(handlerInterfaces);

        // Build method parameters
        var methodParams = new List<ParameterBaseParameters>
        {
            new(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: "this IServiceCollection",
                IsNullableType: false,
                IsReferenceType: true,
                Name: "services",
                DefaultValue: null),
        };

        var method = new MethodParameters(
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: "IServiceCollection",
            Name: $"Add{projectName.ToPascalCaseForDotNet()}Handlers",
            Parameters: methodParams,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: methodContent);

        // Generate header content with usings for all path segments
        var headerContent = GenerateHeaderContent(projectName, pathSegments);

        return new ClassParameters(
            HeaderContent: headerContent,
            Namespace: $"{projectName}.Generated.DependencyInjection",
            DocumentationTags: null,
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicStaticClass,
            ClassTypeName: "ServiceCollectionExtensions",
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties: null,
            Methods: new List<MethodParameters> { method },
            GenerateToStringMethod: false);
    }

    private static string GenerateHeaderContent(
        string projectName,
        List<string>? pathSegments)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine();
        builder.AppendLine("using System.CodeDom.Compiler;");
        builder.AppendLine("using Microsoft.Extensions.DependencyInjection;");

        if (pathSegments is { Count: > 0 })
        {
            foreach (var segment in pathSegments.OrderBy(s => s, StringComparer.Ordinal))
            {
                builder.AppendLine($"using {projectName}.Generated.{segment}.Handlers;");
            }
        }
        else
        {
            builder.AppendLine($"using {projectName}.Generated.Handlers;");
        }

        return builder.ToString();
    }

    private static string GenerateMethodContent(
        HashSet<string> handlerInterfaces)
    {
        var builder = new StringBuilder();

        foreach (var handlerInterface in handlerInterfaces.OrderBy(h => h, StringComparer.Ordinal))
        {
            builder.AppendLine($"// Handler for {handlerInterface} must be registered in the Domain project");
            builder.AppendLine($"// services.AddScoped<{handlerInterface}, YourImplementation>();");
        }

        builder.AppendLine();
        builder.Append("return services;");

        return builder.ToString();
    }
}