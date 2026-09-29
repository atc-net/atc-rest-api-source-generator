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
        bool includeDeprecated = false,
        string? namespaceSegment = null)
    {
        var inlineSchemas = new Dictionary<string, ResultClassInlineSchemaInfo>(StringComparer.Ordinal);
        var resultClasses = ExtractInternal(openApiDoc, projectName, pathSegment, registry, systemTypeResolver, includeDeprecated, inlineSchemas, namespaceSegment);

        // The inline models live in the models namespace the Results file imports: the collapsed segment
        // (e.g. none, on a spec whose paths share one first segment), not the raw path segment.
        if (namespaceSegment is not null)
        {
            foreach (var key in inlineSchemas.Keys.ToList())
            {
                inlineSchemas[key] = inlineSchemas[key] with { PathSegment = namespaceSegment };
            }
        }

        return (resultClasses, inlineSchemas);
    }

    private static List<ClassParameters>? ExtractInternal(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated,
        Dictionary<string, ResultClassInlineSchemaInfo>? inlineSchemas,
        string? namespaceSegment = null)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (openApiDoc.Paths is null || openApiDoc.Paths.Count == 0)
        {
            return null;
        }

        var resultClasses = new List<ClassParameters>();

        // The raw pathSegment filters operations; namespaceSegment drives the emitted namespace.
        var namespaceValue = NamespaceBuilder.ForResults(projectName, namespaceSegment ?? pathSegment);

        foreach (var path in openApiDoc.Paths)
        {
            var pathKey = path.Key;

            // Apply path segment filter if provided
            if (pathKey.ShouldSkipForPathSegment(pathSegment))
            {
                continue;
            }

            if (path.Value?.Operations is null)
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

                var currentPathSegment = PathSegmentHelper.GetFirstPathSegment(pathKey);
                var classParams = ExtractResultClass(
                    openApiDoc,
                    projectName,
                    operationId!,
                    operationValue!,
                    namespaceValue,
                    registry,
                    systemTypeResolver,
                    currentPathSegment,
                    inlineSchemas);

                if (classParams is not null)
                {
                    resultClasses.Add(classParams);
                }
            }
        }

        return resultClasses.Count > 0 ? resultClasses : null;
    }

    private static ClassParameters ExtractResultClass(
        OpenApiDocument openApiDoc,
        string projectName,
        string operationId,
        OpenApiOperation operationValue,
        string namespaceValue,
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
        // (x-return-async-enumerable annotation or an OpenAPI 3.2 itemSchema).
        var isAsyncEnumerable = operationValue.IsStreamingResponse();

        // Generate factory methods for each response defined in spec
        // Note: Only generate factory methods for responses DEFINED in the OpenAPI spec.
        // Auto-apply rules (for 400, 500, etc.) apply to EndpointDefinition (.Produces) and
        // Client EndpointResult only - NOT to server Result classes. This enforces type safety:
        // handlers can only return responses explicitly defined in the spec.
        if (operationValue.Responses is not null)
        {
            foreach (var response in operationValue.Responses)
            {
                if (response.Value is not { } openApiResponse)
                {
                    continue;
                }

                var factoryMethods = GenerateFactoryMethods(openApiDoc, projectName, className, response.Key, openApiResponse, isAsyncEnumerable, operationValue.GetStreamingFraming(), registry, operationId, pathSegment, inlineSchemas);
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
            Content: "result.Result");

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

        // Add Result property to expose the innerResult
        var properties = new List<PropertyParameters>
        {
            new(
                DocumentationTags: null,
                Attributes: null,
                DeclarationModifier: DeclarationModifiers.Public,
                GenericTypeName: null,
                TypeName: "IResult",
                IsNullableType: false,
                Name: "Result",
                JsonName: null,
                DefaultValue: null,
                IsReferenceType: true,
                IsGenericListType: false,
                UseAutoProperty: false,
                UseGet: true,
                UseSet: false,
                UseExpressionBody: true,
                UseRequired: false,
                Content: "innerResult"),
        };

        return new ClassParameters(
            HeaderContent: null, // Header added separately per file
            Namespace: namespaceValue,
            DocumentationTags: new CodeDocumentationTags(summary),
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicSealedClass,
            ClassTypeName: className,
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: "IResult",
            Constructors: new List<ConstructorParameters> { constructor },
            Properties: properties,
            Methods: methods,
            GenerateToStringMethod: false);
    }

    private static List<MethodParameters> GenerateFactoryMethods(
        OpenApiDocument openApiDoc,
        string projectName,
        string className,
        string statusCode,
        IOpenApiResponse responseValue,
        bool isAsyncEnumerable,
        StreamingFraming streamingFraming,
        TypeConflictRegistry? registry,
        string operationId,
        string pathSegment,
        Dictionary<string, ResultClassInlineSchemaInfo>? inlineSchemas)
    {
        var methods = new List<MethodParameters>();
        var description = responseValue.Summary ?? responseValue.Description ?? string.Empty;

        // Error responses (4xx, 5xx, default) follow the error contract: what the spec declares is what the
        // factory takes and writes; nothing declared means an RFC 9457 ProblemDetails.
        if (ErrorResponseShapeResolver.IsErrorStatusCode(statusCode))
        {
            return GenerateErrorFactoryMethods(openApiDoc, className, description, statusCode, responseValue, registry, operationId, pathSegment, inlineSchemas);
        }

        // Check for binary file download response first
        var isFileDownload = false;

        if (responseValue.Content is not null)
        {
            foreach (var contentEntry in responseValue.Content)
            {
                if (OpenApiOperationExtensions.IsFileDownloadContentType(contentEntry.Key))
                {
                    isFileDownload = true;
                    break;
                }
            }
        }

        // Determine response type - use generic support for allOf patterns like PaginatedResult<T>
        string? contentType = null;
        if (!isFileDownload && responseValue.Content is not null && responseValue.Content.TryGetValue("application/json", out var mediaType))
        {
            contentType = GetSchemaTypeName(mediaType.Schema, openApiDoc, registry, operationId, pathSegment, "Response", inlineSchemas);
        }

        // OpenAPI 3.2 streaming: when a response media type declares an itemSchema, the
        // streamed element type comes from it directly (wrapped as IAsyncEnumerable<T> below).
        if (!isFileDownload && contentType is null && isAsyncEnumerable && responseValue.Content is not null)
        {
            foreach (var mt in responseValue.Content.Values)
            {
                if (mt.ItemSchema is not null)
                {
                    contentType = GetSchemaTypeName(mt.ItemSchema, openApiDoc, registry, operationId, pathSegment, "Response", inlineSchemas);
                    break;
                }
            }
        }

        // If no JSON or binary match, check for a textual response (text/plain, text/csv,
        // application/xml, ...) with a type: string schema. We surface it as a raw string body
        // via TypedResults.Text(content, mediaType).
        var isTextResponse = false;
        string? textResponseMediaType = null;
        if (!isFileDownload && contentType is null &&
            responseValue.TryGetTextResponseMediaType(out var matchedMediaType, out _))
        {
            isTextResponse = true;
            textResponseMediaType = matchedMediaType;
            contentType = "string";
        }

        switch (statusCode)
        {
            // Generate factory method based on status code
            case "200":
                methods.AddRange(GenerateOkMethods(projectName, className, description, contentType, isAsyncEnumerable, streamingFraming, isFileDownload, isTextResponse, textResponseMediaType));
                break;
            case "201":
                methods.AddRange(GenerateCreatedMethods(className, description, contentType));
                break;
            case "202":
                methods.Add(GenerateAcceptedMethod(className, description, contentType));
                break;
            case "204":
                methods.Add(GenerateNoContentMethod(className, description));
                break;
            default:
            {
                // An informational (1xx) status is never a final response (RFC 9110 §15.2): no factory
                // (ATC_API_OPR030 warns about the declaration).
                if (int.TryParse(statusCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var statusCodeInt) &&
                    statusCodeInt >= 200)
                {
                    methods.Add(GenerateStatusMethod(className, description, statusCodeInt, contentType, isTextResponse, textResponseMediaType));
                }

                break;
            }
        }

        return methods;
    }

    /// <summary>
    /// Generates the factory methods for one error response (4xx, 5xx or <c>default</c>).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>Nothing declared, or a schema named <c>ProblemDetails</c> / <c>ValidationProblemDetails</c>:
    ///   an RFC 9457 ProblemDetails (<c>application/problem+json</c>) whose status is the response status.
    ///   The 2.x call shapes keep compiling: <c>X(string? message = null)</c>, 400 with
    ///   <c>ValidationProblemDetails?</c>, 500/504 with <c>ProblemDetails?</c>, 429 with <c>int? retryAfterSeconds</c>.
    ///   A declared problem schema adds an <c>X(ProblemDetails problem)</c> overload.</item>
    ///   <item>Any other JSON schema <c>T</c>: <c>X(T? error = null)</c> writes <c>T</c> with the declared media type.</item>
    ///   <item>A JSON string or text: <c>X(string? message = null)</c> writes it as declared.</item>
    ///   <item>A declared body left empty (<c>X()</c>) has no body; 429 always sets <c>Retry-After</c> when given.</item>
    /// </list>
    /// </remarks>
    private static List<MethodParameters> GenerateErrorFactoryMethods(
        OpenApiDocument openApiDoc,
        string className,
        string description,
        string statusCode,
        IOpenApiResponse responseValue,
        TypeConflictRegistry? registry,
        string operationId,
        string pathSegment,
        Dictionary<string, ResultClassInlineSchemaInfo>? inlineSchemas)
    {
        var shape = ErrorResponseShapeResolver.Resolve(responseValue);
        var isDefault = !int.TryParse(statusCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code);
        var name = isDefault ? "Error" : HttpStatusCodeHelper.ToEnumName(code);
        var status = isDefault ? "statusCode" : code.ToString(CultureInfo.InvariantCulture);
        var doc = isDefault
            ? $"Default error response - {description}"
            : $"{statusCode} {Regex.Replace(HttpStatusCodeHelper.ToEnumName(code), "(?<=[a-z])(?=[A-Z])", " ", RegexOptions.None, TimeSpan.FromSeconds(1))} - {description}";
        var isTooManyRequests = code == 429;

        // default: the status is a parameter (default 500), so "any other status" stays expressible.
        List<ParameterBaseParameters> statusParameter = isDefault
            ? [CreateParameter("int", "statusCode", nullable: false, isReference: false, defaultValue: "500")]
            : [];

        string Wrap(string result)
            => isTooManyRequests
                ? $"new(new RetryAfterHttpResult({result}, retryAfterSeconds))"
                : $"new({result})";

        var methods = new List<MethodParameters>();

        if (shape.IsProblem)
        {
            var problemFromMessage = $"TypedResults.Problem(detail: message, statusCode: {status})";

            // A declared problem schema adds a ProblemDetails overload next to the 2.x shape. A null
            // argument keeps resolving to the 2.x shape, as it did in 2.x, instead of being ambiguous.
            var hasProblemOverload = shape.IsDeclared && code is not (400 or 500 or 504);
            IList<AttributeParameters>? legacyShapeAttributes = hasProblemOverload
                ? [new AttributeParameters("System.Runtime.CompilerServices.OverloadResolutionPriority", "1")]
                : null;

            if (code == 400)
            {
                // 2.x shape: BadRequest(ValidationProblemDetails? errors = null) (or the declared ProblemDetails).
                var errorsType = shape is { Kind: ErrorResponseKind.Problem, IsDeclared: true } ? "ProblemDetails" : "ValidationProblemDetails";
                var fromErrors = errorsType == "ValidationProblemDetails"
                    ? ValidationProblemExpression("errors", "400")
                    : ProblemExpression("errors", "400");
                methods.Add(CreateErrorFactory(
                    className,
                    doc,
                    name,
                    [CreateParameter(errorsType, "errors", nullable: true, isReference: true, defaultValue: "null")],
                    $"new(errors is null ? TypedResults.Problem(statusCode: 400) : {fromErrors})"));
            }
            else if (code is 500 or 504)
            {
                // 2.x shape: X(ProblemDetails? error = null).
                methods.Add(CreateErrorFactory(
                    className,
                    doc,
                    name,
                    [CreateParameter("ProblemDetails", "error", nullable: true, isReference: true, defaultValue: "null")],
                    $"new(error is null ? TypedResults.Problem(statusCode: {status}) : {ProblemExpression("error", status)})"));
            }
            else if (isTooManyRequests)
            {
                // 2.x shape: TooManyRequests(int? retryAfterSeconds = null), plus the message.
                methods.Add(CreateErrorFactory(
                    className,
                    doc,
                    name,
                    [
                        CreateParameter("int", "retryAfterSeconds", nullable: true, isReference: false, defaultValue: "null"),
                        CreateParameter("string", "message", nullable: true, isReference: true, defaultValue: "null"),
                    ],
                    Wrap(problemFromMessage),
                    legacyShapeAttributes));
            }
            else
            {
                methods.Add(CreateErrorFactory(
                    className,
                    doc,
                    name,
                    isDefault
                        ? [CreateParameter("string", "message", nullable: true, isReference: true, defaultValue: "null"), .. statusParameter]
                        : [CreateParameter("string", "message", nullable: true, isReference: true, defaultValue: "null")],
                    $"new({problemFromMessage})",
                    legacyShapeAttributes));
            }

            // A declared problem schema also accepts the problem object itself (400/500/504 already do).
            if (hasProblemOverload)
            {
                var problemType = shape.Kind == ErrorResponseKind.ValidationProblem ? "ValidationProblemDetails" : "ProblemDetails";
                var fromProblem = shape.Kind == ErrorResponseKind.ValidationProblem
                    ? ValidationProblemExpression("problem", status)
                    : ProblemExpression("problem", status);
                List<ParameterBaseParameters> parameters = [CreateParameter(problemType, "problem", nullable: false, isReference: true, defaultValue: null), .. statusParameter];
                if (isTooManyRequests)
                {
                    parameters.Add(CreateParameter("int", "retryAfterSeconds", nullable: true, isReference: false, defaultValue: "null"));
                }

                methods.Add(CreateErrorFactory(className, doc, name, parameters, Wrap(fromProblem)));
            }

            return methods;
        }

        // A declared, non-problem body: the handler's value is written as declared.
        var (valueType, valueName, written) = shape.Kind switch
        {
            ErrorResponseKind.Text => (
                "string",
                "message",
                $"TypedResults.Text(message, \"{shape.MediaTypeLiteral}\", statusCode: {status})"),
            ErrorResponseKind.JsonString => (
                "string",
                "message",
                $"TypedResults.Json(message, contentType: \"{shape.MediaTypeLiteral}\", statusCode: {status})"),
            _ => (
                GetSchemaTypeName(shape.Schema, openApiDoc, registry, operationId, pathSegment, ErrorResponseShapeResolver.InlineTypeContext(statusCode), inlineSchemas),
                code == 400 ? "errors" : "error",
                $"TypedResults.Json({(code == 400 ? "errors" : "error")}, contentType: \"{shape.MediaTypeLiteral}\", statusCode: {status})"),
        };

        if (isDefault)
        {
            // 2.x shape: Error(T error) - the value is required, the status optional.
            methods.Add(CreateErrorFactory(
                className,
                doc,
                name,
                [CreateParameter(valueType, valueName, nullable: false, isReference: true, defaultValue: null), .. statusParameter],
                $"new({written})"));

            return methods;
        }

        List<ParameterBaseParameters> valueParameters = [CreateParameter(valueType, valueName, nullable: true, isReference: true, defaultValue: "null")];
        if (isTooManyRequests)
        {
            // First, as in the 2.x shape TooManyRequests(int? retryAfterSeconds): a 2.x call TooManyRequests(30) still compiles.
            valueParameters.Insert(0, CreateParameter("int", "retryAfterSeconds", nullable: true, isReference: false, defaultValue: "null"));
        }

        methods.Add(CreateErrorFactory(
            className,
            doc,
            name,
            valueParameters,
            Wrap($"{valueName} is null ? TypedResults.StatusCode({status}) : {written}")));

        return methods;
    }

    /// <summary>
    /// A ProblemDetails built from a caller's problem object, with the response status.
    /// </summary>
    private static string ProblemExpression(
        string variable,
        string status)
        => $"TypedResults.Problem(detail: {variable}.Detail, instance: {variable}.Instance, statusCode: {status}, title: {variable}.Title, type: {variable}.Type, extensions: {variable}.Extensions)";

    /// <summary>
    /// A validation ProblemDetails built from a caller's <c>ValidationProblemDetails</c>, with the response status.
    /// </summary>
    private static string ValidationProblemExpression(
        string variable,
        string status)
        => status == "400"
            ? $"TypedResults.ValidationProblem({variable}.Errors, detail: {variable}.Detail, instance: {variable}.Instance, title: {variable}.Title, type: {variable}.Type, extensions: {variable}.Extensions)"
            : $"TypedResults.Problem(new HttpValidationProblemDetails({variable}.Errors) {{ Detail = {variable}.Detail, Instance = {variable}.Instance, Status = {status}, Title = {variable}.Title, Type = {variable}.Type, Extensions = {variable}.Extensions }})";

    private static MethodParameters CreateErrorFactory(
        string className,
        string documentation,
        string name,
        List<ParameterBaseParameters> parameters,
        string content,
        IList<AttributeParameters>? attributes = null)
        => new(
            DocumentationTags: new CodeDocumentationTags(documentation),
            Attributes: attributes,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: className,
            Name: name,
            Parameters: parameters,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: true,
            Content: content);

    private static ParameterBaseParameters CreateParameter(
        string typeName,
        string name,
        bool nullable,
        bool isReference,
        string? defaultValue)
        => new(
            Attributes: null,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: typeName,
            IsNullableType: nullable,
            IsReferenceType: isReference,
            Name: name,
            DefaultValue: defaultValue);

    /// <summary>
    /// The <c>LocationHttpResult</c> type the 303 factories use (ASP.NET Core has no redirect result for 303): the
    /// wrapped result, written with a <c>Location</c> header.
    /// </summary>
    public static string LocationHttpResultSource()
        => $$"""

            /// <summary>
            /// Writes the wrapped result with a <c>Location</c> header.
            /// </summary>
            [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
            internal sealed class LocationHttpResult : IResult
            {
                private readonly IResult inner;
                private readonly string location;

                public LocationHttpResult(
                    IResult inner,
                    string location)
                {
                    this.inner = inner;
                    this.location = location;
                }

                public global::System.Threading.Tasks.Task ExecuteAsync(HttpContext httpContext)
                {
                    httpContext.Response.Headers.Location = location;
                    return inner.ExecuteAsync(httpContext);
                }
            }
            """;

    /// <summary>
    /// The source of the <c>IResult</c> wrapper that adds a <c>Retry-After</c> header, emitted once into a
    /// Results file whose factories use it (429 responses).
    /// </summary>
    public static string RetryAfterHttpResultSource()
        => $$"""

            /// <summary>
            /// Writes the wrapped result with a <c>Retry-After</c> header (in seconds), when one is given.
            /// </summary>
            [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
            internal sealed class RetryAfterHttpResult : IResult
            {
                private readonly IResult inner;
                private readonly int? retryAfterSeconds;

                public RetryAfterHttpResult(
                    IResult inner,
                    int? retryAfterSeconds)
                {
                    this.inner = inner;
                    this.retryAfterSeconds = retryAfterSeconds;
                }

                public global::System.Threading.Tasks.Task ExecuteAsync(HttpContext httpContext)
                {
                    if (retryAfterSeconds is not null)
                    {
                        httpContext.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
                    }

                    return inner.ExecuteAsync(httpContext);
                }
            }
            """;

    private static List<MethodParameters> GenerateOkMethods(
        string projectName,
        string className,
        string description,
        string? contentType,
        bool isAsyncEnumerable,
        StreamingFraming streamingFraming,
        bool isFileDownload = false,
        bool isTextResponse = false,
        string? textResponseMediaType = null)
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
                Content: "new(TypedResults.File(bytes, contentType, fileName))");
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

            // For textual responses (text/plain, text/csv, application/xml, ...) the body must
            // be returned verbatim (no JSON quoting). Use TypedResults.Text(content, mediaType)
            // and name the parameter "response" for consistency with implicit-operator generation
            // below — text responses always carry a string payload.
            var paramName = isTextResponse
                ? "response"
                : parameterType == "string" ? "message" : "response";

            // Server-Sent Events use the first-party TypedResults.ServerSentEvents writer; JSON
            // Lines uses the emitted JsonLinesResult<T> writer (fully-qualified — the Results file
            // has no Streaming using); all other framing's (JsonArray, ...) keep TypedResults.Ok.
            string content;
            if (isTextResponse)
            {
                content = $"new(TypedResults.Text({paramName}, \"{textResponseMediaType}\"))";
            }
            else if (isAsyncEnumerable && streamingFraming == StreamingFraming.ServerSentEvents)
            {
                content = $"new(TypedResults.ServerSentEvents({paramName}))";
            }
            else if (isAsyncEnumerable && streamingFraming == StreamingFraming.JsonLines)
            {
                content = $"new(new {projectName}.Generated.Streaming.JsonLinesResult<{contentType}>({paramName}))";
            }
            else if (isAsyncEnumerable && streamingFraming == StreamingFraming.JsonSequence)
            {
                content = $"new(new {projectName}.Generated.Streaming.JsonSequenceResult<{contentType}>({paramName}))";
            }
            else if (isAsyncEnumerable && streamingFraming == StreamingFraming.MultipartMixed)
            {
                content = $"new(new {projectName}.Generated.Streaming.MultipartMixedResult<{contentType}>({paramName}))";
            }
            else
            {
                content = $"new(TypedResults.Ok({paramName}))";
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
                        Name: paramName,
                        DefaultValue: null),
                },
                AlwaysBreakDownParameters: false,
                UseExpressionBody: true,
                Content: content);
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
                    // Single type implicit operator - use same parameter name as Ok method
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
                                Name: paramName,
                                DefaultValue: null),
                        },
                        AlwaysBreakDownParameters: false,
                        UseExpressionBody: true,
                        Content: $"Ok({paramName})");
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
                Content: "new(TypedResults.Ok())");
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
                Content: "new(TypedResults.Created((string?)null, response))");
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
                Content: "new(uri is not null ? TypedResults.Created(uri) : TypedResults.StatusCode(201))");
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
            Content: "new(TypedResults.NoContent())");
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
                Content: "new(TypedResults.Accepted((string?)null, response))");
        }

        // TypedResults.Accepted() has no parameterless overload - every overload expects
        // either a location or a value, and we have neither here - so StatusCode(202) is
        // the cleanest expression of "accepted, nothing to return" (mirrors the same
        // fallback documented and used in WebhookResultExtractor.GetTypedResultExpression).
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
            Content: "new(TypedResults.StatusCode(StatusCodes.Status202Accepted))");
    }

    /// <summary>
    /// The factory of a 2xx other than 200/201/202/204, or a 3xx (RFC 9110):
    /// <list type="bullet">
    ///   <item>301, 302, 303, 307, 308 take the <c>location</c> (a <c>Uri</c>, so a 2.x <c>X(message)</c> call does not
    ///   compile into a redirect to the message) and write it as the <c>Location</c> header, no body.</item>
    ///   <item>205 and 304 never carry a body (§15.3.6, §15.4.5).</item>
    ///   <item>Any other status writes its declared body (JSON or text) with that status, or no body.</item>
    /// </list>
    /// </summary>
    private static MethodParameters GenerateStatusMethod(
        string className,
        string description,
        int statusCode,
        string? contentType,
        bool isTextResponse,
        string? textResponseMediaType)
    {
        var methodName = HttpStatusCodeHelper.ToEnumName(statusCode);
        var doc = new CodeDocumentationTags($"{statusCode} {methodName} - {description}");
        var status = statusCode.ToString(CultureInfo.InvariantCulture);

        MethodParameters Method(
            string? parameterType,
            string parameterName,
            string content)
            => new(
                DocumentationTags: doc,
                Attributes: null,
                DeclarationModifier: DeclarationModifiers.PublicStatic,
                ReturnGenericTypeName: null,
                ReturnTypeName: className,
                Name: methodName,
                Parameters: parameterType is null
                    ? null
                    : new List<ParameterBaseParameters>
                    {
                        new(
                            Attributes: null,
                            GenericTypeName: null,
                            IsGenericListType: false,
                            TypeName: parameterType,
                            IsNullableType: false,
                            IsReferenceType: true,
                            Name: parameterName,
                            DefaultValue: null),
                    },
                AlwaysBreakDownParameters: false,
                UseExpressionBody: true,
                Content: content);

        return statusCode switch
        {
            301 => Method("System.Uri", "location", "new(TypedResults.Redirect(location.OriginalString, permanent: true, preserveMethod: false))"),
            302 => Method("System.Uri", "location", "new(TypedResults.Redirect(location.OriginalString, permanent: false, preserveMethod: false))"),
            303 => Method("System.Uri", "location", "new(new LocationHttpResult(TypedResults.StatusCode(303), location.OriginalString))"),
            307 => Method("System.Uri", "location", "new(TypedResults.Redirect(location.OriginalString, permanent: false, preserveMethod: true))"),
            308 => Method("System.Uri", "location", "new(TypedResults.Redirect(location.OriginalString, permanent: true, preserveMethod: true))"),
            205 or 304 => Method(null, string.Empty, $"new(TypedResults.StatusCode({status}))"),
            _ when isTextResponse && textResponseMediaType is not null =>
                Method("string", "content", $"new(TypedResults.Text(content, \"{textResponseMediaType}\", statusCode: {status}))"),
            _ when !string.IsNullOrEmpty(contentType) =>
                Method(contentType, "response", $"new(TypedResults.Json(response, statusCode: {status}))"),
            _ => Method(null, string.Empty, $"new(TypedResults.StatusCode({status}))"),
        };
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
        if (schema is null)
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
            if (openApiDoc.Components?.Schemas is not null &&
                openApiDoc.Components.Schemas.TryGetValue(refId!, out var resolvedSchema) &&
                resolvedSchema is OpenApiSchema { Type: JsonSchemaType.Array, Items: not null } arraySchema &&
                !arraySchema.HasPrefixItems())
            {
                // This is a simple array alias - resolve to the underlying array type
                return GetArraySchemaType(arraySchema, openApiDoc, registry, operationId, pathSegment, context, inlineSchemas);
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

                return GetArraySchemaType(actualSchema, openApiDoc, registry, operationId, pathSegment, context, inlineSchemas);
            }

            // Handle inline object schemas with properties
            if (InlineSchemaExtractor.IsInlineObjectSchema(actualSchema) &&
                !string.IsNullOrEmpty(operationId) &&
                !string.IsNullOrEmpty(pathSegment) &&
                !string.IsNullOrEmpty(context) &&
                inlineSchemas is not null)
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
        if (openApiDoc.Components?.Schemas is null)
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
        string? context,
        Dictionary<string, ResultClassInlineSchemaInfo>? inlineSchemas)
    {
        if (schema.Items is null)
        {
            return "List<object>";
        }

        // Delegate to GetSchemaTypeName for proper handling of schema references, resolved schemas, and inline objects
        // Array items get the context of the array: "ResponseItem" for a success body (ListReportsResponseItem),
        // "{Status}ErrorItem" for an error body, so an error item never takes the success item's type.
        var itemType = GetSchemaTypeName(
            schema.Items,
            openApiDoc,
            registry,
            operationId,
            pathSegment,
            context is null or "Response" ? "ResponseItem" : context + "Item",
            inlineSchemas);

        return $"List<{itemType}>";
    }
}