namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts validator registrations and converts them to ClassParameters for DI registration class generation.
/// </summary>
public static class ValidatorDependencyRegistrationExtractor
{
    /// <summary>
    /// Extracts dependency registration class parameters from validator information.
    /// </summary>
    /// <param name="rootNamespace">The root namespace for the DI registration class.</param>
    /// <param name="assemblyName">The assembly name for method naming.</param>
    /// <param name="validators">List of validator information (ValidatorName, ValidatorNamespace, ModelType).</param>
    /// <returns>ClassParameters for the dependency registration class.</returns>
    public static ClassParameters? Extract(
        string rootNamespace,
        string assemblyName,
        List<(string ValidatorName, string ValidatorNamespace, string ModelType)> validators)
    {
        if (validators == null || validators.Count == 0)
        {
            return null;
        }

        var methodSuffix = GetLastAssemblyNameTerm(assemblyName);
        var methodName = $"AddApiValidatorsFrom{methodSuffix}";

        // Build method content
        var methodContent = GenerateMethodContent(validators);

        // Build method parameters
        var (methodParams, methodDocParams) = MethodParameterBuilder.BuildServiceCollectionExtensionParameters();

        var method = new MethodParameters(
            DocumentationTags: new CodeDocumentationTags(
                summary: "Registers all FluentValidation validators from this assembly.",
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

        return new ClassParameters(
            HeaderContent: HeaderBuilder.ForValidatorDependencyInjection(),
            Namespace: rootNamespace,
            DocumentationTags: new CodeDocumentationTags("Extension methods for registering FluentValidation validators in the dependency injection container."),
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicStaticClass,
            ClassTypeName: "ApiValidatorServiceCollectionExtensions",
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
        List<(string ValidatorName, string ValidatorNamespace, string ModelType)> validators)
    {
        var builder = new StringBuilder();

        // Generate registration for each validator (sorted by validator name)
        foreach (var validator in validators.OrderBy(x => x.ValidatorName, StringComparer.Ordinal))
        {
            var fullValidatorType = $"{validator.ValidatorNamespace}.{validator.ValidatorName}";
            builder.AppendLine($"services.AddSingleton<IValidator<{validator.ModelType}>, {fullValidatorType}>();");
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