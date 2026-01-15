// ReSharper disable InvertIf
// ReSharper disable RedundantSwitchExpressionArms
// ReSharper disable GrammarMistakeInComment
// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable ConvertIfStatementToSwitchStatement
namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI operation parameters and converts them to RecordParameters for parameter binding records.
/// </summary>
public static class OperationParameterExtractor
{
    /// <summary>
    /// Extracts parameter DTO records from OpenAPI document paths and operations.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>RecordsParameters containing all parameter records, or null if no parameters exist.</returns>
    public static RecordsParameters? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false)
    {
        var recordsList = ExtractIndividual(openApiDoc, projectName, pathSegment: null, registry: registry, includeBindingAttributes: true, namespaceSubFolder: "Parameters", includeDeprecated: includeDeprecated);

        if (recordsList == null || recordsList.Count == 0)
        {
            return null;
        }

        var namespaceValue = NamespaceBuilder.ForParameters(projectName);
        var modelsNamespace = NamespaceBuilder.ForModels(projectName);
        var headerContent = BuildHeaderContent(includeBindingAttributes: true, modelsNamespace);

        return new RecordsParameters(
            HeaderContent: headerContent,
            Namespace: namespaceValue,
            DocumentationTags: null,
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters: recordsList);
    }

    /// <summary>
    /// Extracts parameter DTO records from OpenAPI document paths and operations filtered by path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets"). If null, extracts all parameters.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <param name="includeSharedModelsUsing">Whether to include the shared models namespace in the using directives.</param>
    /// <param name="includeSegmentModelsUsing">Whether to include the segment-specific models namespace in the using directives.</param>
    /// <returns>RecordsParameters containing parameter records for the path segment, or null if no parameters exist.</returns>
    public static RecordsParameters? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false,
        bool includeSharedModelsUsing = false,
        bool includeSegmentModelsUsing = true)
    {
        var recordsList = ExtractIndividual(openApiDoc, projectName, pathSegment, registry: registry, includeBindingAttributes: true, namespaceSubFolder: "Parameters", includeDeprecated: includeDeprecated);

        if (recordsList == null || recordsList.Count == 0)
        {
            return null;
        }

        var namespaceValue = NamespaceBuilder.ForParameters(projectName, pathSegment);

        // Only include segment-specific models namespace if there are segment-specific models
        var segmentModelsNamespace = includeSegmentModelsUsing && !string.IsNullOrEmpty(pathSegment)
            ? NamespaceBuilder.ForModels(projectName, pathSegment)
            : null;

        // Only include shared models using if requested AND there's a path segment
        // (no point including root models using if we're already in the root namespace)
        var sharedModelsNamespace = includeSharedModelsUsing && !string.IsNullOrEmpty(pathSegment)
            ? NamespaceBuilder.ForModels(projectName)
            : null;
        var headerContent = BuildHeaderContent(includeBindingAttributes: true, segmentModelsNamespace, sharedModelsNamespace);

        return new RecordsParameters(
            HeaderContent: headerContent,
            Namespace: namespaceValue,
            DocumentationTags: null,
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters: recordsList);
    }

    /// <summary>
    /// Extracts individual parameter records from OpenAPI document paths and operations.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>List of RecordParameters for each operation that has parameters.</returns>
    public static List<RecordParameters>? ExtractIndividual(
        OpenApiDocument openApiDoc,
        string projectName,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false)
        => ExtractIndividual(openApiDoc, projectName, pathSegment: null, registry: registry, includeBindingAttributes: true, namespaceSubFolder: "Parameters", includeDeprecated: includeDeprecated);

    /// <summary>
    /// Extracts individual parameter records from OpenAPI document paths and operations filtered by path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets"). If null, extracts all parameters.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>List of RecordParameters for each operation that has parameters in the path segment.</returns>
    public static List<RecordParameters>? ExtractIndividual(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false)
        => ExtractIndividual(openApiDoc, projectName, pathSegment, registry: registry, includeBindingAttributes: true, namespaceSubFolder: "Parameters", includeDeprecated: includeDeprecated);

    /// <summary>
    /// Extracts individual parameter records from OpenAPI document paths and operations filtered by path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets"). If null, extracts all parameters.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeBindingAttributes">If true, includes ASP.NET Core binding attributes ([FromQuery], [FromRoute], etc.). Set to false for client DTOs.</param>
    /// <param name="namespaceSubFolder">The namespace subfolder (e.g., "Parameters" for server, "Client" for client).</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>List of RecordParameters for each operation that has parameters in the path segment.</returns>
    public static List<RecordParameters>? ExtractIndividual(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry,
        bool includeBindingAttributes,
        string namespaceSubFolder,
        bool includeDeprecated = false)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (openApiDoc.Paths == null)
        {
            return null;
        }

        var recordsList = new List<RecordParameters>();

        foreach (var path in openApiDoc.Paths)
        {
            var pathKey = path.Key;

            // Apply path segment filter if provided
            if (!string.IsNullOrEmpty(pathSegment))
            {
                var currentSegment = PathSegmentHelper.GetFirstPathSegment(pathKey);
                if (!currentSegment.Equals(pathSegment, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            // Cast to concrete type to access Operations
            if (path.Value is not OpenApiPathItem pathItem || pathItem.Operations == null)
            {
                continue;
            }

            // Get path-level parameters (inherited by all operations on this path)
            var pathParameters = pathItem.Parameters;

            foreach (var operation in pathItem.Operations)
            {
                var operationValue = operation.Value;

                // Skip deprecated operations if not including them
                if (!includeDeprecated && operationValue?.Deprecated == true)
                {
                    continue;
                }

                var operationId = operationValue?.OperationId;

                if (string.IsNullOrEmpty(operationId))
                {
                    continue;
                }

                // Check if operation has parameters (operation-level OR path-level) OR request body
                var hasOperationParams = operationValue!.Parameters is { Count: > 0 };
                var hasPathParams = pathParameters is { Count: > 0 };
                var hasQueryRouteParams = hasOperationParams || hasPathParams;
                var hasRequestBody = operationValue.HasRequestBody();

                if (!hasQueryRouteParams && !hasRequestBody)
                {
                    continue;
                }

                var recordParams = ExtractRequestParameter(
                    operationValue,
                    pathParameters,
                    operationId!,
                    registry,
                    includeBindingAttributes);

                if (recordParams != null)
                {
                    recordsList.Add(recordParams);
                }
            }
        }

        return recordsList.Count > 0 ? recordsList : null;
    }

    /// <summary>
    /// Builds the header content for the generated file.
    /// </summary>
    /// <param name="includeBindingAttributes">Whether to include ASP.NET Core binding attributes.</param>
    /// <param name="segmentModelsNamespace">Optional path-segment-specific models namespace (null if no segment-specific models exist).</param>
    /// <param name="sharedModelsNamespace">Optional shared models namespace for types in the root namespace.</param>
    private static string BuildHeaderContent(
        bool includeBindingAttributes,
        string? segmentModelsNamespace,
        string? sharedModelsNamespace = null)
    {
        var headerBuilder = new StringBuilder();
        headerBuilder.Append("// <auto-generated />\r\n#nullable enable\r\n\r\n");
        headerBuilder.Append("using System.CodeDom.Compiler;\r\n");
        headerBuilder.Append("using System.ComponentModel;\r\n");
        headerBuilder.Append("using System.ComponentModel.DataAnnotations;\r\n");

        // Always include System.IO for Stream types (client-side or server-side octet-stream)
        headerBuilder.Append("using System.IO;\r\n");

        if (includeBindingAttributes)
        {
            // Server-side: include Http namespace for IFormFile
            headerBuilder.Append("using Microsoft.AspNetCore.Http;\r\n");
            headerBuilder.Append("using Microsoft.AspNetCore.Mvc;\r\n");
        }

        // Include shared models namespace (for global schemas like enums)
        if (!string.IsNullOrEmpty(sharedModelsNamespace) &&
            !sharedModelsNamespace!.Equals(segmentModelsNamespace, StringComparison.Ordinal))
        {
            headerBuilder.Append($"using {sharedModelsNamespace};\r\n");
        }

        // Include segment-specific models namespace only if it exists
        if (!string.IsNullOrEmpty(segmentModelsNamespace))
        {
            headerBuilder.Append($"using {segmentModelsNamespace};\r\n");
        }

        headerBuilder.Append("\r\n");

        return headerBuilder.ToString();
    }

    /// <summary>
    /// Extracts a single request parameter record from an OpenAPI operation.
    /// </summary>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="pathParameters">Path-level parameters inherited by this operation.</param>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="registry">Optional conflict registry.</param>
    /// <param name="includeBindingAttributes">Whether to include ASP.NET Core binding attributes.</param>
    private static RecordParameters ExtractRequestParameter(
        OpenApiOperation operation,
        IList<IOpenApiParameter>? pathParameters,
        string operationId,
        TypeConflictRegistry? registry,
        bool includeBindingAttributes)
    {
        var recordName = $"{operationId.ToPascalCaseForDotNet()}Parameters";

        // Build multi-line summary matching old generator format:
        // Parameters for operation request.
        // Description: {summary}.
        // Operation: {operationId}.
        var operationDescription = operation.Summary ?? operationId;
        var summary = $"Parameters for operation request.\nDescription: {operationDescription}.\nOperation: {operationId.ToPascalCaseForDotNet()}.";

        // Build parameters from operation parameters
        var parameters = new List<ParameterBaseParameters>();

        // Combine path-level and operation-level parameters
        // Path parameters are processed first, then operation parameters
        var allParameters = new List<IOpenApiParameter>();
        if (pathParameters != null)
        {
            allParameters.AddRange(pathParameters);
        }

        if (operation.Parameters != null)
        {
            // Operation parameters can override path parameters with the same name
            foreach (var opParam in operation.Parameters)
            {
                var opParamName = opParam.GetName();
                if (!string.IsNullOrEmpty(opParamName))
                {
                    // Remove any path parameter with the same name (operation overrides)
                    allParameters.RemoveAll(p => p.GetName() == opParamName);
                }

                allParameters.Add(opParam);
            }
        }

        // Add query/route/header parameters if they exist
        if (allParameters.Count > 0)
        {
            foreach (var parameterOrRef in allParameters)
            {
                // Resolve parameter reference if needed
                var resolved = parameterOrRef.Resolve();
                var parameter = resolved.Parameter;
                var referenceId = resolved.ReferenceId;

                // Skip if we couldn't resolve the parameter or it has no name
                if (parameter == null || string.IsNullOrEmpty(parameter.Name))
                {
                    continue;
                }

                var paramLocation = parameter.In ?? ParameterLocation.Query;

                // Determine property name based on parameter type
                string propName;
                if (paramLocation == ParameterLocation.Header)
                {
                    // For headers: use reference ID if available, otherwise strip x- prefix
                    // This allows x-continuation header to become Continuation property
                    propName = !string.IsNullOrEmpty(referenceId)
                        ? referenceId!.ToPascalCaseForDotNet()
                        : parameter.Name!.ToHeaderPropertyName();
                }
                else
                {
                    propName = parameter.Name?.ToPascalCaseForDotNet()!;
                }

                if (propName is null)
                {
                    continue;
                }

                var paramType = MapOpenApiTypeToCSharp(parameter.Schema!, parameter.Required, registry);

                // Extract nullability from the type name - the code generation library handles adding "?"
                var isNullableType = paramType.EndsWith("?", StringComparison.Ordinal);
                var cleanTypeName = isNullableType
                    ? paramType.Substring(0, paramType.Length - 1)
                    : paramType;

                // Build attributes list
                var attributes = new List<AttributeParameters>();

                // Add binding attributes only for server-side parameters
                if (includeBindingAttributes)
                {
                    switch (paramLocation)
                    {
                        // Add binding attribute
                        case ParameterLocation.Query:
                            attributes.Add(new AttributeParameters(
                                "FromQuery",
                                $"Name = \"{parameter.Name}\""));
                            break;
                        case ParameterLocation.Path:
                            attributes.Add(new AttributeParameters(
                                "FromRoute",
                                $"Name = \"{parameter.Name}\""));
                            break;
                        case ParameterLocation.Header:
                            attributes.Add(new AttributeParameters(
                                "FromHeader",
                                $"Name = \"{parameter.Name}\""));
                            break;
                        case ParameterLocation.Cookie:
                            attributes.Add(new AttributeParameters(
                                "FromCookie",
                                $"Name = \"{parameter.Name}\""));
                            break;
                    }
                }

                // Add validation attributes from OpenAPI schema constraints
                var validationAttributes = parameter.Schema!.GetValidationAttributes(parameter.Required);
                attributes.AddRange(
                    validationAttributes.Select(
                        validationAttr => new AttributeParameters(validationAttr, null)));

                // Extract schema default value if available
                var schemaDefault = ExtractSchemaDefault(parameter.Schema, cleanTypeName);

                // Add DefaultValue attribute when schema has a default (for OpenAPI/Scalar UI)
                if (schemaDefault != null)
                {
                    var defaultAttrValue = FormatDefaultValueForAttribute(schemaDefault, cleanTypeName);
                    attributes.Add(new AttributeParameters("DefaultValue", defaultAttrValue));
                }

                // Determine property initializer (use schema default if available)
                var defaultValue = GetDefaultValue(cleanTypeName, parameter.Required, parameter.Schema, isNullableType, schemaDefault);

                parameters.Add(
                    new ParameterBaseParameters(
                        Attributes: attributes.Count > 0 ? attributes : null,
                        GenericTypeName: null,
                        IsGenericListType: false,
                        TypeName: cleanTypeName,
                        IsNullableType: isNullableType,
                        IsReferenceType: IsReferenceType(cleanTypeName),
                        Name: propName,
                        DefaultValue: defaultValue));
            }
        }

        // Add request body as Request/File property if present
        if (operation.HasRequestBody())
        {
            var (requestBodySchema, contentType) = operation.GetRequestBodySchemaWithContentType();
            if (requestBodySchema != null)
            {
                var (isFile, isCollection) = requestBodySchema.GetFileUploadInfo();

                // Request bodies are always treated as required to match old generator behavior.
                // The OpenAPI spec defaults requestBody.required to false, but we can't easily
                // distinguish between "explicitly set to false" and "defaulted to false".
                // If you need optional request bodies, use a nullable schema type in your spec.
                var isRequired = true;

                // Only treat as direct file upload if the schema itself is binary/file type
                // Schema references (like FileAsFormDataRequest) should use Request pattern even with multipart/form-data
                var isDirectFileUpload = isFile;

                string bodyType;
                string propertyName;
                var bodyAttributes = new List<AttributeParameters>();

                if (isDirectFileUpload)
                {
                    // File uploads: type depends on content type and server/client mode
                    // - multipart/form-data: IFormFile/IFormFileCollection (server), Stream/Stream[] (client)
                    // - application/octet-stream: Stream (both server and client) - IFormFile doesn't work with octet-stream
                    var isOctetStream = contentType?.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase) == true;

                    if (includeBindingAttributes && !isOctetStream)
                    {
                        // Server-side with multipart/form-data: IFormFile or IFormFileCollection
                        // No [FromBody] attribute needed - ASP.NET Core binds it automatically
                        var baseType = isCollection ? "IFormFileCollection" : "IFormFile";
                        bodyType = isRequired ? baseType : $"{baseType}?";
                    }
                    else
                    {
                        // Client-side OR server-side with application/octet-stream: Stream or Stream[]
                        var baseType = isCollection ? "Stream[]" : "Stream";
                        bodyType = isRequired ? baseType : $"{baseType}?";

                        // Server-side with application/octet-stream needs [FromBody] to bind Stream from request body
                        if (includeBindingAttributes && isOctetStream)
                        {
                            bodyAttributes.Add(new AttributeParameters("FromBody", null));
                        }
                    }

                    propertyName = "File";
                }
                else
                {
                    // JSON body with [FromBody]
                    bodyType = requestBodySchema.ToCSharpType(isRequired, registry);
                    propertyName = "Request";

                    // Add binding attribute only for server-side parameters
                    if (includeBindingAttributes)
                    {
                        bodyAttributes.Add(new AttributeParameters("FromBody", null));
                    }
                }

                // Extract nullability from the type name - the code generation library handles adding "?"
                var bodyIsNullable = bodyType.EndsWith("?", StringComparison.Ordinal);
                var bodyCleanTypeName = bodyIsNullable
                    ? bodyType.Substring(0, bodyType.Length - 1)
                    : bodyType;

                // Add validation attributes from body schema (only for non-file types)
                if (!isDirectFileUpload)
                {
                    var validationAttributes = requestBodySchema.GetValidationAttributes(isRequired);
                    bodyAttributes.AddRange(
                        validationAttributes.Select(
                            validationAttr => new AttributeParameters(validationAttr, null)));
                }
                else if (isRequired)
                {
                    // For file uploads, only add Required attribute if needed
                    bodyAttributes.Add(new AttributeParameters("Required", null));
                }

                parameters.Add(
                    new ParameterBaseParameters(
                        Attributes: bodyAttributes.Count > 0 ? bodyAttributes : null,
                        GenericTypeName: null,
                        IsGenericListType: bodyCleanTypeName.Contains("[]"),
                        TypeName: bodyCleanTypeName,
                        IsNullableType: bodyIsNullable,
                        IsReferenceType: IsReferenceType(bodyCleanTypeName),
                        Name: propertyName,
                        DefaultValue: null));

                // Add FileName property for client-side direct file uploads
                if (isDirectFileUpload && !includeBindingAttributes && !isCollection)
                {
                    parameters.Add(
                        new ParameterBaseParameters(
                            Attributes: null,
                            GenericTypeName: null,
                            IsGenericListType: false,
                            TypeName: "string",
                            IsNullableType: true,
                            IsReferenceType: true,
                            Name: "FileName",
                            DefaultValue: null));
                }
            }
        }

        // Build record documentation
        var recordDoc = new CodeDocumentationTags(summary);

        // Sort parameters: required (no default value) first, then optional (with default value)
        // This is required by C# record syntax: optional parameters must appear after required ones
        var sortedParameters = parameters
            .OrderBy(p => p.DefaultValue != null)
            .ToList();

        return new RecordParameters(
            DocumentationTags: recordDoc,
            DeclarationModifier: DeclarationModifiers.PublicSealedRecord,
            Name: recordName,
            Parameters: sortedParameters);
    }

    /// <summary>
    /// Maps OpenAPI schema type to C# type string.
    /// </summary>
    private static string MapOpenApiTypeToCSharp(
        IOpenApiSchema schemaInterface,
        bool isRequired,
        TypeConflictRegistry? registry = null)
    {
        // Handle schema references
        if (schemaInterface is OpenApiSchemaReference schemaRef)
        {
            // Use Reference.Id first (Microsoft.OpenApi v3.0+), fall back to Id
            var refName = schemaRef.Reference.Id ?? schemaRef.Id ?? "object";
            refName = OpenApiSchemaExtensions.ResolveTypeName(refName, registry);
            return isRequired ? refName : $"{refName}?";
        }

        // Handle actual schemas
        if (schemaInterface is not OpenApiSchema schema)
        {
            return "object";
        }

        // In OpenAPI 3.1, nullable types can have combined flags (e.g., String | Null)
        // Use HasFlag to check for type presence
        var schemaType = schema.Type ?? JsonSchemaType.Null;

        // Handle array type specially to get proper item type
        var baseType = schemaType.HasFlag(JsonSchemaType.Array)
            ? MapArrayType(schema, registry)
            : schemaType.ToCSharpTypeName(schema.Format);

        // Check if schema is explicitly nullable (OpenAPI 3.1 combined type with Null)
        var isNullable = schemaType.HasFlag(JsonSchemaType.Null);

        // Add nullable marker if not required or explicitly nullable
        if (!isRequired || isNullable)
        {
            // Value types need ? to be nullable (including extended value types like DateTimeOffset, Guid)
            if (CSharpTypeHelper.IsBasicValueType(baseType) || IsExtendedValueType(baseType))
            {
                return $"{baseType}?";
            }

            // String and arrays should also be nullable when not required or nullable
            if (baseType == "string" || baseType.EndsWith("[]", StringComparison.Ordinal))
            {
                return $"{baseType}?";
            }
        }

        return baseType;
    }

    /// <summary>
    /// Maps array type from OpenAPI schema.
    /// </summary>
    private static string MapArrayType(
        OpenApiSchema schema,
        TypeConflictRegistry? registry = null)
    {
        if (schema.Items == null)
        {
            return "List<object>";
        }

        if (schema.Items is OpenApiSchemaReference itemRef)
        {
            var itemType = itemRef.Reference.Id ?? "object";
            itemType = OpenApiSchemaExtensions.ResolveTypeName(itemType, registry);
            return $"List<{itemType}>";
        }

        if (schema.Items is OpenApiSchema itemSchema)
        {
            var itemType = MapOpenApiTypeToCSharp(itemSchema, true, registry);
            return $"List<{itemType}>";
        }

        return "List<object>";
    }

    /// <summary>
    /// Extended value types that are not covered by CSharpTypeHelper.IsBasicValueType.
    /// These types need '?' suffix to be nullable.
    /// </summary>
    private static readonly HashSet<string> ExtendedValueTypes = new(StringComparer.Ordinal)
    {
        "DateTimeOffset",
        "DateTime",
        "Guid",
        "TimeSpan",
        "DateOnly",
        "TimeOnly",
    };

    /// <summary>
    /// Determines if a type is an extended value type (struct types not covered by IsBasicValueType).
    /// </summary>
    private static bool IsExtendedValueType(string typeName)
        => ExtendedValueTypes.Contains(typeName);

    /// <summary>
    /// Determines if a type is a reference type.
    /// </summary>
    private static bool IsReferenceType(string typeName)
    {
        // Remove nullable marker for checking
        var baseType = typeName.TrimEnd('?');
        return (!CSharpTypeHelper.IsBasicValueType(baseType) && !IsExtendedValueType(baseType)) ||
               typeName.EndsWith("[]", StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets the default value for a property based on its type, required status, and schema default.
    /// </summary>
    private static string? GetDefaultValue(
        string paramType,
        bool isRequired,
        IOpenApiSchema? schema,
        bool isNullableType,
        string? schemaDefault)
    {
        // If schema has a default value, always use it (even for nullable types)
        if (schemaDefault != null)
        {
            return schemaDefault;
        }

        // Nullable types without schema defaults don't need property initializers
        if (isNullableType)
        {
            return null;
        }

        // For records, required parameters should NOT have default values
        // The absence of a default makes them required positional parameters
        if (isRequired)
        {
            return null;
        }

        // Value types that aren't nullable and aren't required need default!
        if (CSharpTypeHelper.IsBasicValueType(paramType))
        {
            return "default!";
        }

        return null;
    }

    /// <summary>
    /// Extracts the default value from an OpenAPI schema and formats it as a C# literal.
    /// </summary>
    private static string? ExtractSchemaDefault(
        IOpenApiSchema? schemaInterface,
        string paramType)
        => DefaultValueHelper.ExtractSchemaDefault(schemaInterface, paramType);

    /// <summary>
    /// Formats the default value for use in a [DefaultValue] attribute.
    /// </summary>
    private static string FormatDefaultValueForAttribute(
        string defaultValue,
        string paramType)
        => DefaultValueHelper.FormatForAttribute(defaultValue, paramType);
}