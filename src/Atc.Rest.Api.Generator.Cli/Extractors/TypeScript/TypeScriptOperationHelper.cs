namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Shared helper methods for working with OpenAPI operations in TypeScript code generation.
/// Used by both TypeScriptClientExtractor and TypeScriptReactQueryHookExtractor.
/// </summary>
public static class TypeScriptOperationHelper
{
    /// <summary>
    /// Merges path-level and operation-level parameters by location.
    /// Resolves parameter references ($ref) to actual parameters.
    /// Operation-level parameters take precedence over path-level parameters with the same name.
    /// </summary>
    public static List<OpenApiParameter> GetMergedParameters(
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        string path,
        ParameterLocation location)
    {
        var result = new List<OpenApiParameter>();

        // Resolve operation-level parameters (handles both direct and $ref)
        var operationParams = ResolveParametersByLocation(operation.Parameters, location);
        var operationParamNames = new HashSet<string>(
            operationParams.Select(p => p.Name ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);

        // Add path-level parameters first (only those not overridden at operation level)
        if (openApiDoc.Paths != null &&
            openApiDoc.Paths.TryGetValue(path, out var pathItemValue) &&
            pathItemValue is OpenApiPathItem pathItem &&
            pathItem.Parameters != null)
        {
            var pathLevelParams = ResolveParametersByLocation(pathItem.Parameters, location);
            foreach (var param in pathLevelParams)
            {
                if (!operationParamNames.Contains(param.Name ?? string.Empty))
                {
                    result.Add(param);
                }
            }
        }

        // Add operation-level parameters
        result.AddRange(operationParams);

        return result;
    }

    /// <summary>
    /// Resolves a list of IOpenApiParameter (which may include $ref references) to concrete
    /// OpenApiParameter objects filtered by location.
    /// </summary>
    public static List<OpenApiParameter> ResolveParametersByLocation(
        IList<IOpenApiParameter>? parameters,
        ParameterLocation location)
    {
        var result = new List<OpenApiParameter>();
        if (parameters == null)
        {
            return result;
        }

        foreach (var paramInterface in parameters)
        {
            var resolved = paramInterface.Resolve();
            if (resolved.Parameter != null && resolved.Parameter.In == location)
            {
                result.Add(resolved.Parameter);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the TypeScript return type for an operation.
    /// </summary>
    public static string GetReturnType(
        OpenApiOperation operation,
        bool isStreaming,
        bool isFileDownload)
    {
        if (isFileDownload)
        {
            return "Blob";
        }

        // Try to get 200 response schema, then 201 (both default to application/json)
        var schema = operation.GetResponseSchema("200") ?? operation.GetResponseSchema("201");

        // Fall back to a textual response schema (text/plain, text/csv, application/xml, ...)
        // — the body is delivered as a raw string.
        if (schema == null &&
            operation.Responses != null &&
            operation.Responses.TryGetValue("200", out var response) &&
            response.TryGetTextResponseMediaType(out _, out var textMedia) &&
            textMedia is not null)
        {
            schema = textMedia.Schema;
        }

        if (schema == null)
        {
            return isStreaming ? "unknown" : "void";
        }

        return isStreaming
            ? GetStreamingItemType(schema)
            : schema.ToTypeScriptReturnType();
    }

    /// <summary>
    /// Builds a per-operation discriminated union result type alias from an operation's
    /// declared responses. Each declared 2xx/4xx/5xx status produces a narrow arm; a
    /// synthetic <c>'parseError'</c> arm is always appended because the body parse path
    /// can fail on any operation that returns a JSON body. <c>default</c> responses
    /// fan out to the standard error arms so callers can still match common 4xx/5xx
    /// statuses without the spec listing every code.
    /// </summary>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="typeName">PascalCase name for the emitted alias (e.g. <c>GetUserResult</c>).</param>
    /// <param name="isFileDownload">When true, the success arm's data type is <c>Blob</c>.</param>
    /// <param name="isTextDownload">When true, the success arm's data type is <c>string</c>.</param>
    /// <param name="httpClient">Selects <c>Response</c> (Fetch) vs <c>AxiosResponse</c> for the response field.</param>
    /// <returns>The type alias declaration plus the set of model/error type names that
    /// must be imported alongside it.</returns>
    public static (string Declaration, HashSet<string> Imports) BuildPerOperationResultType(
        OpenApiOperation operation,
        string typeName,
        bool isFileDownload,
        bool isTextDownload,
        TypeScriptHttpClient httpClient)
    {
        var responseType = httpClient == TypeScriptHttpClient.Axios ? "AxiosResponse" : "Response";
        var imports = new HashSet<string>(StringComparer.Ordinal);
        var arms = new List<string>();
        var seenDiscriminators = new HashSet<string>(StringComparer.Ordinal);

        void AddArm(
            string discriminator,
            string armBody)
        {
            if (seenDiscriminators.Add(discriminator))
            {
                arms.Add(armBody);
            }
        }

        var hasDefaultResponse = false;

        if (operation.Responses != null)
        {
            foreach (var (statusCode, response) in operation.Responses)
            {
                if (string.Equals(statusCode, "default", StringComparison.OrdinalIgnoreCase))
                {
                    hasDefaultResponse = true;
                    continue;
                }

                var arm = BuildResultArmForStatus(
                    statusCode,
                    operation,
                    isFileDownload,
                    isTextDownload,
                    responseType,
                    imports,
                    out var discriminator);
                if (arm != null && discriminator != null)
                {
                    AddArm(discriminator, arm);
                }
            }
        }

        // `default:` fans out to the common error arms — without this, a spec that uses
        // `default` (PetStore style) would emit a per-op type missing every error case.
        if (hasDefaultResponse)
        {
            foreach (var (discriminator, errorType) in DefaultResponseErrorArms)
            {
                imports.Add(errorType);
                AddArm(discriminator, $"  | {{ status: '{discriminator}'; error: {errorType}; response: {responseType} }}");
            }
        }

        // Universal synthetic: any 2xx with a JSON body can produce parseError; emit it
        // as Error rather than ApiError so consumers know it carries the JS SyntaxError.
        AddArm("parseError", $"  | {{ status: 'parseError'; error: Error; response: {responseType} }}");

        var sb = new StringBuilder();
        sb.Append("export type ").Append(typeName).AppendLine(" =");
        for (var i = 0; i < arms.Count; i++)
        {
            sb.Append(arms[i]);
            sb.AppendLine(i == arms.Count - 1 ? ";" : string.Empty);
        }

        return (sb.ToString(), imports);
    }

    private static readonly Dictionary<string, (string Discriminator, string ErrorType)> ErrorStatusMapping =
        new(StringComparer.Ordinal)
        {
            ["400"] = ("badRequest", "ValidationError"),
            ["401"] = ("unauthorized", "ApiError"),
            ["403"] = ("forbidden", "ApiError"),
            ["404"] = ("notFound", "ApiError"),
            ["409"] = ("conflict", "ApiError"),
            ["422"] = ("unprocessableEntity", "ApiError"),
            ["429"] = ("tooManyRequests", "ApiError"),
        };

    private static readonly (string Discriminator, string ErrorType)[] DefaultResponseErrorArms =
    {
        ("badRequest", "ValidationError"),
        ("unauthorized", "ApiError"),
        ("forbidden", "ApiError"),
        ("notFound", "ApiError"),
        ("conflict", "ApiError"),
        ("unprocessableEntity", "ApiError"),
        ("tooManyRequests", "ApiError"),
        ("serverError", "ApiError"),
    };

    /// <summary>
    /// Walks <paramref name="operation"/>'s declared responses and returns the discriminator
    /// names that <c>handleResponse</c> would emit for each 2xx code (<c>ok</c>, <c>created</c>,
    /// <c>accepted</c>, <c>noContent</c>). Used by hook generators to narrow a per-op result
    /// to the success arms it actually has — without this, a hook emitting
    /// <c>result.status === 'created'</c> would type-error against a per-op union that only
    /// declares 200. When the operation declares no 2xx, returns just <c>['ok']</c> as a
    /// safe default so generated narrowing still compiles.
    /// </summary>
    public static List<string> CollectDeclared2xxDiscriminators(
        OpenApiOperation operation)
    {
        var discriminators = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (operation.Responses != null)
        {
            foreach (var key in operation.Responses.Keys)
            {
                string? discriminator = key switch
                {
                    "200" => "ok",
                    "201" => "created",
                    "202" => "accepted",
                    "204" => "noContent",
                    _ => null,
                };

                if (discriminator != null && seen.Add(discriminator))
                {
                    discriminators.Add(discriminator);
                }
            }
        }

        if (discriminators.Count == 0)
        {
            discriminators.Add("ok");
        }

        return discriminators;
    }

    private static string? BuildResultArmForStatus(
        string statusCode,
        OpenApiOperation operation,
        bool isFileDownload,
        bool isTextDownload,
        string responseType,
        HashSet<string> imports,
        out string? discriminator)
    {
        discriminator = null;

        switch (statusCode)
        {
            case "200":
            case "201":
            case "202":
            {
                discriminator = statusCode switch
                {
                    "201" => "created",
                    "202" => "accepted",
                    _ => "ok",
                };

                var dataType = ResolveSuccessDataType(statusCode, operation, isFileDownload, isTextDownload, imports);
                return dataType == null
                    ? $"  | {{ status: '{discriminator}'; response: {responseType} }}"
                    : $"  | {{ status: '{discriminator}'; data: {dataType}; response: {responseType} }}";
            }

            case "204":
                discriminator = "noContent";
                return $"  | {{ status: 'noContent'; response: {responseType} }}";
        }

        if (ErrorStatusMapping.TryGetValue(statusCode, out var mapped))
        {
            imports.Add(mapped.ErrorType);
            discriminator = mapped.Discriminator;
            return $"  | {{ status: '{mapped.Discriminator}'; error: {mapped.ErrorType}; response: {responseType} }}";
        }

        // Any 5xx (or unmapped 4xx) collapses to 'serverError' to match handleResponse.
        if (statusCode.Length == 3 && (statusCode[0] == '5' || statusCode[0] == '4'))
        {
            imports.Add("ApiError");
            discriminator = "serverError";
            return $"  | {{ status: 'serverError'; error: ApiError; response: {responseType} }}";
        }

        return null;
    }

    private static string? ResolveSuccessDataType(
        string statusCode,
        OpenApiOperation operation,
        bool isFileDownload,
        bool isTextDownload,
        HashSet<string> imports)
    {
        if (isFileDownload)
        {
            return "Blob";
        }

        if (isTextDownload)
        {
            return "string";
        }

        var schema = operation.GetResponseSchema(statusCode);

        // Fall back to text/xml on 200/201/202 — handleResponse delivers those as raw strings.
        if (schema == null &&
            operation.Responses != null &&
            operation.Responses.TryGetValue(statusCode, out var response) &&
            response.TryGetTextResponseMediaType(out _, out var textMedia) &&
            textMedia is not null)
        {
            return "string";
        }

        if (schema == null)
        {
            return null;
        }

        CollectSchemaRefTypes(schema, imports);
        return schema.ToTypeScriptReturnType();
    }

    /// <summary>
    /// Gets the TypeScript type string for a parameter.
    /// </summary>
    /// <param name="param">The OpenAPI parameter.</param>
    /// <param name="convertDates">When true, parameters with <c>format: date</c> or
    /// <c>format: date-time</c> are typed as <see cref="DateTime"/> in the emitted
    /// TypeScript ("Date") instead of "string". Body emission elsewhere must coerce
    /// such values with <c>.toISOString()</c> before they reach the wire.</param>
    public static string GetParameterType(
        OpenApiParameter param,
        bool convertDates = false)
    {
        if (param.Schema == null)
        {
            return "string";
        }

        // Inline enum (no $ref): render as a TS literal union so callers get compile-time
        // checking of the allowed values. The $ref case is handled by ToTypeScriptTypeForModel
        // (it returns the type name and the import-collector adds the matching import).
        if (param.Schema is OpenApiSchema { Enum.Count: > 0 } enumSchema)
        {
            var union = BuildLiteralUnion(enumSchema);
            if (union != null)
            {
                return union;
            }
        }

        if (convertDates && IsDateParam(param))
        {
            return "Date";
        }

        var tsType = param.Schema.ToTypeScriptTypeForModel(isRequired: true);

        // Strip "| null" from query/path parameter types — URL parameters are either
        // present (with a value) or absent (undefined), never null.
        if (tsType.EndsWith(" | null", StringComparison.Ordinal))
        {
            tsType = tsType[..^" | null".Length];
        }

        return tsType;
    }

    /// <summary>
    /// Returns true when the parameter's schema declares <c>format: date</c> or
    /// <c>format: date-time</c>. Used by date-conversion-aware emission paths to decide
    /// whether to type the parameter as <c>Date</c> and coerce with <c>.toISOString()</c>.
    /// </summary>
    public static bool IsDateParam(OpenApiParameter param)
    {
        if (param.Schema is not OpenApiSchema schema)
        {
            return false;
        }

        if (schema.Type?.HasFlag(JsonSchemaType.String) != true)
        {
            return false;
        }

        return string.Equals(schema.Format, "date-time", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(schema.Format, "date", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the conversion expression to use when a Date-typed parameter must be
    /// serialized to a string for the URL/header. <c>date-time</c> -> ISO 8601 datetime;
    /// <c>date</c> -> ISO date (truncated). Result is the suffix appended to the
    /// parameter access expression, including the leading dot.
    /// </summary>
    public static string GetDateSerializationSuffix(OpenApiParameter param)
    {
        if (param.Schema is OpenApiSchema schema &&
            string.Equals(schema.Format, "date", StringComparison.OrdinalIgnoreCase))
        {
            // YYYY-MM-DD slice of an ISO datetime keeps date-only format on the wire.
            return ".toISOString().substring(0, 10)";
        }

        return ".toISOString()";
    }

    private static string? BuildLiteralUnion(OpenApiSchema schema)
    {
        var isStringEnum = schema.Type?.HasFlag(JsonSchemaType.String) == true;
        var isNumericEnum =
            schema.Type?.HasFlag(JsonSchemaType.Integer) == true ||
            schema.Type?.HasFlag(JsonSchemaType.Number) == true;

        if (!isStringEnum && !isNumericEnum)
        {
            return null;
        }

        var parts = new List<string>(schema.Enum!.Count);
        foreach (var value in schema.Enum)
        {
            if (value is not JsonValue jsonValue)
            {
                return null;
            }

            if (isStringEnum)
            {
                if (!jsonValue.TryGetValue<string>(out var s))
                {
                    return null;
                }

                parts.Add("'" + s.Replace("'", "\\'", StringComparison.Ordinal) + "'");
            }
            else
            {
                parts.Add(jsonValue.ToJsonString());
            }
        }

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    /// <summary>
    /// Collects all import types needed by an operation (from response schemas, request body,
    /// and parameter schemas).
    /// </summary>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="importTypes">The set to accumulate referenced type names into.</param>
    /// <param name="openApiDoc">Optional document. When supplied alongside <paramref name="path"/>,
    /// path-item-level parameters (shared by every operation under that path) are visited too.</param>
    /// <param name="path">Optional path key matching <paramref name="openApiDoc"/>.</param>
    public static void CollectImportTypes(
        OpenApiOperation operation,
        HashSet<string> importTypes,
        OpenApiDocument? openApiDoc = null,
        string? path = null)
    {
        // From response schemas (200, 201)
        CollectSchemaRefTypes(operation.GetResponseSchema("200"), importTypes);
        CollectSchemaRefTypes(operation.GetResponseSchema("201"), importTypes);

        // From operation-level parameter schemas. We visit query, path, and header
        // params — all three are surfaced in the generated TS signatures. Cookie
        // params are deliberately excluded: cookies are browser-managed (document.cookie
        // and the credentials fetch option), so SDK methods do not accept a cookies arg
        // and any type they referenced would be a dead import.
        if (operation.Parameters != null)
        {
            foreach (var paramInterface in operation.Parameters)
            {
                var resolved = paramInterface.Resolve();
                if (resolved.Parameter is { In: ParameterLocation.Query or ParameterLocation.Path or ParameterLocation.Header } p)
                {
                    CollectSchemaRefTypes(p.Schema, importTypes);
                }
            }
        }

        // From path-item-level parameters (shared by every operation under that path).
        // Same location filter applies: query / path / header (no cookies).
        if (openApiDoc?.Paths != null &&
            path != null &&
            openApiDoc.Paths.TryGetValue(path, out var pathItemValue) &&
            pathItemValue is OpenApiPathItem pathItem &&
            pathItem.Parameters != null)
        {
            foreach (var paramInterface in pathItem.Parameters)
            {
                var resolved = paramInterface.Resolve();
                if (resolved.Parameter is { In: ParameterLocation.Query or ParameterLocation.Path or ParameterLocation.Header } p)
                {
                    CollectSchemaRefTypes(p.Schema, importTypes);
                }
            }
        }

        // From request body
        var (bodySchema, _) = operation.GetRequestBodySchemaWithContentType();
        if (bodySchema != null)
        {
            CollectSchemaRefTypes(bodySchema, importTypes);

            // For multipart form data objects, collect property types
            // ONLY if the body itself is NOT a file upload (file uploads use inline types)
            var isFileUpload = operation.HasFileUpload();
            if (!isFileUpload && bodySchema.Properties is { Count: > 0 })
            {
                foreach (var prop in bodySchema.Properties)
                {
                    CollectSchemaRefTypes(prop.Value, importTypes);
                }
            }

            // If this is a file upload and the body schema is a $ref, remove it
            // since the client method uses inline type literals, not the named type
            if (isFileUpload && bodySchema is OpenApiSchemaReference fileUploadRef)
            {
                var refName = fileUploadRef.Reference.Id ?? fileUploadRef.Id;
                if (refName != null)
                {
                    importTypes.Remove(refName);
                }
            }
        }
    }

    /// <summary>
    /// Collects $ref type names from a schema. For a direct $ref, follows one level
    /// further <em>only</em> when the target is an array alias (e.g. <c>Accounts =
    /// Account[]</c>) and adds the item's ref name — streaming hooks reference the item
    /// type directly and would otherwise hit TS2552 "Cannot find name". Inline composite
    /// schemas (allOf, arrays) are visited as before, but property recursion is
    /// intentionally bounded so we don't drag every transitively-reachable model into
    /// every client file.
    /// </summary>
    public static void CollectSchemaRefTypes(
        IOpenApiSchema? schema,
        HashSet<string> importTypes)
    {
        if (schema == null)
        {
            return;
        }

        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refName = schemaRef.Reference.Id ?? schemaRef.Id;
            if (refName == null)
            {
                return;
            }

            importTypes.Add(refName);

            // If the named schema is an array alias, also surface its element type so
            // streaming hooks (which yield the element type, not the alias) can name it.
            if (schemaRef.Target is OpenApiSchema target &&
                target.Type?.HasFlag(JsonSchemaType.Array) == true &&
                target.Items is OpenApiSchemaReference itemRef)
            {
                var itemName = itemRef.Reference.Id ?? itemRef.Id;
                if (itemName != null)
                {
                    importTypes.Add(itemName);
                }
            }

            return;
        }

        if (schema is not OpenApiSchema actualSchema)
        {
            return;
        }

        // Handle allOf references — flat list, refs only.
        if (actualSchema.AllOf is { Count: > 0 })
        {
            foreach (var subSchema in actualSchema.AllOf)
            {
                if (subSchema is OpenApiSchemaReference allOfRef)
                {
                    var refName = allOfRef.Reference.Id ?? allOfRef.Id;
                    if (refName != null)
                    {
                        importTypes.Add(refName);
                    }
                }
            }
        }

        // Handle array item references.
        if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true && actualSchema.Items is OpenApiSchemaReference inlineItemRef)
        {
            var refName = inlineItemRef.Reference.Id ?? inlineItemRef.Id;
            if (refName != null)
            {
                importTypes.Add(refName);
            }
        }
    }

    /// <summary>
    /// For streaming endpoints, resolves array type aliases (e.g., Accounts -> Account[])
    /// to their item type, since the server yields individual items, not arrays.
    /// </summary>
    public static string GetStreamingItemType(IOpenApiSchema schema)
    {
        // Resolve $ref to actual schema
        var resolved = schema;
        if (schema is OpenApiSchemaReference schemaRef)
        {
            resolved = schemaRef.Target ?? schema;
        }

        // If resolved schema is an array, return the item type
        if (resolved is OpenApiSchema actualSchema &&
            actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true)
        {
            if (actualSchema.Items is OpenApiSchemaReference itemRef)
            {
                return itemRef.Reference.Id ?? itemRef.Id ?? "unknown";
            }

            if (actualSchema.Items is OpenApiSchema itemSchema)
            {
                return itemSchema.Type.ToTypeScriptTypeName(itemSchema.Format);
            }
        }

        // Not an array — use standard mapping
        return schema.ToTypeScriptReturnType();
    }

    /// <summary>
    /// Builds a TypeScript inline type for header parameters
    /// (e.g., { 'X-Correlation-Id': string; 'X-Continuation'?: string }).
    /// Header names typically contain dashes, so every key is emitted quoted to keep
    /// the output uniform and to avoid invalid identifier errors. The `?` after the key
    /// reflects the parameter's `required` flag from the OpenAPI spec.
    /// </summary>
    public static string BuildHeaderTypeInline(
        List<OpenApiParameter> headerParams,
        bool convertDates = false)
    {
        var parts = new List<string>(headerParams.Count);
        foreach (var param in headerParams)
        {
            var rawName = param.Name ?? string.Empty;
            var paramType = GetParameterType(param, convertDates);
            var optional = param.Required ? string.Empty : "?";
            parts.Add("'" + rawName + "'" + optional + ": " + paramType + FormatDefaultComment(param));
        }

        return "{ " + string.Join("; ", parts) + " }";
    }

    /// <summary>
    /// Formats a parameter's <c>default:</c> value as an inline TypeScript comment so it
    /// shows up in IDE hover/autocomplete. Returns empty when the parameter has no
    /// default — callers append the result verbatim, so empty means "emit nothing extra".
    /// String defaults are single-quoted, primitives are emitted verbatim, and anything
    /// else falls through to <see cref="JsonNode.ToJsonString"/>.
    /// </summary>
    private static string FormatDefaultComment(OpenApiParameter param)
    {
        if (param.Schema is not OpenApiSchema schema || schema.Default == null)
        {
            return string.Empty;
        }

        return " /* default: " + FormatJsonDefault(schema.Default) + " */";
    }

    private static string FormatJsonDefault(JsonNode node)
    {
        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out var s))
            {
                return "'" + s.Replace("'", "\\'", StringComparison.Ordinal) + "'";
            }

            if (jsonValue.TryGetValue<bool>(out var b))
            {
                return b ? "true" : "false";
            }

            // Numbers (int/long/double/decimal) and any other primitives fall back to
            // their JSON representation, which is the canonical TS literal form.
            return jsonValue.ToJsonString();
        }

        // Arrays / objects — uncommon as parameter defaults, but keep them readable.
        return node.ToJsonString();
    }

    /// <summary>
    /// Builds a TypeScript inline type for query parameters (e.g., { limit?: number; offset?: number }).
    /// </summary>
    public static string BuildQueryTypeInline(
        List<OpenApiParameter> queryParams,
        TypeScriptNamingStrategy namingStrategy = TypeScriptNamingStrategy.CamelCase,
        bool convertDates = false)
    {
        var parts = new List<string>();
        foreach (var param in queryParams)
        {
            var paramName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            var paramType = GetParameterType(param, convertDates);
            parts.Add(paramName + "?: " + paramType + FormatDefaultComment(param));
        }

        return "{ " + string.Join("; ", parts) + " }";
    }

    /// <summary>
    /// Builds a path string with template literal interpolation for path parameters.
    /// When <paramref name="convertDates"/> is true and a path parameter has
    /// <c>format: date</c> or <c>format: date-time</c>, the interpolation includes
    /// an explicit <c>.toISOString()</c> coercion so the wire format stays ISO 8601
    /// instead of falling through to JavaScript's default <c>Date.toString()</c>.
    /// </summary>
    public static string BuildInterpolatedPath(
        string path,
        List<OpenApiParameter> pathParams,
        TypeScriptNamingStrategy namingStrategy = TypeScriptNamingStrategy.CamelCase,
        bool convertDates = false)
    {
        if (pathParams.Count == 0)
        {
            return "'" + path + "'";
        }

        // Replace {paramName} with ${paramName} for template literal
        var interpolated = path;
        foreach (var param in pathParams)
        {
            var tsName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            var replacement = convertDates && IsDateParam(param)
                ? "${" + tsName + GetDateSerializationSuffix(param) + "}"
                : "${" + tsName + "}";

            interpolated = interpolated.Replace(
                "{" + param.Name + "}",
                replacement,
                StringComparison.Ordinal);
        }

        return "`" + interpolated + "`";
    }
}