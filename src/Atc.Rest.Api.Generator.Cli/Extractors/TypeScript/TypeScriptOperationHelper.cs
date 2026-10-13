// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
// ReSharper disable InconsistentNaming
namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Shared helper methods for working with OpenAPI operations in TypeScript code generation.
/// Used by both TypeScriptClientExtractor and TypeScriptReactQueryHookExtractor.
/// </summary>
public static class TypeScriptOperationHelper
{
    // Whether string enums are closed (extensibleEnums off) for the generation running on this flow. The parameter
    // types are built deep inside every client and hook extractor, so the setting travels with the run, not the calls.
    private static readonly AsyncLocal<bool> EnumsAreClosed = new();

    /// <summary>
    /// Sets whether an inline string enum parameter is an open union (<c>'a' | 'b' | (string &amp; {})</c>, the
    /// default) for the code generated until the scope is disposed - like the enum types, so a value read from a
    /// response can be passed back.
    /// </summary>
    /// <param name="extensibleEnums">The run's <c>ExtensibleEnums</c> setting.</param>
    /// <returns>A scope that restores the previous setting.</returns>
    public static IDisposable EnumsScope(bool extensibleEnums)
    {
        var previous = EnumsAreClosed.Value;
        EnumsAreClosed.Value = !extensibleEnums;
        return new EnumsScopeRestore(previous);
    }

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
        if (openApiDoc.Paths is not null &&
            openApiDoc.Paths.TryGetValue(path, out var pathItemValue) &&
            pathItemValue is { Parameters: not null } pathItem)
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
        if (parameters is null)
        {
            return result;
        }

        foreach (var paramInterface in parameters)
        {
            var resolved = paramInterface.Resolve();

            // A value the client cannot send is left out (ATC_API_OPR037 warned)
            if (resolved.Parameter is not null && resolved.Parameter.In == location && !resolved.Parameter.IsUnbindable())
            {
                result.Add(resolved.Parameter);
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves the Zod schema expression for an operation's success response. Returns
    /// <c>null</c> when runtime validation can't apply: no JSON response, no <c>$ref</c>,
    /// inline objects with no canonical schema name, or unsupported shapes.
    /// </summary>
    /// <remarks>
    /// Three supported shapes:
    /// <list type="bullet">
    ///   <item>Single <c>$ref</c> to a named schema → <c>PetSchema</c></item>
    ///   <item><c>$ref</c> to an array schema or top-level array of <c>$ref</c> →
    ///     <c>PetListSchema</c> when the array is registered (handled by
    ///     <see cref="TypeScriptZodModelExtractor.ExtractArrayTypeAliases"/>),
    ///     otherwise <c>z.array(PetSchema)</c> + a flag that consumers should import <c>z</c></item>
    ///   <item>Primitive (<c>string</c>, <c>number</c>, <c>boolean</c>) →
    ///     <c>z.string()</c> / <c>z.number()</c> / <c>z.boolean()</c> + the <c>z</c> import flag</item>
    /// </list>
    /// Other shapes (oneOf/anyOf/allOf without ref, inline objects, multi-status with
    /// different schemas) intentionally return null — the validation path falls back
    /// to no-parse, identical to the today's behavior.
    /// </remarks>
    public static ZodResponseSchemaSpec? TryGetResponseZodSchemaSpec(
        OpenApiOperation operation)
    {
        // Validation only makes sense for JSON success bodies. Try 200, 201, 202 in order.
        var schema = operation.GetResponseSchema("200")
                  ?? operation.GetResponseSchema("201")
                  ?? operation.GetResponseSchema("202");
        if (schema is null)
        {
            return null;
        }

        // Direct $ref to a named schema → PetSchema.
        if (schema is OpenApiSchemaReference singleRef)
        {
            var name = singleRef.Reference?.Id ?? singleRef.Id;
            if (!string.IsNullOrEmpty(name))
            {
                return new ZodResponseSchemaSpec(
                    Expression: name + "Schema",
                    RefSchemaNames: new HashSet<string>(StringComparer.Ordinal) { name },
                    NeedsZodImport: false);
            }
        }

        if (schema is OpenApiSchema actual)
        {
            // Array of $ref → z.array(PetSchema). Requires a `z` import in the
            // emitting file; the per-item schema gets imported from its zod module.
            if (actual.Type?.HasFlag(JsonSchemaType.Array) == true &&
                actual.Items is OpenApiSchemaReference itemRef)
            {
                var itemName = itemRef.Reference?.Id ?? itemRef.Id;
                if (!string.IsNullOrEmpty(itemName))
                {
                    return new ZodResponseSchemaSpec(
                        Expression: "z.array(" + itemName + "Schema)",
                        RefSchemaNames: new HashSet<string>(StringComparer.Ordinal) { itemName },
                        NeedsZodImport: true);
                }
            }

            // Primitive — string / number / integer / boolean. Zod renders these as
            // z.string() etc; everything else (date strings, formats) flows through
            // the same constructors so we keep this conservative.
            if (actual.Type?.HasFlag(JsonSchemaType.String) == true && actual.Enum is not { Count: > 0 })
            {
                return new ZodResponseSchemaSpec("z.string()", new HashSet<string>(StringComparer.Ordinal), NeedsZodImport: true);
            }

            if (actual.Type?.HasFlag(JsonSchemaType.Integer) == true || actual.Type?.HasFlag(JsonSchemaType.Number) == true)
            {
                return new ZodResponseSchemaSpec("z.number()", new HashSet<string>(StringComparer.Ordinal), NeedsZodImport: true);
            }

            if (actual.Type?.HasFlag(JsonSchemaType.Boolean) == true)
            {
                return new ZodResponseSchemaSpec("z.boolean()", new HashSet<string>(StringComparer.Ordinal), NeedsZodImport: true);
            }
        }

        return null;
    }

    /// <summary>
    /// The model schema of a response: the JSON one, else an XML-only model body (read by <c>helpers/xml.ts</c>).
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="statusCode">The status code.</param>
    /// <returns>The schema, or <see langword="null"/>.</returns>
    public static IOpenApiSchema? GetResponseModelSchema(
        OpenApiOperation operation,
        string statusCode)
    {
        var schema = operation.GetResponseSchema(statusCode);
        if (schema is not null || operation.Responses is null || !operation.Responses.TryGetValue(statusCode, out var response))
        {
            return schema;
        }

        return XmlBodyHelper.FindXmlOnlyBody(response.Content) is { } xml
            ? response.Content![xml.MediaType].Schema
            : null;
    }

    /// <summary>
    /// Gets the TypeScript return type for an operation: the 200/201 model (or <c>Model | Blob</c> when the
    /// answer may be a file), the per-item type for a stream, <c>Blob</c> for a download, <c>string</c> for a
    /// textual body, else <c>void</c>.
    /// </summary>
    /// <param name="operation">The operation whose responses are inspected.</param>
    /// <param name="isStreaming">Whether the client method yields the items of a streamed response.</param>
    /// <param name="isFileDownload">Whether the operation always answers with a file.</param>
    /// <returns>The TypeScript type the generated client method resolves with.</returns>
    public static string GetReturnType(
        OpenApiOperation operation,
        bool isStreaming,
        bool isFileDownload)
    {
        if (isFileDownload)
        {
            return "Blob";
        }

        // OpenAPI 3.2 streaming: itemSchema is already the per-element type, so map it
        // directly (do not run it through GetStreamingItemType, which unwraps arrays).
        if (isStreaming)
        {
            var itemSchema = operation.GetStreamingItemSchema();
            if (itemSchema is not null)
            {
                return itemSchema.ToTypeScriptReturnType();
            }
        }

        // Try to get 200 response schema, then 201 (both default to application/json; an XML-only model too)
        var schema = GetResponseModelSchema(operation, "200") ?? GetResponseModelSchema(operation, "201");

        // Fall back to a textual response schema (text/plain, text/csv, application/xml, ...)
        // — the body is delivered as a raw string.
        if (schema is null &&
            operation.Responses is not null &&
            operation.Responses.TryGetValue("200", out var response) &&
            response.TryGetTextResponseMediaType(out _, out var textMedia) &&
            textMedia is not null)
        {
            schema = textMedia.Schema;
        }

        if (schema is null)
        {
            return isStreaming ? "unknown" : "void";
        }

        if (isStreaming)
        {
            return GetStreamingItemType(schema);
        }

        // A model or a file: the model, or the file as a Blob (told apart by the answer's Content-Type)
        return operation.GetModelOrFileMediaTypes().Count > 0
            ? schema.ToTypeScriptReturnType() + " | Blob"
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
    /// <returns>The type alias declaration, the set of model/error type names that must be imported
    /// alongside it, and the union's discriminators (the client passes them as <c>arms</c>, so the
    /// ApiClient folds a status the union does not declare into one it does).</returns>
    public static (string Declaration, HashSet<string> Imports, List<string> Arms) BuildPerOperationResultType(
        OpenApiOperation operation,
        string typeName,
        bool isFileDownload,
        bool isTextDownload,
        TypeScriptHttpClient httpClient,
        OperationFeatures? features = null)
    {
        var responseType = httpClient == TypeScriptHttpClient.Axios ? "AxiosResponse" : "Response";
        var imports = new HashSet<string>(StringComparer.Ordinal);
        var successArms = new List<string>();
        var successDiscriminators = new List<string>();
        var seenSuccess = new HashSet<string>(StringComparer.Ordinal);

        // Error arms: discriminator -> base error type + the body types seen for it. A body type of
        // null means "untyped" (nothing declared), which makes the arm a plain ApiError/ValidationError.
        var errorArms = new List<(string Discriminator, string BaseType, List<string?> Bodies)>();

        // onlyIfMissing: arms the server adds (or `default` fans out to) only fill statuses the spec does
        // not declare - like the server, which only auto-adds undeclared statuses - so they never
        // demote a declared, typed arm to an untyped one.
        void AddErrorArm(
            string discriminator,
            string baseType,
            string? bodyType,
            bool onlyIfMissing = false)
        {
            var index = errorArms.FindIndex(a => string.Equals(a.Discriminator, discriminator, StringComparison.Ordinal));
            if (index >= 0 && onlyIfMissing)
            {
                return;
            }

            if (index < 0)
            {
                errorArms.Add((discriminator, baseType, [bodyType]));
            }
            else if (!errorArms[index].Bodies.Contains(bodyType))
            {
                errorArms[index].Bodies.Add(bodyType);
            }

            imports.Add(baseType);
        }

        var hasDefaultResponse = false;
        string? defaultBodyType = null;

        if (operation.Responses is not null)
        {
            foreach (var (statusCode, response) in operation.Responses)
            {
                if (string.Equals(statusCode, "default", StringComparison.OrdinalIgnoreCase))
                {
                    hasDefaultResponse = true;
                    defaultBodyType = ResolveErrorBodyType(response, imports);
                    continue;
                }

                if (ErrorResponseShapeResolver.IsErrorStatusCode(statusCode))
                {
                    var (errorDiscriminator, baseType) = ErrorArmFor(statusCode);
                    AddErrorArm(errorDiscriminator, baseType, ResolveErrorBodyType(response, imports));
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
                if (arm is not null && discriminator is not null && seenSuccess.Add(discriminator))
                {
                    successArms.Add(arm);
                    successDiscriminators.Add(discriminator);
                }
            }

            // A declared redirect: ok (followed - the target's response, untyped unless a 2xx is declared) and
            // redirection (not followed, e.g. redirect: 'manual' / maxRedirects: 0).
            if (operation.Responses.Keys.Any(k => int.TryParse(k, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) && HttpStatusCodeHelper.IsRedirect(code)))
            {
                if (seenSuccess.Add("ok"))
                {
                    successArms.Add($"  | {{ status: 'ok'; data: unknown; response: {responseType} }}");
                    successDiscriminators.Add("ok");
                }

                if (seenSuccess.Add("redirection"))
                {
                    successArms.Add($"  | {{ status: 'redirection'; data?: unknown; response: {responseType} }}");
                    successDiscriminators.Add("redirection");
                }
            }
        }

        // `default:` fans out to the common error arms — without this, a spec that uses
        // `default` (PetStore style) would emit a per-op type missing every error case. Those arms carry
        // the default body, so they are typed with it (the validation 400 stays the filter's ValidationError).
        if (hasDefaultResponse)
        {
            foreach (var (discriminator, errorType) in DefaultResponseErrorArms)
            {
                // The catch-alls may already hold a declared status' body (a 402 model): add the default body to it.
                AddErrorArm(
                    discriminator,
                    errorType,
                    discriminator == "badRequest" ? null : defaultBodyType,
                    onlyIfMissing: discriminator is not ("clientError" or "serverError"));
            }
        }

        // Errors the server produces without the spec listing them - the same rules as the
        // generated server's endpoint metadata - so the union covers what handleResponse returns.
        if (features is not null)
        {
            if (features.HasParameters)
            {
                AddErrorArm("badRequest", "ValidationError", null, onlyIfMissing: true);
            }

            if (features.HasSecurity)
            {
                AddErrorArm("unauthorized", "ApiError", null, onlyIfMissing: true);
            }

            if (features.HasRolesOrPolicies)
            {
                AddErrorArm("forbidden", "ApiError", null, onlyIfMissing: true);
            }

            if (features.HasPathParameters && features.HttpMethod is "GET" or "PUT" or "DELETE" or "PATCH")
            {
                AddErrorArm("notFound", "ApiError", null, onlyIfMissing: true);
            }

            if (features.HttpMethod is "POST" or "PUT")
            {
                AddErrorArm("conflict", "ApiError", null, onlyIfMissing: true);
            }

            if (features.HasRateLimiting)
            {
                AddErrorArm("tooManyRequests", "ApiError", null, onlyIfMissing: true);
            }
        }

        // Any API can answer with an unmapped 4xx (405, 413, 415, ... from the framework) or a 5xx. Without a
        // `default` these catch-all arms carry statuses with other bodies, so they are merged as untyped; with a
        // `default` the specification says what any other status carries, and they keep its type.
        if (!hasDefaultResponse || defaultBodyType is null)
        {
            AddErrorArm("clientError", "ApiError", null);
            AddErrorArm("serverError", "ApiError", null);
        }

        var arms = new List<string>(successArms);
        foreach (var (discriminator, baseType, bodies) in errorArms)
        {
            var errorType = bodies.Contains(null)
                ? baseType
                : $"{baseType}<{string.Join(" | ", bodies)}>";
            arms.Add($"  | {{ status: '{discriminator}'; error: {errorType}; response: {responseType} }}");
        }

        // Universal synthetic: any 2xx with a JSON body can produce parseError; emit it
        // as Error rather than ApiError so consumers know it carries the JS SyntaxError.
        arms.Add($"  | {{ status: 'parseError'; error: Error; response: {responseType} }}");

        var sb = new StringBuilder();
        sb.Append("export type ").Append(typeName).AppendLine(" =");
        for (var i = 0; i < arms.Count; i++)
        {
            sb.Append(arms[i]);
            sb.AppendLine(i == arms.Count - 1 ? ";" : string.Empty);
        }

        return (sb.ToString(), imports, [.. successDiscriminators, .. errorArms.Select(a => a.Discriminator), "parseError"]);
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
        ("clientError", "ApiError"),
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

        if (operation.Responses is not null)
        {
            foreach (var key in operation.Responses.Keys)
            {
                string? discriminator = key switch
                {
                    "200" or "203" or "206" or "207" or "208" or "226" => "ok",
                    "201" => "created",
                    "202" => "accepted",
                    "204" or "205" => "noContent",
                    _ => null,
                };

                if (discriminator is not null && seen.Add(discriminator))
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
        bool convertDates = false,
        bool brandedIds = false,
        string? path = null)
    {
        // No schema, or one that says nothing about the value ({}): the text the value is on the wire
        if (param.Schema is null || param.Schema.IsUntyped())
        {
            return "string";
        }

        // Inline enum (no $ref): render as a TS literal union so callers get compile-time
        // checking of the allowed values. The $ref case is handled by ToTypeScriptTypeForModel
        // (it returns the type name and the import-collector adds the matching import).
        if (param.Schema is OpenApiSchema { Enum.Count: > 0 } enumSchema)
        {
            var union = BuildLiteralUnion(enumSchema);
            if (union is not null)
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

        // Branded IDs: swap the inferred `string` for the resolved brand. Query params
        // get the brand too — the underlying URL serialization is unchanged but call
        // sites refuse to pass a `UserId` where a `PetId` is expected.
        if (brandedIds && tsType == "string" && !string.IsNullOrEmpty(param.Name))
        {
            var brand = param.In == ParameterLocation.Path
                ? TypeScriptBrandedIdExtractor.ResolveParamBrand(path ?? string.Empty, param.Name, param.Schema)
                : TypeScriptBrandedIdExtractor.ResolvePropertyBrand(schemaName: string.Empty, param.Name, param.Schema);
            if (brand is not null)
            {
                tsType = brand;
            }
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
    /// The expression that writes a Date-typed parameter for the URL or a header: a <c>date-time</c> as ISO 8601
    /// (<c>toISOString()</c>), a <c>date</c> as the caller's calendar day, <c>yyyy-MM-dd</c> from the local year, month
    /// and day (<c>dateOnly</c>, exported by the ApiClient). A <c>date</c> is a day, not an instant: the UTC day of a
    /// local midnight is the day before east of UTC. The JSON and XML bodies read and write it the same way.
    /// </summary>
    /// <param name="param">The parameter.</param>
    /// <param name="access">The expression of the value.</param>
    /// <param name="optional">Whether the value may be undefined.</param>
    /// <returns>The expression of the text sent.</returns>
    public static string SerializeDateParameter(
        OpenApiParameter param,
        string access,
        bool optional)
        => IsDateOnly(param.Schema)
            ? "dateOnly(" + access + ")"
            : access + (optional ? "?." : ".") + "toISOString()";

    /// <summary>
    /// Whether a schema declares <c>format: date</c> (a calendar day).
    /// </summary>
    /// <param name="schema">The schema.</param>
    /// <returns><see langword="true"/> for a date.</returns>
    public static bool IsDateOnly(IOpenApiSchema? schema)
        => string.Equals(schema?.Format, "date", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Emits <c>buildUrl</c> (shared by the Fetch and Axios ApiClient extractors): the base URL and the path, with the
    /// query parameters. A relative base URL (<c>/api</c>, or an empty one) resolves against the origin of the page;
    /// without a page (server-side rendering, Node) the URL stays relative - building it never throws, and the request
    /// goes to a fetch that knows its origin, or fails there.
    /// </summary>
    /// <param name="sb">The ApiClient being written.</param>
    /// <param name="modifier">The method's modifier (<c>private </c>, or empty).</param>
    internal static void AppendBuildUrlMethod(
        StringBuilder sb,
        string modifier)
    {
        sb.Append("  ").Append(modifier).AppendLine("buildUrl(path: string, query?: Record<string, string | number | boolean | (string | number | boolean)[] | undefined>): string {");
        sb.AppendLine("    // A relative base URL ('/api', or an empty one) resolves against the origin of the page; without a page");
        sb.AppendLine("    // (server-side rendering, Node) it stays relative");
        sb.AppendLine("    const relativeOrigin = 'http://relative.invalid';");
        sb.AppendLine("    const origin = (globalThis as { location?: { origin?: string } }).location?.origin;");
        sb.AppendLine("    const url = new URL(`${this.baseUrl}${path}`, origin ?? relativeOrigin);");
        sb.AppendLine("    if (query) {");
        sb.AppendLine("      for (const [key, value] of Object.entries(query)) {");
        sb.AppendLine("        if (value === undefined) {");
        sb.AppendLine("          continue;");
        sb.AppendLine("        }");
        sb.AppendLine("        if (Array.isArray(value)) {");
        sb.AppendLine("          for (const item of value) {");
        sb.AppendLine("            url.searchParams.append(key, String(item));");
        sb.AppendLine("          }");
        sb.AppendLine("        } else {");
        sb.AppendLine("          url.searchParams.set(key, String(value));");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("    // Note: URL.searchParams always percent-encodes values.");
        sb.AppendLine("    // OpenAPI allowReserved cannot be honoured via this path.");
        sb.AppendLine("    return url.origin === relativeOrigin ? `${url.pathname}${url.search}${url.hash}` : url.toString();");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits the <c>xml</c> member of <c>RequestOptions</c> when the specification has XML bodies (shared by the Fetch
    /// and Axios ApiClient extractors).
    /// </summary>
    /// <param name="sb">The ApiClient being written.</param>
    /// <param name="hasXml">Whether the specification has XML bodies.</param>
    internal static void AppendXmlRequestOption(
        StringBuilder sb,
        bool hasXml)
    {
        if (!hasXml)
        {
            return;
        }

        sb.AppendLine("  /** An XML body (the generated client passes it): the Accept, how to write the request body and read the response. */");
        sb.AppendLine("  xml?: XmlOptions;");
    }

    /// <summary>
    /// Emits <c>dateOnly</c>: a Date as the calendar day it is in the caller's time zone (<c>yyyy-MM-dd</c>), for the
    /// <c>format: date</c> parameters and form fields of the generated clients (with <c>--convert-dates</c>).
    /// </summary>
    /// <param name="sb">The ApiClient being written.</param>
    internal static void AppendDateOnlyHelper(StringBuilder sb)
    {
        sb.AppendLine("/**");
        sb.AppendLine(" * A format: date value as text: the calendar day the Date is in the caller's time zone (yyyy-MM-dd from the local");
        sb.AppendLine(" * year, month and day). A date is a day, not an instant - the UTC day of a local midnight is the day before east of");
        sb.AppendLine(" * UTC - and the models read and write it the same way.");
        sb.AppendLine(" */");
        sb.AppendLine("export function dateOnly(value: Date): string;");
        sb.AppendLine("export function dateOnly(value: Date | undefined): string | undefined;");
        sb.AppendLine("export function dateOnly(value: Date | undefined): string | undefined {");
        sb.AppendLine("  if (value === undefined) {");
        sb.AppendLine("    return undefined;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  const month = String(value.getMonth() + 1).padStart(2, '0');");
        sb.AppendLine("  const day = String(value.getDate()).padStart(2, '0');");
        sb.AppendLine("  return `${String(value.getFullYear()).padStart(4, '0')}-${month}-${day}`;");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static readonly Regex BlockCommentPattern = new(@"/\*.*?\*/", RegexOptions.Singleline, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Returns true when <paramref name="identifier"/> appears in <paramref name="text"/>
    /// as a standalone TypeScript identifier (bounded by non-identifier characters), so
    /// import lines can be narrowed to types the generated body actually references. A
    /// plain substring check would mismatch (e.g. <c>Device</c> inside
    /// <c>DeviceManagement</c>); this respects identifier boundaries.
    /// </summary>
    /// <param name="text">The generated body text to scan.</param>
    /// <param name="identifier">The candidate identifier to look for.</param>
    /// <returns>True when the identifier occurs as a standalone token.</returns>
    public static bool ReferencesIdentifier(
        string text,
        string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return false;
        }

        // A name in a JSDoc block (an operation summary such as "List accounts") is not a use of the type.
        text = BlockCommentPattern.Replace(text, string.Empty);

        var index = 0;
        while ((index = text.IndexOf(identifier, index, StringComparison.Ordinal)) >= 0)
        {
            var before = index == 0 || !IsIdentifierChar(text[index - 1]);
            var afterIndex = index + identifier.Length;
            var after = afterIndex >= text.Length || !IsIdentifierChar(text[afterIndex]);

            // A name in a string ('ItemLog' in a wire shape) is data, not a use of the type
            var quoted = index > 0 && text[index - 1] == '\'' &&
                         afterIndex < text.Length && text[afterIndex] == '\'';
            if (before && after && !quoted)
            {
                return true;
            }

            index = afterIndex;
        }

        return false;
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
        CollectSchemaRefTypes(GetResponseModelSchema(operation, "200"), importTypes);
        CollectSchemaRefTypes(GetResponseModelSchema(operation, "201"), importTypes);

        // OAS 3.2 itemSchema on streaming media types (text/event-stream, application/jsonl,
        // etc.) does not use the regular 'schema' field, so GetResponseSchema returns null and
        // the item type is never collected. Collect it explicitly here so generated stream
        // client files can import the model type instead of falling back to the DOM 'Event'.
        CollectSchemaRefTypes(operation.GetStreamingItemSchema(), importTypes);

        // From operation-level parameter schemas. We visit query, path, and header
        // params — all three are surfaced in the generated TS signatures. Cookie
        // params are deliberately excluded: cookies are browser-managed (document.cookie
        // and the credentials fetch option), so SDK methods do not accept a cookies arg
        // and any type they referenced would be a dead import.
        if (operation.Parameters is not null)
        {
            foreach (var paramInterface in operation.Parameters)
            {
                var resolved = paramInterface.Resolve();
                if (resolved.Parameter is { In: ParameterLocation.Query or ParameterLocation.Path or ParameterLocation.Header } p && !p.IsUnbindable())
                {
                    CollectSchemaRefTypes(p.Schema, importTypes);
                }
            }
        }

        // From path-item-level parameters (shared by every operation under that path).
        // Same location filter applies: query / path / header (no cookies).
        if (openApiDoc?.Paths is not null &&
            path is not null &&
            openApiDoc.Paths.TryGetValue(path, out var pathItemValue) &&
            pathItemValue is { Parameters: not null } pathItem)
        {
            foreach (var paramInterface in pathItem.Parameters)
            {
                var resolved = paramInterface.Resolve();
                if (resolved.Parameter is { In: ParameterLocation.Query or ParameterLocation.Path or ParameterLocation.Header } p && !p.IsUnbindable())
                {
                    CollectSchemaRefTypes(p.Schema, importTypes);
                }
            }
        }

        // From request body
        var (bodySchema, _) = operation.GetRequestBodySchemaWithContentType();
        if (bodySchema is not null)
        {
            CollectSchemaRefTypes(bodySchema, importTypes);

            // If this is a file upload and the body schema is a $ref, remove it
            // since the client method uses inline type literals, not the named type
            var isFileUpload = operation.HasFileUpload();
            if (isFileUpload && bodySchema is OpenApiSchemaReference fileUploadRef)
            {
                var refName = fileUploadRef.Reference.Id ?? fileUploadRef.Id;
                if (refName is not null)
                {
                    importTypes.Remove(refName);
                }
            }

            // The properties' types: a form model's own, and the members of a file upload's inline type literal
            // ({ ...; metadata?: FileMetadata }), which name their models too
            if (bodySchema.Properties is { Count: > 0 })
            {
                foreach (var prop in bodySchema.Properties)
                {
                    CollectSchemaRefTypes(prop.Value, importTypes);
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
        if (schema is null)
        {
            return;
        }

        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refName = schemaRef.Reference.Id ?? schemaRef.Id;
            if (refName is null)
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
                if (itemName is not null)
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

        // Handle allOf references.
        if (actualSchema.AllOf is { Count: > 0 })
        {
            foreach (var subSchema in actualSchema.AllOf)
            {
                if (subSchema is OpenApiSchemaReference allOfRef)
                {
                    var refName = allOfRef.Reference.Id ?? allOfRef.Id;
                    if (refName is not null)
                    {
                        importTypes.Add(refName);
                    }
                }
                else if (subSchema is OpenApiSchema { Properties.Count: > 0 } inlineSchema)
                {
                    // The inline members' types surface in the allOf's TypeScript type: as the generic
                    // argument of a pagination base (PaginatedResult<Item>, also through an array alias
                    // `results: $ref Accounts`) or as members of an intersection. Collect what they
                    // reference (callers narrow the import line to the names actually used).
                    foreach (var prop in inlineSchema.Properties.Values)
                    {
                        CollectSchemaRefTypes(prop, importTypes);
                    }
                }
            }
        }

        // Handle array item references.
        if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true && actualSchema.Items is OpenApiSchemaReference inlineItemRef)
        {
            var refName = inlineItemRef.Reference.Id ?? inlineItemRef.Id;
            if (refName is not null)
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
            var optional = MustBeSupplied(param) ? string.Empty : "?";
            parts.Add("'" + rawName + "'" + optional + ": " + paramType + FormatDefaultComment(param));
        }

        return "{ " + string.Join("; ", parts) + " }";
    }

    /// <summary>
    /// An object-literal / type-literal key: the name itself when it is an identifier, otherwise quoted
    /// (<c>'page-token'</c>), as the <c>Original</c> naming strategy keeps hyphenated names.
    /// </summary>
    public static string PropertyKey(string name)
        => IsIdentifier(name) ? name : "'" + name + "'";

    /// <summary>
    /// The identifier a path or querystring parameter is declared and passed as - a parameter of the method, the
    /// hook and the cache key - in the naming strategy, and never a keyword: <c>class</c>, <c>function</c>,
    /// <c>package</c> or <c>default</c> are <c>_class</c>, <c>_function</c>, ... (TS1390, TS1212). A query, header
    /// or cookie parameter is a property of an object and keeps its name (<see cref="PropertyKey"/>).
    /// </summary>
    /// <param name="parameter">The parameter.</param>
    /// <param name="namingStrategy">The naming strategy.</param>
    /// <returns>The identifier.</returns>
    public static string ParameterIdentifier(
        OpenApiParameter parameter,
        TypeScriptNamingStrategy namingStrategy)
        => (parameter.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy).ToTypeScriptIdentifier();

    /// <summary>
    /// Optional-chained property access: <c>query?.pageSize</c>, or <c>query?.['page-token']</c> for a name that
    /// is not an identifier.
    /// </summary>
    public static string OptionalPropertyAccess(
        string variable,
        string name)
        => IsIdentifier(name) ? variable + "?." + name : variable + "?.['" + name + "']";

    /// <summary>
    /// Builds a TypeScript inline type for query parameters (e.g., { limit?: number; offset?: number }).
    /// </summary>
    /// <param name="queryParams">The operation's query parameters.</param>
    /// <param name="namingStrategy">How the parameter names are cased in the type.</param>
    /// <param name="convertDates">When true, date and date-time parameters are typed <c>Date</c> instead of <c>string</c>.</param>
    /// <returns>The type literal; a parameter is optional unless the specification requires it without a default.</returns>
    public static string BuildQueryTypeInline(
        List<OpenApiParameter> queryParams,
        TypeScriptNamingStrategy namingStrategy = TypeScriptNamingStrategy.CamelCase,
        bool convertDates = false)
    {
        var parts = new List<string>();
        foreach (var param in queryParams)
        {
            var paramName = PropertyKey((param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy));
            var paramType = GetParameterType(param, convertDates);
            var optional = MustBeSupplied(param) ? string.Empty : "?";
            parts.Add(paramName + optional + ": " + paramType + FormatDefaultComment(param));
        }

        return "{ " + string.Join("; ", parts) + " }";
    }

    /// <summary>
    /// The <c>query</c> argument of a client method or hook: required (<c>query: { ... }</c>) when the
    /// specification requires one of the query parameters, otherwise optional (<c>query?: { ... }</c>).
    /// </summary>
    public static string BuildQueryParameter(
        List<OpenApiParameter> queryParams,
        TypeScriptNamingStrategy namingStrategy = TypeScriptNamingStrategy.CamelCase,
        bool convertDates = false)
        => (queryParams.Exists(MustBeSupplied) ? "query: " : "query?: ") +
           BuildQueryTypeInline(queryParams, namingStrategy, convertDates);

    /// <summary>
    /// The <c>headers</c> argument of a client method or hook: required when the specification requires
    /// one of the header parameters, otherwise optional.
    /// </summary>
    public static string BuildHeaderParameter(
        List<OpenApiParameter> headerParams,
        bool convertDates = false)
        => (headerParams.Exists(MustBeSupplied) ? "headers: " : "headers?: ") +
           BuildHeaderTypeInline(headerParams, convertDates);

    /// <summary>
    /// Joins a parameter list. TypeScript does not allow an optional parameter before a required one, so an
    /// optional parameter that is followed by a required one keeps its position and takes <c>undefined</c>
    /// explicitly: <c>query?: T, headers: U</c> becomes <c>query: T | undefined, headers: U</c>.
    /// </summary>
    public static string JoinParameters(IReadOnlyList<string> parameters)
    {
        var lastRequired = -1;
        for (var i = parameters.Count - 1; i >= 0; i--)
        {
            if (!IsOptionalParameter(parameters[i]))
            {
                lastRequired = i;
                break;
            }
        }

        var parts = new List<string>(parameters.Count);
        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            if (i < lastRequired && IsOptionalParameter(parameter))
            {
                var separator = parameter.IndexOf("?: ", StringComparison.Ordinal);
                var type = parameter[(separator + 3)..];
                if (type.Contains("=>", StringComparison.Ordinal))
                {
                    type = "(" + type + ")";
                }

                parameter = parameter[..separator] + ": " + type + " | undefined";
            }

            parts.Add(parameter);
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// The name of the trailing <c>AbortSignal</c> parameter of a client method: <c>signal</c>, or
    /// <c>abortSignal</c> when a parameter of the operation is called <c>signal</c> already.
    /// </summary>
    /// <param name="parameters">The other parameters, as declarations (<c>name?: type</c>) or names.</param>
    public static string SignalParameterName(IEnumerable<string> parameters)
        => parameters.Any(p => string.Equals(p.Split(':')[0].Trim().TrimEnd('?'), "signal", StringComparison.Ordinal))
            ? "abortSignal"
            : "signal";

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
            var tsName = ParameterIdentifier(param, namingStrategy);

            // Every value is percent-encoded: "a/b?c" must stay one path segment, not address another endpoint.
            string value;
            if (convertDates && IsDateParam(param))
            {
                value = SerializeDateParameter(param, tsName, optional: false);
            }
            else if (param.Schema is { } schema &&
                     (schema.IsOfType(JsonSchemaType.String) ||
                      schema.IsOfType(JsonSchemaType.Integer) ||
                      schema.IsOfType(JsonSchemaType.Number) ||
                      schema.IsOfType(JsonSchemaType.Boolean) ||
                      schema is OpenApiSchemaReference))
            {
                value = tsName;
            }
            else
            {
                // encodeURIComponent takes a string, number or boolean
                value = "String(" + tsName + ")";
            }

            var replacement = "${encodeURIComponent(" + value + ")}";

            interpolated = interpolated.Replace(
                "{" + param.Name + "}",
                replacement,
                StringComparison.Ordinal);
        }

        return "`" + interpolated + "`";
    }

    /// <summary>
    /// Emits the request body of a streamed read - a POST or QUERY search sends its criteria - as JSON (renamed to
    /// the API's property names and with dates as ISO strings, like <c>request</c>), into <c>streamBody</c>.
    /// Shared by the Axios and Fetch ApiClient extractors; expects a <c>headers: Headers</c> local.
    /// </summary>
    internal static void AppendStreamRequestBody(
        StringBuilder sb,
        bool hasWireNames)
    {
        var value = hasWireNames ? "toWire(options.body, options.wire?.body)" : "options.body";
        var stringify = $"JSON.stringify({value})";
        sb.Append("    const streamBody = options?.body !== undefined ? ").Append(stringify).AppendLine(" : undefined;");
        sb.AppendLine("    if (streamBody !== undefined) {");
        sb.AppendLine("      headers.set('Content-Type', 'application/json');");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits the ApiClient's <c>requestStream</c> for a specification whose models rename keys: it reads the items
    /// with <c>readStream</c> and renames each from the API's property names to the models'. Shared by the Axios and
    /// Fetch ApiClient extractors.
    /// </summary>
    internal static void AppendRenamingRequestStream(StringBuilder sb)
    {
        sb.AppendLine("  async *requestStream<T>(method: string, path: string, options?: RequestOptions, framing: StreamFraming = 'json-array', sseItemEvents?: readonly string[]): AsyncGenerator<T> {");
        sb.AppendLine("    const shape = options?.wire?.response;");
        sb.AppendLine("    for await (const item of this.readStream<unknown>(method, path, options, framing, sseItemEvents)) {");
        sb.AppendLine("      yield fromWire<T>(item, shape);");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits the module-level parseEventStream async generator function shared by both
    /// Axios and Fetch ApiClient extractors. Centralising the SSE parsing logic here
    /// ensures the two clients stay in sync without copying the block.
    /// </summary>
    internal static void AppendParseEventStreamHelper(StringBuilder sb)
    {
        sb.AppendLine("/** Releases a body reader; a stream that was not read to its end is cancelled first, so the connection is closed. */");
        sb.AppendLine("async function releaseReader(reader: ReadableStreamDefaultReader<Uint8Array>, finished: boolean): Promise<void> {");
        sb.AppendLine("  if (!finished) {");
        sb.AppendLine("    await reader.cancel().catch(() => undefined);");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  reader.releaseLock();");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/**");
        sb.AppendLine(" * The longest record (an event, a JSON Lines line, a JSON Text Sequence record, a JSON array element, a multipart");
        sb.AppendLine(" * part) a stream reader takes, in characters: a longer one - or a server that never ends one - is a RangeError, not");
        sb.AppendLine(" * a buffer that grows until the tab or the process runs out of memory. Default: 16 MB. Settable.");
        sb.AppendLine(" */");
        sb.AppendLine("export const streamLimits = { maxRecordLength: 16 * 1024 * 1024 };");
        sb.AppendLine();
        sb.AppendLine("function guardRecord(length: number): void {");
        sb.AppendLine("  if (length > streamLimits.maxRecordLength) {");
        sb.AppendLine("    throw new RangeError(`A streamed record is longer than ${streamLimits.maxRecordLength} characters.`);");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/**");
        sb.AppendLine(" * The body of one part of a multipart response - what follows its boundary: past the boundary's padding, the");
        sb.AppendLine(" * part's headers (a part may have none) and the blank line after them. Null for the closing boundary and for a");
        sb.AppendLine(" * part whose headers have not ended yet.");
        sb.AppendLine(" */");
        sb.AppendLine("function multipartBody(part: string): string | null {");
        sb.AppendLine("  if (part.startsWith('--')) return null;");
        sb.AppendLine("  const head = part.replace(/^[ \\t]*\\r\\n/, '');");
        sb.AppendLine("  if (head.startsWith('\\r\\n')) return head.substring(2).trim();");
        sb.AppendLine("  const sep = head.indexOf('\\r\\n\\r\\n');");
        sb.AppendLine("  return sep === -1 ? null : head.substring(sep + 4).trim();");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("async function* parseEventStream<T>(reader: ReadableStreamDefaultReader<Uint8Array>, decoder: TextDecoder, reviver?: (key: string, value: unknown) => unknown, itemEvents?: readonly string[]): AsyncGenerator<T> {");
        sb.AppendLine("  let buffer = '';");
        sb.AppendLine("  let finished = false;");
        sb.AppendLine("  try {");
        sb.AppendLine("    while (true) {");
        sb.AppendLine("      const { done, value } = await reader.read();");
        sb.AppendLine("      if (done) { finished = true; break; }");
        sb.AppendLine("      buffer += decoder.decode(value, { stream: true }).replace(/\\r/g, '');");
        sb.AppendLine("      let sep: number;");
        sb.AppendLine("      while ((sep = buffer.indexOf('\\n\\n')) !== -1) {");
        sb.AppendLine("        guardRecord(sep);");
        sb.AppendLine("        const rawEvent = buffer.substring(0, sep);");
        sb.AppendLine("        buffer = buffer.substring(sep + 2);");
        sb.AppendLine("        const lines = rawEvent.split('\\n');");
        sb.AppendLine("        const eventLine = lines.find((l) => l.startsWith('event:'));");
        sb.AppendLine("        // An event field without a value names no event");
        sb.AppendLine("        const eventType = eventLine?.slice(6).trim() || 'message';");
        sb.AppendLine("        const data = lines");
        sb.AppendLine("          .filter((l) => l.startsWith('data:'))");
        sb.AppendLine("          .map((l) => l.slice(5).trimStart())");
        sb.AppendLine("          .join('\\n');");
        sb.AppendLine("        // The item event is the unnamed one (or 'message', the same by the standard), and the ones the operation");
        sb.AppendLine("        // names (x-sse-item-events): each must be an item. An event without data and any other named event (a");
        sb.AppendLine("        // heartbeat, a control frame) are read past.");
        sb.AppendLine("        if ((eventType !== 'message' && !itemEvents?.includes(eventType)) || data.trim().length === 0) continue;");
        sb.AppendLine("        yield JSON.parse(data, reviver) as T;");
        sb.AppendLine("      }");
        sb.AppendLine("      guardRecord(buffer.length);");
        sb.AppendLine("    }");
        sb.AppendLine("  } finally {");
        sb.AppendLine("    await releaseReader(reader, finished);");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        AppendFramedStreamParsers(sb);
    }

    /// <summary>
    /// Emits the readers of the JSON framings a streamed response comes in: a JSON array read one element at a
    /// time (any element - an object, an array, a string, a number, a literal - split across chunks anywhere),
    /// JSON Lines and JSON Text Sequence (RFC 7464). The buffer is consumed by an offset, not copied per item.
    /// </summary>
    private static void AppendFramedStreamParsers(StringBuilder sb)
    {
        sb.AppendLine("/** The end (exclusive) of the JSON value that starts at `start`, or -1 while the text holds only part of it. A number or a literal at the text's end is complete only when the stream is. */");
        sb.AppendLine("function scanJsonValue(text: string, start: number, atEnd: boolean): number {");
        sb.AppendLine("  const first = text.charAt(start);");
        sb.AppendLine("  if (first === '{' || first === '[') {");
        sb.AppendLine("    let depth = 0, inStr = false, esc = false;");
        sb.AppendLine("    for (let i = start; i < text.length; i++) {");
        sb.AppendLine("      const ch = text.charAt(i);");
        sb.AppendLine("      if (inStr) { if (esc) esc = false; else if (ch === '\\\\') esc = true; else if (ch === '\"') inStr = false; continue; }");
        sb.AppendLine("      if (ch === '\"') inStr = true;");
        sb.AppendLine("      else if (ch === '{' || ch === '[') depth++;");
        sb.AppendLine("      else if (ch === '}' || ch === ']') { depth--; if (depth === 0) return i + 1; }");
        sb.AppendLine("    }");
        sb.AppendLine("    return -1;");
        sb.AppendLine("  }");
        sb.AppendLine("  if (first === '\"') {");
        sb.AppendLine("    let esc = false;");
        sb.AppendLine("    for (let i = start + 1; i < text.length; i++) {");
        sb.AppendLine("      const ch = text.charAt(i);");
        sb.AppendLine("      if (esc) esc = false; else if (ch === '\\\\') esc = true; else if (ch === '\"') return i + 1;");
        sb.AppendLine("    }");
        sb.AppendLine("    return -1;");
        sb.AppendLine("  }");
        sb.AppendLine("  let i = start;");
        sb.AppendLine("  while (i < text.length && !' \\t\\r\\n,]}'.includes(text.charAt(i))) i++;");
        sb.AppendLine("  return i < text.length || atEnd ? i : -1;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/** Reads a JSON array one element at a time; values that are not wrapped in an array are read one after the other. */");
        sb.AppendLine("async function* parseJsonArrayStream<T>(reader: ReadableStreamDefaultReader<Uint8Array>, decoder: TextDecoder): AsyncGenerator<T> {");
        sb.AppendLine("  let buffer = '';");
        sb.AppendLine("  let pos = 0;");
        sb.AppendLine("  let finished = false;");
        sb.AppendLine("  let inArray: boolean | undefined;");
        sb.AppendLine("  try {");
        sb.AppendLine("    while (true) {");
        sb.AppendLine("      const { done, value } = await reader.read();");
        sb.AppendLine("      if (done) finished = true; else buffer += decoder.decode(value, { stream: true });");
        sb.AppendLine("      let ended = false;");
        sb.AppendLine("      while (true) {");
        sb.AppendLine("        while (pos < buffer.length && ' \\t\\r\\n,'.includes(buffer.charAt(pos))) pos++;");
        sb.AppendLine("        if (pos >= buffer.length) break;");
        sb.AppendLine("        if (inArray === undefined) { inArray = buffer.charAt(pos) === '['; if (inArray) { pos++; continue; } }");
        sb.AppendLine("        if (inArray && buffer.charAt(pos) === ']') { ended = true; break; }");
        sb.AppendLine("        const end = scanJsonValue(buffer, pos, done);");
        sb.AppendLine("        if (end === -1) break;");
        sb.AppendLine("        guardRecord(end - pos);");
        sb.AppendLine("        yield JSON.parse(buffer.substring(pos, end)) as T;");
        sb.AppendLine("        pos = end;");
        sb.AppendLine("      }");
        sb.AppendLine("      if (ended || done) break;");
        sb.AppendLine("      if (pos > 0) { buffer = buffer.substring(pos); pos = 0; }");
        sb.AppendLine("      guardRecord(buffer.length);");
        sb.AppendLine("    }");
        sb.AppendLine("  } finally {");
        sb.AppendLine("    await releaseReader(reader, finished);");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/** Reads JSON Lines: one value per line, blank lines skipped, a last line without its newline included. */");
        sb.AppendLine("async function* parseJsonLinesStream<T>(reader: ReadableStreamDefaultReader<Uint8Array>, decoder: TextDecoder): AsyncGenerator<T> {");
        sb.AppendLine("  let buffer = '';");
        sb.AppendLine("  let finished = false;");
        sb.AppendLine("  try {");
        sb.AppendLine("    while (true) {");
        sb.AppendLine("      const { done, value } = await reader.read();");
        sb.AppendLine("      if (done) { finished = true; break; }");
        sb.AppendLine("      buffer += decoder.decode(value, { stream: true });");
        sb.AppendLine("      let nl: number;");
        sb.AppendLine("      while ((nl = buffer.indexOf('\\n')) !== -1) {");
        sb.AppendLine("        guardRecord(nl);");
        sb.AppendLine("        const line = buffer.substring(0, nl).trim();");
        sb.AppendLine("        buffer = buffer.substring(nl + 1);");
        sb.AppendLine("        if (line.length > 0) yield JSON.parse(line) as T;");
        sb.AppendLine("      }");
        sb.AppendLine("      guardRecord(buffer.length);");
        sb.AppendLine("    }");
        sb.AppendLine("    const rest = buffer.trim();");
        sb.AppendLine("    if (rest.length > 0) yield JSON.parse(rest) as T;");
        sb.AppendLine("  } finally {");
        sb.AppendLine("    await releaseReader(reader, finished);");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/** Reads a JSON Text Sequence (RFC 7464): each record is a record separator (0x1E), the JSON text and a newline. */");
        sb.AppendLine("async function* parseJsonSeqStream<T>(reader: ReadableStreamDefaultReader<Uint8Array>, decoder: TextDecoder): AsyncGenerator<T> {");
        sb.AppendLine("  let buffer = '';");
        sb.AppendLine("  let finished = false;");
        sb.AppendLine("  const parse = (record: string): T | undefined => { const text = record.replace(/\\x1e/g, '').trim(); return text.length > 0 ? JSON.parse(text) as T : undefined; };");
        sb.AppendLine("  try {");
        sb.AppendLine("    while (true) {");
        sb.AppendLine("      const { done, value } = await reader.read();");
        sb.AppendLine("      if (done) { finished = true; break; }");
        sb.AppendLine("      buffer += decoder.decode(value, { stream: true });");
        sb.AppendLine("      let rs: number;");
        sb.AppendLine("      while ((rs = buffer.indexOf('\\x1e', 1)) !== -1) {");
        sb.AppendLine("        guardRecord(rs);");
        sb.AppendLine("        const item = parse(buffer.substring(0, rs));");
        sb.AppendLine("        buffer = buffer.substring(rs);");
        sb.AppendLine("        if (item !== undefined) yield item;");
        sb.AppendLine("      }");
        sb.AppendLine("      guardRecord(buffer.length);");
        sb.AppendLine("      if (buffer.endsWith('\\n')) { const item = parse(buffer); buffer = ''; if (item !== undefined) yield item; }");
        sb.AppendLine("    }");
        sb.AppendLine("    const last = parse(buffer);");
        sb.AppendLine("    if (last !== undefined) yield last;");
        sb.AppendLine("  } finally {");
        sb.AppendLine("    await releaseReader(reader, finished);");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    /// <summary>
    /// The discriminator and base error type <c>handleResponse</c> uses for an error status.
    /// </summary>
    private static (string Discriminator, string BaseType) ErrorArmFor(
        string statusCode)
        => ErrorStatusMapping.TryGetValue(statusCode, out var mapped)
            ? mapped
            : statusCode.StartsWith('4')
                ? ("clientError", "ApiError")
                : ("serverError", "ApiError");

    /// <summary>
    /// The TypeScript type of a declared error body - the declared model, <c>string</c> for a JSON
    /// string or text - or <see langword="null"/> when nothing usable is declared (then the arm's error
    /// stays an untyped ApiError). Uses the same resolution as the generated server and C# client.
    /// </summary>
    private static string? ResolveErrorBodyType(
        IOpenApiResponse? response,
        HashSet<string> imports)
    {
        var shape = ErrorResponseShapeResolver.Resolve(response);
        switch (shape.Kind)
        {
            case ErrorResponseKind.JsonString:
            case ErrorResponseKind.Text:
                return "string";
            case ErrorResponseKind.JsonObject:
            case ErrorResponseKind.XmlObject:
            case ErrorResponseKind.Problem when shape.Schema is not null:
            case ErrorResponseKind.ValidationProblem when shape.Schema is not null:
                CollectSchemaRefTypes(shape.Schema!, imports);
                return shape.Schema!.ToTypeScriptReturnType();
            default:
                return null;
        }
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
                return dataType is null
                    ? $"  | {{ status: '{discriminator}'; response: {responseType} }}"
                    : $"  | {{ status: '{discriminator}'; data: {dataType}; response: {responseType} }}";
            }

            case "204":
            case "205":
                discriminator = "noContent";
                return $"  | {{ status: 'noContent'; response: {responseType} }}";

            // The other 2xx with content (203, 206, 207, 208, 226) are ok, with their declared data.
            case "203":
            case "206":
            case "207":
            case "208":
            case "226":
            {
                discriminator = "ok";
                var dataType = ResolveSuccessDataType(statusCode, operation, isFileDownload, isTextDownload, imports);
                return dataType is null
                    ? $"  | {{ status: 'ok'; response: {responseType} }}"
                    : $"  | {{ status: 'ok'; data: {dataType}; response: {responseType} }}";
            }

            // A redirect is followed by fetch / axios (their default): its target's response is what comes back. The
            // redirect arms are added after the loop, so a declared 200 keeps its typed ok arm.
            case "301":
            case "302":
            case "303":
            case "307":
            case "308":
                return null;

            case "304":
                discriminator = "notModified";
                return $"  | {{ status: 'notModified'; response: {responseType} }}";

            // A 3xx the runtime does not follow (300, 305, 306): its body, if one is declared.
            case "300":
            case "305":
            case "306":
            {
                discriminator = "redirection";
                var dataType = GetResponseModelSchema(operation, statusCode) is not null
                    ? ResolveSuccessDataType(statusCode, operation, isFileDownload: false, isTextDownload: false, imports)
                    : null;
                return $"  | {{ status: 'redirection'; data?: {dataType ?? "unknown"}; response: {responseType} }}";
            }
        }

        // Error statuses are handled by BuildPerOperationResultType (ErrorArmFor).
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

        var schema = GetResponseModelSchema(operation, statusCode);

        // Fall back to text/xml on 200/201/202 — handleResponse delivers those as raw strings.
        if (schema is null &&
            operation.Responses is not null &&
            operation.Responses.TryGetValue(statusCode, out var response) &&
            response.TryGetTextResponseMediaType(out _, out var textMedia) &&
            textMedia is not null)
        {
            return "string";
        }

        if (schema is null)
        {
            return null;
        }

        CollectSchemaRefTypes(schema, imports);

        // A model or a file: the model, or the file as a Blob
        return statusCode == "200" && operation.GetModelOrFileMediaTypes().Count > 0
            ? schema.ToTypeScriptReturnType() + " | Blob"
            : schema.ToTypeScriptReturnType();
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

        if (parts.Count == 0)
        {
            return null;
        }

        // Open, like the enum types: a value the server adds later, or one read from a response, can be passed
        return isStringEnum && !EnumsAreClosed.Value
            ? string.Join(" | ", parts) + " | (string & {})"
            : string.Join(" | ", parts);
    }

    private sealed class EnumsScopeRestore(bool previous) : IDisposable
    {
        public void Dispose()
            => EnumsAreClosed.Value = previous;
    }

    private static bool IsIdentifierChar(char c)
        => char.IsLetterOrDigit(c) || c == '_' || c == '$';

    /// <summary>
    /// Formats a parameter's <c>default:</c> value as an inline TypeScript comment so it
    /// shows up in IDE hover/autocomplete. Returns empty when the parameter has no
    /// default — callers append the result verbatim, so empty means "emit nothing extra".
    /// String defaults are single-quoted, primitives are emitted verbatim, and anything
    /// else falls through to <see cref="JsonNode.ToJsonString"/>.
    /// </summary>
    private static string FormatDefaultComment(OpenApiParameter param)
    {
        if (param.Schema is not OpenApiSchema schema || schema.Default is null)
        {
            return string.Empty;
        }

        return " /* default: " + TypeScriptLiteralHelper.EscapeComment(FormatJsonDefault(schema.Default)) + " */";
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

    private static bool IsIdentifier(string name)
        => name.Length > 0 &&
           (char.IsLetter(name[0]) || name[0] is '_' or '$') &&
           name.All(c => char.IsLetterOrDigit(c) || c is '_' or '$');

    /// <summary>
    /// A parameter the caller has to pass: the specification requires it and gives no default. With a default the
    /// parameter may be left out - the default is what a missing value means (the C# clients do the same).
    /// </summary>
    private static bool MustBeSupplied(OpenApiParameter param)
        => param.Required && param.Schema is not OpenApiSchema { Default: not null };

    private static bool IsOptionalParameter(string parameter)
    {
        // "name?: type" - the "?" belongs to the parameter name, not to a member inside its type.
        var colon = parameter.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && parameter[colon - 1] == '?';
    }
}