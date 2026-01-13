// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable InvertIf

namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI operations and converts them to ClassParameters list for result class generation.
/// </summary>
public static class ResultClassExtractor
{
    /// <summary>
    /// Extracts result class parameters from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>List of ClassParameters for result classes, or null if no paths exist.</returns>
    public static List<ClassParameters>? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated = false)
        => Extract(openApiDoc, projectName, pathSegment: null, registry: registry, systemTypeResolver: systemTypeResolver, includeDeprecated: includeDeprecated);

    /// <summary>
    /// Extracts result class parameters from OpenAPI document filtered by path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets"). If null, extracts all results.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>List of ClassParameters for result classes in the path segment, or null if no paths exist.</returns>
    public static List<ClassParameters>? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated = false)
        => ExtractInternal(openApiDoc, projectName, pathSegment, registry, systemTypeResolver, includeDeprecated, inlineSchemas: null);

    /// <summary>
    /// Extracts result class parameters along with any inline schemas discovered.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets"). If null, extracts all results.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>A tuple containing the result classes and a dictionary of discovered inline schemas.</returns>
    public static (List<ClassParameters>? ResultClasses, Dictionary<string, ResultClassInlineSchemaInfo> InlineSchemas) ExtractWithInlineSchemas(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated = false)
    {
        var inlineSchemas = new Dictionary<string, ResultClassInlineSchemaInfo>(StringComparer.Ordinal);
        var resultClasses = ExtractInternal(openApiDoc, projectName, pathSegment, registry, systemTypeResolver, includeDeprecated, inlineSchemas);
        return (resultClasses, inlineSchemas);
    }

    private static List<ClassParameters>? ExtractInternal(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated,
        Dictionary<string, ResultClassInlineSchemaInfo>? inlineSchemas)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (openApiDoc.Paths == null || openApiDoc.Paths.Count == 0)
        {
            return null;
        }

        var resultClasses = new List<ClassParameters>();
        var namespaceValue = NamespaceBuilder.ForResults(projectName, pathSegment);
        var modelsNamespace = NamespaceBuilder.ForModels(projectName, pathSegment);

        foreach (var path in openApiDoc.Paths)
        {
            var pathKey = path.Key;

            // Apply path segment filter if provided
            if (pathKey.ShouldSkipForPathSegment(pathSegment))
            {
                continue;
            }

            if (path.Value?.Operations == null)
            {
                continue;
            }

            foreach (var operation in path.Value.Operations)
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

                var httpMethod = operation
                    .Key
                    .ToString()
                    .ToUpperInvariant();
                var pathItem = path.Value as OpenApiPathItem;
                var currentPathSegment = PathSegmentHelper.GetFirstPathSegment(pathKey);
                var classParams = ExtractResultClass(openApiDoc, operationId!, operationValue!, pathItem!, httpMethod, namespaceValue, modelsNamespace, registry, systemTypeResolver, currentPathSegment, inlineSchemas);
                if (classParams != null)
                {
                    resultClasses.Add(classParams);
                }
            }
        }

        return resultClasses.Count > 0 ? resultClasses : null;
    }

    private static ClassParameters ExtractResultClass(
        OpenApiDocument openApiDoc,
        string operationId,
        OpenApiOperation operationValue,
        OpenApiPathItem pathItem,
        string httpMethod,
        string namespaceValue,
        string modelsNamespace,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        string pathSegment,
        Dictionary<string, ResultClassInlineSchemaInfo>? inlineSchemas)
    {
        var className = $"{operationId.ToPascalCaseForDotNet()}Result";
        var summary = operationValue.Summary ?? $"Result for operation: {operationId}";

        // Build methods (factory methods + ExecuteAsync)
        var methods = new List<MethodParameters>();

        // Check if this operation should use IAsyncEnumerable for streaming
        var isAsyncEnumerable = operationValue.IsAsyncEnumerableOperation();

        // Generate factory methods for each response defined in spec
        // Note: Only generate factory methods for responses DEFINED in the OpenAPI spec.
        // Auto-apply rules (for 400, 500, etc.) apply to EndpointDefinition (.Produces) and
        // Client EndpointResult only - NOT to server Result classes. This enforces type safety:
        // handlers can only return responses explicitly defined in the spec.
        if (operationValue.Responses != null)
        {
            foreach (var response in operationValue.Responses)
            {
                if (response.Value is not OpenApiResponse openApiResponse)
                {
                    continue;
                }

                var factoryMethods = GenerateFactoryMethods(openApiDoc, className, response.Key, openApiResponse, isAsyncEnumerable, registry, operationId, pathSegment, inlineSchemas);
                methods.AddRange(factoryMethods);
            }
        }

        var executeAsync = new MethodParameters(
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            ReturnGenericTypeName: null,
            ReturnTypeName: systemTypeResolver.EnsureFullNamespaceIfNeeded(nameof(Task)),
            Name: "ExecuteAsync",
            Parameters: new List<ParameterBaseParameters>
            {
                new(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: "HttpContext",
                    IsNullableType: false,
                    IsReferenceType: true,
                    Name: "httpContext",
                    DefaultValue: null),
            },
            AlwaysBreakDownParameters: false,
            UseExpressionBody: true,
            Content: "innerResult.ExecuteAsync(httpContext)");

        methods.Add(executeAsync);

        // Add ToIResult static method for endpoint definition pattern
        var toIResult = new MethodParameters(
            DocumentationTags: new CodeDocumentationTags($"Converts {className} to IResult for endpoint responses."),
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: "IResult",
            Name: "ToIResult",
            Parameters: new List<ParameterBaseParameters>
            {
                new(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: className,
                    IsNullableType: false,
                    IsReferenceType: true,
                    Name: "result",
                    DefaultValue: null),
            },
            AlwaysBreakDownParameters: false,
            UseExpressionBody: true,
            Content: "result");

        methods.Add(toIResult);

        // Build constructor - result parameter will create private readonly innerResult member
        var constructorParams = new List<ConstructorParameterBaseParameters>
        {
            new(
                GenericTypeName: null,
                TypeName: "IResult",
                IsNullableType: false,
                Name: "innerResult",
                DefaultValue: null,
                PassToInheritedClass: false,
                CreateAsPrivateReadonlyMember: true,
                CreateAaOneLiner: false),
        };

        var constructor = new ConstructorParameters(
            DocumentationTags: null,
            DeclarationModifier: DeclarationModifiers.Private,
            GenericTypeName: null,
            TypeName: className,
            InheritedClassTypeName: null,
            Parameters: constructorParams);

        return new ClassParameters(
            HeaderContent: null, // Header added separately per file
            Namespace: namespaceValue,
            DocumentationTags: new CodeDocumentationTags(summary),
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicClass,
            ClassTypeName: className,
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: "IResult",
            Constructors: new List<ConstructorParameters> { constructor },
            Properties: null, // Using constructor with CreateAsPrivateReadonlyMember instead
            Methods: methods,
            GenerateToStringMethod: false);
    }

    private static List<MethodParameters> GenerateFactoryMethods(
        OpenApiDocument openApiDoc,
        string className,
        string statusCode,
        OpenApiResponse responseValue,
        bool isAsyncEnumerable,
        TypeConflictRegistry? registry,
        string operationId,
        string pathSegment,
        Dictionary<string, ResultClassInlineSchemaInfo>? inlineSchemas)
    {
        var methods = new List<MethodParameters>();
        var description = responseValue.Description ?? string.Empty;

        // Check for binary file download response first
        string? fileDownloadContentType = null;
        bool isFileDownload = false;

        if (responseValue.Content != null)
        {
            foreach (var contentEntry in responseValue.Content)
            {
                if (OpenApiOperationExtensions.IsFileDownloadContentType(contentEntry.Key))
                {
                    isFileDownload = true;
                    fileDownloadContentType = contentEntry.Key;
                    break;
                }
            }
        }

        // Determine response type - use generic support for allOf patterns like PaginatedResult<T>
        string? contentType = null;
        if (!isFileDownload && responseValue.Content != null && responseValue.Content.TryGetValue("application/json", out var mediaType))
        {
            contentType = GetSchemaTypeName(mediaType.Schema, openApiDoc, registry, operationId, pathSegment, "Response", inlineSchemas);
        }

        // Generate factory method based on status code
        if (statusCode == "200")
        {
            methods.AddRange(GenerateOkMethods(className, description, contentType, isAsyncEnumerable, isFileDownload, fileDownloadContentType));
        }
        else if (statusCode == "201")
        {
            methods.AddRange(GenerateCreatedMethods(className, description, contentType));
        }
        else if (statusCode == "204")
        {
            methods.Add(GenerateNoContentMethod(className, description));
        }
        else if (statusCode == "202")
        {
            methods.Add(GenerateAcceptedMethod(className, description, contentType));
        }
        else if (statusCode == "400")
        {
            methods.Add(GenerateBadRequestMethod(className, description, contentType));
        }
        else if (statusCode == "401")
        {
            methods.Add(GenerateUnauthorizedMethod(className, description));
        }
        else if (statusCode == "403")
        {
            methods.Add(GenerateForbiddenMethod(className, description));
        }
        else if (statusCode == "404")
        {
            methods.Add(GenerateNotFoundMethod(className, description, contentType));
        }
        else if (statusCode == "409")
        {
            methods.Add(GenerateConflictMethod(className, description, contentType));
        }
        else if (statusCode == "429")
        {
            methods.Add(GenerateTooManyRequestsMethod(className, description));
        }
        else if (statusCode == "500")
        {
            methods.Add(GenerateInternalServerErrorMethod(className, description, contentType));
        }
        else if (statusCode.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            methods.Add(GenerateErrorMethod(className, description, contentType));
        }

        return methods;
    }

    private static List<MethodParameters> GenerateOkMethods(
        string className,
        string description,
        string? contentType,
        bool isAsyncEnumerable,
        bool isFileDownload = false,
        string? fileDownloadContentType = null)
    {
        var methods = new List<MethodParameters>();
        var doc = new CodeDocumentationTags($"200 OK - {description}");

        // Handle file download responses
        if (isFileDownload)
        {
            var fileMethod = new MethodParameters(
                DocumentationTags: doc,
                Attributes: null,
                DeclarationModifier: DeclarationModifiers.PublicStatic,
                ReturnGenericTypeName: null,
                ReturnTypeName: className,
                Name: "Ok",
                Parameters: new List<ParameterBaseParameters>
                {
                    new(
                        Attributes: null,
                        GenericTypeName: null,
                        IsGenericListType: false,
                        TypeName: "byte[]",
                        IsNullableType: false,
                        IsReferenceType: true,
                        Name: "bytes",
                        DefaultValue: null),
                    new(
                        Attributes: null,
                        GenericTypeName: null,
                        IsGenericListType: false,
                        TypeName: "string",
                        IsNullableType: false,
                        IsReferenceType: true,
                        Name: "contentType",
                        DefaultValue: null),
                    new(
                        Attributes: null,
                        GenericTypeName: null,
                        IsGenericListType: false,
                        TypeName: "string",
                        IsNullableType: false,
                        IsReferenceType: true,
                        Name: "fileName",
                        DefaultValue: null),
                },
                AlwaysBreakDownParameters: false,
                UseExpressionBody: true,
                Content: "new(Microsoft.AspNetCore.Http.Results.File(bytes, contentType, fileName))");
            methods.Add(fileMethod);
            return methods;
        }

        if (!string.IsNullOrEmpty(contentType))
        {
            // For async enumerable operations, wrap the content type with IAsyncEnumerable<>
            var parameterType = contentType!;
            if (isAsyncEnumerable)
            {
                if (contentType!.StartsWith("List<", StringComparison.Ordinal) && contentType.EndsWith(">", StringComparison.Ordinal))
                {
                    // For List<T>, extract T and wrap as IAsyncEnumerable<T>
                    var elementType = contentType.Substring(5, contentType.Length - 6);
                    parameterType = $"IAsyncEnumerable<{elementType}>";
                }
                else
                {
                    // For other types like PaginationResult<T>, wrap the entire type
                    parameterType = $"IAsyncEnumerable<{contentType}>";
                }
            }

            // Factory method with parameter
            var okMethod = new MethodParameters(
                DocumentationTags: doc,
                Attributes: null,
                DeclarationModifier: DeclarationModifiers.PublicStatic,
                ReturnGenericTypeName: null,
                ReturnTypeName: className,
                Name: "Ok",
                Parameters: new List<ParameterBaseParameters>
                {
                    new(
                        Attributes: null,
                        GenericTypeName: null,
                        IsGenericListType: false,
                        TypeName: parameterType,
                        IsNullableType: false,
                        IsReferenceType: true,
                        Name: "response",
                        DefaultValue: null),
                },
                AlwaysBreakDownParameters: false,
                UseExpressionBody: true,
                Content: "new(Microsoft.AspNetCore.Http.Results.Ok(response))");
            methods.Add(okMethod);

            // Add implicit conversion if content type is List<T> or single object (but not 'object' base type)
            // Skip implicit operators for IAsyncEnumerable - they don't make sense for streaming
            if (contentType != "object" && !isAsyncEnumerable)
            {
                if (contentType!.StartsWith("List<", StringComparison.Ordinal) && contentType.EndsWith(">", StringComparison.Ordinal))
                {
                    var elementType = contentType.Substring(5, contentType.Length - 6); // Extract T from List<T>

                    // List implicit operator (direct)
                    var listOperator = new MethodParameters(
                        DocumentationTags: null,
                        Attributes: null,
                        DeclarationModifier: DeclarationModifiers.PublicStaticImplicitOperator,
                        ReturnGenericTypeName: null,
                        ReturnTypeName: className,
                        Name: string.Empty, // Name is not used for implicit operator
                        Parameters: new List<ParameterBaseParameters>
                        {
                            new(
                                Attributes: null,
                                GenericTypeName: "List",
                                IsGenericListType: true,
                                TypeName: elementType,
                                IsNullableType: false,
                                IsReferenceType: true,
                                Name: "response",
                                DefaultValue: null),
                        },
                        AlwaysBreakDownParameters: false,
                        UseExpressionBody: true,
                        Content: "Ok(response)");
                    methods.Add(listOperator);

                    // Array to List implicit operator (for backwards compatibility)
                    var arrayOperator = new MethodParameters(
                        DocumentationTags: null,
                        Attributes: null,
                        DeclarationModifier: DeclarationModifiers.PublicStaticImplicitOperator,
                        ReturnGenericTypeName: null,
                        ReturnTypeName: className,
                        Name: string.Empty, // Name is not used for implicit operator
                        Parameters: new List<ParameterBaseParameters>
                        {
                            new(
                                Attributes: null,
                                GenericTypeName: null,
                                IsGenericListType: false,
                                TypeName: $"{elementType}[]",
                                IsNullableType: false,
                                IsReferenceType: true,
                                Name: "response",
                                DefaultValue: null),
                        },
                        AlwaysBreakDownParameters: false,
                        UseExpressionBody: true,
                        Content: "Ok(response.ToList())");
                    methods.Add(arrayOperator);
                }
                else
                {
                    // Single type implicit operator
                    var implicitOperator = new MethodParameters(
                        DocumentationTags: null,
                        Attributes: null,
                        DeclarationModifier: DeclarationModifiers.PublicStaticImplicitOperator,
                        ReturnGenericTypeName: null,
                        ReturnTypeName: className,
                        Name: string.Empty, // Name is not used for implicit operator
                        Parameters: new List<ParameterBaseParameters>
                        {
                            new(
                                Attributes: null,
                                GenericTypeName: null,
                                IsGenericListType: false,
                                TypeName: contentType,
                                IsNullableType: false,
                                IsReferenceType: true,
                                Name: "response",
                                DefaultValue: null),
                        },
                        AlwaysBreakDownParameters: false,
                        UseExpressionBody: true,
                        Content: "Ok(response)");
                    methods.Add(implicitOperator);
                }
            }
        }
        else
        {
            // Factory method without parameter
            var okMethod = new MethodParameters(
                DocumentationTags: doc,
                Attributes: null,
                DeclarationModifier: DeclarationModifiers.PublicStatic,
                ReturnGenericTypeName: null,
                ReturnTypeName: className,
                Name: "Ok",
                Parameters: null,
                AlwaysBreakDownParameters: false,
                UseExpressionBody: true,
                Content: "new(Microsoft.AspNetCore.Http.Results.Ok())");
            methods.Add(okMethod);
        }

        return methods;
    }

    private static List<MethodParameters> GenerateCreatedMethods(
        string className,
        string description,
        string? contentType)
    {
        var methods = new List<MethodParameters>();
        var doc = new CodeDocumentationTags($"201 Created - {description}");

        if (!string.IsNullOrEmpty(contentType))
        {
            // Generate Created method with response body
            var createdWithResponse = new MethodParameters(
                DocumentationTags: doc,
                Attributes: null,
                DeclarationModifier: DeclarationModifiers.PublicStatic,
                ReturnGenericTypeName: null,
                ReturnTypeName: className,
                Name: "Created",
                Parameters: new List<ParameterBaseParameters>
                {
                    new(
                        Attributes: null,
                        GenericTypeName: null,
                        IsGenericListType: false,
                        TypeName: contentType!,
                        IsNullableType: false,
                        IsReferenceType: true,
                        Name: "response",
                        DefaultValue: null),
                },
                AlwaysBreakDownParameters: false,
                UseExpressionBody: true,
                Content: $"new(Microsoft.AspNetCore.Http.Results.Created((string?)null, response))");
            methods.Add(createdWithResponse);

            // Generate implicit operator for convenience
            var implicitOp = new MethodParameters(
                DocumentationTags: null,
                Attributes: null,
                DeclarationModifier: DeclarationModifiers.PublicStaticImplicitOperator,
                ReturnGenericTypeName: null,
                ReturnTypeName: className,
                Name: string.Empty, // Name is not used for implicit operator
                Parameters: new List<ParameterBaseParameters>
                {
                    new(
                        Attributes: null,
                        GenericTypeName: null,
                        IsGenericListType: false,
                        TypeName: contentType!,
                        IsNullableType: false,
                        IsReferenceType: true,
                        Name: "response",
                        DefaultValue: null),
                },
                AlwaysBreakDownParameters: false,
                UseExpressionBody: true,
                Content: "Created(response)");
            methods.Add(implicitOp);
        }
        else
        {
            // Generate Created method without response body (just status code)
            var createdNoContent = new MethodParameters(
                DocumentationTags: doc,
                Attributes: null,
                DeclarationModifier: DeclarationModifiers.PublicStatic,
                ReturnGenericTypeName: null,
                ReturnTypeName: className,
                Name: "Created",
                Parameters: new List<ParameterBaseParameters>
                {
                    new(
                        Attributes: null,
                        GenericTypeName: null,
                        IsGenericListType: false,
                        TypeName: "string",
                        IsNullableType: true,
                        IsReferenceType: true,
                        Name: "uri",
                        DefaultValue: "null"),
                },
                AlwaysBreakDownParameters: false,
                UseExpressionBody: true,
                Content: "new(uri != null ? Microsoft.AspNetCore.Http.Results.Created(uri, null) : Microsoft.AspNetCore.Http.Results.StatusCode(201))");
            methods.Add(createdNoContent);
        }

        return methods;
    }

    private static MethodParameters GenerateNoContentMethod(
        string className,
        string description)
    {
        var doc = new CodeDocumentationTags($"204 No Content - {description}");

        return new MethodParameters(
            DocumentationTags: doc,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: className,
            Name: "NoContent",
            Parameters: null,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: true,
            Content: "new(Microsoft.AspNetCore.Http.Results.NoContent())");
    }

    private static MethodParameters GenerateNotFoundMethod(
        string className,
        string description,
        string? contentType)
    {
        var doc = new CodeDocumentationTags($"404 Not Found - {description}");

        if (!string.IsNullOrEmpty(contentType))
        {
            return new MethodParameters(
                DocumentationTags: doc,
                Attributes: null,
                DeclarationModifier: DeclarationModifiers.PublicStatic,
                ReturnGenericTypeName: null,
                ReturnTypeName: className,
                Name: "NotFound",
                Parameters: new List<ParameterBaseParameters>
                {
                    new(
                        Attributes: null,
                        GenericTypeName: null,
                        IsGenericListType: false,
                        TypeName: contentType!,
                        IsNullableType: true,
                        IsReferenceType: true,
                        Name: "error",
                        DefaultValue: "null"),
                },
                AlwaysBreakDownParameters: false,
                UseExpressionBody: true,
                Content: "new(error != null ? Microsoft.AspNetCore.Http.Results.NotFound(error) : Microsoft.AspNetCore.Http.Results.NotFound())");
        }

        return new MethodParameters(
            DocumentationTags: doc,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: className,
            Name: "NotFound",
            Parameters: null,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: true,
            Content: "new(Microsoft.AspNetCore.Http.Results.NotFound())");
    }

    private static MethodParameters GenerateErrorMethod(
        string className,
        string description,
        string? contentType)
    {
        var doc = new CodeDocumentationTags($"Default error response - {description}");

        if (!string.IsNullOrEmpty(contentType))
        {
            return new MethodParameters(
                DocumentationTags: doc,
                Attributes: null,
                DeclarationModifier: DeclarationModifiers.PublicStatic,
                ReturnGenericTypeName: null,
                ReturnTypeName: className,
                Name: "Error",
                Parameters: new List<ParameterBaseParameters>
                {
                    new(
                        Attributes: null,
                        GenericTypeName: null,
                        IsGenericListType: false,
                        TypeName: contentType!,
                        IsNullableType: false,
                        IsReferenceType: true,
                        Name: "error",
                        DefaultValue: null),
                },
                AlwaysBreakDownParameters: false,
                UseExpressionBody: true,
                Content: "new(Microsoft.AspNetCore.Http.Results.Json(error, statusCode: 500))");
        }

        return new MethodParameters(
            DocumentationTags: doc,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: className,
            Name: "Error",
            Parameters: new List<ParameterBaseParameters>
            {
                new(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: "string",
                    IsNullableType: false,
                    IsReferenceType: true,
                    Name: "message",
                    DefaultValue: null),
            },
            AlwaysBreakDownParameters: false,
            UseExpressionBody: true,
            Content: "new(Microsoft.AspNetCore.Http.Results.Problem(message))");
    }

    /// <summary>
    /// Generates an Accepted (202) factory method.
    /// These methods are ONLY generated when the response is explicitly defined in the OpenAPI spec.
    /// </summary>
    private static MethodParameters GenerateAcceptedMethod(
        string className,
        string description,
        string? contentType)
    {
        var doc = new CodeDocumentationTags($"202 Accepted - {description}");

        if (!string.IsNullOrEmpty(contentType))
        {
            return new MethodParameters(
                DocumentationTags: doc,
                Attributes: null,
                DeclarationModifier: DeclarationModifiers.PublicStatic,
                ReturnGenericTypeName: null,
                ReturnTypeName: className,
                Name: "Accepted",
                Parameters: new List<ParameterBaseParameters>
                {
                    new(
                        Attributes: null,
                        GenericTypeName: null,
                        IsGenericListType: false,
                        TypeName: contentType!,
                        IsNullableType: false,
                        IsReferenceType: true,
                        Name: "response",
                        DefaultValue: null),
                },
                AlwaysBreakDownParameters: false,
                UseExpressionBody: true,
                Content: "new(Microsoft.AspNetCore.Http.Results.Accepted((string?)null, response))");
        }

        return new MethodParameters(
            DocumentationTags: doc,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: className,
            Name: "Accepted",
            Parameters: null,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: true,
            Content: "new(Microsoft.AspNetCore.Http.Results.Accepted())");
    }

    private static MethodParameters GenerateBadRequestMethod(
        string className,
        string description,
        string? contentType)
    {
        var doc = new CodeDocumentationTags($"400 Bad Request - {description}");
        var errorTypeName = !string.IsNullOrEmpty(contentType) ? contentType : "ValidationProblemDetails";

        return new MethodParameters(
            DocumentationTags: doc,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: className,
            Name: "BadRequest",
            Parameters: new List<ParameterBaseParameters>
            {
                new(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: errorTypeName!,
                    IsNullableType: true,
                    IsReferenceType: true,
                    Name: "errors",
                    DefaultValue: "null"),
            },
            AlwaysBreakDownParameters: false,
            UseExpressionBody: true,
            Content: $"new(\n        errors is null\n            ? Microsoft.AspNetCore.Http.Results.BadRequest()\n            : Microsoft.AspNetCore.Http.Results.BadRequest(errors))");
    }

    private static MethodParameters GenerateUnauthorizedMethod(
        string className,
        string description)
    {
        var doc = new CodeDocumentationTags($"401 Unauthorized - {description}");

        return new MethodParameters(
            DocumentationTags: doc,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: className,
            Name: "Unauthorized",
            Parameters: null,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: true,
            Content: "new(Microsoft.AspNetCore.Http.Results.Unauthorized())");
    }

    private static MethodParameters GenerateForbiddenMethod(
        string className,
        string description)
    {
        var doc = new CodeDocumentationTags($"403 Forbidden - {description}");

        return new MethodParameters(
            DocumentationTags: doc,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: className,
            Name: "Forbidden",
            Parameters: null,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: true,
            Content: "new(Microsoft.AspNetCore.Http.Results.Forbid())");
    }

    private static MethodParameters GenerateConflictMethod(
        string className,
        string description,
        string? contentType)
    {
        var doc = new CodeDocumentationTags($"409 Conflict - {description}");
        var errorTypeName = !string.IsNullOrEmpty(contentType) ? contentType : "ProblemDetails";

        return new MethodParameters(
            DocumentationTags: doc,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: className,
            Name: "Conflict",
            Parameters: new List<ParameterBaseParameters>
            {
                new(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: errorTypeName!,
                    IsNullableType: true,
                    IsReferenceType: true,
                    Name: "error",
                    DefaultValue: "null"),
            },
            AlwaysBreakDownParameters: false,
            UseExpressionBody: true,
            Content: $"new(\n        error is null\n            ? Microsoft.AspNetCore.Http.Results.Conflict()\n            : Microsoft.AspNetCore.Http.Results.Conflict(error))");
    }

    private static MethodParameters GenerateTooManyRequestsMethod(
        string className,
        string description)
    {
        var doc = new CodeDocumentationTags($"429 Too Many Requests - {description}");

        return new MethodParameters(
            DocumentationTags: doc,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: className,
            Name: "TooManyRequests",
            Parameters: new List<ParameterBaseParameters>
            {
                new(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: "int",
                    IsNullableType: true,
                    IsReferenceType: false,
                    Name: "retryAfterSeconds",
                    DefaultValue: "null"),
            },
            AlwaysBreakDownParameters: false,
            UseExpressionBody: true,
            Content: "new(Microsoft.AspNetCore.Http.Results.StatusCode(429))");
    }

    private static MethodParameters GenerateInternalServerErrorMethod(
        string className,
        string description,
        string? contentType)
    {
        var doc = new CodeDocumentationTags($"500 Internal Server Error - {description}");
        var errorTypeName = !string.IsNullOrEmpty(contentType) ? contentType : "ProblemDetails";

        return new MethodParameters(
            DocumentationTags: doc,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: className,
            Name: "InternalServerError",
            Parameters: new List<ParameterBaseParameters>
            {
                new(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: errorTypeName!,
                    IsNullableType: true,
                    IsReferenceType: true,
                    Name: "error",
                    DefaultValue: "null"),
            },
            AlwaysBreakDownParameters: false,
            UseExpressionBody: true,
            Content: $"new(\n        error is null\n            ? Microsoft.AspNetCore.Http.Results.StatusCode(500)\n            : Microsoft.AspNetCore.Http.Results.Json(error, statusCode: 500))");
    }

    private static string GetSchemaTypeName(
        IOpenApiSchema? schema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        string operationId,
        string pathSegment,
        string context,
        Dictionary<string, ResultClassInlineSchemaInfo>? inlineSchemas)
    {
        if (schema == null)
        {
            return "object";
        }

        // Handle schema references - decide whether to use the type name or resolve to array
        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refId = schemaRef.Reference.Id;

            if (string.IsNullOrEmpty(refId))
            {
                return "object";
            }

            // Check if this reference points to an array alias (type: array with items but no prefixItems)
            // Array aliases like "Pets" (type: array, items: $ref Pet) should resolve to Pet[]
            // But tuple types with prefixItems (like Coordinate) should keep their type name
            if (openApiDoc.Components?.Schemas != null &&
                openApiDoc.Components.Schemas.TryGetValue(refId!, out var resolvedSchema) &&
                resolvedSchema is OpenApiSchema { Type: JsonSchemaType.Array } arraySchema &&
                arraySchema.Items != null &&
                !arraySchema.HasPrefixItems())
            {
                // This is a simple array alias - resolve to the underlying array type
                return GetArraySchemaType(arraySchema, openApiDoc, registry, operationId, pathSegment, inlineSchemas);
            }

            // Return the resolved type name for schema references
            return OpenApiSchemaExtensions.ResolveTypeName(refId!, registry);
        }

        if (schema is OpenApiSchema actualSchema)
        {
            // Handle allOf composition - delegate to ToCSharpTypeWithGenericSupport for PaginatedResult patterns
            if (actualSchema.AllOf is { Count: > 0 })
            {
                return actualSchema.ToCSharpTypeWithGenericSupport(openApiDoc, isRequired: true, registry);
            }

            // Handle array type specially
            if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true)
            {
                // Check if this is a resolved schema reference (e.g., Coordinate tuple type)
                // If it's a component schema (has prefixItems or is a named array), return the schema name
                var schemaName = FindSchemaNameByReference(openApiDoc, actualSchema);
                if (!string.IsNullOrEmpty(schemaName))
                {
                    return OpenApiSchemaExtensions.ResolveTypeName(schemaName!, registry);
                }

                return GetArraySchemaType(actualSchema, openApiDoc, registry, operationId, pathSegment, inlineSchemas);
            }

            // Handle inline object schemas with properties
            if (InlineSchemaExtractor.IsInlineObjectSchema(actualSchema) &&
                !string.IsNullOrEmpty(operationId) &&
                !string.IsNullOrEmpty(pathSegment) &&
                !string.IsNullOrEmpty(context) &&
                inlineSchemas != null)
            {
                var typeName = InlineSchemaExtractor.GenerateInlineTypeName(operationId, context);
                if (!inlineSchemas.ContainsKey(typeName))
                {
                    var recordParams = InlineSchemaExtractor.ExtractRecordFromInlineSchema(actualSchema, typeName, registry);
                    inlineSchemas[typeName] = new ResultClassInlineSchemaInfo(typeName, pathSegment, recordParams);
                }

                return typeName;
            }

            // Use centralized primitive type mapping
            return actualSchema.Type.ToPrimitiveCSharpTypeName(actualSchema.Format) ?? "object";
        }

        return "object";
    }

    /// <summary>
    /// Finds the schema name in Components.Schemas by comparing schema properties.
    /// This is needed when Microsoft.OpenApi resolves $ref to the actual schema object.
    /// </summary>
    private static string? FindSchemaNameByReference(
        OpenApiDocument openApiDoc,
        OpenApiSchema itemSchema)
    {
        if (openApiDoc.Components?.Schemas == null)
        {
            return null;
        }

        foreach (var kvp in openApiDoc.Components.Schemas)
        {
            // First try: compare by reference (same object instance)
            if (ReferenceEquals(kvp.Value, itemSchema))
            {
                return kvp.Key;
            }

            // Second try: match by Title property if set
            // OpenAPI specs often have title matching the schema name
            if (kvp.Value is OpenApiSchema componentSchema &&
                !string.IsNullOrEmpty(componentSchema.Title) &&
                !string.IsNullOrEmpty(itemSchema.Title) &&
                string.Equals(componentSchema.Title, itemSchema.Title, StringComparison.Ordinal))
            {
                return kvp.Key;
            }
        }

        return null;
    }

    private static string GetArraySchemaType(
        OpenApiSchema schema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        string operationId,
        string pathSegment,
        Dictionary<string, ResultClassInlineSchemaInfo>? inlineSchemas)
    {
        if (schema.Items == null)
        {
            return "List<object>";
        }

        // Delegate to GetSchemaTypeName for proper handling of schema references, resolved schemas, and inline objects
        // Use "ResponseItem" context for array items (e.g., ListReportsResponseItem)
        var itemType = GetSchemaTypeName(
            schema.Items,
            openApiDoc,
            registry,
            operationId,
            pathSegment,
            "ResponseItem",
            inlineSchemas);

        return $"List<{itemType}>";
    }
}