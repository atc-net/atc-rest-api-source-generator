// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable CommentTypo
// ReSharper disable InvertIf
// ReSharper disable LoopCanBeConvertedToQuery
namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// The operation rules (ATC_API_OPR): operation ids, parameters, request bodies, responses and their statuses.
/// </summary>
public static partial class OpenApiDocumentValidator
{
    /// <summary>
    /// ATC_API_OPR036: an OAS 3.2 <c>in: querystring</c> parameter is the whole query string - it MUST NOT appear more
    /// than once, nor next to an <c>in: query</c> parameter of the operation or its path item (the generated server
    /// would bind both from one query string, a client could not send both). Every strategy.
    /// </summary>
    private static List<DiagnosticMessage> ValidateQueryStringParameters(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();
        foreach (var path in document.Paths ?? [])
        {
            foreach (var operation in path.Value?.Operations ?? [])
            {
                var locations = (path.Value!.Parameters ?? [])
                    .Concat(operation.Value?.Parameters ?? [])
                    .Select(p => p.Resolve().Parameter?.In)
                    .ToList();
                var queryStrings = locations.Count(l => l == ParameterLocation.QueryString);
                if (queryStrings > 1 || (queryStrings == 1 && locations.Contains(ParameterLocation.Query)))
                {
                    var operationId = operation.Value?.OperationId ?? $"{operation.Key.Method} {path.Key}";
                    diagnostics.Add(new DiagnosticMessage(
                        RuleIdentifiers.QueryStringParameterNotAlone,
                        $"Operation '{operationId}' has {(queryStrings > 1 ? "more than one 'in: querystring' parameter" : "an 'in: querystring' parameter next to 'in: query' parameters")}; the querystring parameter is the whole query string (OpenAPI 3.2).",
                        DiagnosticSeverity.Error,
                        sourceFilePath,
                        Suggestions: ["Keep one 'in: querystring' parameter and move the query parameters into its content, or use 'in: query' parameters only"]));
                }
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// ATC_API_OPR037: a parameter whose value the generated code cannot bind or send (see
    /// <see cref="OpenApiParameterExtensions"/>' <c>IsUnbindable</c>). A warning - a client leaves the parameter out, a
    /// server generator raises it to an error - and an error in the path, where no client could build the URL.
    /// Every strategy.
    /// </summary>
    private static List<DiagnosticMessage> ValidateParameterValues(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();
        foreach (var path in document.Paths ?? [])
        {
            foreach (var operation in path.Value?.Operations ?? [])
            {
                var operationId = operation.Value?.OperationId ?? $"{operation.Key.Method} {path.Key}";
                var resolved = (path.Value!.Parameters ?? [])
                    .Concat(operation.Value?.Parameters ?? [])
                    .Select(parameter => parameter.Resolve().Parameter)
                    .OfType<OpenApiParameter>()
                    .ToList();

                // ATC_API_OPR038: a form object's property is sent by its own name - one another query parameter has
                var queryNames = new HashSet<string>(
                    resolved.Where(p => p.In == ParameterLocation.Query && p.Name is not null).Select(p => p.Name!),
                    StringComparer.Ordinal);
                foreach (var queryObject in resolved.Select(p => p.GetQueryObject()).OfType<OpenApiParameterExtensions.QueryObject>().Where(o => !o.DeepObject))
                {
                    foreach (var property in queryObject.Properties.Where(p => queryNames.Contains(p.WireName)))
                    {
                        diagnostics.Add(new DiagnosticMessage(
                            RuleIdentifiers.QueryObjectKeyCollision,
                            $"Property '{property.WireName}' of the query object '{queryObject.ParameterName}' of operation '{operationId}' is sent by its own name ('form', 'explode'), which another query parameter of the operation has; both would be one key.",
                            DiagnosticSeverity.Error,
                            sourceFilePath,
                            Suggestions: ["Send the object as 'style: deepObject' (its keys are 'paging[limit]'), or rename the parameter or the property"]));
                    }
                }

                foreach (var p in resolved)
                {
                    if (p.IsUnbindable())
                    {
                        var inPath = p.In == ParameterLocation.Path;
                        diagnostics.Add(new DiagnosticMessage(
                            RuleIdentifiers.ParameterValueNotBindable,
                            $"Parameter '{p.Name}' ({p.In?.ToString().ToLowerInvariant()}) of operation '{operationId}' is a value the generated code cannot bind or send (a nested object or an array in an object, an object outside the query or in 'form' without 'explode', an inline object, or a 'oneOf'/'anyOf'/'allOf' without a type){(inPath ? "." : "; a client leaves it out, a server cannot be generated.")}",
                            inPath ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                            sourceFilePath,
                            Suggestions: ["Send the object as 'style: deepObject' or 'form' with 'explode: true' with one value per property, declare its properties as parameters of their own, send it in a request body, or give the parameter a 'content' map"]));
                    }
                }

                // An object, a map or a composition (or a list of one) is an application/json part of a multipart form;
                // a URL-encoded form has no encoding for it, nor has a multipart form whose encoding names another type
                foreach (var field in FormFieldsWithNoFormEncoding(operation.Value))
                {
                    diagnostics.Add(field.EncodingContentType is null
                        ? new DiagnosticMessage(
                            RuleIdentifiers.ParameterValueNotBindable,
                            $"Operation '{operationId}' request body ({field.MediaType}) field '{field.Name}' is an object, a map or a composition, which has no URL-encoded form; the generated clients leave it out, a server cannot be generated.",
                            DiagnosticSeverity.Warning,
                            sourceFilePath,
                            Suggestions: ["Declare the field as a scalar, a list of scalars or a file, declare the body as multipart/form-data (the field is then an application/json part), or declare the body as application/json"])
                        : new DiagnosticMessage(
                            RuleIdentifiers.ParameterValueNotBindable,
                            $"Operation '{operationId}' request body ({field.MediaType}) field '{field.Name}' is an object, a map or a composition whose encoding names '{field.EncodingContentType}', which does not carry it (an application/json part carries any such field, an XML part a model, a URL-encoded part a model whose properties are each one value); the generated clients leave it out, a server cannot be generated.",
                            DiagnosticSeverity.Warning,
                            sourceFilePath,
                            Suggestions: ["Leave out the field's encoding contentType, or set it to application/json - or to application/xml for a model, or application/x-www-form-urlencoded for a model whose properties are each one value"]));
                }
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// The fields of an operation's form body (a form-only body; beside JSON the body is JSON), in the form the
    /// generated code sends (multipart before URL-encoded), that have no encoding there (see
    /// <see cref="FormBodyHelper.HasNoFormEncoding"/>), with the media type the multipart encoding names, if any.
    /// </summary>
    private static IEnumerable<(string MediaType, string Name, string? EncodingContentType)> FormFieldsWithNoFormEncoding(
        OpenApiOperation? operation)
    {
        if (operation?.RequestBody?.Content is not { Count: > 0 } content ||
            content.Any(e => ResponseBodyClassifier.Classify(e.Key, e.Value.Schema) == ResponseBodyKind.Json) ||
            operation.GetFormBodyMediaType() is not { } mediaType)
        {
            yield break;
        }

        var form = content[mediaType];
        var (_, properties, _) = operation.GetFormBodySchemaInfo();
        foreach (var property in properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            if (FormBodyHelper.HasNoFormEncoding(mediaType, form, property.Key, property.Value))
            {
                var encodingContentType = FormBodyHelper.FindMultipartFormDataMediaType(content) == mediaType &&
                                          form.Encoding?.TryGetValue(property.Key, out var encoding) == true
                    ? encoding.ContentType
                    : null;
                yield return (mediaType, property.Key, encodingContentType);
            }
        }
    }

    /// <summary>
    /// Warns (Standard level) when an operation does not declare an <c>operationId</c>.
    /// The generator synthesises a name from the HTTP method and path, which tends to be
    /// long and unreadable. This warning surfaces the issue before the user encounters
    /// cryptic CS0234/CS0246 compilation errors in the generated code.
    /// </summary>
    private static List<DiagnosticMessage> ValidateMissingOperationIds(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.Paths is null)
        {
            return diagnostics;
        }

        foreach (var pathEntry in document.Paths)
        {
            var pathKey = pathEntry.Key;

            if (pathEntry.Value is not IOpenApiPathItem pathItem || pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operationEntry in pathItem.Operations)
            {
                var opValue = operationEntry.Value;
                if (opValue is null || !string.IsNullOrEmpty(opValue.OperationId))
                {
                    continue;
                }

                var httpMethodLower = operationEntry.Key.ToString().ToLowerInvariant();
                var httpMethodUpper = httpMethodLower.ToUpperInvariant();

                // Compute the synthetic class name the generator will use
                // (mirrors EndpointPerOperationExtractor / HttpClientExtractor logic)
                var normalizedPath = pathKey
                    .Replace("/", "_")
                    .Replace("{", string.Empty)
                    .Replace("}", string.Empty);
                var syntheticId = $"{httpMethodLower}{normalizedPath}";
                var syntheticClassName = CasingHelper.ToPascalCase(syntheticId);

                // Build a concise suggested operationId from the last non-parameter path segment
                var lastSegment = pathKey
                    .Split('/')
                    .LastOrDefault(s => s.Length > 0 && !s.StartsWith("{", StringComparison.Ordinal));
                var suggestedId = lastSegment is not null
                    ? $"{httpMethodLower}{CasingHelper.ToPascalCase(lastSegment)}"
                    : syntheticId;

                diagnostics.Add(DiagnosticBuilder.MissingOperationIdWarning(
                    httpMethodUpper,
                    pathKey,
                    syntheticClassName,
                    suggestedId,
                    sourceFilePath));
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Checks if a type is a simple data type.
    /// </summary>
    private static bool IsSimpleDataType(string? type)
    {
        if (string.IsNullOrEmpty(type))
        {
            return false;
        }

        return type is
            "string" or
            "integer" or
            "number" or
            "boolean";
    }

    /// <summary>
    /// Validates that no operation declares a response for a status range (ATC_API_OPR034): <c>2XX</c> names no
    /// status, so it is generated as its first status (200) - unless the operation declares that status, in which
    /// case the range is not generated. The normalizer rewrites the document accordingly after validation.
    /// </summary>
    private static List<DiagnosticMessage> ValidateResponseRanges(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();
        var pathItems = (document.Paths ?? []).Select(p => (p.Key, p.Value))
            .Concat((document.Webhooks ?? new Dictionary<string, IOpenApiPathItem>(StringComparer.Ordinal)).Select(w => (w.Key, w.Value)));
        foreach (var (pathKey, pathItem) in pathItems)
        {
            foreach (var operationEntry in pathItem?.Operations ?? new Dictionary<HttpMethod, OpenApiOperation>())
            {
                var operation = operationEntry.Value;
                if (operation?.Responses is null)
                {
                    continue;
                }

                var operationId = operation.OperationId ?? $"{operationEntry.Key.ToString().ToUpperInvariant()} {pathKey}";
                foreach (var (key, status, statusDeclared) in ResponseRangeNormalizer.RangesOf(operation))
                {
                    var outcome = statusDeclared
                        ? $"{status} is declared, so the range is not generated"
                        : $"it is generated as {status}";
                    diagnostics.Add(new DiagnosticMessage(
                        RuleIdentifiers.ResponseStatusRange,
                        $"Operation '{operationId}' declares the status range '{key}', which names no status a server could answer or a client could read with: {outcome}. Declare each status the operation returns.",
                        DiagnosticSeverity.Warning,
                        sourceFilePath));
                }
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates that no HEAD operation declares content for a response (ATC_API_OPR039): a HEAD answer has no body,
    /// so the content is not generated. The normalizer removes it from the document after validation.
    /// </summary>
    private static List<DiagnosticMessage> ValidateHeadResponses(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();
        var pathItems = (document.Paths ?? []).Select(p => (p.Key, p.Value))
            .Concat((document.Webhooks ?? new Dictionary<string, IOpenApiPathItem>(StringComparer.Ordinal)).Select(w => (w.Key, w.Value)));
        foreach (var (pathKey, pathItem) in pathItems)
        {
            foreach (var operationEntry in pathItem?.Operations ?? new Dictionary<HttpMethod, OpenApiOperation>())
            {
                var statuses = HeadResponseNormalizer.ResponsesWithContent(operationEntry.Key, operationEntry.Value);
                if (statuses.Count == 0)
                {
                    continue;
                }

                var operationId = operationEntry.Value.OperationId ?? $"HEAD {pathKey}";
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.HeadResponseContentIgnored,
                    $"Operation '{operationId}' is a HEAD and declares content for {string.Join(", ", statuses)}. A HEAD answer has no body, so the content is not generated: the clients return no value for it and the server's result takes none. Remove the content from the HEAD's responses (its headers are kept).",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates operations (ATC_API_OPR001-018).
    /// </summary>
    private static List<DiagnosticMessage> ValidateOperations(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.Paths is null)
        {
            return diagnostics;
        }

        foreach (var pathEntry in document.Paths)
        {
            var pathKey = pathEntry.Key;
            var pathItem = pathEntry.Value;
            var pathStart = diagnostics.Count;

            // Get global path parameters
            var globalPathParameters = pathItem.Parameters?
                .Where(p => p.In == ParameterLocation.Path)
                .Select(p => p.Name)
                .ToList() ?? [];

            // ATC_API_OPR011: Global path parameter not present in route
            foreach (var globalParam in globalPathParameters)
            {
                if (pathKey.IndexOf($"{{{globalParam}}}", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    diagnostics.Add(new DiagnosticMessage(
                        RuleIdentifiers.GlobalPathParameterNotInRoute,
                        $"Defined global path parameter '{globalParam}' does not exist in route '{pathKey}'.",
                        DiagnosticSeverity.Error,
                        sourceFilePath));
                }
            }

            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operationEntry in pathItem.Operations)
            {
                var httpMethod = operationEntry
                    .Key
                    .ToString()
                    .ToLowerInvariant();

                var operation = operationEntry.Value;
                var operationStart = diagnostics.Count;

                ValidateOperation(
                    diagnostics,
                    sourceFilePath,
                    document,
                    pathKey,
                    pathItem,
                    httpMethod,
                    operation);
                InScope(diagnostics, operationStart, SpecSuppressions.Operation(httpMethod, pathKey));
            }

            InScope(diagnostics, pathStart, SpecSuppressions.Path(pathKey));
        }

        return diagnostics;
    }

    private static void ValidateOperation(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document,
        string pathKey,
        IOpenApiPathItem pathItem,
        string httpMethod,
        OpenApiOperation operation)
    {
        var httpMethodUpper = httpMethod.ToUpperInvariant();

        // ATC_API_OPR001: Missing operationId
        if (string.IsNullOrEmpty(operation.OperationId))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.OperationIdMissing,
                $"Missing operationId in path '{httpMethodUpper} {pathKey}'.",
                DiagnosticSeverity.Error,
                sourceFilePath));
            return;
        }

        var operationId = operation.OperationId!;
        var context = new OperationContext(diagnostics, sourceFilePath, document, pathKey, pathItem, httpMethod, operation, operationId);

        // ATC_API_OPR002: OperationId not using correct casing style
        ValidateOperationIdCasing(context);

        // ATC_API_OPR003-007: HTTP method prefix validations
        ValidateOperationIdPrefix(context);

        // ATC_API_OPR008/009: Pluralization validation
        ValidateOperationIdPluralization(context);

        // ATC_API_OPR010: BadRequest without parameters
        ValidateBadRequestResponse(context);

        // ATC_API_OPR021: Unauthorized without security
        ValidateUnauthorizedResponse(context);

        // ATC_API_OPR022: Forbidden without authorization
        ValidateForbiddenResponse(context);

        // ATC_API_OPR023: NotFound on POST operation
        ValidateNotFoundResponse(context);

        // ATC_API_OPR024: Conflict on non-mutating operation
        ValidateConflictResponse(context);

        // ATC_API_OPR025: TooManyRequests without rate limiting
        ValidateTooManyRequestsResponse(context);

        // ATC_API_OPR012 (a placeholder no parameter declares) is checked under every strategy, with the paths

        // ATC_API_OPR013: Operation path parameter not present in route
        ValidateOperationPathParametersInRoute(context);

        // ATC_API_OPR014: GET with path parameter missing NotFound response
        ValidateGetWithPathParameterNotFound(context);

        // ATC_API_OPR015/016: Path parameter validation
        ValidatePathParameters(context);

        // ATC_API_OPR017: RequestBody with inline model not supported
        ValidateRequestBody(context);

        // ATC_API_OPR018: Multiple 2xx status codes not supported
        ValidateResponseStatusCodes(context);

        // ATC_API_CACHE001: Output caching configured on a method the middleware never caches
        ValidateOutputCacheMethod(context);

        // ATC_API_CACHE002: Output caching configured on an operation whose callers are authenticated
        ValidateOutputCacheAuthentication(context);

        // ATC_API_OPR027: GET with an array query parameter that can outgrow the request line
        ValidateLargeArrayQueryParameters(context);

        // ATC_API_OPR028/029: QUERY operation whose shape contradicts what the method means
        ValidateQueryOperationShape(context);

        // ATC_API_OPR026: Parameter serialization not supported
        ValidateParameterSerialization(context);
    }

    // ATC_API_OPR002: an operationId neither camelCase nor kebab-case
    private static void ValidateOperationIdCasing(OperationContext context)
    {
        var (diagnostics, sourceFilePath, httpMethodUpper, pathKey, operationId) = (context.Diagnostics, context.SourceFilePath, context.HttpMethodUpper, context.PathKey, context.OperationId);

        if (!CasingHelper.IsValidOperationIdCasing(operationId))
        {
            var detectedStyle = CasingHelper.GetDetectedCasingStyle(operationId);
            var suggestedCamelCase = CasingHelper.SuggestCamelCase(operationId);

            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.OperationIdCasing,
                $"OperationId '{operationId}' is not using a valid casing style. " +
                $"Detected: {detectedStyle}. " +
                $"Expected: camelCase or kebab-case. " +
                $"Suggestion: '{suggestedCamelCase}'. " +
                $"Location: {httpMethodUpper} {pathKey}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    // ATC_API_OPR013: an operation's path parameter the route does not have
    private static void ValidateOperationPathParametersInRoute(
        OperationContext context)
    {
        var (diagnostics, sourceFilePath, pathKey, operation, operationId) = (context.Diagnostics, context.SourceFilePath, context.PathKey, context.Operation, context.OperationId);

        var opPathParams = operation.Parameters?
            .Where(p => p.In == ParameterLocation.Path)
            .ToList() ?? [];

        foreach (var pathParam in opPathParams)
        {
            if (pathKey.IndexOf($"{{{pathParam.Name}}}", StringComparison.OrdinalIgnoreCase) < 0)
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.OperationPathParameterNotInRoute,
                    $"Defined path parameter '{pathParam.Name}' does not exist in route '{pathKey}' for operation '{operationId}'.",
                    DiagnosticSeverity.Error,
                    sourceFilePath));
            }
        }
    }

    // ATC_API_OPR014: a GET by a path parameter without a 404 response
    private static void ValidateGetWithPathParameterNotFound(
        OperationContext context)
    {
        var (diagnostics, sourceFilePath, pathItem, httpMethod, operation, operationId) = (context.Diagnostics, context.SourceFilePath, context.PathItem, context.HttpMethod, context.Operation, context.OperationId);

        if (string.Equals(httpMethod, "get", StringComparison.OrdinalIgnoreCase))
        {
            var hasPathParam = (pathItem.Parameters?.Any(p => p.In == ParameterLocation.Path) ?? false) ||
                               (operation.Parameters?.Any(p => p.In == ParameterLocation.Path) ?? false);

            if (hasPathParam && !operation.HasNotFoundResponse())
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.GetMissingNotFoundResponse,
                    $"Missing NotFound (404) response type for operation '{operationId}', required by path parameter.",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }
    }

    // ATC_API_OPR026: a parameter serialization the generated code does not support (one that cannot be bound at all
    // is ATC_API_OPR037, under every strategy)
    private static void ValidateParameterSerialization(OperationContext context)
    {
        var (diagnostics, sourceFilePath, operation) = (context.Diagnostics, context.SourceFilePath, context.Operation);

        if (operation.Parameters is not null)
        {
            foreach (var parameter in operation.Parameters)
            {
                // A value that cannot be bound at all is ATC_API_OPR037 (every strategy)
                if (parameter.Resolve().Parameter is { } p && !p.IsUnbindable())
                {
                    var serialization = p.GetParameterSerialization();
                    if (!serialization.IsSupported)
                    {
                        diagnostics.Add(DiagnosticBuilder.ParameterSerializationNotSupportedWarning(
                            p.Name ?? "(unnamed)",
                            $"style '{serialization.Style}' explode={serialization.Explode.ToString().ToLowerInvariant()} on {serialization.ValueKind}",
                            sourceFilePath));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates operationId prefix based on HTTP method.
    /// </summary>
    private static void ValidateOperationIdPrefix(OperationContext context)
    {
        var (diagnostics, sourceFilePath, httpMethod, operationId, pathKey) = (context.Diagnostics, context.SourceFilePath, context.HttpMethod, context.OperationId, context.PathKey);

        var httpMethodUpper = httpMethod.ToUpperInvariant();

        if (string.Equals(httpMethod, "get", StringComparison.OrdinalIgnoreCase))
        {
            // ATC_API_OPR003: GET operationId should start with 'Get' or 'List'
            if (!operationId.StartsWith("get", StringComparison.OrdinalIgnoreCase) &&
                !operationId.StartsWith("list", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.GetOperationIdPrefix,
                    $"OperationId '{operationId}' should start with 'get' or 'list' for GET operation. Location: {httpMethodUpper} {pathKey}",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }
        else if (string.Equals(httpMethod, "post", StringComparison.OrdinalIgnoreCase))
        {
            // ATC_API_OPR004: POST operationId should not start with 'Delete'
            if (operationId.StartsWith("delete", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PostOperationIdPrefix,
                    $"OperationId '{operationId}' should not start with 'delete' for POST operation. Location: {httpMethodUpper} {pathKey}",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }
        else if (string.Equals(httpMethod, "put", StringComparison.OrdinalIgnoreCase))
        {
            // ATC_API_OPR005: PUT operationId should start with 'Update'
            if (!operationId.StartsWith("update", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PutOperationIdPrefix,
                    $"OperationId '{operationId}' should start with 'update' for PUT operation. Location: {httpMethodUpper} {pathKey}",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }
        else if (string.Equals(httpMethod, "patch", StringComparison.OrdinalIgnoreCase))
        {
            // ATC_API_OPR006: PATCH operationId should start with 'Patch' or 'Update'
            if (!operationId.StartsWith("patch", StringComparison.OrdinalIgnoreCase) &&
                !operationId.StartsWith("update", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PatchOperationIdPrefix,
                    $"OperationId '{operationId}' should start with 'patch' or 'update' for PATCH operation. Location: {httpMethodUpper} {pathKey}",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }
        else if (string.Equals(httpMethod, "delete", StringComparison.OrdinalIgnoreCase) &&
            !operationId.StartsWith("delete", StringComparison.OrdinalIgnoreCase) &&
            !operationId.StartsWith("remove", StringComparison.OrdinalIgnoreCase))
        {
            // ATC_API_OPR007: DELETE operationId should start with 'Delete' or 'Remove'
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.DeleteOperationIdPrefix,
                $"OperationId '{operationId}' should start with 'delete' or 'remove' for DELETE operation. Location: {httpMethodUpper} {pathKey}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates operationId pluralization matches response type.
    /// </summary>
    private static void ValidateOperationIdPluralization(
        OperationContext context)
    {
        var (diagnostics, sourceFilePath, document, operation, operationId, pathKey, httpMethod) = (context.Diagnostics, context.SourceFilePath, context.Document, context.Operation, context.OperationId, context.PathKey, context.HttpMethod);

        // OPR008/OPR009 enforce a *read-side* naming convention: does this operation's name
        // promise one item or many? For an action operation the operationId names what it acts on,
        // not what it returns, so `resendPendingDevices` answering with one summary of counts is
        // correct rather than a defect. Applying the convention to writes flagged that whole
        // category and rewarded padding the name until it stopped ending in 's'.
        if (!IsCardinalityCheckedMethod(httpMethod))
        {
            return;
        }

        var responseSchema = GetSuccessResponseSchema(operation);
        if (responseSchema is null)
        {
            return;
        }

        // Author assertion via `x-operation-response-cardinality: single|array` trumps the
        // name-based heuristic for the reads that reach this point. It is the intended answer for a
        // read whose plural name describes what it aggregated — `getTotalDownloads` counts many
        // downloads and returns one total. The response-shape cross-check still runs, so an
        // annotation that disagrees with the actual response still surfaces a warning.
        var cardinalityAnnotation = operation.GetResponseCardinalityAnnotation();
        var isPluralized = cardinalityAnnotation switch
        {
            "single" => false,
            "array" => true,
            _ => IsOperationIdPluralized(operationId),
        };

        // Direct array or paginated response (applies to both OPR008 and OPR009). A streamed response is one too: a
        // sequence of its schema, whatever the framing (Server-Sent Events, JSON Lines, an itemSchema)
        var isDirectArrayResponse = IsArraySchema(responseSchema, document) ||
                                    IsPaginatedSchema(responseSchema, document) ||
                                    operation.IsStreamingResponse();

        // Wrapper object containing an array (only applies to OPR008)
        var isWrapperWithArray = IsObjectContainingArray(responseSchema, document);

        // ATC_API_OPR008: Pluralized operationId but response is single item
        // Allow if response is direct array, paginated, OR wrapper containing array
        if (isPluralized && !isDirectArrayResponse && !isWrapperWithArray)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleId: RuleIdentifiers.OperationIdPluralizationMismatch,
                Message: $"OperationId '{operationId}' is pluralized but response is a single item. " +
                         $"Location: {httpMethod.ToUpperInvariant()} {pathKey}",
                Severity: DiagnosticSeverity.Warning,
                FilePath: sourceFilePath,
                Suggestions:
                [
                    "Add 'x-operation-response-cardinality: single' to the operation if the plural operationId names what it aggregates rather than what it returns.",
                    "Otherwise rename the operation to a singular subject, or return the collection the name promises.",
                ],
                DocumentationUrl: Constants.Documentation.GetRuleUrl(RuleIdentifiers.OperationIdPluralizationMismatch)));
        }

        // ATC_API_OPR009: Singular operationId but response is array
        // Only trigger for direct arrays or paginated responses, NOT for wrappers with arrays
        if (!isPluralized && isDirectArrayResponse && !isWrapperWithArray)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleId: RuleIdentifiers.OperationIdSingularMismatch,
                Message: $"OperationId '{operationId}' is singular but response is an array. " +
                         $"Location: {httpMethod.ToUpperInvariant()} {pathKey}",
                Severity: DiagnosticSeverity.Warning,
                FilePath: sourceFilePath,
                Suggestions:
                [
                    "Add 'x-operation-response-cardinality: array' to the operation if the singular operationId names one subject whose result is legitimately a list.",
                    "Otherwise rename the operation to a plural subject, or return the single item the name promises.",
                ],
                DocumentationUrl: Constants.Documentation.GetRuleUrl(RuleIdentifiers.OperationIdSingularMismatch)));
        }
    }

    /// <summary>
    /// Validates BadRequest response has parameters.
    /// </summary>
    private static void ValidateBadRequestResponse(OperationContext context)
    {
        var (diagnostics, sourceFilePath, pathItem, operation, operationId) = (context.Diagnostics, context.SourceFilePath, context.PathItem, context.Operation, context.OperationId);

        var hasBadRequest = operation.Responses?.ContainsKey("400") ?? false;
        if (!hasBadRequest)
        {
            return;
        }

        var hasParameters = (operation.Parameters?.Count > 0) ||
                           (operation.RequestBody is not null) ||
                           (pathItem.Parameters?.Count > 0);

        // ATC_API_OPR010: Has BadRequest response but no parameters
        if (!hasParameters)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.BadRequestWithoutParameters,
                $"Operation '{operationId}' contains BadRequest (400) response but has no parameters or request body.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates 401 Unauthorized response has security requirements.
    /// </summary>
    private static void ValidateUnauthorizedResponse(OperationContext context)
    {
        var (diagnostics, sourceFilePath, document, pathItem, operation, operationId) = (context.Diagnostics, context.SourceFilePath, context.Document, context.PathItem, context.Operation, context.OperationId);

        var hasUnauthorized = operation.Responses?.ContainsKey("401") ?? false;
        if (!hasUnauthorized)
        {
            return;
        }

        var securityConfig = operation.ExtractUnifiedSecurityConfiguration(pathItem, document);
        var hasSecurity = securityConfig is { AuthenticationRequired: true };

        // ATC_API_OPR021: Has 401 Unauthorized but no security requirements
        if (!hasSecurity)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.UnauthorizedWithoutSecurity,
                $"Operation '{operationId}' defines 401 Unauthorized response but has no security requirements.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates 403 Forbidden response is on an operation with an effective security requirement.
    /// <para>
    /// An authenticated operation needs no role, policy or scope to return 403: the handler may
    /// refuse a signed-in caller for application-level reasons ("not a known user", "not your
    /// resource"). Only an anonymous operation declaring 403 is flagged.
    /// </para>
    /// </summary>
    private static void ValidateForbiddenResponse(OperationContext context)
    {
        var (diagnostics, sourceFilePath, document, pathItem, operation, operationId) = (context.Diagnostics, context.SourceFilePath, context.Document, context.PathItem, context.Operation, context.OperationId);

        var hasForbidden = operation.Responses?.ContainsKey("403") ?? false;
        if (!hasForbidden)
        {
            return;
        }

        var securityConfig = operation.ExtractUnifiedSecurityConfiguration(pathItem, document);
        var hasSecurity = securityConfig is not null &&
            (securityConfig.AuthenticationRequired ||
             securityConfig.Roles.Count > 0 ||
             securityConfig.Policies.Count > 0 ||
             securityConfig.Scopes.Count > 0);

        // ATC_API_OPR022: Has 403 Forbidden but no security requirement at all (anonymous)
        if (!hasSecurity)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ForbiddenWithoutAuthorization,
                $"Operation '{operationId}' defines 403 Forbidden response but has no security requirements (it allows anonymous access).",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates 404 NotFound response is not on POST operation (unless it has path parameters).
    /// </summary>
    private static void ValidateNotFoundResponse(OperationContext context)
    {
        var (diagnostics, sourceFilePath, httpMethod, pathKey, operation, operationId) = (context.Diagnostics, context.SourceFilePath, context.HttpMethod, context.PathKey, context.Operation, context.OperationId);

        var hasNotFound = operation.Responses?.ContainsKey("404") ?? false;
        if (!hasNotFound)
        {
            return;
        }

        // ATC_API_OPR023: Has 404 NotFound on POST operation
        if (string.Equals(httpMethod, "post", StringComparison.OrdinalIgnoreCase))
        {
            // Skip warning if POST has path parameters - the referenced resource might not exist
            // Example: POST /devices/{deviceId}/scan - the device might not exist, so 404 is valid
            var hasPathParameters = pathKey.Contains('{') ||
                                    (operation.Parameters?.Any(p => p.In == ParameterLocation.Path) ?? false);
            if (hasPathParameters)
            {
                return;
            }

            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.NotFoundOnPostOperation,
                $"Operation '{operationId}' defines 404 NotFound response on POST operation - POST creates resources, so 'not found' is unusual.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates 409 Conflict response is on mutating operation (POST/PUT/PATCH/DELETE).
    /// </summary>
    private static void ValidateConflictResponse(OperationContext context)
    {
        var (diagnostics, sourceFilePath, httpMethod, operation, operationId) = (context.Diagnostics, context.SourceFilePath, context.HttpMethod, context.Operation, context.OperationId);

        var hasConflict = operation.Responses?.ContainsKey("409") ?? false;
        if (!hasConflict)
        {
            return;
        }

        // ATC_API_OPR024: Has 409 Conflict on read-only operation (GET)
        var isReadOnly = string.Equals(httpMethod, "get", StringComparison.OrdinalIgnoreCase);

        if (isReadOnly)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ConflictOnNonMutatingOperation,
                $"Operation '{operationId}' defines 409 Conflict response but operation is {httpMethod.ToUpperInvariant()} - conflicts typically occur during POST/PUT/PATCH/DELETE operations.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates 429 TooManyRequests response has rate limiting configured.
    /// </summary>
    private static void ValidateTooManyRequestsResponse(
        OperationContext context)
    {
        var (diagnostics, sourceFilePath, document, pathItem, operation, operationId) = (context.Diagnostics, context.SourceFilePath, context.Document, context.PathItem, context.Operation, context.OperationId);

        var hasTooManyRequests = operation.Responses?.ContainsKey("429") ?? false;
        if (!hasTooManyRequests)
        {
            return;
        }

        var rateLimitConfig = operation.ExtractRateLimitConfiguration(pathItem, document);
        var hasRateLimiting = rateLimitConfig is not null;

        // ATC_API_OPR025: Has 429 TooManyRequests but no rate limiting configured
        if (!hasRateLimiting)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.TooManyRequestsWithoutRateLimiting,
                $"Operation '{operationId}' defines 429 TooManyRequests response but no rate limiting is configured (x-ratelimit-* extensions).",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Validates path parameters have required=true and are not nullable.
    /// </summary>
    private static void ValidatePathParameters(OperationContext context)
    {
        var (diagnostics, sourceFilePath, operation) = (context.Diagnostics, context.SourceFilePath, context.Operation);

        if (operation.Parameters is null)
        {
            return;
        }

        foreach (var parameter in operation.Parameters.Where(p => p.In == ParameterLocation.Path))
        {
            // ATC_API_OPR015: Path parameter missing required=true
            if (!parameter.Required)
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PathParameterNotRequired,
                    $"Path parameter '{parameter.Name}' for operation '{operation.OperationId}' is missing required=true.",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }

            // ATC_API_OPR016: Path parameter must not be nullable
            if (parameter.Schema is OpenApiSchema schema && schema.IsNullable)
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.PathParameterNullable,
                    $"Path parameter '{parameter.Name}' for operation '{operation.OperationId}' must not be nullable.",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }
        }
    }

    /// <summary>
    /// Validates request body is not inline model.
    /// </summary>
    private static void ValidateRequestBody(OperationContext context)
    {
        var (diagnostics, sourceFilePath, operation, operationId) = (context.Diagnostics, context.SourceFilePath, context.Operation, context.OperationId);

        if (operation.RequestBody?.Content is null)
        {
            return;
        }

        foreach (var contentEntry in operation.RequestBody.Content)
        {
            var schema = contentEntry.Value.Schema;
            if (schema is null)
            {
                continue;
            }

            // Skip binary/file uploads
            if (schema is OpenApiSchema openApiSchema &&
                string.Equals(openApiSchema.Format, "binary", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // ATC_API_OPR017: RequestBody with inline model not supported
            if (schema is not OpenApiSchemaReference &&
                schema is OpenApiSchema { Properties.Count: > 0 })
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.RequestBodyInlineModel,
                    $"RequestBody is defined with inline model for operation '{operationId}' - only reference to component schemas are supported.",
                    DiagnosticSeverity.Error,
                    sourceFilePath));
            }
        }
    }

    /// <summary>
    /// Validates operation does not have multiple 2xx response codes.
    /// </summary>
    private static void ValidateResponseStatusCodes(OperationContext context)
    {
        var (diagnostics, sourceFilePath, operation, operationId) = (context.Diagnostics, context.SourceFilePath, context.Operation, context.OperationId);

        if (operation.Responses is null)
        {
            return;
        }

        var successResponses = operation.Responses.Keys
            .Where(k => k.StartsWith("2", StringComparison.Ordinal))
            .ToList();

        // ATC_API_OPR018: Multiple 2xx status codes not supported
        if (successResponses.Count > 1)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.Multiple2xxStatusCodes,
                $"Operation '{operationId}' contains multiple 2xx status codes ({string.Join(", ", successResponses)}), which is not supported.",
                DiagnosticSeverity.Error,
                sourceFilePath));
        }

        // ATC_API_OPR030: an informational (1xx) status is never a final response
        foreach (var informational in operation.Responses.Keys.Where(k => k.Length == 3 && k.StartsWith("1", StringComparison.Ordinal)))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.InformationalResponseStatusCode,
                $"Operation '{operationId}' declares the informational status {informational}, which is never a final response (RFC 9110 §15.2): no factory or client accessor is generated for it.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        // ATC_API_OPR031: a body that is not generated as declared (never dropped without notice)
        foreach (var message in BodiesNotGeneratedAsDeclared(operationId, operation))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.BodyNotGeneratedAsDeclared,
                message,
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// The bodies of an operation the generator does not represent as declared (ATC_API_OPR031).
    /// </summary>
    private static IEnumerable<string> BodiesNotGeneratedAsDeclared(
        string operationId,
        OpenApiOperation operation)
    {
        foreach (var entry in operation.Responses ?? new OpenApiResponses())
        {
            var status = entry.Key;
            var response = entry.Value;
            if (response.Content is not { Count: > 0 } content)
            {
                continue;
            }

            var hasJson = content.Any(e => ResponseBodyClassifier.Classify(e.Key, e.Value.Schema) == ResponseBodyKind.Json);
            var xmlEntries = content.Where(e => ResponseBodyClassifier.Classify(e.Key, e.Value.Schema) == ResponseBodyKind.Xml).Select(e => e.Key).ToList();
            if (ErrorResponseShapeResolver.IsErrorStatusCode(status))
            {
                // An error: an XML body is written as declared only as a declared XML model
                var shape = ErrorResponseShapeResolver.Resolve(response);
                foreach (var xml in xmlEntries.Where(_ => !hasJson))
                {
                    if (!(shape.Kind == ErrorResponseKind.XmlObject && string.Equals(shape.MediaType, xml, StringComparison.OrdinalIgnoreCase)))
                    {
                        yield return $"Operation '{operationId}' response {status} ({xml}) has no XML mapping (only a component model is mapped, not an inline, polymorphic or list schema): the error is answered and read as a ProblemDetails instead.";
                    }
                }

                continue;
            }

            var xmlOnly = content.XmlOnlyBody;
            var alternative = content.XmlAlternative;
            foreach (var xml in xmlEntries)
            {
                var generated = string.Equals(xmlOnly?.MediaType, xml, StringComparison.OrdinalIgnoreCase) ||
                                alternative?.AlternativeMediaTypes?.Contains(xml, StringComparer.OrdinalIgnoreCase) is true;
                if (!generated)
                {
                    yield return hasJson
                        ? $"Operation '{operationId}' response {status} ({xml}) is not generated: its schema is not the JSON body's component model, so the response is served and read as JSON only."
                        : $"Operation '{operationId}' response {status} ({xml}) has no XML mapping (only a component model or a list of one is mapped, not an inline or polymorphic schema): its body is not generated.";
                }
            }

            // A model and a file: generated as a model or a file for a 200 (decided by the answer's Content-Type), for
            // its primary media type only for any other status
            if (!string.Equals(status, "200", StringComparison.Ordinal) && ResponseBodyClassifier.PrimaryBody(content) is { } primary)
            {
                var primaryIsFile = primary.Kind == ResponseBodyKind.Binary;
                foreach (var other in content.Keys)
                {
                    var kind = ResponseBodyClassifier.Classify(other, content[other].Schema);
                    if (kind is ResponseBodyKind.Binary or ResponseBodyKind.Json or ResponseBodyKind.Xml &&
                        (kind == ResponseBodyKind.Binary) != primaryIsFile)
                    {
                        yield return $"Operation '{operationId}' response {status} ({other}) is not generated: the response declares a model and a file, which is generated for a 200 only; this status is generated for {primary.MediaType} only.";
                    }
                }
            }
        }

        if (operation.RequestBody?.Content is { Count: > 0 } requestContent)
        {
            var hasJson = requestContent.Any(e => ResponseBodyClassifier.Classify(e.Key, e.Value.Schema) == ResponseBodyKind.Json);

            // A URL-encoded form beside JSON: the body is JSON (a form-only body is generated as the form's fields)
            foreach (var form in requestContent.Keys.Where(k => k.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)))
            {
                if (hasJson)
                {
                    yield return $"Operation '{operationId}' request body ({form}) is not generated: the body is sent and bound as JSON only.";
                }
            }

            var xmlOnly = requestContent.XmlOnlyBody;
            var alternative = requestContent.XmlAlternative;
            foreach (var xml in requestContent.Where(e => ResponseBodyClassifier.Classify(e.Key, e.Value.Schema) == ResponseBodyKind.Xml).Select(e => e.Key))
            {
                var body = string.Equals(xmlOnly?.MediaType, xml, StringComparison.OrdinalIgnoreCase)
                    ? xmlOnly
                    : alternative?.AlternativeMediaTypes?.Contains(xml, StringComparer.OrdinalIgnoreCase) is true ? alternative : null;
                if (body is null)
                {
                    yield return hasJson
                        ? $"Operation '{operationId}' request body ({xml}) is not generated: its schema is not the JSON body's component model, so the body is sent and bound as JSON only."
                        : $"Operation '{operationId}' request body ({xml}) has no XML mapping (only a component model or a list of one is mapped, not an inline or polymorphic schema): the body is not generated.";
                }
            }
        }
    }

    /// <summary>
    /// Gets the success response schema (2xx) from an operation.
    /// </summary>
    private static IOpenApiSchema? GetSuccessResponseSchema(
        OpenApiOperation operation)
    {
        if (operation.Responses is null)
        {
            return null;
        }

        // Look for 200, 201, or any 2xx response
        foreach (var key in new[] { "200", "201", "202", "204" })
        {
            if (operation.Responses.TryGetValue(key, out var response))
            {
                var content = response.Content?.FirstOrDefault();
                return content?.Value?.Schema;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether OPR008/OPR009 apply to an operation declared with this HTTP method.
    /// </summary>
    /// <remarks>
    /// The rule pair is a read-side convention — it asks whether the operationId promises one item
    /// or many, and compares that against the response. Only <c>GET</c> and <c>QUERY</c> are reads
    /// whose name is expected to describe the response; on a write the operationId names the
    /// subject being acted on, and a plural subject with a single summary response is correct.
    /// </remarks>
    private static bool IsCardinalityCheckedMethod(string httpMethod)
        => string.Equals(httpMethod, "get", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(httpMethod, "query", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if an operationId signals a collection (plural) response. Considers all PascalCase
    /// words after the verb prefix, so connector patterns like `getJobsForDevice` or
    /// `listPathsByRepositoryName` are recognized as plural via the subject noun (`Jobs` / `Paths`),
    /// not the trailing qualifier (`Device` / `Name`).
    /// </summary>
    private static bool IsOperationIdPluralized(string operationId)
    {
        // Collection-intent prefixes (list/search/find) signal a collection response regardless of
        // the trailing noun — aligning with OPR003 which blesses `List` as the "GET array" prefix.
        // Also ensures `listUser` (collection prefix, singular body) is still caught by OPR008.
        foreach (var prefix in CollectionIntentPrefixes)
        {
            if (operationId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Remove common verb prefixes
        var name = operationId;
        var prefixStripped = false;

        foreach (var prefix in QueryVerbPrefixes)
        {
            if (operationId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                name = operationId.Substring(prefix.Length);
                prefixStripped = true;
                break;
            }
        }

        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (prefixStripped)
        {
            // Query-intent prefix stripped — analyse subject noun(s) to catch connector shapes
            // like `getJobsForDevice` (subject `Jobs`) or `getDeviceDetailsHistory` (suffix `Details`).
            var words = SplitPascalCaseWords(name);

            foreach (var word in words)
            {
                foreach (var suffix in SingleItemSuffixes)
                {
                    if (word.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            foreach (var word in words)
            {
                if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
                    !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
                    !word.EndsWith("us", StringComparison.OrdinalIgnoreCase) &&
                    !word.EndsWith("is", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        // No query-intent prefix recognized (e.g. `scanConfigurationsByDevice`, `createFoo`).
        // Keep the original trailing-noun heuristic to avoid flagging action operations whose
        // body contains an object noun in plural form.
        foreach (var suffix in SingleItemSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return name.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
               !name.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
               !name.EndsWith("us", StringComparison.OrdinalIgnoreCase) &&
               !name.EndsWith("is", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Splits a PascalCase identifier into its component words. Each uppercase letter starts a new
    /// word; leading lowercase characters form the first word.
    /// </summary>
    private static List<string> SplitPascalCaseWords(string input)
    {
        var words = new List<string>();
        if (string.IsNullOrEmpty(input))
        {
            return words;
        }

        var start = 0;
        for (var i = 1; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]))
            {
                words.Add(input.Substring(start, i - start));
                start = i;
            }
        }

        words.Add(input.Substring(start));
        return words;
    }

    /// <summary>
    /// Validates that no two operations have operationIds that become one C# name (ATC_API_OPR032): 'getUser' and
    /// 'get_user' or 'GetUSER' would be one handler interface, one result class and one client method. Names that
    /// differ by case only ('GETUSER' becomes 'Getuser') collide too: one hint name, and one file on a Windows disk.
    /// </summary>
    private static List<DiagnosticMessage> ValidateOperationIdCollisions(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();
        var byName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in document.Paths ?? [])
        {
            foreach (var operation in path.Value?.Operations ?? [])
            {
                var method = operation.Key.Method.ToUpperInvariant();
                var name = OperationIdHelper.Effective(operation.Value, method, path.Key).ToPascalCaseForDotNet();
                if (!byName.TryGetValue(name, out var operations))
                {
                    operations = [];
                    byName[name] = operations;
                }

                operations.Add($"'{operation.Value?.OperationId ?? $"{method} {path.Key}"}' ({method} {path.Key})");
            }
        }

        foreach (var group in byName.Where(g => g.Value.Count > 1))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.OperationIdCollision,
                $"The operations {string.Join(" and ", group.Value)} have operationIds that become the same name '{group.Key}': they would share a handler interface, a result class and a client method.",
                DiagnosticSeverity.Error,
                sourceFilePath,
                Context: $"Operation: {group.Key}",
                Suggestions: ["Give each operation an operationId that is distinct when PascalCased"]));
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates that every parameter has a type (ATC_API_OPR033): a 'schema' or a 'content' map. One with neither
    /// would be an 'object' in the clients, passed where a string belongs. A parameter that is a dangling reference
    /// is reported by the reference check instead.
    /// </summary>
    private static List<DiagnosticMessage> ValidateParameterSchemas(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();
        foreach (var path in document.Paths ?? [])
        {
            foreach (var parameter in path.Value?.Parameters ?? [])
            {
                Check(parameter, $"path '{path.Key}'", path.Key);
            }

            foreach (var operation in path.Value?.Operations ?? [])
            {
                var name = operation.Value?.OperationId ?? $"{operation.Key.Method.ToUpperInvariant()} {path.Key}";
                foreach (var parameter in operation.Value?.Parameters ?? [])
                {
                    Check(parameter, $"operation '{name}'", name);
                }
            }
        }

        return diagnostics;

        void Check(
            IOpenApiParameter? parameter,
            string owner,
            string context)
        {
            if (parameter is OpenApiParameterReference { Target: null } || parameter is null)
            {
                return;
            }

            if (parameter.Schema is null && (parameter.Content is null || parameter.Content.Count == 0))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.ParameterWithoutSchema,
                    $"Parameter '{parameter.Name}' of {owner} has neither a 'schema' nor a 'content' map, so it has no type.",
                    DiagnosticSeverity.Error,
                    sourceFilePath,
                    Context: context,
                    Suggestions: ["Give the parameter a schema (schema: { type: string })", "Or describe it with a content map when it is serialized as JSON"]));
            }
        }
    }

    /// <summary>
    /// Emits ATC_API_OPR028 and ATC_API_OPR029 when a <c>QUERY</c> operation is shaped in a way that
    /// contradicts what the method means.
    /// </summary>
    /// <remarks>
    /// <c>QUERY</c> is a safe, idempotent read whose criteria travel in a request body. Without a
    /// body it is a <c>GET</c> written with a verb that fewer intermediaries understand — all of the
    /// cost, none of the benefit. And a <c>201</c> or <c>409</c> response describes a mutation under
    /// a method that callers, caches and proxies are entitled to treat as read-only.
    /// </remarks>
    private static void ValidateQueryOperationShape(OperationContext context)
    {
        var (diagnostics, sourceFilePath, httpMethodUpper, operation, operationId) = (context.Diagnostics, context.SourceFilePath, context.HttpMethodUpper, context.Operation, context.OperationId);

        if (!string.Equals(httpMethodUpper, "QUERY", StringComparison.Ordinal))
        {
            return;
        }

        if (operation.RequestBody is null)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleId: RuleIdentifiers.QueryOperationWithoutRequestBody,
                Message: $"QUERY operation '{operationId}' declares no 'requestBody'. The only thing QUERY offers " +
                         "over GET is that the criteria travel in a body instead of the URL, so without one this is " +
                         "a GET expressed with a verb that fewer proxies, caches and client libraries understand. " +
                         "Add a requestBody, or declare the operation as 'get:'.",
                Severity: DiagnosticSeverity.Warning,
                FilePath: sourceFilePath));
        }

        if (operation.Responses is null)
        {
            return;
        }

        foreach (var statusCode in new[] { "201", "409" })
        {
            if (!operation.Responses.ContainsKey(statusCode))
            {
                continue;
            }

            diagnostics.Add(new DiagnosticMessage(
                RuleId: RuleIdentifiers.QueryOperationWithMutationResponse,
                Message: $"QUERY operation '{operationId}' declares a '{statusCode}' response, which implies it " +
                         "changes state. QUERY is safe and idempotent — callers, caches and proxies may retry it " +
                         "freely and assume it mutates nothing. Use 'post:' for an operation that creates or " +
                         "conflicts, or drop the response if the operation really is a read.",
                Severity: DiagnosticSeverity.Warning,
                FilePath: sourceFilePath));
        }
    }

    /// <summary>
    /// Emits ATC_API_OPR027 when a <c>GET</c> declares an array query parameter whose serialized
    /// form can outgrow the request line.
    /// </summary>
    /// <remarks>
    /// This is the build-time counterpart of a runtime <c>414</c>: a read whose criteria are a large
    /// set of ids cannot express them in a URL, and the OpenAPI 3.2 <c>query:</c> method exists
    /// precisely so they can travel in a request body instead.
    /// <para>
    /// The rule only speaks when it can justify the claim. An array with no <c>maxItems</c> is
    /// unbounded, so the risk is real by definition. A bounded array is measured against a known
    /// per-item length — a <c>uuid</c> format, an explicit <c>maxLength</c>, the longest enum value,
    /// or the widest form of a numeric or boolean type. A bounded array whose item length cannot be
    /// derived is left alone: guessing there would fire on specs that are perfectly fine.
    /// </para>
    /// </remarks>
    private static void ValidateLargeArrayQueryParameters(
        OperationContext context)
    {
        var (diagnostics, sourceFilePath, pathItem, httpMethodUpper, operation, operationId) = (context.Diagnostics, context.SourceFilePath, context.PathItem, context.HttpMethodUpper, context.Operation, context.OperationId);

        // Only GET. A QUERY has already taken this rule's advice, and for a mutating verb the
        // criteria are not what is being sent.
        if (!string.Equals(httpMethodUpper, "GET", StringComparison.Ordinal))
        {
            return;
        }

        // Path-level parameters apply to every operation beneath the path, so an oversized array
        // declared there is a problem for this operation too.
        var parameters = new List<IOpenApiParameter>();
        if (pathItem.Parameters is not null)
        {
            parameters.AddRange(pathItem.Parameters);
        }

        if (operation.Parameters is not null)
        {
            parameters.AddRange(operation.Parameters);
        }

        foreach (var parameter in parameters)
        {
            if (parameter.In != ParameterLocation.Query ||
                string.IsNullOrEmpty(parameter.Name) ||
                parameter.Schema is not { } schema ||
                schema.Type?.HasFlag(JsonSchemaType.Array) != true)
            {
                continue;
            }

            var maxItems = schema.MaxItems;
            var itemLength = EstimateSerializedItemLength(schema.Items);

            string reason;
            if (maxItems is null or <= 0)
            {
                reason = "it declares no 'maxItems', so the serialized length is unbounded";
            }
            else
            {
                if (itemLength is null)
                {
                    // Bounded, but the item length cannot be derived — do not guess.
                    continue;
                }

                // "name=" + value + "&" per item.
                var estimatedBytes = (long)maxItems.Value * (parameter.Name!.Length + 2 + itemLength.Value);
                if (estimatedBytes <= RequestLineBudgetInBytes)
                {
                    continue;
                }

                reason = $"'maxItems: {maxItems.Value}' with values of up to {itemLength.Value} characters " +
                         $"serializes to roughly {estimatedBytes} bytes";
            }

            diagnostics.Add(new DiagnosticMessage(
                RuleId: RuleIdentifiers.LargeArrayQueryParameterShouldUseQueryMethod,
                Message: $"Array query parameter '{parameter.Name}' on GET operation '{operationId}' may not fit in " +
                         $"the request line: {reason}. Kestrel's default limit is {RequestLineBudgetInBytes} bytes, and " +
                         "proxies and CDNs often impose lower ones, so an oversized request fails with 414 URI Too " +
                         "Long before reaching the handler. Consider an OpenAPI 3.2 'query:' operation, which carries " +
                         "the criteria in a request body, or add a 'maxItems' that keeps the URL within the limit.",
                Severity: DiagnosticSeverity.Warning,
                FilePath: sourceFilePath));
        }
    }

    /// <summary>
    /// Derives an upper bound on the serialized length of one array item, or <see langword="null"/>
    /// when the schema gives no basis for one.
    /// </summary>
    private static int? EstimateSerializedItemLength(IOpenApiSchema? itemSchema)
    {
        if (itemSchema is null)
        {
            return null;
        }

        if (string.Equals(itemSchema.Format, "uuid", StringComparison.OrdinalIgnoreCase))
        {
            return 36;
        }

        if (itemSchema.MaxLength is > 0)
        {
            return itemSchema.MaxLength.Value;
        }

        if (itemSchema.Enum is { Count: > 0 })
        {
            return itemSchema.Enum.Max(value => value?.ToString()?.Length ?? 0);
        }

        var type = itemSchema.Type;
        if (type is null)
        {
            return null;
        }

        if (type.Value.HasFlag(JsonSchemaType.Boolean))
        {
            return 5;
        }

        if (type.Value.HasFlag(JsonSchemaType.Integer))
        {
            // long.MinValue is 20 characters.
            return 20;
        }

        if (type.Value.HasFlag(JsonSchemaType.Number))
        {
            // A round-trippable double is at most 24 characters.
            return 24;
        }

        // An unconstrained string, or anything else, has no derivable upper bound.
        return null;
    }
}