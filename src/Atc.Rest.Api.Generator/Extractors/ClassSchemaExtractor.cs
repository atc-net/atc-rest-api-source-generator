namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI schema definitions and converts them to ClassParameters for partial class generation.
/// Used when UsePartialClassForModels is enabled in the client configuration.
/// Supports x-implements extension for interface implementation.
/// </summary>
public static class ClassSchemaExtractor
{
    /// <summary>
    /// Extracts model classes from OpenAPI document components.
    /// Generates partial classes with properties, supporting x-implements for interface implementation.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="includeDeprecated">Whether to include deprecated schemas.</param>
    /// <returns>List of ClassParameters for each model, or null if no schemas exist.</returns>
    public static List<ClassParameters>? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
        => ExtractIndividual(openApiDoc, schemaFilter: null, includeDeprecated: includeDeprecated);

    /// <summary>
    /// Extracts model classes from OpenAPI document components filtered by path segment.
    /// Only includes schemas that are used by operations in the specified path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets").</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated schemas.</param>
    /// <returns>List of ClassParameters for each model used by operations in the path segment, or null if no schemas exist.</returns>
    public static List<ClassParameters>? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false)
    {
        if (string.IsNullOrEmpty(pathSegment))
        {
            return ExtractIndividual(openApiDoc, schemaFilter: null, registry: registry, includeDeprecated: includeDeprecated);
        }

        var usedSchemas = PathSegmentHelper.GetSchemasUsedBySegment(openApiDoc, pathSegment);
        return ExtractIndividual(openApiDoc, usedSchemas, registry, includeDeprecated);
    }

    /// <summary>
    /// Extracts individual class parameters from OpenAPI document components.
    /// </summary>
    private static List<ClassParameters>? ExtractIndividual(
        OpenApiDocument openApiDoc,
        HashSet<string>? schemaFilter,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false)
    {
        if (openApiDoc.Components?.Schemas == null || openApiDoc.Components.Schemas.Count == 0)
        {
            return null;
        }

        var classParametersList = new List<ClassParameters>();

        foreach (var schema in openApiDoc.Components.Schemas)
        {
            var schemaName = schema.Key;
            var schemaValue = schema.Value;

            // Apply schema filter if provided
            if (schemaFilter != null && !schemaFilter.Contains(schemaName))
            {
                continue;
            }

            // Skip schema references as they point to other schemas
            if (schemaValue is OpenApiSchemaReference)
            {
                continue;
            }

            // Skip deprecated schemas if not including them
            if (!includeDeprecated && schemaValue is OpenApiSchema { Deprecated: true })
            {
                continue;
            }

            // Skip polymorphic base schemas (oneOf/anyOf)
            if (schemaValue.HasPolymorphicComposition())
            {
                continue;
            }

            if (schemaValue is OpenApiSchema actualSchema)
            {
                // Skip array schemas (like "Pets"), they're handled inline
                if (actualSchema.Type == JsonSchemaType.Array)
                {
                    continue;
                }

                // Skip enum schemas - handled by EnumExtractor
                if (actualSchema.Type == JsonSchemaType.String && actualSchema.Enum is { Count: > 0 })
                {
                    continue;
                }

                if (actualSchema.Type == JsonSchemaType.Object)
                {
                    var classParams = ExtractClassFromSchema(schemaName, actualSchema, registry);
                    if (classParams != null)
                    {
                        classParametersList.Add(classParams);
                    }
                }
            }
        }

        return classParametersList.Count > 0 ? classParametersList : null;
    }

    /// <summary>
    /// Extracts a single class definition from an OpenAPI schema.
    /// Supports x-implements extension for interface implementation.
    /// </summary>
    private static ClassParameters? ExtractClassFromSchema(
        string schemaName,
        OpenApiSchema schema,
        TypeConflictRegistry? registry = null)
    {
        var properties = schema.Properties?.ToList() ?? [];
        var required = schema.Required ?? new HashSet<string>(StringComparer.Ordinal);

        if (properties.Count == 0)
        {
            return null;
        }

        // Get interfaces from x-implements extension
        var interfaces = schema.GetImplementedInterfaces();
        var interfaceList = interfaces.Count > 0 ? string.Join(", ", interfaces) : null;

        var propertyList = new List<PropertyParameters>();

        foreach (var prop in properties)
        {
            var propName = prop.Key.ToPascalCaseForDotNet();
            var isRequired = required.Contains(prop.Key, StringComparer.Ordinal);

            // Check for additionalProperties (Dictionary types) first
            var csharpType = prop.Value.HasAdditionalProperties()
                ? prop.Value.GetDictionaryTypeString(isRequired, registry) ?? prop.Value.ToCSharpTypeForModel(isRequired, registry)
                : prop.Value.ToCSharpTypeForModel(isRequired, registry);

            // Extract nullability from the type name
            var isNullableType = csharpType.EndsWith("?", StringComparison.Ordinal);
            var cleanTypeName = isNullableType
                ? csharpType.Substring(0, csharpType.Length - 1)
                : csharpType;

            // Get validation attributes from OpenAPI schema constraints
            var validationAttributes = prop.Value.GetValidationAttributes(isRequired);

            // Convert validation attributes to AttributeParameters
            IList<AttributeParameters>? attributes = null;
            if (validationAttributes.Count > 0)
            {
                attributes = validationAttributes
                    .Select(attr => new AttributeParameters(attr, null))
                    .ToList();
            }

            // Use TypeHelpers extension methods for type classification
            var isReferenceType = cleanTypeName.IsReferenceType();

            // Extract default value from schema
            var defaultValue = ExtractSchemaDefault(prop.Value, cleanTypeName);

            propertyList.Add(new PropertyParameters(
                DocumentationTags: null,
                Attributes: attributes,
                DeclarationModifier: DeclarationModifiers.Public,
                GenericTypeName: null,
                TypeName: cleanTypeName,
                IsNullableType: isNullableType,
                Name: propName,
                JsonName: prop.Key != propName ? prop.Key : null,
                DefaultValue: defaultValue,
                IsReferenceType: isReferenceType,
                IsGenericListType: false,
                UseAutoProperty: true,
                UseGet: true,
                UseSet: true,
                UseExpressionBody: false,
                UseRequired: false,
                Content: null));
        }

        return new ClassParameters(
            HeaderContent: BuildHeaderContent(propertyList),
            Namespace: string.Empty, // Will be set by caller
            DocumentationTags: null,
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicPartialClass,
            ClassTypeName: schemaName,
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: interfaceList,
            Constructors: null,
            Properties: propertyList,
            Methods: null,
            GenerateToStringMethod: false);
    }

    /// <summary>
    /// Extracts the default value from an OpenAPI schema and formats it as a C# literal.
    /// </summary>
    private static string? ExtractSchemaDefault(
        IOpenApiSchema? schemaInterface,
        string typeName)
    {
        if (schemaInterface is not OpenApiSchema schema)
        {
            return null;
        }

        var defaultValue = schema.Default;
        if (defaultValue == null)
        {
            return null;
        }

        var rawValue = defaultValue.ToString();
        if (string.IsNullOrEmpty(rawValue))
        {
            return null;
        }

        // String types - return unquoted value
        if (typeName == "string")
        {
            if (rawValue.StartsWith("\"", StringComparison.Ordinal) && rawValue.EndsWith("\"", StringComparison.Ordinal) && rawValue.Length >= 2)
            {
                return rawValue.Substring(1, rawValue.Length - 2);
            }

            return rawValue;
        }

        // Boolean values need lowercase in C#
        if (typeName == "bool")
        {
            return rawValue.ToLowerInvariant();
        }

        // Numeric types can use the raw value directly
        if (CSharpTypeHelper.IsNumericType(typeName))
        {
            return rawValue;
        }

        return null;
    }

    /// <summary>
    /// Builds the header content for generated class files.
    /// </summary>
    private static string BuildHeaderContent(
        List<PropertyParameters> properties)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Check if any property uses IFormFile or IFormFileCollection
        var usesFormFile = properties.Any(p =>
            p.TypeName.IndexOf("IFormFile", StringComparison.Ordinal) >= 0);

        // Check if any property uses Dictionary types
        var usesDictionary = properties.Any(p =>
            p.TypeName.StartsWith("Dictionary<", StringComparison.Ordinal));

        if (usesFormFile)
        {
            sb.AppendLine("using Microsoft.AspNetCore.Http;");
        }

        sb.AppendLine("using System.CodeDom.Compiler;");

        if (usesDictionary)
        {
            sb.AppendLine("using System.Collections.Generic;");
        }

        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();

        return sb.ToString();
    }
}