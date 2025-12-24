namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI operations and converts them to ClassParameters for handler scaffold class generation.
/// </summary>
public static class HandlerScaffoldExtractor
{
    /// <summary>
    /// Extracts handler scaffold class parameters from OpenAPI operation.
    /// </summary>
    /// <param name="handlerName">The handler class name (e.g., "CreatePetsHandler").</param>
    /// <param name="handlerNamespace">The handler namespace.</param>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="pathItem">The OpenAPI path item (for path-level parameters).</param>
    /// <param name="operationId">The operation ID.</param>
    /// <param name="handlerSuffix">The handler suffix (e.g., "Handler").</param>
    /// <param name="stubImplementation">The stub implementation type.</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    /// <returns>ClassParameters for the handler scaffold class.</returns>
    public static ClassParameters Extract(
        string handlerName,
        string handlerNamespace,
        OpenApiOperation operation,
        IOpenApiPathItem? pathItem,
        string operationId,
        string handlerSuffix,
        string stubImplementation,
        SystemTypeConflictResolver systemTypeResolver)
    {
        var operationIdPascal = operationId.ToPascalCaseForDotNet();
        var interfaceName = $"I{operationIdPascal}{handlerSuffix}";
        var resultTypeName = $"{operationIdPascal}Result";

        // Determine method signature based on parameters and request body
        // Check both operation-level AND path-level parameters
        var hasOperationParams = operation.HasParameters();
        var hasPathParams = pathItem?.Parameters?.Count > 0;
        var hasParameters = hasOperationParams || hasPathParams;
        var hasRequestBody = operation.HasRequestBody();

        // Build ExecuteAsync method parameters
        var methodParams = new List<ParameterBaseParameters>();

        // Add Parameters class if operation has parameters OR request body
        if (hasParameters || hasRequestBody)
        {
            var parametersTypeName = $"{operationIdPascal}Parameters";
            methodParams.Add(new ParameterBaseParameters(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: parametersTypeName,
                IsNullableType: false,
                IsReferenceType: true,
                Name: "parameters",
                DefaultValue: null));
        }

        methodParams.Add(new ParameterBaseParameters(
            Attributes: null,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: "CancellationToken",
            IsNullableType: false,
            IsReferenceType: false,
            Name: "cancellationToken",
            DefaultValue: "default"));

        // Generate method body content based on stub type
        var taskTypeName = systemTypeResolver.EnsureFullNamespaceIfNeeded(nameof(Task));
        var methodContent = GenerateStubContent(resultTypeName, operationId, stubImplementation, taskTypeName);

        var method = new MethodParameters(
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            ReturnGenericTypeName: taskTypeName,
            ReturnTypeName: resultTypeName,
            Name: "ExecuteAsync",
            Parameters: methodParams,
            AlwaysBreakDownParameters: methodParams.Count > 1,
            UseExpressionBody: false,
            Content: methodContent);

        return new ClassParameters(
            HeaderContent: null, // Usings are in GlobalUsings.cs
            Namespace: handlerNamespace,
            DocumentationTags: new CodeDocumentationTags($"Handler business logic for the {operationIdPascal} operation."),
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicSealedClass,
            ClassTypeName: handlerName,
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: interfaceName,
            Constructors: null, // No constructors - commented example only
            Properties: null,
            Methods: new List<MethodParameters> { method },
            GenerateToStringMethod: false);
    }

    private static string GenerateStubContent(
        string resultTypeName,
        string operationId,
        string stubImplementation,
        string taskTypeName)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"// TODO: Implement {operationId} logic");

        switch (stubImplementation.ToLowerInvariant())
        {
            case "error-501":
                builder.AppendLine("// NOTE: Requires an 'Error' model in your OpenAPI specification");
                builder.AppendLine($"var error = new Error(501, \"{operationId} not implemented\");");
                builder.Append($"return {taskTypeName}.FromResult({resultTypeName}.Error(error));");
                break;

            case "default-value":
                builder.Append($"return {taskTypeName}.FromResult({resultTypeName}.Ok(default!));");
                break;

            case "throw-not-implemented":
                builder.Append($"throw new NotImplementedException(\"{operationId} not implemented\");");
                break;
            default:
                builder.Append($"throw new NotImplementedException(\"{operationId} not implemented!\");");
                break;
        }

        return builder.ToString();
    }
}