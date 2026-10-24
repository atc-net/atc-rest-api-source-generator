// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable CommentTypo
// ReSharper disable InvertIf
// ReSharper disable LoopCanBeConvertedToQuery
namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// The streaming and caching rules (ATC_API_STREAM, ATC_API_CACHE).
/// </summary>
public static partial class OpenApiDocumentValidator
{
    /// <summary>
    /// Emits ATC_API_CACHE001 when output caching is configured on an operation whose HTTP method
    /// the ASP.NET Core output-cache middleware never caches.
    /// </summary>
    /// <remarks>
    /// The middleware's default policy stores only GET and HEAD responses, so the generated
    /// <c>.CacheOutput(...)</c> call silently does nothing on any other verb — the worst kind of
    /// gap, because the endpoint looks configured. The call is still emitted, because a consumer
    /// can override the base policy to opt a verb in, but the build now says so.
    /// <para>
    /// For an OpenAPI 3.2 <c>query:</c> operation there is a second problem even after opting in:
    /// the middleware keys cache entries on the URL alone, and a QUERY carries its criteria in the
    /// request body — so every distinct criteria set would collide on one entry.
    /// </para>
    /// HybridCache is unaffected: it runs inside the handler, not in the middleware.
    /// </remarks>
    private static void ValidateOutputCacheMethod(OperationContext context)
    {
        var (diagnostics, sourceFilePath, document, pathItem, httpMethodUpper, operation, operationId) = (context.Diagnostics, context.SourceFilePath, context.Document, context.PathItem, context.HttpMethodUpper, context.Operation, context.OperationId);

        if (httpMethodUpper is "GET" or "HEAD")
        {
            return;
        }

        var cacheConfiguration = operation.ExtractCacheConfiguration(pathItem, document);
        if (cacheConfiguration is null ||
            !cacheConfiguration.Enabled ||
            cacheConfiguration.Type != CacheType.Output)
        {
            return;
        }

        diagnostics.Add(new DiagnosticMessage(
            RuleId: RuleIdentifiers.OutputCacheOnNonCacheableMethod,
            Message: $"Output caching is configured on operation '{operationId}', but its HTTP method " +
                     $"is {httpMethodUpper}. The ASP.NET Core output-cache middleware caches only GET and " +
                     "HEAD responses by default, so the generated '.CacheOutput(...)' call has no effect. " +
                     "The middleware also keys cache entries on the URL only, so it cannot distinguish two " +
                     "requests that differ by request body. Use 'x-cache-type: hybrid' and cache inside the " +
                     "handler instead, or override the output-cache base policy to opt this method in.",
            Severity: DiagnosticSeverity.Warning,
            FilePath: sourceFilePath));
    }

    /// <summary>
    /// Emits ATC_API_CACHE002 when output caching is configured on a GET or HEAD operation that requires an
    /// authenticated caller and does not say that its answer is the same for every caller (<c>x-cache-shared</c>).
    /// </summary>
    /// <remarks>
    /// The output-cache middleware's default policy, which a named policy starts from, never caches a request that
    /// carries an <c>Authorization</c> header or has an authenticated user: an answer may be the caller's own. On such an
    /// operation the generated <c>.CacheOutput(...)</c> therefore caches nothing, with no word at build or run time.
    /// Another verb is ATC_API_CACHE001's.
    /// </remarks>
    private static void ValidateOutputCacheAuthentication(
        OperationContext context)
    {
        var (diagnostics, sourceFilePath, document, pathItem, httpMethodUpper, operation, operationId) = (context.Diagnostics, context.SourceFilePath, context.Document, context.PathItem, context.HttpMethodUpper, context.Operation, context.OperationId);

        if (httpMethodUpper is not ("GET" or "HEAD"))
        {
            return;
        }

        var cacheConfiguration = operation.ExtractCacheConfiguration(pathItem, document);
        if (cacheConfiguration is null ||
            !cacheConfiguration.Enabled ||
            cacheConfiguration.Type != CacheType.Output ||
            cacheConfiguration.Shared)
        {
            return;
        }

        var security = operation.ExtractUnifiedSecurityConfiguration(pathItem, document);
        if (!security.AuthenticationRequired || security.AllowAnonymous)
        {
            return;
        }

        diagnostics.Add(new DiagnosticMessage(
            RuleId: RuleIdentifiers.OutputCacheOnAuthenticatedOperation,
            Message: $"Output caching is configured on operation '{operationId}', which requires an authenticated " +
                     "caller. ASP.NET Core's output-cache default policy never caches a request with an " +
                     "Authorization header or an authenticated user, so nothing is cached. If the answer is the " +
                     "same for every caller, add 'x-cache-shared: true'; if it is the caller's own, use " +
                     "'x-cache-type: hybrid' and cache inside the handler, or remove the output cache.",
            Severity: DiagnosticSeverity.Warning,
            FilePath: sourceFilePath));
    }

    /// <summary>
    /// Emits ATC_API_STREAM002 (Error) for a request body declared only as Server-Sent Events or
    /// <c>multipart/mixed</c>, and ATC_API_STREAM003 (Error) for a JSON Lines or JSON Text Sequence request body without
    /// the schema of its records - bodies no generator would send or read. Operations and webhooks.
    /// </summary>
    private static void ValidateStreamedRequestBodies(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        foreach (var (name, pathItem, isWebhook) in PathItemsAndWebhooks(document))
        {
            if (pathItem?.Operations is null)
            {
                continue;
            }

            foreach (var operationEntry in pathItem.Operations)
            {
                var operation = operationEntry.Value;
                if (operation?.RequestBody?.Content is not { Count: > 0 } content)
                {
                    continue;
                }

                var location = $"{operationEntry.Key.ToString().ToUpperInvariant()} {name}";
                var scope = isWebhook ? null : SpecSuppressions.Operation(operationEntry.Key.Method, name);
                foreach (var entry in content)
                {
                    var framing = StreamingMediaType.Classify(entry.Key);
                    if (framing is StreamingFraming.JsonLines or StreamingFraming.JsonSequence &&
                        entry.Value?.Schema is null &&
                        entry.Value?.ItemSchema is null)
                    {
                        diagnostics.Add(new DiagnosticMessage(
                            RuleId: RuleIdentifiers.StreamedRequestBodyWithoutRecordSchema,
                            Message: $"The request body of {location} is '{entry.Key}' without the schema of its records: the records cannot be typed, so the body would be dropped. " +
                                     "Declare a 'schema' (one record, or an array of them) or an OpenAPI 3.2 'itemSchema'.",
                            Severity: DiagnosticSeverity.Error,
                            FilePath: sourceFilePath,
                            Scope: scope));
                    }
                }

                // Only an event stream or multipart/mixed: nothing sends it - a JSON (or other) body beside it is sent
                if (content.Keys.All(key => StreamingMediaType.Classify(key) is StreamingFraming.ServerSentEvents or StreamingFraming.MultipartMixed))
                {
                    diagnostics.Add(new DiagnosticMessage(
                        RuleId: RuleIdentifiers.StreamingRequestBodyFramingUnsupported,
                        Message: $"The request body of {location} is only '{string.Join("', '", content.Keys)}': no generated client sends it and the server does not read it, so it would be dropped. " +
                                 "Send a stream of records as JSON Lines ('application/jsonl') or a JSON Text Sequence ('application/json-seq').",
                        Severity: DiagnosticSeverity.Error,
                        FilePath: sourceFilePath,
                        Scope: scope));
                }
            }
        }
    }

    /// <summary>
    /// Emits ATC_API_STREAM001 (Info) when any response media type declares <c>prefixEncoding</c>.
    /// The generator does not yet emit per-prefix encoding headers; the field is silently ignored.
    /// </summary>
    private static void ValidateStreamingEncodings(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        if (document.Paths is null)
        {
            return;
        }

        foreach (var pathEntry in document.Paths)
        {
            var pathItem = pathEntry.Value;
            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operationEntry in pathItem.Operations)
            {
                var operation = operationEntry.Value;
                if (operation.Responses is null)
                {
                    continue;
                }

                foreach (var response in operation.Responses.Values)
                {
                    if (response.Content is null)
                    {
                        continue;
                    }

                    foreach (var contentEntry in response.Content)
                    {
                        var mediaTypeKey = contentEntry.Key;
                        var mediaTypeValue = contentEntry.Value;

                        if (mediaTypeValue.PrefixEncoding is not { Count: > 0 })
                        {
                            continue;
                        }

                        diagnostics.Add(new DiagnosticMessage(
                            RuleId: RuleIdentifiers.StreamingPrefixEncodingUnsupported,
                            Message: $"Media type '{mediaTypeKey}' on operation '{operation.OperationId}' declares 'prefixEncoding' " +
                                     "which is not yet supported by the generator. The field will be ignored.",
                            Severity: DiagnosticSeverity.Info,
                            FilePath: sourceFilePath,
                            Scope: SpecSuppressions.Operation(operationEntry.Key.Method, pathEntry.Key)));
                    }
                }
            }
        }
    }
}