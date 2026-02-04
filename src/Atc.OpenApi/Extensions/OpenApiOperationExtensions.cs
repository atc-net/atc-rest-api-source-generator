// ReSharper disable InvertIf
namespace Atc.OpenApi.Extensions;

/// <summary>
/// Extension methods for OpenApiOperation to handle operation queries and analysis.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "CA2208:Method ReplaceAt passes 'name' as the paramName argument to a ArgumentNullException constructor", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "S1144:Remove the unused private method", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "S3928:The parameter name 'schema'", Justification = "OK - CLang14 - extension")]
public static class OpenApiOperationExtensions
{
    /// <param name="operation">The OpenAPI operation.</param>
    extension(OpenApiOperation operation)
    {
        /// <summary>
        /// Gets the operation ID or generates a default one from path and HTTP method.
        /// </summary>
        /// <param name="path">The API path.</param>
        /// <param name="httpMethod">The HTTP method (GET, POST, etc.).</param>
        /// <returns>The operation ID.</returns>
        public string GetOperationId(
            string path,
            string httpMethod)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!string.IsNullOrEmpty(operation.OperationId))
            {
                return operation.OperationId!;
            }

            // Generate default: HttpMethodPathWithoutSlashesAndBraces
            var sanitizedPath = path
                .Replace("/", "_")
                .Replace("{", string.Empty)
                .Replace("}", string.Empty);
            return $"{httpMethod}{sanitizedPath}";
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
            if (operation is { RequestBody.Content: not null } &&
                operation.RequestBody.Content.TryGetValue(contentType, out var mediaType))
            {
                return mediaType.Schema;
            }

            return null;
        }

        /// <summary>
        /// Gets the response schema for a specific status code and content type.
        /// </summary>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <param name="contentType">The content type (default: application/json).</param>
        /// <returns>The schema or null if not found.</returns>
        public IOpenApiSchema? GetResponseSchema(
            string statusCode,
            string contentType = "application/json")
        {
            if (operation is { Responses: not null } &&
                operation.Responses.TryGetValue(statusCode, out var response) &&
                response.Content != null &&
                response.Content.TryGetValue(contentType, out var mediaType))
            {
                return mediaType.Schema;
            }

            return null;
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
        /// Gets all header parameters from the operation.
        /// </summary>
        /// <returns>Enumerable of header parameters.</returns>
        public IEnumerable<OpenApiParameter> GetHeaderParameters()
            => operation.GetParametersByLocation(ParameterLocation.Header);

        /// <summary>
        /// Gets all cookie parameters from the operation.
        /// </summary>
        /// <returns>Enumerable of cookie parameters.</returns>
        public IEnumerable<OpenApiParameter> GetCookieParameters()
            => operation.GetParametersByLocation(ParameterLocation.Cookie);

        /// <summary>
        /// Checks if the operation has a file upload request body.
        /// </summary>
        /// <returns>True if the operation has a file upload request body.</returns>
        public bool HasFileUpload()
        {
            if (operation.RequestBody?.Content == null)
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
            if (operation.RequestBody?.Content == null)
            {
                return (null, string.Empty);
            }

            // Priority 1: JSON
            if (operation.RequestBody.Content.TryGetValue("application/json", out var jsonMedia))
            {
                return (jsonMedia.Schema, "application/json");
            }

            // Priority 2: File uploads
            foreach (var kvp in operation.RequestBody.Content)
            {
                if (IsFileUploadContentType(kvp.Key))
                {
                    return (kvp.Value.Schema, kvp.Key);
                }
            }

            return (null, string.Empty);
        }

        /// <summary>
        /// Checks if the operation has x-return-async-enumerable: true extension.
        /// When true, the result class Ok method should accept IAsyncEnumerable&lt;T&gt; for streaming.
        /// </summary>
        /// <returns>True if the operation should return IAsyncEnumerable.</returns>
        public bool IsAsyncEnumerableOperation()
        {
            if (operation.Extensions == null ||
                !operation.Extensions.TryGetValue("x-return-async-enumerable", out var extension) ||
                extension == null)
            {
                return false;
            }

            // Use reflection to access Node property (Microsoft.OpenApi v3.0.1 pattern)
            var extensionType = extension.GetType();
            var nodeProperty = extensionType.GetProperty("Node");
            if (nodeProperty == null)
            {
                return false;
            }

            var node = nodeProperty.GetValue(extension);
            if (node is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var boolValue))
            {
                return boolValue;
            }

            return false;
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
            if (operation.Responses == null)
            {
                return false;
            }

            if (!operation.Responses.TryGetValue("200", out var response))
            {
                return false;
            }

            if (response.Content == null)
            {
                return false;
            }

            foreach (var key in response.Content.Keys)
            {
                if (IsFileDownloadContentType(key))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the file download content type from the response if present.
        /// </summary>
        /// <returns>The file download content type or null if not found.</returns>
        public string? GetFileDownloadContentType()
        {
            if (operation.Responses == null)
            {
                return null;
            }

            if (!operation.Responses.TryGetValue("200", out var response))
            {
                return null;
            }

            return response.Content?.Keys.FirstOrDefault(IsFileDownloadContentType);
        }

        /// <summary>
        /// Checks if the operation has a multipart/form-data request body with a complex schema
        /// that requires flattening for proper parameter binding in Minimal APIs.
        /// This is needed for any schema reference (complex type wrapper) because ASP.NET Core
        /// Minimal APIs with [AsParameters] can't bind wrapped complex types from form data.
        /// </summary>
        /// <returns>True if the request body requires form binding flattening.</returns>
        public bool RequiresFormBindingFlattening()
        {
            if (operation.RequestBody?.Content == null)
            {
                return false;
            }

            // Only applies to multipart/form-data
            if (!operation.RequestBody.Content.TryGetValue("multipart/form-data", out var mediaType))
            {
                return false;
            }

            var schema = mediaType.Schema;
            if (schema == null)
            {
                return false;
            }

            // Check if it's a schema reference (complex type) with properties
            // Direct file upload schemas (binary type) don't need flattening
            var (isFile, _) = schema.GetFileUploadInfo();
            if (isFile)
            {
                return false;
            }

            // Check if schema has properties - if it's a reference, resolve it
            var properties = schema.Properties;
            if (properties == null || properties.Count == 0)
            {
                return false;
            }

            // Any complex schema (object wrapper) with properties needs flattening
            // because ASP.NET Core Minimal APIs can't bind wrapped types from form data
            return true;
        }

        /// <summary>
        /// Gets the multipart/form-data schema and its properties for flattening.
        /// </summary>
        /// <returns>The schema reference name and its properties, or null if not applicable.</returns>
        public (string? SchemaName, IDictionary<string, IOpenApiSchema>? Properties) GetMultipartFormDataSchemaInfo()
        {
            if (operation.RequestBody?.Content == null)
            {
                return (null, null);
            }

            if (!operation.RequestBody.Content.TryGetValue("multipart/form-data", out var mediaType))
            {
                return (null, null);
            }

            var schema = mediaType.Schema;
            if (schema == null)
            {
                return (null, null);
            }

            // Get schema name from reference
            string? schemaName = null;
            if (schema is OpenApiSchemaReference schemaRef)
            {
                schemaName = schemaRef.Reference?.Id ?? schemaRef.Id;
            }

            return (schemaName, schema.Properties);
        }

        private IEnumerable<OpenApiParameter> GetParametersByLocationIterator(
            ParameterLocation location)
        {
            if (operation.Parameters == null)
            {
                yield break;
            }

            foreach (var paramInterface in operation.Parameters)
            {
                if (paramInterface is OpenApiParameter param && param.In == location)
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
        => contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
           contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) ||
           contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines if a content type represents a file download (binary response).
    /// </summary>
    /// <param name="contentType">The content type to check.</param>
    /// <returns>True if the content type represents a file download.</returns>
    public static bool IsFileDownloadContentType(string contentType)
        => contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
           contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
           contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
           contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
           contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
           contentType.Equals("application/zip", StringComparison.OrdinalIgnoreCase);
}