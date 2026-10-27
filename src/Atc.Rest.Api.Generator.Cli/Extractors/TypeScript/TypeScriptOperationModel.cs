namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// One operation of the TypeScript client as its request method is written: its parameters by location, its body, its answer and the options of its request.
/// </summary>
/// <param name="MethodName">The request method's name.</param>
/// <param name="Path">The operation's path template.</param>
/// <param name="HttpMethod">The HTTP method.</param>
/// <param name="PathParams">The path parameters.</param>
/// <param name="QueryParams">The query parameters.</param>
/// <param name="HeaderParams">The header parameters.</param>
/// <param name="CookieParams">The cookie parameters.</param>
/// <param name="QuerystringParams">The OAS 3.2 query-string parameter, if any.</param>
/// <param name="BodySchema">The request body's schema, or <see langword="null"/> without a body.</param>
/// <param name="BodyContentType">The request body's media type.</param>
/// <param name="IsFileUpload">Whether the body is a file upload.</param>
/// <param name="IsFileDownload">Whether the answer is a file.</param>
/// <param name="IsTextDownload">Whether the answer is text.</param>
/// <param name="ReturnType">The TypeScript type of the answer.</param>
/// <param name="PerOpResultTypeName">The operation's own result union, or <see langword="null"/> for the shared one.</param>
/// <param name="ZodSpec">The Zod schema the answer is parsed with at run time, or <see langword="null"/>.</param>
/// <param name="RetryOption">The request's retry option (x-retry-*), or <see langword="null"/> for the client's default.</param>
/// <param name="ArmsOption">The error statuses the result union has arms for, or <see langword="null"/>.</param>
/// <param name="WireOption">How the body's, the answer's and the errors' keys are renamed between the wire and the models, or <see langword="null"/>.</param>
/// <param name="XmlOption">How an XML body is written and read, or <see langword="null"/>.</param>
/// <param name="ModelOrFileAccept">The Accept of a model-or-file answer, or <see langword="null"/>.</param>
/// <param name="RawUploadMediaTypes">The media types a raw file upload declares, or <see langword="null"/>.</param>
/// <param name="SecurityOption">The request's security option, or <see langword="null"/>.</param>
/// <param name="BodyMediaType">The declared request body (a multipart form's encoding), or <see langword="null"/>.</param>
/// <param name="HeaderReadersOption">The response headers the result reads, or <see langword="null"/>.</param>
/// <param name="BodyOptional">Whether a streamed body may be left out.</param>
internal sealed record TypeScriptOperationModel(
    string MethodName,
    string Path,
    string HttpMethod,
    List<OpenApiParameter> PathParams,
    List<OpenApiParameter> QueryParams,
    List<OpenApiParameter> HeaderParams,
    List<OpenApiParameter> CookieParams,
    List<OpenApiParameter> QuerystringParams,
    IOpenApiSchema? BodySchema,
    string BodyContentType,
    bool IsFileUpload,
    bool IsFileDownload,
    bool IsTextDownload,
    string ReturnType,
    string? PerOpResultTypeName,
    ZodResponseSchemaSpec? ZodSpec,
    string? RetryOption,
    string? ArmsOption,
    string? WireOption,
    string? XmlOption,
    string? ModelOrFileAccept,
    IReadOnlyList<string>? RawUploadMediaTypes,
    string? SecurityOption,
    IOpenApiMediaType? BodyMediaType,
    IReadOnlyList<string>? HeaderReadersOption,
    bool BodyOptional);