namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts and generates custom error response model from configuration.
/// Used when CustomErrorResponseModel is specified in the client configuration.
/// </summary>
public static class CustomErrorResponseExtractor
{
    /// <summary>
    /// Extracts custom error response class parameters from configuration.
    /// </summary>
    /// <param name="config">The custom error response model configuration.</param>
    /// <param name="namespaceName">The namespace for the generated class.</param>
    /// <returns>ClassParameters for the custom error response model.</returns>
    public static ClassParameters Extract(
        CustomErrorResponseModelConfig config,
        string namespaceName)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var propertyList = new List<PropertyParameters>();

        if (config.Schema != null)
        {
            foreach (var prop in config.Schema)
            {
                var propName = prop.Key.ToPascalCaseForDotNet();
                var dataType = prop.Value.DataType ?? "string?";

                // Extract nullability from the type name
                var isNullableType = dataType.EndsWith("?", StringComparison.Ordinal);
                var cleanTypeName = isNullableType
                    ? dataType.Substring(0, dataType.Length - 1)
                    : dataType;

                var isReferenceType = cleanTypeName.IsReferenceType();

                // Create documentation tags if description is provided
                CodeDocumentationTags? docTags = null;
                if (!string.IsNullOrEmpty(prop.Value.Description))
                {
                    docTags = new CodeDocumentationTags(prop.Value.Description!);
                }

                // Add JsonPropertyName attribute if property name differs from JSON name
                IList<AttributeParameters>? attributes = null;
                if (prop.Key != propName)
                {
                    attributes = new List<AttributeParameters>
                    {
                        new("JsonPropertyName", $"\"{prop.Key}\""),
                    };
                }

                propertyList.Add(new PropertyParameters(
                    DocumentationTags: docTags,
                    Attributes: attributes,
                    DeclarationModifier: DeclarationModifiers.Public,
                    GenericTypeName: null,
                    TypeName: cleanTypeName,
                    IsNullableType: isNullableType,
                    Name: propName,
                    JsonName: prop.Key != propName ? prop.Key : null,
                    DefaultValue: null,
                    IsReferenceType: isReferenceType,
                    IsGenericListType: false,
                    UseAutoProperty: true,
                    UseGet: true,
                    UseSet: true,
                    UseExpressionBody: false,
                    UseRequired: false,
                    Content: null));
            }
        }

        // Create documentation tags if description is provided
        CodeDocumentationTags? classDocTags = null;
        if (!string.IsNullOrEmpty(config.Description))
        {
            classDocTags = new CodeDocumentationTags(config.Description!);
        }

        return new ClassParameters(
            HeaderContent: BuildHeaderContent(),
            Namespace: namespaceName,
            DocumentationTags: classDocTags,
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicClass,
            ClassTypeName: config.Name,
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties: propertyList,
            Methods: null,
            GenerateToStringMethod: false);
    }

    /// <summary>
    /// Generates the custom error response class code.
    /// </summary>
    /// <param name="config">The custom error response model configuration.</param>
    /// <param name="namespaceName">The namespace for the generated class.</param>
    /// <returns>The generated C# code for the custom error response model.</returns>
    public static string Generate(
        CustomErrorResponseModelConfig config,
        string namespaceName)
    {
        var classParams = Extract(config, namespaceName);
        var generator = new GenerateContentForClass(
            new CodeDocumentationTagsGenerator(),
            classParams);

        return generator.Generate();
    }

    /// <summary>
    /// Builds the header content for generated custom error response file.
    /// </summary>
    private static string BuildHeaderContent()
        => HeaderBuilder.WithUsings(
            "System.CodeDom.Compiler",
            "System.Text.Json.Serialization");
}