namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI paths and operations and converts them to ClassParameters for endpoint registration class generation.
/// Also provides extraction for endpoint definition mapping extension methods.
/// </summary>
public static class EndpointRegistrationExtractor
{
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
        if (endpointDefinitionClassNames is null || endpointDefinitionClassNames.Count == 0)
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

    /// <summary>
    /// Extracts a single endpoint definition mapping extension class containing one
    /// Map{Segment}Endpoints method per path segment.
    /// The class is placed in the root Endpoints namespace.
    /// </summary>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="segmentDefinitions">The endpoint definition class names grouped by resolved path segment.</param>
    /// <returns>ClassParameters for the consolidated endpoint mapping extension class.</returns>
    public static ClassParameters? ExtractConsolidatedEndpointMappingExtension(
        string projectName,
        IReadOnlyList<(string Segment, List<string> EndpointDefinitionClassNames)> segmentDefinitions)
    {
        if (segmentDefinitions is null || segmentDefinitions.Count == 0)
        {
            return null;
        }

        var methods = new List<MethodParameters>();

        foreach (var (segment, endpointDefinitionClassNames) in segmentDefinitions)
        {
            if (endpointDefinitionClassNames is null || endpointDefinitionClassNames.Count == 0)
            {
                continue;
            }

            var methodName = string.IsNullOrEmpty(segment)
                ? "MapApiEndpoints"
                : $"Map{segment}Endpoints";

            var (methodParams, _) = MethodParameterBuilder.BuildWebApplicationExtensionParameters();

            var methodDocParams = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "app", "The web application." },
            };

            methods.Add(new MethodParameters(
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
                Content: GenerateEndpointMappingMethodContent(endpointDefinitionClassNames)));
        }

        if (methods.Count == 0)
        {
            return null;
        }

        return new ClassParameters(
            HeaderContent: null,
            Namespace: NamespaceBuilder.ForEndpoints(projectName),
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
            Methods: methods,
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
}