// ReSharper disable InvertIf
namespace Atc.OpenApi.Extensions;

/// <summary>
/// Extension methods for OpenApiOperation to handle operation queries and analysis.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "CA1708:Names should differ by more than case", Justification = "OK - CLang14 - multiple extension(...) blocks")]
[SuppressMessage("", "CA2208:Method ReplaceAt passes 'name' as the paramName argument to a ArgumentNullException constructor", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "S1144:Remove the unused private method", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "S3928:The parameter name 'schema'", Justification = "OK - CLang14 - extension")]
public static class OpenApiOperationExtensions
{
    /// <summary>
    /// The names of a paginated response's continuation-token property, in order of preference.
    /// </summary>
    private static readonly string[] PaginationTokenPropertyNames =
    [
        "continuationToken",
        "continuation",
        "nextPageToken",
        "nextCursor",
        "cursor",
    ];

    /// <summary>
    /// Whether a schema is a string (also a nullable one, <c>type: [string, 'null']</c>).
    /// </summary>
    /// <param name="schema">The schema.</param>
    /// <returns><see langword="true"/> for a string schema.</returns>
    public static bool IsStringSchema(IOpenApiSchema? schema)
        => schema?.Type is { } type &&
           type.HasFlag(JsonSchemaType.String) &&
           (type | JsonSchemaType.String | JsonSchemaType.Null) == (JsonSchemaType.String | JsonSchemaType.Null);

    /// <param name="operation">The OpenAPI operation.</param>
    extension(OpenApiOperation operation)
    {
        /// <summary>
        /// Gets the operation ID, or generates one from the HTTP method and the path when it is missing or empty -
        /// in both cases as an identifier: a character that is none becomes an underscore (<c>repos/get</c> is
        /// <c>repos_get</c>) and an id that starts with a digit is prefixed (<c>1stThing</c> is <c>Op1stThing</c>),
        /// so that no generator derives a name that breaks the consumer's build.
        /// </summary>
        /// <param name="path">The API path.</param>
        /// <param name="httpMethod">The HTTP method (GET, POST, etc.).</param>
        /// <returns>The operation ID.</returns>
        public string GetOperationId(
            string path,
            string httpMethod)
        {
            if (operation is null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var operationId = operation.OperationId;
            if (string.IsNullOrWhiteSpace(operationId))
            {
                // Generate default: HttpMethodPathWithoutSlashesAndBraces
                operationId = httpMethod + path
                    .Replace("/", "_")
                    .Replace("{", string.Empty)
                    .Replace("}", string.Empty);
            }

            var sb = new System.Text.StringBuilder(operationId!.Length + 2);
            foreach (var c in operationId)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            }

            if (sb.Length > 0 && char.IsDigit(sb[0]))
            {
                sb.Insert(0, "Op");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Checks if the operation has parameters.
        /// </summary>
        /// <returns>True if the operation has parameters.</returns>
        public bool HasParameters()
            => operation is { Parameters.Count: > 0 };

        /// <summary>
        /// Checks if the operation has a request body.
        /// </summary>
        /// <returns>True if the operation has a request body.</returns>
        public bool HasRequestBody()
            => operation is { RequestBody.Content.Count: > 0 };

        /// <summary>
        /// Gets the request body schema for a specific content type.
        /// </summary>
        /// <param name="contentType">The content type (default: application/json).</param>
        /// <returns>The schema or null if not found.</returns>
        public IOpenApiSchema? GetRequestBodySchema(string contentType = "application/json")
        {
            if (operation is not { RequestBody.Content: not null })
            {
                return null;
            }

            if (operation.RequestBody.Content.TryGetValue(contentType, out var mediaType))
            {
                return mediaType.Schema;
            }

            // A JSON body declared under another JSON media type (application/merge-patch+json, application/vnd.x+json)
            return string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase) &&
                   operation.GetJsonRequestBodyMediaType() is { } jsonMediaType
                ? operation.RequestBody.Content[jsonMediaType].Schema
                : null;
        }

        /// <summary>
        /// The request body sent as a stream of records: a JSON Lines or JSON Text Sequence media type with a
        /// <c>schema</c> (one record, or an array of them) or an <c>itemSchema</c>. Server-Sent Events and
        /// <c>multipart/mixed</c> are no request bodies (the validator says so).
        /// </summary>
        /// <returns>The streamed body, or <see langword="null"/> when the body is not one.</returns>
        public StreamedRequestBody? GetStreamedRequestBody()
        {
            if (operation.RequestBody?.Content is not { Count: > 0 } content)
            {
                return null;
            }

            var mediaTypes = new List<string>();
            IOpenApiSchema? itemSchema = null;
            IOpenApiSchema? collectionSchema = null;
            foreach (var kvp in content)
            {
                if (StreamingMediaType.Classify(kvp.Key) is not (StreamingFraming.JsonLines or StreamingFraming.JsonSequence))
                {
                    continue;
                }

                var item = kvp.Value?.ItemSchema ?? StreamingMediaType.ItemSchemaOf(kvp.Key, kvp.Value?.Schema);
                if (item is null)
                {
                    continue;
                }

                mediaTypes.Add(kvp.Key);
                if (itemSchema is null)
                {
                    itemSchema = item;
                    collectionSchema = kvp.Value?.ItemSchema is null && kvp.Value?.Schema is { } schema && !ReferenceEquals(schema, item)
                        ? schema
                        : null;
                }
            }

            if (itemSchema is null)
            {
                return null;
            }

            var acceptsJsonArray = content.TryGetValue("application/json", out var json) &&
                                   json?.Schema is { } jsonSchema &&
                                   jsonSchema.Type?.HasFlag(JsonSchemaType.Array) == true;

            return new StreamedRequestBody(
                mediaTypes,
                itemSchema,
                collectionSchema?.MinItems > 0 ? collectionSchema.MinItems : null,
                collectionSchema?.MaxItems,
                acceptsJsonArray,
                operation.RequestBody.Required);
        }

        /// <summary>
        /// The media type of the operation's JSON request body: <c>application/json</c>, else the first media type
        /// with a <c>+json</c> suffix (<c>application/merge-patch+json</c>, <c>application/vnd.x+json</c>), by name.
        /// </summary>
        /// <returns>The media type as declared, or <see langword="null"/> when the body is not JSON.</returns>
        public string? GetJsonRequestBodyMediaType()
        {
            if (operation.RequestBody?.Content is not { } content)
            {
                return null;
            }

            if (content.ContainsKey("application/json"))
            {
                return "application/json";
            }

            return content.Keys
                .Where(key => key.Split(';')[0].Trim().EndsWith("+json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets the response schema for a specific status code and content type.
        /// </summary>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <param name="contentType">The content type (default: application/json).</param>
        /// <returns>The schema or null if not found.</returns>
        /// <remarks>
        /// With the default <c>application/json</c>, a response that only declares another JSON media type
        /// (a <c>+json</c> suffix such as <c>application/problem+json</c> or <c>application/vnd.x+json</c>)
        /// yields that media type's schema.
        /// </remarks>
        public IOpenApiSchema? GetResponseSchema(
            string statusCode,
            string contentType = "application/json")
        {
            if (operation is not { Responses: not null } ||
                !operation.Responses.TryGetValue(statusCode, out var response) ||
                response.Content is null)
            {
                return null;
            }

            if (response.Content.TryGetValue(contentType, out var mediaType))
            {
                return mediaType.Schema;
            }

            if (!string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return response.Content
                .Where(kv => kv.Key.Split(';')[0].Trim().EndsWith("+json", StringComparison.OrdinalIgnoreCase) && kv.Value?.Schema is not null)
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => kv.Value!.Schema)
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets all parameters filtered by location (Query, Path, Header, Cookie).
        /// </summary>
        /// <param name="location">The parameter location to filter by.</param>
        /// <returns>Enumerable of OpenApiParameter matching the location.</returns>
        public IEnumerable<OpenApiParameter> GetParametersByLocation(ParameterLocation location)
            => operation is null
                ? throw new ArgumentNullException(nameof(operation))
                : GetParametersByLocationIterator(operation, location);

        /// <summary>
        /// Gets all query parameters from the operation.
        /// </summary>
        /// <returns>Enumerable of query parameters.</returns>
        public IEnumerable<OpenApiParameter> GetQueryParameters()
            => operation.GetParametersByLocation(ParameterLocation.Query);

        /// <summary>
        /// Gets all path parameters from the operation.
        /// </summary>
        /// <returns>Enumerable of path parameters.</returns>
        public IEnumerable<OpenApiParameter> GetPathParameters()
            => operation.GetParametersByLocation(ParameterLocation.Path);

        /// <summary>
        /// Checks if the operation has a file upload request body.
        /// </summary>
        /// <returns>True if the operation has a file upload request body.</returns>
        public bool HasFileUpload()
        {
            if (operation.RequestBody?.Content is null)
            {
                return false;
            }

            foreach (var key in operation.RequestBody.Content.Keys)
            {
                if (IsFileUploadContentType(key))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the file upload content type if present.
        /// </summary>
        /// <returns>The file upload content type or null if not found.</returns>
        public string? GetFileUploadContentType()
            => operation.RequestBody?.Content?.Keys.FirstOrDefault(IsFileUploadContentType);

        /// <summary>
        /// Gets request body schema with content type info. Prioritizes JSON, then file uploads.
        /// </summary>
        /// <returns>A tuple containing the schema and content type.</returns>
        public (IOpenApiSchema? Schema, string ContentType) GetRequestBodySchemaWithContentType()
        {
            if (operation.RequestBody?.Content is null)
            {
                return (null, string.Empty);
            }

            // Priority 0: a stream of records (JSON Lines, JSON Text Sequences) - the record's schema. It wins over a
            // JSON array offered beside it: the server streams that array as the same records
            if (operation.GetStreamedRequestBody() is { } streamed)
            {
                return (streamed.ItemSchema, streamed.PrimaryMediaType);
            }

            // Priority 1: JSON (application/json, else a +json media type)
            if (operation.GetJsonRequestBodyMediaType() is { } jsonMediaType)
            {
                return (operation.RequestBody.Content[jsonMediaType].Schema, jsonMediaType);
            }

            // Priority 2: File uploads
            foreach (var kvp in operation.RequestBody.Content)
            {
                if (IsFileUploadContentType(kvp.Key))
                {
                    return (kvp.Value.Schema, kvp.Key);
                }
            }

            // Priority 3: a URL-encoded form (a model sent as form fields)
            foreach (var kvp in operation.RequestBody.Content)
            {
                if (IsFormContentType(kvp.Key))
                {
                    return (kvp.Value.Schema, kvp.Key);
                }
            }

            // Priority 4: an XML model body (written by the model's generated XML mapping)
            foreach (var kvp in operation.RequestBody.Content)
            {
                if (ResponseBodyClassifier.Classify(kvp.Key, kvp.Value.Schema) == ResponseBodyKind.Xml)
                {
                    return (kvp.Value.Schema, kvp.Key);
                }
            }

            return (null, string.Empty);
        }

        /// <summary>
        /// Reads <c>x-sse-item-events</c>: the names of the Server-Sent Events that carry an item of a streamed
        /// response, besides the unnamed event (and <c>message</c>, the same by the standard), which always does.
        /// One name or a list of names; without the extension the list is empty, and every other named event - a
        /// heartbeat, a control frame - is read past.
        /// </summary>
        /// <returns>The event names, in the order given, without duplicates.</returns>
        public IReadOnlyList<string> GetServerSentEventItemNames()
        {
            if (operation.Extensions is null ||
                !operation.Extensions.TryGetValue("x-sse-item-events", out var extension) ||
                extension is not JsonNodeExtension { Node: { } node })
            {
                return [];
            }

            var names = new List<string>();
            foreach (var candidate in node is JsonArray array ? array : [node.DeepClone()])
            {
                if (candidate is JsonValue value &&
                    value.TryGetValue<string>(out var name) &&
                    name.Length > 0 &&
                    !names.Contains(name, StringComparer.Ordinal))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        /// <summary>
        /// Gets the explicit <c>x-operation-response-cardinality</c> annotation from the operation.
        /// Recognized values are <c>single</c> and <c>array</c> (case-insensitive); any other value
        /// (including missing) returns <c>null</c> so the caller falls back to heuristic inference.
        /// </summary>
        /// <returns>"single", "array", or null when the annotation is absent or invalid.</returns>
        public string? GetResponseCardinalityAnnotation()
        {
            if (operation.Extensions is null ||
                !operation.Extensions.TryGetValue("x-operation-response-cardinality", out var extension) ||
                extension is not JsonNodeExtension jsonNodeExt ||
                jsonNodeExt.Node is not JsonValue jsonValue ||
                !jsonValue.TryGetValue<string>(out var value))
            {
                return null;
            }

            if (string.Equals(value, "single", StringComparison.OrdinalIgnoreCase))
            {
                return "single";
            }

            if (string.Equals(value, "array", StringComparison.OrdinalIgnoreCase))
            {
                return "array";
            }

            return null;
        }

        /// <summary>
        /// Checks if the operation is a file download with the x-return-stream: true extension.
        /// When true, the EndpointPerOperation client hands the open response stream to the caller instead of the bytes.
        /// </summary>
        /// <returns>True if the operation's download should be streamed.</returns>
        public bool IsStreamedDownloadOperation()
        {
            if (!operation.HasFileDownload() ||
                operation.Extensions is null ||
                !operation.Extensions.TryGetValue("x-return-stream", out var extension) ||
                extension is not JsonNodeExtension jsonNodeExt)
            {
                return false;
            }

            return jsonNodeExt.Node is JsonValue jsonValue
                && jsonValue.TryGetValue<bool>(out var boolValue)
                && boolValue;
        }

        /// <summary>
        /// Checks if the operation has x-return-async-enumerable: true extension.
        /// </summary>
        /// <returns>True if the operation should return IAsyncEnumerable.</returns>
        public bool IsAsyncEnumerableOperation()
        {
            if (operation.Extensions is null ||
                !operation.Extensions.TryGetValue("x-return-async-enumerable", out var extension) ||
                extension is not JsonNodeExtension jsonNodeExt)
            {
                return false;
            }

            return jsonNodeExt.Node is JsonValue jsonValue
                && jsonValue.TryGetValue<bool>(out var boolValue)
                && boolValue;
        }

        /// <summary>
        /// Gets the streaming response media type and its per-element <c>itemSchema</c>,
        /// inspecting the <c>200</c> and <c>201</c> responses specifically, or <c>null</c>
        /// when neither declares one.
        /// </summary>
        public (string MediaType, IOpenApiSchema ItemSchema)? GetStreamingResponse()
        {
            if (operation.Responses is null)
            {
                return null;
            }

            foreach (var statusCode in new[] { "200", "201" })
            {
                if (!operation.Responses.TryGetValue(statusCode, out var response) ||
                    response.Content is null)
                {
                    continue;
                }

                foreach (var kvp in response.Content)
                {
                    if (kvp.Value.ItemSchema is not null)
                    {
                        return (kvp.Key, kvp.Value.ItemSchema);
                    }
                }
            }

            return operation.GetAnnotatedStreamingResponse();
        }

        /// <summary>
        /// The pre-3.2 way to declare a sequential stream: a <c>schema</c> under a streaming media type
        /// (<c>text/event-stream</c>, <c>application/jsonl</c>, <c>application/json-seq</c>, ...). The schema describes
        /// one element, or the array of them. The media type says it is a stream on its own;
        /// <c>multipart/mixed</c> - as often a batch answer as a stream - streams only with
        /// <c>x-return-async-enumerable</c>.
        /// </summary>
        private (string MediaType, IOpenApiSchema ItemSchema)? GetAnnotatedStreamingResponse()
        {
            if (operation.Responses is null)
            {
                return null;
            }

            var annotated = operation.IsAsyncEnumerableOperation();
            foreach (var statusCode in new[] { "200", "201" })
            {
                if (!operation.Responses.TryGetValue(statusCode, out var response) ||
                    response.Content is null)
                {
                    continue;
                }

                foreach (var kvp in response.Content)
                {
                    if ((annotated || StreamingMediaType.Classify(kvp.Key) != StreamingFraming.MultipartMixed) &&
                        StreamingMediaType.ItemSchemaOf(kvp.Key, kvp.Value.Schema) is { } itemSchema)
                    {
                        return (kvp.Key, itemSchema);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Classifies the streaming response's wire framing from its declared media type.
        /// Returns <see cref="StreamingFraming.JsonArray"/> when the only streaming signal is the
        /// legacy <c>x-return-async-enumerable</c> annotation on <c>application/json</c>.
        /// When both an <c>itemSchema</c> and <c>x-return-async-enumerable</c> are present the
        /// media-type framing wins, because <see cref="GetStreamingResponse"/> returns non-null
        /// and the annotation is ignored.
        /// </summary>
        public StreamingFraming GetStreamingFraming()
        {
            var streaming = operation.GetStreamingResponse();
            return streaming is { } s
                ? StreamingMediaType.Classify(s.MediaType)
                : StreamingFraming.JsonArray;
        }

        /// <summary>
        /// Gets the per-element schema for an OpenAPI 3.2 streaming response — the
        /// <c>itemSchema</c> declared on a 2xx response media type (e.g.
        /// <c>text/event-stream</c>, <c>application/jsonl</c>). Unlike <c>schema</c>,
        /// <c>itemSchema</c> already describes a single streamed element, so callers must
        /// use it directly (do not unwrap arrays/pagination).
        /// </summary>
        /// <returns>The item schema, or <c>null</c> when no response media type declares one.</returns>
        public IOpenApiSchema? GetStreamingItemSchema()
            => operation.GetStreamingResponse()?.ItemSchema;

        /// <summary>
        /// Determines whether the operation produces a streamed/sequential response that
        /// should be generated as <c>IAsyncEnumerable&lt;T&gt;</c> (server) / async-iterator
        /// (clients). True when the operation carries the <c>x-return-async-enumerable</c>
        /// annotation, declares an OpenAPI 3.2 <c>itemSchema</c> on a 2xx response, or has a
        /// <c>schema</c> under a sequential media type (JSON Lines, JSON Text Sequences, Server-Sent Events).
        /// </summary>
        /// <returns>True if the response should be streamed.</returns>
        public bool IsStreamingResponse()
            => operation.IsAsyncEnumerableOperation() || operation.GetStreamingItemSchema() is not null;

        /// <summary>
        /// Gets the continuation-token property of a paginated response: a property of the 200 response
        /// (directly, or in one of its <c>allOf</c> parts - typically a pagination wrapper such as
        /// <c>PaginatedResult</c>) named <c>continuationToken</c>, <c>continuation</c>, <c>nextPageToken</c>,
        /// <c>nextCursor</c> or <c>cursor</c>. The caller pages by sending that value back through the
        /// operation's own continuation parameter.
        /// </summary>
        /// <returns>The property name as declared in the schema, or <see langword="null"/>.</returns>
        public string? GetPaginationTokenProperty()
        {
            if (operation.IsStreamingResponse())
            {
                return null;
            }

            var schema = operation.GetResponseSchema("200");
            if (schema is null)
            {
                return null;
            }

            var candidates = new List<IOpenApiSchema> { schema };
            if (schema.AllOf is { Count: > 0 } allOf)
            {
                candidates.AddRange(allOf);
            }

            foreach (var tokenName in PaginationTokenPropertyNames)
            {
                foreach (var candidate in candidates)
                {
                    // A continuation token is an opaque string.
                    var match = candidate.Properties?.FirstOrDefault(p =>
                        string.Equals(p.Key, tokenName, StringComparison.OrdinalIgnoreCase) &&
                        IsStringSchema(p.Value));
                    if (match?.Key is { } key)
                    {
                        return key;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if the operation has a NotFound (404) response.
        /// </summary>
        /// <returns>True if the operation has a 404 response defined.</returns>
        public bool HasNotFoundResponse()
            => operation.Responses?.ContainsKey("404") ?? false;

        /// <summary>
        /// Gets the first tag from an operation.
        /// </summary>
        /// <returns>The first tag name in PascalCase, or empty string if no tags.</returns>
        public string GetFirstTag()
            => operation
                .Tags?
                .FirstOrDefault()?
                .Name?
                .ToPascalCaseForDotNet() ?? string.Empty;

        /// <summary>
        /// Checks if the operation response is a file download (binary content).
        /// </summary>
        /// <returns>True if the operation response is a file download.</returns>
        public bool HasFileDownload()
        {
            if (operation.Responses is null)
            {
                return false;
            }

            if (!operation.Responses.TryGetValue("200", out var response))
            {
                return false;
            }

            // The primary body decides (ResponseBodyClassifier): any media type that is neither JSON, XML nor text.
            return ResponseBodyClassifier.PrimaryBody(response.Content)?.Kind == ResponseBodyKind.Binary;
        }

        /// <summary>
        /// Checks if the operation's 200 response is a textual body (e.g. <c>text/plain</c>,
        /// <c>text/csv</c>, <c>application/xml</c>) with a <c>type: string</c> schema.
        /// </summary>
        /// <returns>True when the success response should be surfaced as a raw <see cref="string"/>.</returns>
        public bool HasTextResponse()
            => operation.Responses is not null &&
               operation.Responses.TryGetValue("200", out var response) &&
               response.TryGetTextResponseMediaType(out _, out _);

        /// <summary>
        /// The file media types of a 200 response that is a model <b>or</b> a file (<c>application/json</c> and
        /// <c>application/pdf</c>), told apart by the answer's <c>Content-Type</c> (<see cref="ResponseBodyClassifier.FileAlternatives"/>).
        /// </summary>
        /// <returns>The file media types; empty when the 200 response is not a model or a file.</returns>
        public IReadOnlyList<string> GetModelOrFileMediaTypes()
            => operation.Responses is not null &&
               operation.Responses.TryGetValue("200", out var response)
                ? ResponseBodyClassifier.FileAlternatives(response.Content)
                : [];

        /// <summary>
        /// Gets the file download content type from the response if present.
        /// </summary>
        /// <returns>The file download content type or null if not found.</returns>
        public string? GetFileDownloadContentType()
        {
            if (operation.Responses is null)
            {
                return null;
            }

            if (!operation.Responses.TryGetValue("200", out var response))
            {
                return null;
            }

            return ResponseBodyClassifier.PrimaryBody(response.Content) is { Kind: ResponseBodyKind.Binary } body
                ? body.MediaType
                : null;
        }

        /// <summary>
        /// The media type of the operation's form body: <c>multipart/form-data</c> (first, it can carry files), else
        /// <c>application/x-www-form-urlencoded</c>, as declared (with its parameters).
        /// </summary>
        /// <returns>The media type, or <see langword="null"/> when the body is not a form.</returns>
        public string? GetFormBodyMediaType()
        {
            var mediaTypes = operation.GetFormBodyMediaTypes();
            return mediaTypes.Count > 0 ? mediaTypes[0] : null;
        }

        /// <summary>
        /// The form media types the operation declares, <c>multipart/form-data</c> before
        /// <c>application/x-www-form-urlencoded</c>: a server bound by form binding reads both.
        /// </summary>
        /// <returns>The media types as declared; empty when the body is not a form.</returns>
        public IReadOnlyList<string> GetFormBodyMediaTypes()
        {
            if (operation.RequestBody?.Content is not { } content)
            {
                return [];
            }

            var keys = content.Keys.Where(IsFormContentType).ToList();
            return keys
                .Where(k => k.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
                .Concat(keys.Where(k => !k.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        /// <summary>
        /// Checks if the operation has a form request body (<c>multipart/form-data</c> or
        /// <c>application/x-www-form-urlencoded</c>) with a model schema, which the Minimal API handler binds field by
        /// field: ASP.NET Core Minimal APIs with [AsParameters] can't bind wrapped complex types from form data.
        /// A body that is also JSON is bound as JSON (what the clients send; ATC_API_OPR031 reports the form).
        /// </summary>
        /// <returns>True if the request body requires form binding flattening.</returns>
        public bool RequiresFormBindingFlattening()
        {
            if (operation.GetJsonRequestBodyMediaType() is not null ||
                operation.GetFormBodyMediaType() is not { } formMediaType ||
                operation.RequestBody!.Content![formMediaType].Schema is not { } schema)
            {
                return false;
            }

            // Direct file upload schemas (binary type) don't need flattening
            var (isFile, _) = schema.GetFileUploadInfo();
            if (isFile)
            {
                return false;
            }

            // Any model (object wrapper) with properties needs flattening
            return schema.Properties is { Count: > 0 };
        }

        /// <summary>
        /// Gets the form body's schema name, its properties and its required property names, for binding the form
        /// field by field (<see cref="RequiresFormBindingFlattening"/>).
        /// </summary>
        /// <returns>The schema reference name, its properties and its required names; nulls when the body is not a form.</returns>
        public (string? SchemaName, IDictionary<string, IOpenApiSchema>? Properties, ISet<string> Required) GetFormBodySchemaInfo()
        {
            var required = new HashSet<string>(StringComparer.Ordinal);
            if (operation.GetFormBodyMediaType() is not { } formMediaType ||
                operation.RequestBody!.Content![formMediaType].Schema is not { } schema)
            {
                return (null, null, required);
            }

            if (schema.Required is { } requiredNames)
            {
                required.UnionWith(requiredNames);
            }

            // Get schema name from reference
            string? schemaName = null;
            if (schema is OpenApiSchemaReference schemaRef)
            {
                schemaName = schemaRef.Reference?.Id ?? schemaRef.Id;
            }

            return (schemaName, schema.Properties, required);
        }

        private IEnumerable<OpenApiParameter> GetParametersByLocationIterator(
            ParameterLocation location)
        {
            if (operation.Parameters is null)
            {
                yield break;
            }

            foreach (var paramInterface in operation.Parameters)
            {
                // Resolve $ref parameters (components/parameters) as well as inline ones.
                var param = paramInterface switch
                {
                    OpenApiParameter direct => direct,
                    OpenApiParameterReference reference => reference.Target as OpenApiParameter,
                    _ => null,
                };

                if (param is not null && param.In == location)
                {
                    yield return param;
                }
            }
        }
    }

    /// <summary>
    /// Determines if a content type represents a file upload.
    /// </summary>
    /// <param name="contentType">The content type to check.</param>
    /// <returns>True if the content type represents a file upload.</returns>
    public static bool IsFileUploadContentType(string contentType)
        => contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) ||
           IsRawFileUploadContentType(contentType);

    /// <summary>
    /// Determines if a content type is a form: <c>multipart/form-data</c> or <c>application/x-www-form-urlencoded</c>
    /// (with or without parameters) - the two media types ASP.NET Core's form binding reads and a browser form posts.
    /// </summary>
    /// <param name="contentType">The content type to check.</param>
    /// <returns>True if the content type is a form.</returns>
    public static bool IsFormContentType(string? contentType)
    {
        if (contentType is null)
        {
            return false;
        }

        var type = contentType.Split(';')[0].Trim();
        return type.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if a request body media type is a raw file upload: the body is the file itself, of any binary media
    /// type (<c>application/octet-stream</c>, <c>application/pdf</c>, <c>image/png</c>, an Office document, ...) -
    /// the rule every generator shares (<see cref="ResponseBodyClassifier"/>). Multipart and URL-encoded forms are not.
    /// </summary>
    /// <param name="contentType">The content type to check.</param>
    /// <returns>True if the body is the raw file.</returns>
    public static bool IsRawFileUploadContentType(string? contentType)
    {
        if (contentType is null)
        {
            return false;
        }

        var type = contentType.Split(';')[0].Trim();
        return !type.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase) &&
               !type.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) &&
               ResponseBodyClassifier.Classify(type, schema: null) == ResponseBodyKind.Binary;
    }

    /// <summary>
    /// Determines if a content type represents a file download (binary response).
    /// </summary>
    /// <param name="contentType">The content type to check.</param>
    /// <returns>True if the content type represents a file download.</returns>
    public static bool IsFileDownloadContentType(string contentType)
        => ResponseBodyClassifier.Classify(contentType, schema: null) == ResponseBodyKind.Binary;

    /// <summary>
    /// Determines if a content type represents a textual response body that the generator
    /// can surface as a raw <see cref="string"/> (e.g. <c>text/plain</c>, <c>text/csv</c>,
    /// <c>text/html</c>, <c>application/xml</c>).
    /// </summary>
    /// <param name="contentType">The content type to check.</param>
    /// <returns>True if the content type represents a textual response.</returns>
    public static bool IsTextResponseMediaType(string contentType)
        => !string.IsNullOrEmpty(contentType) &&
           (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("application/xml", StringComparison.OrdinalIgnoreCase));

    /// <param name="response">The OpenAPI response.</param>
    extension(IOpenApiResponse response)
    {
        /// <summary>
        /// Tries to find the first textual response media type (e.g. <c>text/plain</c>) declared
        /// on the response with a <c>type: string</c> schema (excluding <c>format: binary</c>,
        /// which is a file download). When multiple textual media types are present, <c>text/plain</c>
        /// is preferred so consumers using <c>HttpClient.GetStringAsync</c> get the canonical body.
        /// </summary>
        /// <param name="mediaType">The matched media type (e.g. <c>"text/plain"</c>) when true.</param>
        /// <param name="media">The matched <see cref="IOpenApiMediaType"/> when true.</param>
        /// <returns>True when a textual string-typed media type is found.</returns>
        public bool TryGetTextResponseMediaType(
            out string? mediaType,
            out IOpenApiMediaType? media)
        {
            mediaType = null;
            media = null;

            if (response?.Content is null || response.Content.Count == 0)
            {
                return false;
            }

            // Prefer text/plain over other text/* and application/xml when several are declared.
            var orderedKeys = response.Content.Keys
                .Where(IsTextResponseMediaType)
                .OrderBy(k => k.Equals("text/plain", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(k => k, StringComparer.OrdinalIgnoreCase);

            foreach (var key in orderedKeys)
            {
                var candidate = response.Content[key];
                var schema = candidate?.Schema;
                if (schema is null)
                {
                    continue;
                }

                // Must be type: string (skip type: object / array / etc.) and not the
                // file-download convention type: string + format: binary.
                if (schema.Type?.HasFlag(JsonSchemaType.String) == true &&
                    !string.Equals(schema.Format, "binary", StringComparison.OrdinalIgnoreCase))
                {
                    mediaType = key;
                    media = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}