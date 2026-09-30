namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Resolves how an error response is written, from what the specification declares. Shared by the server
/// result factories, the server <c>.Produces</c> metadata and the generated clients, so all three agree.
/// </summary>
/// <remarks>
/// "ProblemDetails" is decided by the schema <b>name</b> (<see cref="BuiltInErrorContractNames"/>), never by
/// structure: a schema named <c>ProblemDetails</c> binds to the framework type, every other schema is a
/// plain model even when it looks like a problem.
/// </remarks>
public static class ErrorResponseShapeResolver
{
    /// <summary>
    /// Whether a response key is an error response: <c>default</c>, or a 4xx/5xx (or higher) status code.
    /// </summary>
    /// <param name="statusCode">The response key from the specification.</param>
    /// <returns><see langword="true"/> for error responses.</returns>
    public static bool IsErrorStatusCode(string statusCode)
        => string.Equals(statusCode, "default", StringComparison.OrdinalIgnoreCase) ||
           (int.TryParse(statusCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) && code >= 400);

    /// <summary>
    /// Whether a media type is JSON: <c>application/json</c> or any <c>+json</c> suffix
    /// (<c>application/problem+json</c>, <c>application/vnd.x+json</c>). Parameters are ignored.
    /// </summary>
    /// <param name="mediaType">The media type.</param>
    /// <returns><see langword="true"/> for JSON media types.</returns>
    public static bool IsJsonMediaType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return false;
        }

        var type = mediaType!.Split(';')[0].Trim();
        return type.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
               type.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The endpoint-metadata call (<c>.ProducesProblem(...)</c>, <c>.Produces&lt;T&gt;(...)</c>, ...) that
    /// describes an error response exactly as the generated result factories write it. A <c>default</c>
    /// response is described under 500.
    /// </summary>
    /// <param name="statusCode">The response key (<c>"404"</c>, <c>"default"</c>).</param>
    /// <param name="shape">The resolved shape.</param>
    /// <param name="modelTypeName">The C# type of a <see cref="ErrorResponseKind.JsonObject"/> body.</param>
    /// <returns>The call, e.g. <c>.ProducesProblem(StatusCodes.Status404NotFound)</c>.</returns>
    public static string ToProducesCall(
        string statusCode,
        ErrorResponseShape shape,
        string? modelTypeName)
    {
        if (shape is null)
        {
            throw new ArgumentNullException(nameof(shape));
        }

        var code = int.TryParse(statusCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 500;
        var status = HttpStatusCodeHelper.ToStatusCodesExpression(code);

        return shape.Kind switch
        {
            ErrorResponseKind.ValidationProblem when code == 400 => ".ProducesValidationProblem()",
            ErrorResponseKind.ValidationProblem => $".ProducesValidationProblem({status})",
            ErrorResponseKind.Problem when code == 400 && !shape.IsDeclared => ".ProducesValidationProblem()",
            ErrorResponseKind.Problem => $".ProducesProblem({status})",
            ErrorResponseKind.JsonString or ErrorResponseKind.Text => $".Produces<string>({status}, contentType: \"{shape.MediaTypeLiteral}\")",
            _ => $".Produces<{modelTypeName ?? "object"}>({status}, contentType: \"{shape.MediaTypeLiteral}\")",
        };
    }

    /// <summary>
    /// The naming context of an inline (anonymous) error schema, so its generated model is
    /// <c>{Operation}{Status}Error</c> (e.g. <c>GetItemNotFoundError</c>, <c>GetItemDefaultError</c>) - distinct
    /// from the inline success model (<c>{Operation}Response</c>) and from other statuses. Used by the server
    /// factories, the server metadata and the clients alike.
    /// </summary>
    /// <param name="statusCode">The response key (<c>"404"</c>, <c>"default"</c>).</param>
    /// <returns>The context passed to the inline type naming.</returns>
    public static string InlineTypeContext(string statusCode)
        => int.TryParse(statusCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
            ? $"{HttpStatusCodeHelper.ToEnumName(code)}Error"
            : "DefaultError";

    /// <summary>
    /// Resolves the wire shape of an error response.
    /// </summary>
    /// <param name="response">The response (inline or a <c>$ref</c> to <c>components/responses</c>).</param>
    /// <returns>The resolved shape.</returns>
    public static ErrorResponseShape Resolve(IOpenApiResponse? response)
    {
        if (response?.Content is { Count: > 0 } content)
        {
            // application/json first (the historical key), then any other +json media type.
            var json = content
                .Where(kv => IsJsonMediaType(kv.Key) && kv.Value?.Schema is not null)
                .OrderBy(kv => kv.Key.Equals("application/json", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => (MediaType: kv.Key, Schema: kv.Value!.Schema!))
                .FirstOrDefault();

            if (json.MediaType is not null)
            {
                // A $ref to a primitive alias (ErrorMessage: { type: string }) is no generated type: use the
                // primitive itself. An enum is a generated type and keeps its reference.
                if (json.Schema is OpenApiSchemaReference { Target: OpenApiSchema target } &&
                    target.Type is { } targetType &&
                    !targetType.HasFlag(JsonSchemaType.Object) &&
                    !targetType.HasFlag(JsonSchemaType.Array) &&
                    target.Enum is not { Count: > 0 })
                {
                    json.Schema = target;
                }

                var schemaName = json.Schema.GetReferenceId();
                if (string.Equals(schemaName, "ProblemDetails", StringComparison.Ordinal))
                {
                    return new ErrorResponseShape(ErrorResponseKind.Problem, json.MediaType, json.Schema);
                }

                if (string.Equals(schemaName, "ValidationProblemDetails", StringComparison.Ordinal))
                {
                    return new ErrorResponseShape(ErrorResponseKind.ValidationProblem, json.MediaType, json.Schema);
                }

                if (schemaName is null &&
                    json.Schema.Type?.HasFlag(JsonSchemaType.String) == true &&
                    !string.Equals(json.Schema.Format, "binary", StringComparison.OrdinalIgnoreCase))
                {
                    return new ErrorResponseShape(ErrorResponseKind.JsonString, json.MediaType, json.Schema);
                }

                return new ErrorResponseShape(ErrorResponseKind.JsonObject, json.MediaType, json.Schema);
            }

            if (response.TryGetTextResponseMediaType(out var textMediaType, out var textMedia))
            {
                return new ErrorResponseShape(ErrorResponseKind.Text, textMediaType, textMedia?.Schema);
            }

            // A declared model as XML (a third-party API's XML error): written and read by the model's XML mapping
            if (XmlBodyHelper.FindXmlOnlyBody(content) is { IsList: false } xml)
            {
                return new ErrorResponseShape(ErrorResponseKind.XmlObject, xml.MediaType, content[xml.MediaType].Schema);
            }
        }

        // Nothing (usable) declared: the 3.0 default is an RFC 9457 ProblemDetails.
        return new ErrorResponseShape(ErrorResponseKind.Problem, MediaType: null, Schema: null);
    }
}