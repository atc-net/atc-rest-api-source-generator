// ReSharper disable InvertIf
// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI webhooks and converts them to InterfaceParameters for webhook handler interface generation.
/// </summary>
public static class WebhookHandlerExtractor
{
    /// <summary>
    /// Extracts webhook handler interfaces from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing webhook definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated webhooks.</param>
    /// <returns>List of InterfaceParameters for each webhook handler.</returns>
    public static List<InterfaceParameters>? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated = false)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (!openApiDoc.HasWebhooks())
        {
            return null;
        }

        var interfacesList = new List<InterfaceParameters>();
        var namespaceValue = NamespaceBuilder.ForWebhookHandlers(projectName);

        foreach (var (webhookName, httpMethod, operation) in openApiDoc.GetAllWebhookOperations())
        {
            // Skip deprecated webhooks if not including them
            if (!includeDeprecated && operation.Deprecated)
            {
                continue;
            }

            var interfaceParams = ExtractWebhookHandlerInterface(
                webhookName,
                httpMethod,
                operation,
                projectName,
                namespaceValue,
                systemTypeResolver);

            if (interfaceParams != null)
            {
                interfacesList.Add(interfaceParams);
            }
        }

        return interfacesList.Count > 0 ? interfacesList : null;
    }

    private static InterfaceParameters? ExtractWebhookHandlerInterface(
        string webhookName,
        string httpMethod,
        OpenApiOperation operation,
        string projectName,
        string namespaceValue,
        SystemTypeConflictResolver systemTypeResolver)
    {
        if (operation == null)
        {
            return null;
        }

        // Get operation ID or generate from webhook name
        var operationId = operation.OperationId ?? $"On{webhookName.EnsureFirstCharacterToUpper()}";
        var handlerName = operationId.EnsureFirstCharacterToUpper();
        var interfaceName = $"I{handlerName}WebhookHandler";

        // Determine return type based on responses
        var resultTypeName = $"{handlerName}WebhookResult";

        // Determine parameter type
        var hasParameters = operation.RequestBody != null;
        var parameterTypeName = hasParameters ? $"{handlerName}WebhookParameters" : null;

        // Build the method signature
        var methodParams = new List<ParameterBaseParameters>();

        if (parameterTypeName != null)
        {
            methodParams.Add(new ParameterBaseParameters(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: parameterTypeName,
                IsNullableType: false,
                IsReferenceType: true,
                Name: "parameters",
                DefaultValue: null));
        }

        // Add cancellation token parameter
        methodParams.Add(new ParameterBaseParameters(
            Attributes: null,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: "CancellationToken",
            IsNullableType: false,
            IsReferenceType: false,
            Name: "cancellationToken",
            DefaultValue: "default"));

        // Build method definition
        var taskTypeName = systemTypeResolver.EnsureFullNamespaceIfNeeded(nameof(Task));
        var methodReturnType = $"{taskTypeName}<{resultTypeName}>";
        var methodName = "ExecuteAsync";

        var methodParameters = new MethodParameters(
            DocumentationTags: new CodeDocumentationTags(
                operation.Summary ?? $"Handles the {webhookName} webhook."),
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.None,
            ReturnGenericTypeName: null,
            ReturnTypeName: methodReturnType,
            Name: methodName,
            Parameters: methodParams,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: null);

        // Build interface with GeneratedCode attribute
        var attributes = new List<AttributeParameters>
        {
            new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
        };

        return new InterfaceParameters(
            HeaderContent: HeaderBuilder.Simple,
            Namespace: namespaceValue,
            DocumentationTags: new CodeDocumentationTags(
                $"Handler interface for the {webhookName} webhook."),
            Attributes: attributes,
            DeclarationModifier: DeclarationModifiers.PublicInterface,
            InterfaceTypeName: interfaceName,
            InheritedInterfaceTypeName: null,
            Properties: null,
            Methods: [methodParameters]);
    }
}