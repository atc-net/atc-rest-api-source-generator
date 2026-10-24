// ReSharper disable InvertIf
namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// The typed client's response side: the completion of a send by payload kind, and the response helpers of the Result style.
/// </summary>
public static partial class HttpClientExtractor
{
    /// <summary>
    /// Checks if any of the response content types are binary (application/octet-stream, image/*, etc.).
    /// </summary>
    private static bool IsBinaryResponseContent(
        IDictionary<string, IOpenApiMediaType> content)
        => ResponseBodyClassifier.PrimaryBody(content)?.Kind == ResponseBodyKind.Binary;

    /// <summary>
    /// How a response body is read: as text (a text response), by its model's XML mapping, or as JSON; none without a
    /// return type.
    /// </summary>
    private static ResponsePayloadKind PayloadKind(
        bool hasReturnType,
        XmlReadPlan? xmlRead,
        bool isTextResponse)
    {
        if (!hasReturnType)
        {
            return ResponsePayloadKind.None;
        }

        if (isTextResponse)
        {
            return ResponsePayloadKind.Text;
        }

        return xmlRead?.ReadMethod is null || xmlRead.ByContentType ? ResponsePayloadKind.Json : ResponsePayloadKind.Xml;
    }

    /// <summary>
    /// Checks if any of the response content types are text-based (text/plain, text/html, etc.).
    /// </summary>
    private static bool IsTextResponseContent(
        IDictionary<string, IOpenApiMediaType> content)
    {
        foreach (var key in content.Keys)
        {
            if (key.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Emits the statement(s) that terminate a method body once <c>response</c> is in scope.
    /// </summary>
    /// <remarks>
    /// This is the single place where the Throw-vs-Result decision is made:
    /// <list type="bullet">
    /// <item>Throw - calls <c>EnsureSuccessAsync</c> (which throws on a non-success status) and then reads the content directly.</item>
    /// <item>Result - never throws on status; delegates to a generated Build*ResponseAsync helper that wraps status, raw content and deserialized content in an EndpointResponse envelope.</item>
    /// </list>
    /// Keeping both styles here guarantees the Throw output stays byte-identical to the pre-Result behaviour.
    /// </remarks>
    private static void AppendResponseCompletion(
        StringBuilder builder,
        TypedClientResultStyleType resultStyle,
        ResponsePayloadKind payloadKind,
        string returnType,
        bool isLastStatement,
        XmlReadPlan? xmlRead = null,
        bool emptyBodyIsNull = false)
    {
        if (xmlRead?.ModelOrFileType is { } modelType)
        {
            AppendModelOrFileCompletion(builder, resultStyle, payloadKind, returnType, modelType, isLastStatement, xmlRead, emptyBodyIsNull);
            return;
        }

        if (payloadKind == ResponsePayloadKind.Json && xmlRead is { ByContentType: true, ReadMethod: not null })
        {
            // Offered as JSON and XML: the model read by the response's Content-Type
            builder.AppendLine("if (response.IsSuccessStatusCode && XmlModelSupport.IsXmlMediaType(response.Content.Headers.ContentType?.MediaType))");
            builder.AppendLine("{");
            if (resultStyle == TypedClientResultStyleType.Result)
            {
                builder.AppendLine(4, "var xmlBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);");
                builder.AppendLine(4, $"return new EndpointResponse<{returnType}>(true, response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), xmlBytes.Length > 0 ? {xmlRead.ReadMethod!}(XmlModelSupport.ParseContent(xmlBytes)) : null, ReadHeaders(response));");
            }
            else
            {
                builder.AppendLine(4, $"return {xmlRead.ReadMethod!}(XmlModelSupport.ParseContent(await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false)));");
            }

            builder.AppendLine("}");
            builder.AppendLine();
            AppendResponseCompletion(builder, resultStyle, ResponsePayloadKind.Json, returnType, isLastStatement, xmlRead: null, emptyBodyIsNull: emptyBodyIsNull);
            return;
        }

        if (payloadKind == ResponsePayloadKind.Xml && xmlRead?.ReadMethod is not null)
        {
            // The model read from the XML body by its generated XML mapping - from the bytes, so the document's own
            // encoding declaration is honoured (a string read would decode by the charset parameter or UTF-8 only)
            if (resultStyle == TypedClientResultStyleType.Result)
            {
                builder.AppendLine("var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);");
                builder.AppendLine("var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
                AppendStatement(builder, $"return new EndpointResponse<{returnType}>(response.IsSuccessStatusCode, response.StatusCode, content, response.IsSuccessStatusCode && bytes.Length > 0 ? {xmlRead.ReadMethod!}(XmlModelSupport.ParseContent(bytes)) : null, ReadHeaders(response));", isLastStatement);
                return;
            }

            builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);");
            AppendStatement(builder, $"return {xmlRead.ReadMethod!}(XmlModelSupport.ParseContent(await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false)));", isLastStatement);
            return;
        }

        if (resultStyle == TypedClientResultStyleType.Result)
        {
            var call = payloadKind switch
            {
                ResponsePayloadKind.Json => $"return await BuildJsonResponseAsync<{returnType}>(response, jsonSerializerOptions, cancellationToken).ConfigureAwait(false);",
                ResponsePayloadKind.Binary => "return await BuildBinaryResponseAsync(response, cancellationToken).ConfigureAwait(false);",
                ResponsePayloadKind.Text => "return await BuildTextResponseAsync(response, cancellationToken).ConfigureAwait(false);",
                _ => "return await BuildEmptyResponseAsync(response, cancellationToken).ConfigureAwait(false);",
            };

            AppendStatement(builder, call, isLastStatement);
            return;
        }

        if (payloadKind == ResponsePayloadKind.None)
        {
            AppendStatement(builder, "await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);", isLastStatement);
            return;
        }

        builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);");

        if (payloadKind == ResponsePayloadKind.Json)
        {
            // A success without a body is null where the operation declares one (a 204 beside the model's 200); where it
            // declares none, an empty answer is not the model it declares: a JsonException that says so, not a null
            // through the non-nullable return type
            builder.AppendLine("var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
            if (emptyBodyIsNull)
            {
                AppendStatement(builder, $"return json.Length == 0 ? default! : JsonSerializer.Deserialize(json, jsonSerializerOptions.TypeInfo<{returnType}>())!;", isLastStatement);
                return;
            }

            builder.AppendLine("if (json.Length == 0)");
            builder.AppendLine("{");
            builder.AppendLine(4, $"throw new JsonException($\"The {{(int)response.StatusCode}} answer has no body, but the operation declares a {returnType} for it.\");");
            builder.AppendLine("}");
            builder.AppendLine();
            AppendStatement(builder, $"return JsonSerializer.Deserialize(json, jsonSerializerOptions.TypeInfo<{returnType}>())!;", isLastStatement);
            return;
        }

        var read = payloadKind switch
        {
            ResponsePayloadKind.Binary => "return await DownloadedFile.FromHttpResponseAsync(response, cancellationToken).ConfigureAwait(false);",
            _ => "return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);",
        };

        AppendStatement(builder, read, isLastStatement);
    }

    /// <summary>
    /// Emits the completion of a model or a file: the file when the answer's Content-Type is neither JSON nor XML,
    /// else the model, read as it would be alone (by a local function) and wrapped in the <c>ContentOrFile&lt;T&gt;</c>.
    /// </summary>
    private static void AppendModelOrFileCompletion(
        StringBuilder builder,
        TypedClientResultStyleType resultStyle,
        ResponsePayloadKind payloadKind,
        string returnType,
        string modelType,
        bool isLastStatement,
        XmlReadPlan xmlRead,
        bool emptyBodyIsNull)
    {
        var model = new StringBuilder();
        AppendResponseCompletion(model, resultStyle, payloadKind, modelType, isLastStatement: true, xmlRead: xmlRead with { ModelOrFileType = null }, emptyBodyIsNull: emptyBodyIsNull);

        if (resultStyle == TypedClientResultStyleType.Result)
        {
            builder.AppendLine("if (response.IsSuccessStatusCode && DownloadedFile.IsFileMediaType(response.Content.Headers.ContentType?.MediaType))");
            builder.AppendLine("{");
            builder.AppendLine(4, "var file = await DownloadedFile.FromHttpResponseAsync(response, cancellationToken).ConfigureAwait(false);");
            builder.AppendLine(4, $"return new EndpointResponse<{returnType}>(true, response.StatusCode, string.Empty, {returnType}.FromFile(file), ReadHeaders(response));");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("var modelResponse = await ReadContentAsync().ConfigureAwait(false);");
            builder.AppendLine($"return new EndpointResponse<{returnType}>(modelResponse.IsSuccess, modelResponse.StatusCode, modelResponse.Content, modelResponse.IsSuccess && modelResponse.ContentObject is {modelType} content ? {returnType}.FromContent(content) : null, ReadHeaders(response));");
            builder.AppendLine();
            builder.AppendLine($"async Task<EndpointResponse<{modelType}>> ReadContentAsync()");
        }
        else
        {
            builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);");
            builder.AppendLine("if (DownloadedFile.IsFileMediaType(response.Content.Headers.ContentType?.MediaType))");
            builder.AppendLine("{");
            builder.AppendLine(4, $"return {returnType}.FromFile(await DownloadedFile.FromHttpResponseAsync(response, cancellationToken).ConfigureAwait(false));");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine($"return {returnType}.FromContent(await ReadContentAsync().ConfigureAwait(false));");
            builder.AppendLine();
            builder.AppendLine($"async Task<{modelType}> ReadContentAsync()");
        }

        // The Throw style has checked the status already
        var modelBody = model.ToString();
        if (resultStyle != TypedClientResultStyleType.Result)
        {
            modelBody = modelBody.Replace("await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);" + Environment.NewLine, string.Empty);
        }

        builder.AppendLine("{");
        foreach (var line in modelBody.Split('\n'))
        {
            var text = line.TrimEnd('\r');
            if (text.Length == 0)
            {
                builder.AppendLine();
            }
            else
            {
                builder.AppendLine(4, text);
            }
        }

        AppendStatement(builder, "}", isLastStatement);
    }

    private static void AppendStatement(
        StringBuilder builder,
        string statement,
        bool isLastStatement)
    {
        if (isLastStatement)
        {
            builder.Append(statement);
        }
        else
        {
            builder.AppendLine(statement);
        }
    }

    /// <summary>
    /// Creates the EnsureSuccessAsync helper method that reads error response body before throwing.
    /// This replaces the standard EnsureSuccessStatusCode() to preserve error details: it throws the generated
    /// exception (<see cref="ApiExceptionExtractor"/>), which carries the status, the body and the headers.
    /// </summary>
    private static MethodParameters CreateEnsureSuccessMethod(
        string exceptionTypeName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("if (response.IsSuccessStatusCode)");
        sb.AppendLine("{");
        sb.AppendLine(4, "return;");
        sb.AppendLine("}");
        sb.AppendLine();

        // At most 4 KB of the error body in the message: a proxy's 10 MB HTML page is not read whole into it
        sb.AppendLine("const int MaxErrorBytes = 4096;");
        sb.AppendLine("using var errorStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("var errorBuffer = new byte[MaxErrorBytes + 1];");
        sb.AppendLine("var errorLength = 0;");
        sb.AppendLine("int errorRead;");
        sb.AppendLine("while (errorLength < errorBuffer.Length && (errorRead = await errorStream.ReadAsync(errorBuffer.AsMemory(errorLength), cancellationToken).ConfigureAwait(false)) > 0)");
        sb.AppendLine("{");
        sb.AppendLine(4, "errorLength += errorRead;");
        sb.AppendLine("}");
        sb.AppendLine();

        // The text is in the charset the Content-Type names (a proxy, an older server); UTF-8 without one or for a
        // name the runtime does not know
        sb.AppendLine("var errorEncoding = System.Text.Encoding.UTF8;");
        sb.AppendLine("if (response.Content.Headers.ContentType?.CharSet is { Length: > 0 } errorCharset)");
        sb.AppendLine("{");
        sb.AppendLine(4, "try");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "errorEncoding = System.Text.Encoding.GetEncoding(errorCharset.Trim('\"'));");
        sb.AppendLine(4, "}");
        sb.AppendLine(4, "catch (System.ArgumentException)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "errorEncoding = System.Text.Encoding.UTF8;");
        sb.AppendLine(4, "}");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("var errorContent = errorLength > MaxErrorBytes");
        sb.AppendLine(4, "? errorEncoding.GetString(errorBuffer, 0, MaxErrorBytes) + \"…\"");
        sb.AppendLine(4, ": errorEncoding.GetString(errorBuffer, 0, errorLength);");
        sb.Append($"throw {exceptionTypeName}.From(response, errorContent);");

        return new MethodParameters(
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PrivateStaticAsync,
            ReturnGenericTypeName: null,
            ReturnTypeName: "System.Threading.Tasks.Task",
            Name: "EnsureSuccessAsync",
            Parameters:
            [
                new ParameterBaseParameters(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: "HttpResponseMessage",
                    IsNullableType: false,
                    IsReferenceType: true,
                    Name: "response",
                    DefaultValue: null),
                new ParameterBaseParameters(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: "CancellationToken",
                    IsNullableType: false,
                    IsReferenceType: false,
                    Name: "cancellationToken",
                    DefaultValue: null),
            ],
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: sb.ToString());
    }

    /// <summary>
    /// Creates the helper methods used exclusively by the Result style to wrap a response in an
    /// <c>EndpointResponse</c> envelope instead of throwing on a non-success status code.
    /// </summary>
    /// <remarks>
    /// Transport-level failures (DNS, TLS, connection reset, cancellation) still surface as exceptions
    /// from <c>HttpClient</c> itself - only status-code failures are converted into an envelope.
    /// </remarks>
    private static List<MethodParameters> CreateResultStyleHelperMethods(
        bool hasFileDownload)
    {
        var helpers = new List<MethodParameters>
        {
            CreateReadHeadersMethod(),
            CreateBuildEmptyResponseMethod(),
            CreateBuildJsonResponseMethod(),
        };

        if (hasFileDownload)
        {
            helpers.Add(CreateBuildBinaryResponseMethod());
        }

        helpers.Add(CreateBuildTextResponseMethod());
        helpers.Add(CreateBuildLocationResponseMethod());
        return helpers;
    }

    private static MethodParameters CreateReadHeadersMethod()
    {
        var sb = new StringBuilder();
        sb.AppendLine("var headers = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine();
        sb.AppendLine("foreach (var header in response.Headers)");
        sb.AppendLine("{");
        sb.AppendLine(4, "headers[header.Key] = header.Value;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("foreach (var header in response.Content.Headers)");
        sb.AppendLine("{");
        sb.AppendLine(4, "headers[header.Key] = header.Value;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.Append("return headers;");

        return new MethodParameters(
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PrivateStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: "IReadOnlyDictionary<string, IEnumerable<string>>",
            Name: "ReadHeaders",
            Parameters: [CreateResponseParameter()],
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: sb.ToString());
    }

    private static MethodParameters CreateBuildEmptyResponseMethod()
    {
        var sb = new StringBuilder();
        sb.AppendLine("var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine("return new EndpointResponse(");
        sb.AppendLine(4, "response.IsSuccessStatusCode,");
        sb.AppendLine(4, "response.StatusCode,");
        sb.AppendLine(4, "content,");
        sb.AppendLine(4, "contentObject: null,");
        sb.Append(4, "ReadHeaders(response));");

        return CreateBuildResponseMethod("BuildEmptyResponseAsync", "EndpointResponse", genericName: null, includeSerializerOptions: false, sb.ToString());
    }

    private static MethodParameters CreateBuildJsonResponseMethod()
    {
        var sb = new StringBuilder();
        sb.AppendLine("var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine("object? contentObject = null;");
        sb.AppendLine("var isSuccess = response.IsSuccessStatusCode;");
        sb.AppendLine("if (isSuccess && !string.IsNullOrEmpty(content))");
        sb.AppendLine("{");
        sb.AppendLine(4, "// A success whose body is no JSON (a proxy's HTML page) is no success - this style never throws for it");
        sb.AppendLine(4, "try");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "contentObject = JsonSerializer.Deserialize(content, jsonSerializerOptions.TypeInfo<T>());");
        sb.AppendLine(4, "}");
        sb.AppendLine(4, "catch (JsonException)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "isSuccess = false;");
        sb.AppendLine(4, "}");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("return new EndpointResponse<T>(");
        sb.AppendLine(4, "isSuccess,");
        sb.AppendLine(4, "response.StatusCode,");
        sb.AppendLine(4, "content,");
        sb.AppendLine(4, "contentObject,");
        sb.Append(4, "ReadHeaders(response));");

        return CreateBuildResponseMethod("BuildJsonResponseAsync<T>", "EndpointResponse<T>", genericName: "T", includeSerializerOptions: true, sb.ToString());
    }

    private static MethodParameters CreateBuildBinaryResponseMethod()
    {
        var sb = new StringBuilder();
        sb.AppendLine("// The file on success; on an error status the error body, as text, in Content.");
        sb.AppendLine("if (response.IsSuccessStatusCode)");
        sb.AppendLine("{");
        sb.AppendLine(4, "var file = await DownloadedFile.FromHttpResponseAsync(response, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine(4, "return new EndpointResponse<DownloadedFile>(true, response.StatusCode, string.Empty, file, ReadHeaders(response));");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
        sb.Append("return new EndpointResponse<DownloadedFile>(false, response.StatusCode, errorBody, null, ReadHeaders(response));");

        return CreateBuildResponseMethod("BuildBinaryResponseAsync", "EndpointResponse<DownloadedFile>", genericName: null, includeSerializerOptions: false, sb.ToString());
    }

    // The open file on success - the envelope's content owns the response; on an error status the error body, as text,
    // in Content, and the response disposed
    private static MethodParameters CreateBuildStreamedFileResponseMethod()
    {
        var sb = new StringBuilder();
        sb.AppendLine("if (response.IsSuccessStatusCode)");
        sb.AppendLine("{");
        sb.AppendLine(4, "var file = await StreamedFile.FromHttpResponseAsync(response, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine(4, "return new EndpointResponse<StreamedFile>(true, response.StatusCode, string.Empty, file, ReadHeaders(response));");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("using (response)");
        sb.AppendLine("{");
        sb.AppendLine(4, "var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine(4, "return new EndpointResponse<StreamedFile>(false, response.StatusCode, errorBody, null, ReadHeaders(response));");
        sb.Append('}');

        return CreateBuildResponseMethod("BuildStreamedFileResponseAsync", "EndpointResponse<StreamedFile>", genericName: null, includeSerializerOptions: false, sb.ToString());
    }

    private static MethodParameters CreateBuildTextResponseMethod()
    {
        var sb = new StringBuilder();
        sb.AppendLine("var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine("return new EndpointResponse<string>(");
        sb.AppendLine(4, "response.IsSuccessStatusCode,");
        sb.AppendLine(4, "response.StatusCode,");
        sb.AppendLine(4, "content,");
        sb.AppendLine(4, "response.IsSuccessStatusCode ? content : null,");
        sb.Append(4, "ReadHeaders(response));");

        return CreateBuildResponseMethod("BuildTextResponseAsync", "EndpointResponse<string>", genericName: null, includeSerializerOptions: false, sb.ToString());
    }

    private static MethodParameters CreateBuildLocationResponseMethod()
    {
        var sb = new StringBuilder();
        sb.AppendLine("var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine("return new EndpointResponse<Uri>(");
        sb.AppendLine(4, "response.IsSuccessStatusCode,");
        sb.AppendLine(4, "response.StatusCode,");
        sb.AppendLine(4, "content,");
        sb.AppendLine(4, "response.Headers.Location,");
        sb.Append(4, "ReadHeaders(response));");

        return CreateBuildResponseMethod("BuildLocationResponseAsync", "EndpointResponse<Uri>", genericName: null, includeSerializerOptions: false, sb.ToString());
    }

    private static MethodParameters CreateBuildResponseMethod(
        string name,
        string envelopeTypeName,
        string? genericName,
        bool includeSerializerOptions,
        string content)
    {
        _ = genericName;

        var parameters = new List<ParameterBaseParameters> { CreateResponseParameter() };

        if (includeSerializerOptions)
        {
            parameters.Add(
                new ParameterBaseParameters(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: "JsonSerializerOptions",
                    IsNullableType: false,
                    IsReferenceType: true,
                    Name: "jsonSerializerOptions",
                    DefaultValue: null));
        }

        parameters.Add(CreateCancellationTokenParameter());

        return new MethodParameters(
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PrivateStaticAsync,
            ReturnGenericTypeName: "System.Threading.Tasks.Task",
            ReturnTypeName: envelopeTypeName,
            Name: name,
            Parameters: parameters,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: content);
    }

    private static ParameterBaseParameters CreateResponseParameter()
        => new(
            Attributes: null,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: "HttpResponseMessage",
            IsNullableType: false,
            IsReferenceType: true,
            Name: "response",
            DefaultValue: null);

    private static ParameterBaseParameters CreateCancellationTokenParameter()
        => new(
            Attributes: null,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: "CancellationToken",
            IsNullableType: false,
            IsReferenceType: false,
            Name: "cancellationToken",
            DefaultValue: null);
}