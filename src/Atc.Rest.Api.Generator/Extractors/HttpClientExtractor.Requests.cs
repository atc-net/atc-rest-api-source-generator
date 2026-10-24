// ReSharper disable InvertIf
namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// The typed client's request side: a method's URL, its parameters by location, its body and its send.
/// </summary>
public static partial class HttpClientExtractor
{
    private static string GenerateMethodBody(
        string path,
        string httpMethod,
        OpenApiOperation operation,
        IList<IOpenApiParameter>? pathLevelParameters,
        OpenApiDocument openApiDoc,
        string returnType,
        bool hasParameters,
        bool isAsyncEnumerable,
        string? streamingItemType,
        bool hasReturnType,
        bool hasLocationHeader,
        bool useServersBasePath = true,
        string parametersClassName = "",
        TypedClientResultStyleType resultStyle = TypedClientResultStyleType.Throw,
        XmlReadPlan? xmlRead = null,
        bool isTextResponse = false)
    {
        var builder = new StringBuilder();
        var propertyNames = ParameterPropertyNames.For(operation, pathLevelParameters);

        // A success the operation declares without content (a 204 beside the model's 200): an empty answer is null
        var emptyBodyIsNull = operation.Responses is { } responses && responses.Any(r => r.Key.StartsWith("2", StringComparison.Ordinal) && r.Value?.Content is not { Count: > 0 });

        // The URL: relative (no leading slash) so the HttpClient's BaseAddress keeps its path, with the path parameters
        var effectiveParameters = operation.EffectiveParameters(pathLevelParameters);
        AppendUrl(builder, path, effectiveParameters, openApiDoc, propertyNames, parametersClassName, useServersBasePath);

        // The query, header, cookie and query-string parameters (a value the client cannot send is left out:
        // ATC_API_OPR037 warned)
        var (queryParams, headerParams, cookieParams, querystringParams) = ParametersByLocation(effectiveParameters);

        AppendQueryParameters(builder, queryParams, propertyNames, openApiDoc, parametersClassName);

        AppendQueryStrings(builder, querystringParams, propertyNames);

        // Per-operation retry (x-retry-*): the statement that tells the resilience handler how to run
        // this request, for a given request variable — null when the handler default already matches.
        IOpenApiPathItem? pathItem = null;
        _ = openApiDoc.Paths?.TryGetValue(path, out pathItem);
        string? Stamp(
            string requestVariable,
            bool streamed)
        {
            // What every send has to put on its request: the header and cookie parameters, then the retry stamp, and
            // for a response that is not streamed the one that has the resilience handler read its body in the attempt
            var statements = new[]
                {
                    BuildHeaderAndCookieStatements(headerParams, cookieParams, requestVariable, openApiDoc, parametersClassName, propertyNames),
                    ResilienceRequestHelper.BuildStampStatement(operation, pathItem, openApiDoc, httpMethod, requestVariable),
                    streamed ? null : ResilienceRequestHelper.BuildBufferStatement(openApiDoc, requestVariable),
                }
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            return statements.Count == 0
                ? null
                : string.Join(Environment.NewLine, statements);
        }

        // A streamed response on another verb (a POST that streams its answer, a body streamed in and out): the request
        // with its body, read as far as the headers, then the items as they arrive - as for a GET
        if (isAsyncEnumerable && !string.IsNullOrEmpty(streamingItemType) && !string.Equals(httpMethod, "GET", StringComparison.Ordinal))
        {
            var content = AppendRequestContent(builder, operation, openApiDoc, hasParameters, xmlRead);
            GenerateGetMethodBody(returnType, isAsyncEnumerable, streamingItemType, operation.GetStreamingFraming(), operation.GetServerSentEventItemNames(), hasReturnType, builder, resultStyle, Stamp, xmlRead, EndpointMapHelper.BuildHttpMethodExpression(httpMethod), content);
            return builder
                .ToString()
                .Trim();
        }

        switch (httpMethod)
        {
            case "GET":
                GenerateGetMethodBody(returnType, isAsyncEnumerable, streamingItemType, operation.GetStreamingFraming(), operation.GetServerSentEventItemNames(), hasReturnType, builder, resultStyle, Stamp, xmlRead, emptyBodyIsNull: emptyBodyIsNull);
                break;
            case "POST":
                GeneratePostMethodBody(operation, openApiDoc, returnType, hasParameters, hasReturnType, hasLocationHeader, builder, resultStyle, Stamp("requestMessage", streamed: false), xmlRead, isTextResponse, emptyBodyIsNull);
                break;
            case "PUT":
                GeneratePutMethodBody(operation, openApiDoc, returnType, hasParameters, hasReturnType, builder, resultStyle, Stamp("requestMessage", streamed: false), xmlRead, isTextResponse, emptyBodyIsNull);
                break;
            case "DELETE":
                GenerateDeleteMethodBody(returnType, hasReturnType, builder, resultStyle, Stamp("requestMessage", streamed: false), xmlRead, isTextResponse, emptyBodyIsNull);
                break;
            default:
                // Non-standard verbs: OpenAPI 3.2 `query` and `additionalOperations`
                // (e.g. LINK), plus PATCH. No dedicated HttpClient JSON helper exists,
                // so build the request explicitly and dispatch by body/return presence.
                GenerateGenericMethodBody(httpMethod, operation, openApiDoc, returnType, hasParameters, hasReturnType, builder, resultStyle, Stamp("requestMessage", streamed: false), xmlRead, isTextResponse, emptyBodyIsNull);
                break;
        }

        return builder
            .ToString()
            .Trim();
    }

    // The URL statement: the server's base path (when used) and the path, the path parameters written into it
    private static void AppendUrl(
        StringBuilder builder,
        string path,
        IReadOnlyList<IOpenApiParameter> effectiveParameters,
        OpenApiDocument openApiDoc,
        ParameterPropertyNames propertyNames,
        string parametersClassName,
        bool useServersBasePath)
    {
        // Get server base path if enabled (e.g., "/api/v1" from servers[0].url)
        var serverBasePath = useServersBasePath ? ServerUrlHelper.GetServersBasePath(openApiDoc) : null;

        // Build the URL - optionally prepend server base path, then replace path parameters. The URL is relative
        // (no leading slash), so the HttpClient's BaseAddress keeps its path: https://gw/svc-a/ + prices is
        // /svc-a/prices; a leading slash would replace that path
        var urlBuilder = (serverBasePath is not null ? $"{serverBasePath}{path}" : path).TrimStart('/');

        // The path parameters into the URL
        foreach (var paramInterface in effectiveParameters)
            {
                var resolved = paramInterface.Resolve();
                var (param, referenceId) = (resolved.Parameter, resolved.ReferenceId);
                if (param is null || string.IsNullOrEmpty(param.Name))
                {
                    continue;
                }

                if (param.In == ParameterLocation.Path)
                {
                    var propName = propertyNames.Get(param, referenceId);
                    var paramType = GetParameterTypeWithInlineEnumAwareness(param, openApiDoc, parametersClassName, propName);
                    var replacement = param.GetArrayDelimiter() is { } delimiter
                        ? $"{{{JoinedItemsExpression(param, $"parameters.{propName}", paramType, delimiter, escapeItems: true)}}}"
                        : WireValueHelper.IsStringEnum(param.Schema)
                            ? $"{{Uri.EscapeDataString(parameters.{propName}.ToWireValue())}}"
                            : WireValueHelper.IsIntegerEnum(param.Schema)
                                ? $"{{{WireValueHelper.IntegerEnumWireExpression($"parameters.{propName}")}}}"
                                : BuildPathParameterReplacement(propName, paramType);
                    urlBuilder = urlBuilder.Replace($"{{{param.Name}}}", replacement);
                }
            }

        builder.AppendLine(
            urlBuilder.Contains('{')
                ? $"var url = $\"{urlBuilder}\";"
                : $"var url = \"{urlBuilder}\";");
    }

    // The operation's query, header, cookie and query-string parameters, in the specification's order; a value the
    // client cannot send is left out
    private static (List<(OpenApiParameter Param, string? ReferenceId)> Query, List<(OpenApiParameter Param, string? ReferenceId)> Header, List<(OpenApiParameter Param, string? ReferenceId)> Cookie, List<(OpenApiParameter Param, string? ReferenceId)> QueryString) ParametersByLocation(
        IReadOnlyList<IOpenApiParameter> effectiveParameters)
    {
        var queryParams = new List<(OpenApiParameter Param, string? ReferenceId)>();
        var headerParams = new List<(OpenApiParameter Param, string? ReferenceId)>();
        var cookieParams = new List<(OpenApiParameter Param, string? ReferenceId)>();
        var querystringParams = new List<(OpenApiParameter Param, string? ReferenceId)>();
        foreach (var paramInterface in effectiveParameters)
        {
            var resolved = paramInterface.Resolve();
            var (param, referenceId) = (resolved.Parameter, resolved.ReferenceId);

            // A value the client cannot send is left out (ATC_API_OPR037 warned)
            if (param?.IsUnbindable() == true)
            {
                continue;
            }

            switch (param)
            {
                case { In: ParameterLocation.Query }:
                    queryParams.Add((param, referenceId));
                    break;
                case { In: ParameterLocation.Header }:
                    headerParams.Add((param, referenceId));
                    break;
                case { In: ParameterLocation.Cookie }:
                    cookieParams.Add((param, referenceId));
                    break;
                case { In: ParameterLocation.QueryString }:
                    querystringParams.Add((param, referenceId));
                    break;
            }
        }

        return (queryParams, headerParams, cookieParams, querystringParams);
    }

    // The query parameters into the URL: each by its serialization, then joined after a '?'
    private static void AppendQueryParameters(
        StringBuilder builder,
        List<(OpenApiParameter Param, string? ReferenceId)> queryParams,
        ParameterPropertyNames propertyNames,
        OpenApiDocument openApiDoc,
        string parametersClassName)
    {
        if (queryParams.Count == 0)
        {
            return;
        }

        builder.AppendLine("var queryParams = new List<string>();");
        foreach (var (param, referenceId) in queryParams)
        {
            AppendQueryParameter(builder, param, propertyNames.Get(param, referenceId), openApiDoc, parametersClassName);
        }

        builder.AppendLine();
        builder.AppendLine("if (queryParams.Count > 0)");
        builder.AppendLine("{");
        builder.AppendLine(4, "url += \"?\" + string.Join(\"&\", queryParams);");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    // One query parameter: an object by its properties' keys, an array joined or repeated, an allowReserved value, an
    // enum by its wire value, a primitive URL-encoded - an optional one only when it is set
    private static void AppendQueryParameter(
        StringBuilder builder,
        OpenApiParameter param,
        string propName,
        OpenApiDocument openApiDoc,
        string parametersClassName)
    {
        var paramAccess = $"parameters.{propName}";
        var paramType = GetParameterTypeWithInlineEnumAwareness(param, openApiDoc, parametersClassName, propName);
        var isRequired = param.Required;
        var serialization = param.GetParameterSerialization();

        // An object: each property set by its own key - filter[name] (deepObject) or name (form, explode)
        if (param.GetQueryObject() is { } queryObject)
        {
            AppendQueryObject(builder, queryObject, paramAccess);
            return;
        }

        // An array whose items are one value (form with explode: false, pipeDelimited, spaceDelimited): the
        // items joined on the declared delimiter, each as the specification writes it
        if (serialization.ValueKind == ParameterValueKind.Array &&
            param.GetArrayDelimiter() is { } delimiter)
        {
            var joined = JoinedItemsExpression(param, paramAccess, paramType, delimiter, escapeItems: false);
            var value = serialization.AllowReserved ? joined : $"Uri.EscapeDataString({joined})";

            builder.AppendLine();
            builder.AppendLine($"if ({paramAccess} is not null && global::System.Linq.Enumerable.Any({paramAccess}))");
            builder.AppendLine("{");
            builder.AppendLine(4, $"queryParams.Add($\"{QueryKey(param.Name!)}={{{value}}}\");");
            builder.AppendLine("}");
            return;
        }

        // An array: repeated keys (form, explode: true; pipeDelimited and spaceDelimited with explode: true are
        // the same) - never as the list's ToString()
        if (serialization.ValueKind == ParameterValueKind.Array)
        {
            AppendQueryArrayForeach(builder, param.Name!, paramAccess, paramType, serialization.AllowReserved, isRequired, WireValueHelper.IsStringEnum(param.Schema), WireValueHelper.IsIntegerEnum(param.Schema.Dereferenced?.Items));
            return;
        }

        // allowReserved primitive: the reserved characters as they are, everything else (and #) encoded
        if (serialization.AllowReserved && serialization.ValueKind == ParameterValueKind.Primitive)
        {
            var reservedValue = paramType.TrimEnd('?') == "string"
                ? WireValueHelper.AllowReservedEscapeExpression(paramAccess + "!")
                : BuildEncodedExpression(paramAccess, paramType);
            if (isRequired)
            {
                builder.AppendLine($"queryParams.Add($\"{QueryKey(param.Name!)}={{{reservedValue}}}\");");
            }
            else
            {
                var nullCheck = BuildNullCheck(paramAccess, paramType);

                builder.AppendLine();
                builder.AppendLine($"if ({nullCheck})");
                builder.AppendLine("{");
                builder.AppendLine(4, $"queryParams.Add($\"{QueryKey(param.Name!)}={{{reservedValue}}}\");");
                builder.AppendLine("}");
            }

            return;
        }

        // An enum: the value as the specification writes it (an integer enum's number), not the member name
        if (WireValueHelper.IsStringEnum(param.Schema) || WireValueHelper.IsIntegerEnum(param.Schema))
        {
            var wireValue = WireValueHelper.IsIntegerEnum(param.Schema)
                ? WireValueHelper.IntegerEnumWireExpression(paramAccess)
                : $"Uri.EscapeDataString({paramAccess}.ToWireValue()!)";
            if (isRequired)
            {
                builder.AppendLine($"queryParams.Add($\"{QueryKey(param.Name!)}={{{wireValue}}}\");");
            }
            else
            {
                builder.AppendLine();
                builder.AppendLine($"if ({paramAccess} is not null)");
                builder.AppendLine("{");
                builder.AppendLine(4, $"queryParams.Add($\"{QueryKey(param.Name!)}={{{wireValue}}}\");");
                builder.AppendLine("}");
            }

            return;
        }

        // Default path: scalar primitive with URL encoding (existing behavior)
        if (isRequired)
        {
            var valueExpression = BuildEncodedExpression(paramAccess, paramType);
            builder.AppendLine($"queryParams.Add($\"{QueryKey(param.Name!)}={{{valueExpression}}}\");");
        }
        else
        {
            // Use appropriate null check based on type for optional parameters. A bare
            // T[] that reaches this scalar path (not the form-explode foreach above) keeps
            // its length guard; everything else delegates to the shared BuildNullCheck.
            var nullCheck = paramType.EndsWith("[]", StringComparison.Ordinal)
                ? $"{paramAccess} is not null && {paramAccess}.Length > 0"
                : BuildNullCheck(paramAccess, paramType);

            var valueExpression = BuildEncodedExpression(paramAccess, paramType);

            builder.AppendLine();
            builder.AppendLine($"if ({nullCheck})");
            builder.AppendLine("{");
            builder.AppendLine(4, $"queryParams.Add($\"{QueryKey(param.Name!)}={{{valueExpression}}}\");");
            builder.AppendLine("}");
        }
    }

    // OAS 3.2 in:querystring: the raw query string appended to the URL as it is (no key=value encoding); several on one
    // operation are unusual but appended
    private static void AppendQueryStrings(
        StringBuilder builder,
        List<(OpenApiParameter Param, string? ReferenceId)> querystringParams,
        ParameterPropertyNames propertyNames)
    {
        foreach (var (param, referenceId) in querystringParams)
        {
            var propName = propertyNames.Get(param, referenceId);
            var paramAccess = $"parameters.{propName}";

            builder.AppendLine();
            builder.AppendLine($"if (!string.IsNullOrEmpty({paramAccess}))");
            builder.AppendLine("{");
            builder.AppendLine(4, $"url += (url.Contains('?') ? \"&\" : \"?\") + {paramAccess};");
            builder.AppendLine("}");

            builder.AppendLine();
        }
    }

    private static void GenerateGetMethodBody(
        string returnType,
        bool isAsyncEnumerable,
        string? streamingItemType,
        StreamingFraming streamingFraming,
        IReadOnlyList<string> sseItemEvents,
        bool hasReturnType,
        StringBuilder builder,
        TypedClientResultStyleType resultStyle,
        Func<string, bool, string?> resilienceStamp,
        XmlReadPlan? xmlRead = null,
        string httpMethodExpression = "HttpMethod.Get",
        string? contentExpression = null,
        bool emptyBodyIsNull = false)
    {
        // Special handling for async enumerable streaming
        if (isAsyncEnumerable && !string.IsNullOrEmpty(streamingItemType))
        {
            if (resultStyle == TypedClientResultStyleType.Result)
            {
                AppendStreamingResultBody(builder, streamingItemType!, streamingFraming, sseItemEvents, resilienceStamp("requestMessage", true), httpMethodExpression, contentExpression);
                return;
            }

            AppendSend(
                builder,
                "using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);",
                resilienceStamp("requestMessage", true),
                httpMethodExpression,
                contentExpression,
                responseHeadersRead: true,
                disposeResponse: true,
                forceRequestMessage: httpMethodExpression != "HttpMethod.Get" || contentExpression is not null);
            builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);");
            builder.AppendLine();
            builder.AppendLine("var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);");
            builder.AppendLine();

            // Server-Sent Events, JSON Lines and JSON Text Sequence use the emitted StreamReaders
            // helper to parse their wire framing; multipart/mixed additionally extracts the boundary
            // from the response Content-Type and passes it to the reader. The remaining framings
            // (JsonArray, ...) keep the legacy DeserializeAsyncEnumerable brace-scan path byte-for-byte.
            if (streamingFraming == StreamingFraming.ServerSentEvents)
            {
                builder.AppendLine($"await foreach (var item in {ServerSentEventsReader(streamingItemType!, sseItemEvents)}.ConfigureAwait(false))");
            }
            else if (streamingFraming == StreamingFraming.JsonLines)
            {
                builder.AppendLine($"await foreach (var item in StreamReaders.ReadJsonLinesAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken).ConfigureAwait(false))");
            }
            else if (streamingFraming == StreamingFraming.JsonSequence)
            {
                builder.AppendLine($"await foreach (var item in StreamReaders.ReadJsonSequenceAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken).ConfigureAwait(false))");
            }
            else if (streamingFraming == StreamingFraming.MultipartMixed)
            {
                builder.AppendLine("var boundary = response.Content.Headers.ContentType?.Parameters");
                builder.AppendLine(4, ".FirstOrDefault(p => string.Equals(p.Name, \"boundary\", StringComparison.OrdinalIgnoreCase))?.Value?.Trim('\"')");
                builder.AppendLine(4, "?? throw new global::System.IO.InvalidDataException(\"A multipart/mixed response without a boundary in its Content-Type cannot be split into its parts.\");");
                builder.AppendLine($"await foreach (var item in StreamReaders.ReadMultipartMixedAsync<{streamingItemType}>(stream, boundary, jsonSerializerOptions, cancellationToken).ConfigureAwait(false))");
            }
            else if (streamingFraming == StreamingFraming.JsonArray)
            {
                builder.AppendLine($"await foreach (var item in StreamReaders.ReadJsonArrayAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken).ConfigureAwait(false))");
            }
            else
            {
                // JsonArray is the legitimate legacy fallthrough above. Any other framing reaching
                // here is a StreamReaders-based wire framing that was added without an explicit
                // reader branch — fail loudly at generation time rather than silently emitting the
                // JSON-array brace-scan path (wrong bytes). Mirrors the per-op ReaderMethodName switch.
                throw new InvalidOperationException(
                    $"No typed-client stream reader is defined for framing '{streamingFraming}'.");
            }

            builder.AppendLine("{");
            builder.AppendLine(4, "if (item is not null)");
            builder.AppendLine(4, "{");
            builder.AppendLine(8, "yield return item;");
            builder.AppendLine(4, "}");
            builder.AppendLine("}");
        }
        else if (hasReturnType && returnType == "StreamedFile")
        {
            // A streamed download: read as far as the headers and handed over open - the StreamedFile owns the response
            AppendSend(
                builder,
                "var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);",
                resilienceStamp("requestMessage", false),
                "HttpMethod.Get",
                contentExpression: null,
                responseHeadersRead: true);
            if (resultStyle == TypedClientResultStyleType.Result)
            {
                builder.AppendLine("return await BuildStreamedFileResponseAsync(response, cancellationToken).ConfigureAwait(false);");
            }
            else
            {
                // On an error status the response is the exception's to describe, not the caller's to dispose
                builder.AppendLine("try");
                builder.AppendLine("{");
                builder.AppendLine(4, "await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);");
                builder.AppendLine(4, "return await StreamedFile.FromHttpResponseAsync(response, cancellationToken).ConfigureAwait(false);");
                builder.AppendLine("}");
                builder.AppendLine("catch");
                builder.AppendLine("{");
                builder.AppendLine(4, "response.Dispose();");
                builder.AppendLine(4, "throw;");
                builder.AppendLine("}");
            }
        }
        else if (hasReturnType && returnType == "DownloadedFile")
        {
            // Binary content download - use ReadAsByteArrayAsync
            AppendSend(builder, "var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);", resilienceStamp("requestMessage", false), "HttpMethod.Get", contentExpression: null);
            AppendResponseCompletion(builder, resultStyle, ResponsePayloadKind.Binary, returnType, isLastStatement: true);
        }
        else if (hasReturnType && returnType == "string")
        {
            // Text content - use ReadAsStringAsync
            AppendSend(builder, "var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);", resilienceStamp("requestMessage", false), "HttpMethod.Get", contentExpression: null);
            AppendResponseCompletion(builder, resultStyle, ResponsePayloadKind.Text, returnType, isLastStatement: true);
        }
        else
        {
            // No GetFromJsonAsync shortcut for the Throw style: its exception drops the error body, whereas
            // EnsureSuccessAsync (emitted by AppendResponseCompletion) keeps it in the message.
            AppendSend(builder, "var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);", resilienceStamp("requestMessage", false), "HttpMethod.Get", contentExpression: null, accept: xmlRead?.Accept);

            AppendResponseCompletion(
                builder,
                resultStyle,
                hasReturnType ? (xmlRead?.ReadMethod is null || xmlRead.ByContentType ? ResponsePayloadKind.Json : ResponsePayloadKind.Xml) : ResponsePayloadKind.None,
                returnType,
                isLastStatement: true,
                xmlRead: xmlRead,
                emptyBodyIsNull: emptyBodyIsNull);
        }
    }

    /// <summary>
    /// Emits the Result-style body for a streaming operation.
    /// </summary>
    /// <remarks>
    /// The method returns <c>Task&lt;StreamingEndpointResponse&lt;T&gt;&gt;</c>, so it cannot be an iterator
    /// (<c>yield</c> is illegal in a Task-returning method). Instead the status envelope is resolved as soon
    /// as the response headers arrive and the still-unconsumed reader is handed to the envelope as its content,
    /// which preserves lazy streaming. This mirrors EndpointPerOperation mode.
    /// The response is deliberately NOT disposed here - <c>StreamingEndpointResponse&lt;T&gt;</c> owns it and
    /// disposes it once the caller disposes the envelope, otherwise the stream would be closed before it is read.
    /// </remarks>
    private static void AppendStreamingResultBody(
        StringBuilder builder,
        string streamingItemType,
        StreamingFraming streamingFraming,
        IReadOnlyList<string> sseItemEvents,
        string? resilienceStamp,
        string httpMethodExpression = "HttpMethod.Get",
        string? contentExpression = null)
    {
        AppendSend(
            builder,
            "var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);",
            resilienceStamp,
            httpMethodExpression,
            contentExpression,
            responseHeadersRead: true,
            forceRequestMessage: httpMethodExpression != "HttpMethod.Get" || contentExpression is not null);
        builder.AppendLine();
        builder.AppendLine("if (!response.IsSuccessStatusCode)");
        builder.AppendLine("{");
        builder.AppendLine(4, "var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
        builder.AppendLine(4, $"return new StreamingEndpointResponse<{streamingItemType}>(false, response.StatusCode, content: null, errorContent, response);");
        builder.AppendLine("}");
        builder.AppendLine();

        // Until the envelope owns the response, a failure (the body cannot be opened, no multipart boundary) disposes it
        var reader = new StringBuilder();
        reader.AppendLine("var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);");
        reader.AppendLine();
        AppendMultipartBoundaryIfNeeded(reader, streamingFraming);
        if (streamingFraming == StreamingFraming.ServerSentEvents)
        {
            // An event stream: Atc.Rest.Client's reader, so the envelope's LastEventId follows the events as they are
            // read - what a caller sends as Last-Event-ID to continue a stream that dropped
            var itemEvents = sseItemEvents.Count == 0
                ? string.Empty
                : $", ItemEventNames = new[] {{ {string.Join(", ", sseItemEvents.Select(CSharpLiteralHelper.ToStringLiteral))} }}";
            reader.AppendLine($"return global::Atc.Rest.Client.ServerSentEventReader.Read<{streamingItemType}>(");
            reader.AppendLine("    response,");
            reader.AppendLine("    stream,");
            reader.AppendLine($"    json => JsonSerializer.Deserialize(json, jsonSerializerOptions.TypeInfo<{streamingItemType}>()),");
            reader.AppendLine($"    new global::Atc.Rest.Client.StreamReadOptions {{ MaxRecordLength = StreamReaders.MaxRecordLength{itemEvents} }},");
            reader.Append("    cancellationToken);");
        }
        else
        {
            reader.AppendLine($"var content = {GetStreamReaderExpression(streamingItemType, streamingFraming, sseItemEvents)};");
            reader.AppendLine();
            reader.Append($"return new StreamingEndpointResponse<{streamingItemType}>(true, response.StatusCode, content, errorContent: null, response);");
        }

        builder.AppendLine("try");
        builder.AppendLine("{");
        foreach (var line in reader.ToString().Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            if (line.Length == 0)
            {
                builder.AppendLine();
            }
            else
            {
                builder.AppendLine(4, line);
            }
        }

        builder.AppendLine("}");
        builder.AppendLine("catch");
        builder.AppendLine("{");
        builder.AppendLine(4, "response.Dispose();");
        builder.AppendLine(4, "throw;");
        builder.Append('}');
    }

    /// <summary>
    /// Emits the multipart/mixed boundary extraction, which must precede the reader expression.
    /// </summary>
    private static void AppendMultipartBoundaryIfNeeded(
        StringBuilder builder,
        StreamingFraming streamingFraming)
    {
        if (streamingFraming != StreamingFraming.MultipartMixed)
        {
            return;
        }

        builder.AppendLine("var boundary = response.Content.Headers.ContentType?.Parameters");
        builder.AppendLine(4, ".FirstOrDefault(p => string.Equals(p.Name, \"boundary\", StringComparison.OrdinalIgnoreCase))?.Value?.Trim('\"')");
        builder.AppendLine(4, "?? throw new global::System.IO.InvalidDataException(\"A multipart/mixed response without a boundary in its Content-Type cannot be split into its parts.\");");
    }

    /// <summary>
    /// Returns the reader expression that produces the <c>IAsyncEnumerable</c> for a wire framing.
    /// </summary>
    private static string GetStreamReaderExpression(
        string streamingItemType,
        StreamingFraming streamingFraming,
        IReadOnlyList<string> sseItemEvents)
        => streamingFraming switch
        {
            StreamingFraming.ServerSentEvents => ServerSentEventsReader(streamingItemType, sseItemEvents),
            StreamingFraming.JsonLines => $"StreamReaders.ReadJsonLinesAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken)",
            StreamingFraming.JsonSequence => $"StreamReaders.ReadJsonSequenceAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken)",
            StreamingFraming.MultipartMixed => $"StreamReaders.ReadMultipartMixedAsync<{streamingItemType}>(stream, boundary, jsonSerializerOptions, cancellationToken)",

            // A JSON array: the serializer's own element-by-element read, behind a look for an empty body
            StreamingFraming.JsonArray => $"StreamReaders.ReadJsonArrayAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken)",

            // Any other framing is a StreamReaders-based wire framing that was added without an explicit
            // reader branch - fail loudly at generation time rather than silently emitting the
            // JSON-array brace-scan path (wrong bytes). Mirrors the per-op ReaderMethodName switch.
            _ => throw new InvalidOperationException(
                $"No typed-client stream reader is defined for framing '{streamingFraming}'."),
        };

    // The reader of an event stream: with the names of x-sse-item-events when the operation gives any
    private static string ServerSentEventsReader(
        string streamingItemType,
        IReadOnlyList<string> sseItemEvents)
        => sseItemEvents.Count == 0
            ? $"StreamReaders.ReadServerSentEventsAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken)"
            : $"StreamReaders.ReadServerSentEventsOfAsync<{streamingItemType}>(stream, jsonSerializerOptions, new[] {{ {string.Join(", ", sseItemEvents.Select(CSharpLiteralHelper.ToStringLiteral))} }}, cancellationToken)";

    private static void GenerateGenericMethodBody(
        string httpMethod,
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        string returnType,
        bool hasParameters,
        bool hasReturnType,
        StringBuilder builder,
        TypedClientResultStyleType resultStyle,
        string? resilienceStamp,
        XmlReadPlan? xmlRead = null,
        bool isTextResponse = false,
        bool emptyBodyIsNull = false)
    {
        // Resolve the verb through the same helper the per-operation client uses, so both
        // generated clients express the same verb the same way: the static HttpMethod property
        // for RFC verbs (HttpMethod.Query, ...) and new HttpMethod("VERB") only for custom
        // additionalOperations verbs that have none.
        var httpMethodExpression = EndpointMapHelper.BuildHttpMethodExpression(httpMethod);

        // The body by the same rule as every other verb; a form, file or multipart body is built into a local first
        var content = AppendRequestContent(builder, operation, openApiDoc, hasParameters, xmlRead);
        if (!string.Equals(content, LocalContent, StringComparison.Ordinal))
        {
            builder.AppendLine();
        }

        builder.AppendLine($"using var requestMessage = new HttpRequestMessage({httpMethodExpression}, url);");

        if (content is not null)
        {
            builder.AppendLine($"requestMessage.Content = {content};");
        }

        if (xmlRead?.Accept is not null)
        {
            builder.AppendLine($"requestMessage.Headers.Accept.ParseAdd(\"{xmlRead.Accept}\");");
        }

        if (resilienceStamp is not null)
        {
            builder.AppendLine(resilienceStamp);
        }

        builder.AppendLine();
        builder.AppendLine("using var response = await httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);");

        AppendResponseCompletion(
            builder,
            resultStyle,
            PayloadKind(hasReturnType, xmlRead, isTextResponse),
            returnType,
            isLastStatement: hasReturnType,
            xmlRead: xmlRead,
            emptyBodyIsNull: emptyBodyIsNull);
    }

    /// <summary>
    /// The content of a direct file upload, into a <c>content</c> local: the file(s) as multipart/form-data parts, or
    /// the raw file with its media type.
    /// </summary>
    private static void AppendDirectFileUploadContent(
        StringBuilder builder,
        OpenApiOperation operation,
        string? fileUploadContentType)
    {
        // Check if this is a multi-file upload (array of binary)
        var isMultiFileUpload = IsMultiFileUpload(operation);

        // Generate file upload code (use null-forgiving operator since File is optional in spec but required for upload)
        if (fileUploadContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
        {
            builder.AppendLine("using var content = new MultipartFormDataContent();");

            if (isMultiFileUpload)
            {
                // Multi-file upload - iterate over the array
                builder.AppendLine();
                builder.AppendLine("for (var i = 0; i < parameters.File!.Length; i++)");
                builder.AppendLine("{");
                builder.AppendLine(4, "var fileItem = parameters.File[i];");
                builder.AppendLine(4, "var streamContent = new StreamContent(fileItem.OpenReadStream());");
                builder.AppendLine();
                builder.AppendLine(4, "if (fileItem.ContentType is not null)");
                builder.AppendLine(4, "{");
                builder.AppendLine(8, "streamContent.Headers.ContentType = new MediaTypeHeaderValue(fileItem.ContentType);");
                builder.AppendLine(4, "}");
                builder.AppendLine();
                builder.AppendLine(4, "content.Add(streamContent, \"files\", fileItem.FileName);");
                builder.AppendLine("}");
            }
            else
            {
                // Single file upload
                builder.AppendLine("var streamContent = new StreamContent(parameters.File!.OpenReadStream());");
                builder.AppendLine();
                builder.AppendLine("if (parameters.File!.ContentType is not null)");
                builder.AppendLine("{");
                builder.AppendLine(4, "streamContent.Headers.ContentType = new MediaTypeHeaderValue(parameters.File.ContentType);");
                builder.AppendLine("}");
                builder.AppendLine();
                builder.AppendLine("content.Add(streamContent, \"file\", parameters.File.FileName);");
            }
        }
        else
        {
            // application/octet-stream or image/* content types
            builder.AppendLine("using var content = new StreamContent(parameters.File!.OpenReadStream());");
            builder.AppendLine($"content.Headers.ContentType = new MediaTypeHeaderValue({RawFileBodyHelper.ContentTypeExpression(operation, "parameters.File", fileUploadContentType ?? "application/octet-stream")});");
        }
    }

    /// <summary>
    /// The name of the local a form, file or multipart body is built into.
    /// </summary>
    private const string LocalContent = "content";

    /// <summary>
    /// The request body of an operation as content - the one rule every verb sends its body by: a stream of records, a
    /// file, a multipart form, a URL-encoded form, XML or JSON, with the statements that build it emitted first;
    /// <see langword="null"/> when there is no body.
    /// </summary>
    private static string? AppendRequestContent(
        StringBuilder builder,
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        bool hasParameters,
        XmlReadPlan? xmlRead)
    {
        if (!hasParameters || !operation.HasRequestBody())
        {
            return null;
        }

        const string requestAccess = "parameters.Request";
        if (StreamedBodyContent(operation, requestAccess) is { } streamed)
        {
            return streamed;
        }

        if (IsDirectFileUpload(operation))
        {
            builder.AppendLine();
            AppendDirectFileUploadContent(builder, operation, operation.GetFileUploadContentType());
            builder.AppendLine();
            return LocalContent;
        }

        if (GetSchemaBasedMultipartSchema(operation, openApiDoc) is { } multipartSchema)
        {
            // The parts end with a blank line of their own
            GenerateSchemaBasedMultipartFormData(builder, multipartSchema, FormBodyHelper.FindMultipartFormData(operation), requestAccess);
            return LocalContent;
        }

        if (UrlEncodedFormSchema(operation, openApiDoc) is { } formSchema)
        {
            builder.AppendLine();
            GenerateUrlEncodedForm(builder, formSchema, requestAccess);
            return LocalContent;
        }

        if (xmlRead?.RequestContent is { } xmlContent)
        {
            return xmlContent;
        }

        // Text: the string as UTF-8, with the declared media type (StringContent adds the charset)
        if (operation.GetTextRequestBodyMediaType() is { } textMediaType)
        {
            return $"new StringContent({requestAccess}, global::System.Text.Encoding.UTF8, {CSharpLiteralHelper.ToStringLiteral(textMediaType.BaseType)})";
        }

        return operation.GetJsonRequestBodyMediaType() is not null
            ? JsonBodyContent(operation, requestAccess)
            : null;
    }

    private static void GeneratePostMethodBody(
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        string returnType,
        bool hasParameters,
        bool hasReturnType,
        bool hasLocationHeader,
        StringBuilder builder,
        TypedClientResultStyleType resultStyle,
        string? resilienceStamp,
        XmlReadPlan? xmlRead = null,
        bool isTextResponse = false,
        bool emptyBodyIsNull = false)
    {
        AppendBodySend(builder, "PostAsync", "HttpMethod.Post", operation, openApiDoc, hasParameters, resilienceStamp, xmlRead);

        if (hasLocationHeader)
        {
            // Return the Location header as Uri
            if (resultStyle == TypedClientResultStyleType.Result)
            {
                builder.Append("return await BuildLocationResponseAsync(response, cancellationToken).ConfigureAwait(false);");
            }
            else
            {
                builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);");
                builder.Append("return response.Headers.Location!;");
            }
        }
        else
        {
            AppendResponseCompletion(
                builder,
                resultStyle,
                PayloadKind(hasReturnType, xmlRead, isTextResponse),
                returnType,
                isLastStatement: true,
                xmlRead: xmlRead,
                emptyBodyIsNull: emptyBodyIsNull);
        }
    }

    private static void GeneratePutMethodBody(
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        string returnType,
        bool hasParameters,
        bool hasReturnType,
        StringBuilder builder,
        TypedClientResultStyleType resultStyle,
        string? resilienceStamp,
        XmlReadPlan? xmlRead = null,
        bool isTextResponse = false,
        bool emptyBodyIsNull = false)
    {
        AppendBodySend(builder, "PutAsync", "HttpMethod.Put", operation, openApiDoc, hasParameters, resilienceStamp, xmlRead);

        AppendResponseCompletion(
            builder,
            resultStyle,
            PayloadKind(hasReturnType, xmlRead, isTextResponse),
            returnType,
            isLastStatement: true,
            xmlRead: xmlRead,
            emptyBodyIsNull: emptyBodyIsNull);
    }

    /// <summary>
    /// Emits the send of a verb with an <c>HttpClient</c> helper (<c>PostAsync</c>, <c>PutAsync</c>) and its body, built by
    /// <see cref="AppendRequestContent"/>.
    /// </summary>
    private static void AppendBodySend(
        StringBuilder builder,
        string helper,
        string httpMethodExpression,
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        bool hasParameters,
        string? resilienceStamp,
        XmlReadPlan? xmlRead)
    {
        var content = AppendRequestContent(builder, operation, openApiDoc, hasParameters, xmlRead);
        AppendSend(
            builder,
            $"var response = await httpClient.{helper}(url, {content ?? "null"}, cancellationToken).ConfigureAwait(false);",
            resilienceStamp,
            httpMethodExpression,
            content,
            accept: xmlRead?.Accept);
    }

    private static void GenerateDeleteMethodBody(
        string returnType,
        bool hasReturnType,
        StringBuilder builder,
        TypedClientResultStyleType resultStyle,
        string? resilienceStamp,
        XmlReadPlan? xmlRead = null,
        bool isTextResponse = false,
        bool emptyBodyIsNull = false)
    {
        AppendSend(builder, "var response = await httpClient.DeleteAsync(url, cancellationToken).ConfigureAwait(false);", resilienceStamp, "HttpMethod.Delete", contentExpression: null, accept: xmlRead?.Accept);

        AppendResponseCompletion(
            builder,
            resultStyle,
            PayloadKind(hasReturnType, xmlRead, isTextResponse),
            returnType,
            isLastStatement: true,
            xmlRead: xmlRead,
            emptyBodyIsNull: emptyBodyIsNull);
    }

    /// <summary>
    /// Whether the JSON request body is declared as <c>application/json</c> (the content's default media type).
    /// </summary>
    private static bool IsPlainJson(OpenApiOperation operation)
        => string.Equals(operation.GetJsonRequestBodyMediaType(), "application/json", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A request body sent as a stream of records (JSON Lines, JSON Text Sequences) as content: the generated
    /// <c>StreamedRequestContent</c>, written with the client's JSON options in the first declared sequential media
    /// type; <see langword="null"/> when the body is not one.
    /// </summary>
    private static string? StreamedBodyContent(
        OpenApiOperation operation,
        string requestAccess)
        => operation.GetStreamedRequestBody() is { } streamed
            ? StreamedRequestContentExtractor.CreateContentExpression(requestAccess, "jsonSerializerOptions", streamed.PrimaryMediaType, streamed.IsRequired)
            : null;

    /// <summary>
    /// The JSON request body as content: with the declared media type when it is another JSON media type
    /// (<c>application/merge-patch+json</c>, <c>application/vnd.x+json</c>).
    /// </summary>
    private static string JsonBodyContent(
        OpenApiOperation operation,
        string requestAccess)
        => IsPlainJson(operation)
            ? $"jsonSerializerOptions.ToJsonContent({requestAccess})"
            : $"jsonSerializerOptions.ToJsonContent({requestAccess}, new System.Net.Http.Headers.MediaTypeHeaderValue(\"{operation.GetJsonRequestBodyMediaType()}\"))";

    /// <summary>
    /// Emits the send for an operation: the verb helper call (<c>GetAsync</c>, <c>PostAsJsonAsync</c>, ...) as
    /// before, or — when the operation has its own retry settings (x-retry-*) — the same request built as an
    /// <c>HttpRequestMessage</c> carrying the resilience stamp the API resilience handler reads.
    /// </summary>
    private static void AppendSend(
        StringBuilder builder,
        string plainSendStatement,
        string? resilienceStamp,
        string httpMethodExpression,
        string? contentExpression,
        bool responseHeadersRead = false,
        bool disposeResponse = false,
        string? accept = null,
        bool forceRequestMessage = false)
    {
        // A buffered response is read in full before the method returns, so the method disposes it; only a streamed
        // response the caller takes over (the Result style's streaming envelope) is left undisposed
        disposeResponse |= !responseHeadersRead;
        if (resilienceStamp is null && accept is null && !forceRequestMessage)
        {
            builder.AppendLine(disposeResponse && plainSendStatement.StartsWith("var response", StringComparison.Ordinal)
                ? "using " + plainSendStatement
                : plainSendStatement);
            return;
        }

        builder.AppendLine($"using var requestMessage = new HttpRequestMessage({httpMethodExpression}, url);");
        if (contentExpression is not null)
        {
            builder.AppendLine($"requestMessage.Content = {contentExpression};");
        }

        if (accept is not null)
        {
            builder.AppendLine($"requestMessage.Headers.Accept.ParseAdd(\"{accept}\");");
        }

        if (resilienceStamp is not null)
        {
            builder.AppendLine(resilienceStamp);
            if (resilienceStamp.Contains('\n'))
            {
                builder.AppendLine();
            }
        }

        var completion = responseHeadersRead ? "HttpCompletionOption.ResponseHeadersRead, " : string.Empty;
        var declaration = disposeResponse ? "using var" : "var";
        builder.AppendLine($"{declaration} response = await httpClient.SendAsync(requestMessage, {completion}cancellationToken).ConfigureAwait(false);");
    }

    /// <summary>
    /// The statements that put the operation's header and cookie parameters on a request, or
    /// <see langword="null"/> when it has none. A value is written as its wire text: never with the
    /// culture of the machine the client runs on.
    /// </summary>
    private static string? BuildHeaderAndCookieStatements(
        List<(OpenApiParameter Param, string? ReferenceId)> headerParams,
        List<(OpenApiParameter Param, string? ReferenceId)> cookieParams,
        string requestVariable,
        OpenApiDocument openApiDoc,
        string parametersClassName,
        ParameterPropertyNames propertyNames)
    {
        if (headerParams.Count == 0 && cookieParams.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();
        foreach (var (param, referenceId) in headerParams)
        {
            var propName = propertyNames.Get(param, referenceId);
            var variable = "header" + propName;
            var paramType = GetParameterTypeWithInlineEnumAwareness(param, openApiDoc, parametersClassName, propName);

            sb.AppendLine($"if (parameters.{propName} is {ValuePattern(paramType)} {variable})");
            sb.AppendLine("{");

            // An array: its items joined by commas (style: simple)
            var headerText = param.GetArrayDelimiter() is { } delimiter
                ? JoinedItemsExpression(param, variable, paramType, delimiter, escapeItems: false)
                : WireValueHelper.IsIntegerEnum(param.Schema)
                    ? WireValueHelper.IntegerEnumWireExpression(variable) + "!"
                    : BuildWireTextExpression(variable, paramType, WireValueHelper.IsStringEnum(param.Schema));
            sb.AppendLine(4, $"{requestVariable}.Headers.Add({CSharpLiteralHelper.ToStringLiteral(param.Name!)}, {headerText});");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // RFC 6265 Cookie header (name=value; name2=value2), each value percent-encoded (the server decodes it) unless
        // the parameter declares style: cookie (sent as is)
        if (cookieParams.Count > 0)
        {
            sb.AppendLine("var cookieParts = new List<string>();");
            foreach (var (param, referenceId) in cookieParams)
            {
                var propName = propertyNames.Get(param, referenceId);
                var variable = "cookie" + propName;
                var paramType = GetParameterTypeWithInlineEnumAwareness(param, openApiDoc, parametersClassName, propName);

                sb.AppendLine($"if (parameters.{propName} is {ValuePattern(paramType)} {variable})");
                sb.AppendLine("{");
                var text = BuildWireTextExpression(variable, paramType, WireValueHelper.IsStringEnum(param.Schema));
                sb.AppendLine(4, $"cookieParts.Add(\"{CSharpLiteralHelper.EscapeString(param.Name!)}=\" + {(param.Style == ParameterStyle.Cookie ? text : $"Uri.EscapeDataString({text})")});");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            sb.AppendLine("if (cookieParts.Count > 0)");
            sb.AppendLine("{");
            sb.AppendLine(4, $"{requestVariable}.Headers.TryAddWithoutValidation(\"Cookie\", string.Join(\"; \", cookieParts));");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();

        // A value that is there: an empty string is left out, as before
        static string ValuePattern(string paramType)
            => paramType.TrimEnd('?') == "string" ? "{ Length: > 0 }" : "{ }";
    }

    /// <summary>
    /// A C# expression of type <c>string</c>: an array's items joined on its delimiter, each as the specification writes
    /// it (an enum by its wire value) and, in a path, percent-encoded on its own so the delimiter stays one.
    /// </summary>
    private static string JoinedItemsExpression(
        OpenApiParameter param,
        string access,
        string paramType,
        char delimiter,
        bool escapeItems)
    {
        var itemsSchema = param.Schema.Dereferenced?.Items;
        var itemType = TryGetListElementType(paramType, out var elementType) ? elementType : "string";
        var itemText = WireValueHelper.IsIntegerEnum(itemsSchema)
            ? WireValueHelper.IntegerEnumWireExpression("item") + "!"
            : BuildWireTextExpression("item", itemType, WireValueHelper.IsStringEnum(itemsSchema));
        if (escapeItems)
        {
            itemText = $"Uri.EscapeDataString({itemText})";
        }

        return $"string.Join({CSharpLiteralHelper.ToStringLiteral(delimiter.ToString())}, global::System.Linq.Enumerable.Select({access}, item => {itemText}))";
    }

    /// <summary>
    /// A C# expression of type <c>string</c> that gives the wire text of a non-null scalar value.
    /// </summary>
    private static string BuildWireTextExpression(
        string valueExpression,
        string paramType,
        bool isEnum)
    {
        if (isEnum)
        {
            return $"{valueExpression}.ToWireValue()";
        }

        var type = paramType.TrimEnd('?');
        if (type == "string")
        {
            return valueExpression;
        }

        if (type == "bool")
        {
            return $"({valueExpression} ? \"true\" : \"false\")";
        }

        return WireValueHelper.TryBuildWireExpression(valueExpression, type) is { } wireValue
            ? wireValue + "!"
            : $"global::System.FormattableString.Invariant($\"{{{valueExpression}}}\")";
    }

    private static string GetParameterType(
        OpenApiParameter param,
        OpenApiDocument openApiDoc)
    {
        if (param.Schema is null)
        {
            return "string";
        }

        return GetSchemaTypeName(param.Schema, openApiDoc);
    }

    /// <summary>
    /// Returns the C# type name for a parameter, accounting for inline enums.
    /// When the schema is an inline enum (string + enum values, no $ref), the type is
    /// the generated enum name (<c>{parametersClassName}{PropertyName}</c>) — matches what
    /// <see cref="OperationParameterExtractor"/> writes on the corresponding record property.
    /// The URL-builder then takes the non-string branch in <see cref="BuildEncodedExpression"/>,
    /// which produces <c>Uri.EscapeDataString($"{...}")</c> — interpolation handles the
    /// enum-to-string conversion safely.
    /// </summary>
    private static string GetParameterTypeWithInlineEnumAwareness(
        OpenApiParameter param,
        OpenApiDocument openApiDoc,
        string parametersClassName,
        string propName)
    {
        if (param.Schema is OpenApiSchema actualSchema &&
            actualSchema.IsInlineEnum &&
            !string.IsNullOrEmpty(param.Name))
        {
            return InlineEnumExtractor.GenerateInlineEnumTypeName(parametersClassName, propName, TypeConflictRegistry.ComponentTypeNamesOf(openApiDoc));
        }

        return GetParameterType(param, openApiDoc);
    }

    private static string GetSchemaTypeName(
        IOpenApiSchema? schema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry = null)
        => GetSchemaTypeName(schema, openApiDoc, registry, operationId: null, pathSegment: null, context: null, inlineSchemas: null);

    private static string GetSchemaTypeName(
        IOpenApiSchema? schema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        string? operationId,
        string? pathSegment,
        string? context,
        Dictionary<string, InlineSchemaInfo>? inlineSchemas)
        => SchemaTypeResolver.Resolve(
            schema,
            openApiDoc,
            registry,
            collection: "List",
            topCollection: null,
            context,
            SchemaTypeResolver.InlineRecords(inlineSchemas, operationId, pathSegment, registry));

    /// <summary>
    /// Checks if the operation is a direct file upload (binary or array of binary schema).
    /// Returns false for schema references to objects that may contain file properties.
    /// </summary>
    private static bool IsDirectFileUpload(OpenApiOperation operation)
    {
        if (operation.RequestBody?.Content is null)
        {
            return false;
        }

        foreach (var contentEntry in operation.RequestBody.Content)
        {
            var schema = contentEntry.Value.Schema;

            // Single binary file
            if (schema is OpenApiSchema directSchema &&
                directSchema.Type?.HasFlag(JsonSchemaType.String) == true &&
                string.Equals(directSchema.Format, "binary", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Array of binary files
            if (schema is OpenApiSchema { Items: OpenApiSchema itemSchema } && schema.IsOfType(JsonSchemaType.Array) &&
                string.Equals(itemSchema.Format, "binary", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Note: Schema references (like FileAsFormDataRequest) are NOT direct file uploads
        }

        return false;
    }

    /// <summary>
    /// Appends a repeated-key foreach block for a form-explode array query parameter.
    /// For required parameters, emits the bare foreach.
    /// For optional parameters, wraps the foreach in a null guard.
    /// Each element is encoded using <see cref="BuildEncodedExpression"/> (or raw when allowReserved).
    /// </summary>
    /// <summary>
    /// A wire name for the inside of a generated interpolated string literal: escaped as text, braces doubled.
    /// </summary>
    // An object in the query: each property that is set, by its key (escaped: filter%5Bname%5D), as a scalar of its type
    private static void AppendQueryObject(
        StringBuilder builder,
        OpenApiParameterExtensions.QueryObject queryObject,
        string paramAccess)
    {
        var local = "queryObject" + paramAccess.Substring(paramAccess.LastIndexOf('.') + 1);
        builder.AppendLine();
        builder.AppendLine($"if ({paramAccess} is {{ }} {local})");
        builder.AppendLine("{");
        foreach (var property in queryObject.Properties)
        {
            var access = $"{local}.{property.WireName.ToPascalCaseForDotNet()}";
            var key = QueryKey(queryObject.KeyOf(property));
            var schema = (IOpenApiSchema?)property.Schema.WrappedEnum() ?? property.Schema;
            var type = property.Schema.ToCSharpTypeForModel(property.Required);
            var value = WireValueHelper.IsIntegerEnum(schema)
                ? WireValueHelper.IntegerEnumWireExpression(access)
                : WireValueHelper.IsStringEnum(schema)
                    ? $"Uri.EscapeDataString({access}.ToWireValue()!)"
                    : BuildEncodedExpression(access, type);
            if (property.Schema.CanBeNullInQueryObject(property.Required))
            {
                builder.AppendLine(4, $"if ({access} is not null)");
                builder.AppendLine(4, "{");
                builder.AppendLine(8, $"queryParams.Add($\"{key}={{{value}}}\");");
                builder.AppendLine(4, "}");
            }
            else
            {
                builder.AppendLine(4, $"queryParams.Add($\"{key}={{{value}}}\");");
            }
        }

        builder.AppendLine("}");
    }

    /// <summary>
    /// A query key as the URI carries it, escaped as its value is: <c>page[size]</c> is <c>page%5Bsize%5D</c>, and a
    /// name with <c>&amp;</c>, <c>=</c>, <c>#</c> or a space stays one key. The escaped text holds nothing a C#
    /// interpolated string would read (no quote, backslash or brace).
    /// </summary>
    private static string QueryKey(string name)
        => Uri.EscapeDataString(name);

    private static void AppendQueryArrayForeach(
        StringBuilder builder,
        string paramName,
        string paramAccess,
        string paramType,
        bool allowReserved,
        bool isRequired,
        bool itemIsEnum = false,
        bool itemIsIntegerEnum = false)
    {
        // Element type comes off the shared List<T>/T[] extractor; if neither shape matches
        // (no array param should reach here that way) fall back to the param type itself.
        if (!TryGetListElementType(paramType, out var elementType))
        {
            elementType = paramType;
        }

        // allowReserved: the reserved characters of a string item as they are, everything else (and #) encoded; a
        // value that is no string is formatted for the wire as always
        var encodedItem = allowReserved
            ? (elementType == "string" ? WireValueHelper.AllowReservedEscapeExpression("item") : BuildEncodedExpression("item", elementType))
            : BuildEncodedExpression("item", elementType);

        // An enum item: the value as the specification writes it
        if (itemIsEnum)
        {
            encodedItem = allowReserved ? WireValueHelper.AllowReservedEscapeExpression("item.ToWireValue()") : "Uri.EscapeDataString(item.ToWireValue())";
        }

        // An integer enum item: its number
        if (itemIsIntegerEnum)
        {
            encodedItem = WireValueHelper.IntegerEnumWireExpression("item");
        }

        if (isRequired)
        {
            builder.AppendLine($"foreach (var item in {paramAccess})");
            builder.AppendLine("{");
            builder.AppendLine(4, $"queryParams.Add($\"{QueryKey(paramName)}={{{encodedItem}}}\");");
            builder.AppendLine("}");
        }
        else
        {
            builder.AppendLine();
            builder.AppendLine($"if ({paramAccess} is not null)");
            builder.AppendLine("{");
            builder.AppendLine(4, $"foreach (var item in {paramAccess})");
            builder.AppendLine(4, "{");
            builder.AppendLine(8, $"queryParams.Add($\"{QueryKey(paramName)}={{{encodedItem}}}\");");
            builder.AppendLine(4, "}");
            builder.AppendLine("}");
        }
    }

    /// <summary>
    /// Extracts the element type from a <c>List&lt;T&gt;</c> or <c>T[]</c> type name.
    /// Upstream contract: array query params arrive as a non-nullable <c>List&lt;T&gt;</c>
    /// (no trailing <c>?</c>, no nesting), and streaming response bodies arrive as
    /// <c>List&lt;T&gt;</c>; both feed this single extractor so the surgery lives in one place.
    /// </summary>
    /// <returns><c>true</c> with <paramref name="elementType"/> set when the shape matched; otherwise <c>false</c>.</returns>
    private static bool TryGetListElementType(
        string paramType,
        out string elementType)
    {
        if (paramType.EndsWith("[]", StringComparison.Ordinal))
        {
            elementType = paramType.Substring(0, paramType.Length - 2);
            return true;
        }

        if (paramType.StartsWith("List<", StringComparison.Ordinal) &&
            paramType.EndsWith(">", StringComparison.Ordinal))
        {
            elementType = paramType.Substring(5, paramType.Length - 6);
            return true;
        }

        elementType = string.Empty;
        return false;
    }

    /// <summary>
    /// Builds the null/empty guard expression for an optional query parameter, selecting the
    /// right form for the parameter's C# type: <c>!string.IsNullOrEmpty(x)</c> for strings,
    /// <c>x.HasValue</c> for nullable value types, and <c>x is not null</c> otherwise.
    /// </summary>
    private static string BuildNullCheck(
        string paramAccess,
        string paramType)
    {
        if (paramType == "string")
        {
            return $"!string.IsNullOrEmpty({paramAccess})";
        }

        if (CSharpTypeHelper.IsBasicValueType(paramType))
        {
            return $"{paramAccess}.HasValue";
        }

        return $"{paramAccess} is not null";
    }

    /// <summary>
    /// Checks if the operation is a multi-file upload (direct array of binary).
    /// Only returns true for direct arrays, not for schema references to objects.
    /// </summary>
    private static bool IsMultiFileUpload(OpenApiOperation operation)
    {
        if (operation.RequestBody?.Content is null)
        {
            return false;
        }

        foreach (var contentEntry in operation.RequestBody.Content)
        {
            var schema = contentEntry.Value.Schema;

            // Direct array of binary - this is a multi-file upload
            if (schema is OpenApiSchema { Items: OpenApiSchema { Format: "binary" } } && schema.IsOfType(JsonSchemaType.Array))
            {
                return true;
            }

            // Note: Schema references to objects (like FilesAsFormDataRequest) that contain
            // file arrays should NOT be treated as direct file uploads - they use the Request pattern
        }

        return false;
    }

    /// <summary>
    /// Gets the schema for schema-based multipart/form-data requests.
    /// Returns the resolved schema if the request body is multipart/form-data (declared with any parameter and in any
    /// case) with a schema reference, otherwise returns null.
    /// </summary>
    private static IOpenApiSchema? GetSchemaBasedMultipartSchema(
        OpenApiOperation operation,
        OpenApiDocument openApiDoc)
    {
        if (operation.RequestBody?.Content is null)
        {
            return null;
        }

        // A model ($ref) under multipart/form-data, declared with any parameter and in any case
        if (FormBodyHelper.FindMultipartFormDataMediaType(operation.RequestBody.Content) is not { } multipartMediaType ||
            operation.RequestBody.Content[multipartMediaType].Schema is not { } schema ||
            schema.GetFileUploadInfo().IsFile)
        {
            return null;
        }

        return FormBodyHelper.ResolveModelSchema(schema, openApiDoc);
    }

    /// <summary>
    /// The model of a request body that is sent as a URL-encoded form: the operation's form body is
    /// <c>application/x-www-form-urlencoded</c> (not multipart, which carries files) and is not also JSON (JSON is
    /// sent then).
    /// </summary>
    private static IOpenApiSchema? UrlEncodedFormSchema(
        OpenApiOperation operation,
        OpenApiDocument openApiDoc)
    {
        if (operation.GetJsonRequestBodyMediaType() is not null ||
            operation.HasFileUpload() ||
            operation.GetFormBodyMediaType() is not { } formMediaType)
        {
            return null;
        }

        return FormBodyHelper.ResolveModelSchema(operation.RequestBody?.Content?[formMediaType].Schema, openApiDoc);
    }

    /// <summary>
    /// Generates the <c>FormUrlEncodedContent</c> of a form model: every field as its wire text, a list as repeated
    /// fields (<see cref="FormBodyHelper"/>).
    /// </summary>
    private static void GenerateUrlEncodedForm(
        StringBuilder builder,
        IOpenApiSchema schema,
        string requestAccess)
    {
        builder.AppendLine("var fields = new List<KeyValuePair<string, string>>();");
        FormBodyHelper.AppendUrlEncodedFields(builder, schema, requestAccess, "fields");
        builder.AppendLine("using var content = new FormUrlEncodedContent(fields);");
        builder.AppendLine();
    }

    /// <summary>
    /// Generates code for schema-based multipart/form-data requests.
    /// Creates MultipartFormDataContent with StringContent for text fields, StreamContent for binary fields and
    /// JsonContent for an object, a map or a composition.
    /// </summary>
    private static void GenerateSchemaBasedMultipartFormData(
        StringBuilder builder,
        IOpenApiSchema schema,
        IOpenApiMediaType? multipart,
        string requestAccess)
    {
        builder.AppendLine("using var content = new MultipartFormDataContent();");
        builder.AppendLine();

        // A file is a StreamContent with its own content type; a text field a StringContent; an object an
        // application/json part written through the client's serializer options (as a JSON body is), or an XML or a
        // URL-encoded part as a StringContent of its media type
        FormBodyHelper.AppendMultipartFields(
            builder,
            schema,
            multipart,
            requestAccess,
            new MultipartPartWriter(
                File: (sb, indent, name, file) =>
                {
                    sb.AppendLine(indent, $"var fileContent = new StreamContent({file}.OpenReadStream());");
                    sb.AppendLine();
                    sb.AppendLine(indent, $"if ({file}.ContentType is not null)");
                    sb.AppendLine(indent, "{");
                    sb.AppendLine(indent + 4, $"fileContent.Headers.ContentType = new MediaTypeHeaderValue({file}.ContentType);");
                    sb.AppendLine(indent, "}");
                    sb.AppendLine();
                    sb.AppendLine(indent, $"content.Add(fileContent, {CSharpLiteralHelper.ToStringLiteral(name)}, {file}.FileName);");
                },
                Text: (sb, indent, name, text) => sb.AppendLine(indent, $"content.Add(new StringContent({text}), {CSharpLiteralHelper.ToStringLiteral(name)});"),
                Json: (sb, indent, name, value) => sb.AppendLine(indent, $"content.Add(jsonSerializerOptions.ToJsonContent({value}), {CSharpLiteralHelper.ToStringLiteral(name)});"),
                Part: (sb, indent, name, text, mediaType) => sb.AppendLine(indent, $"content.Add(new StringContent({text}, global::System.Text.Encoding.UTF8, {CSharpLiteralHelper.ToStringLiteral(mediaType)}), {CSharpLiteralHelper.ToStringLiteral(name)});")));
    }
}