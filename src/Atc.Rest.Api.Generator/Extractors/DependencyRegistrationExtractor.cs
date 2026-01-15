namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts handler registrations and converts them to ClassParameters for DI registration class generation.
/// </summary>
public static class DependencyRegistrationExtractor
{
    /// <summary>
    /// Extracts dependency registration class parameters from handler information.
    /// </summary>
    /// <param name="rootNamespace">The root namespace for the DI registration class.</param>
    /// <param name="assemblyName">The assembly name for method naming.</param>
    /// <param name="handlers">List of handler information (OperationId, HandlerName, HandlerNamespace).</param>
    /// <param name="handlerSuffix">The handler suffix (e.g., "Handler").</param>
    /// <param name="handlerInterfaceNamespaces">Optional list of handler interface namespaces for using statements.</param>
    /// <returns>ClassParameters for the dependency registration class.</returns>
    public static ClassParameters? Extract(
        string rootNamespace,
        string assemblyName,
        List<(string OperationId, string HandlerName, string HandlerNamespace)> handlers,
        string handlerSuffix,
        List<string>? handlerInterfaceNamespaces = null)
    {
        if (handlers == null || handlers.Count == 0)
        {
            return null;
        }

        var methodSuffix = GetLastAssemblyNameTerm(assemblyName);
        var methodName = $"AddApiHandlersFrom{methodSuffix}";

        // Build method content
        var methodContent = GenerateMethodContent(handlers, handlerSuffix);

        // Build method parameters
        var (methodParams, methodDocParams) = MethodParameterBuilder.BuildServiceCollectionExtensionParameters();

        var method = new MethodParameters(
            DocumentationTags: new CodeDocumentationTags(
                summary: "Registers all API handler implementations from this assembly.",
                parameters: methodDocParams,
                remark: null,
                code: null,
                example: null,
                exceptions: null,
                @return: "The service collection for method chaining."),
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: "IServiceCollection",
            Name: methodName,
            Parameters: methodParams,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: methodContent);

        // Build header with handler interface namespaces
        var headerUsings = new List<string>
        {
            NamespaceConstants.SystemCodeDomCompiler,
            NamespaceConstants.MicrosoftExtensionsDependencyInjection,
        };

        if (handlerInterfaceNamespaces is { Count: > 0 })
        {
            headerUsings.AddRange(handlerInterfaceNamespaces);
        }

        return new ClassParameters(
            HeaderContent: HeaderBuilder.WithUsings(headerUsings.ToArray()),
            Namespace: rootNamespace,
            DocumentationTags: new CodeDocumentationTags("Extension methods for registering API handlers in the dependency injection container."),
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicStaticClass,
            ClassTypeName: "ApiHandlerServiceCollectionExtensions",
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties: null,
            Methods: new List<MethodParameters> { method },
            GenerateToStringMethod: false);
    }

    private static string GenerateMethodContent(
        List<(string OperationId, string HandlerName, string HandlerNamespace)> handlers,
        string handlerSuffix)
    {
        var builder = new StringBuilder();

        // Generate registration for each handler (sorted by operation ID)
        foreach (var handler in handlers.OrderBy(x => x.OperationId, StringComparer.Ordinal))
        {
            var operationIdPascal = handler.OperationId.ToPascalCaseForDotNet();
            var interfaceName = $"I{operationIdPascal}{handlerSuffix}";
            var implementationName = handler.HandlerName;
            var fullNamespace = handler.HandlerNamespace;

            builder.AppendLine($"services.AddScoped<{interfaceName}, global::{fullNamespace}.{implementationName}>();");
        }

        builder.AppendLine();
        builder.Append("return services;");

        return builder.ToString();
    }

    private static string GetLastAssemblyNameTerm(string assemblyName)
    {
        var parts = assemblyName.Split('.');
        if (parts.Length > 0)
        {
            return parts[parts.Length - 1];
        }

        return "Assembly";
    }
}