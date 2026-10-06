namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptFetchApiClientExtractorTests
{
    [Fact]
    public void Generate_RequestOptions_IncludesTextInResponseTypeUnion()
    {
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null);

        // The exposed RequestOptions.responseType must offer 'text' so callers (and the
        // per-operation client emitter) can opt in for text/plain responses.
        Assert.Contains("responseType?: 'json' | 'blob' | 'text';", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_HandleResponse_ReadsBodyAsTextWhenResponseTypeIsText()
    {
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null);

        // The handleResponse branch must call response.text() when the caller asked for
        // text. Otherwise the raw body silently degrades to a Blob.
        Assert.Contains("await response.text()", result, StringComparison.Ordinal);
        Assert.Contains("isText", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_HandleResponse_SniffsContentTypeForTextWhenResponseTypeAbsent()
    {
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null);

        // When the per-operation method does NOT pass responseType but the server returns
        // text/* or application/xml, handleResponse must still read as text.
        Assert.Contains("contentType.startsWith('text/')", result, StringComparison.Ordinal);
        Assert.Contains("contentType.includes('application/xml')", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_HandleResponse_KeepsJsonAndBlobBranches()
    {
        // Regression: the new text branch must NOT break the existing JSON / Blob paths.
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null);

        Assert.Contains("await response.json()", result, StringComparison.Ordinal);
        Assert.Contains("await response.blob()", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_SuccessPath_WrapsJsonParseInTryCatch()
    {
        // A 200 OK response with malformed JSON used to bubble a raw SyntaxError. Wrap
        // the parse so the discriminated 'parseError' ApiResult arm fires instead. Only a
        // malformed body is a parseError: an aborted or cut-off body read rejects like the request.
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null);

        Assert.Contains(
            "return { status: 'parseError', error: parseError, response };",
            result,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (!(parseError instanceof SyntaxError)) {\n          throw parseError;",
            result.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithRetry_ReadsTheBodyInsideTheAttempt()
    {
        // The response interceptors and handleResponse run inside the attempt, so the per-attempt
        // timeout and the caller's signal cover the body read, not only the request.
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null, hasRetry: true);

        Assert.Contains("(response) => this.readResponse<T>(response, options),", result, StringComparison.Ordinal);
        Assert.Contains("return this.readResponse<T>(await fetch(url, init), options);", result, StringComparison.Ordinal);
        Assert.Contains("private async readResponse<T>(response: Response, options?: RequestOptions): Promise<ApiResult<T>> {", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithRetry_PassesPerAttemptSignalToFetch()
    {
        // Retry path must hand fetch the per-attempt signal so policy.timeoutMs can
        // actually cancel an in-flight request. Spreading init then overriding signal
        // keeps caller-supplied init fields intact.
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null, hasRetry: true);

        Assert.Contains(
            "(attemptSignal) => fetch(url, { ...init, signal: attemptSignal })",
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithoutRetry_DoesNotUseRetryWrapper()
    {
        // Regression guard: don't accidentally emit the retry path for clients that have
        // no retry policy declared.
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null, hasRetry: false);

        Assert.DoesNotContain("retryWithBackoff", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ZodRuntimeValidate_ImportsZodTypeAnyAndAddsParseSchemaOption()
    {
        // Wires the schema in via RequestOptions.parseSchema. ZodTypeAny is the broad
        // parent type — any z.object/z.array/primitive schema fits. Type-only import
        // keeps the bundle clean for consumers that never narrow into schemaMismatch.
        var result = TypeScriptFetchApiClientExtractor.Generate(
            headerContent: null,
            hasRetry: false,
            zodRuntimeValidate: true);

        Assert.Contains("import type { ZodTypeAny } from 'zod';", result, StringComparison.Ordinal);
        Assert.Contains("parseSchema?: ZodTypeAny;", result, StringComparison.Ordinal);
        Assert.Contains("parseSchema?: ZodTypeAny): Promise<ApiResult<T>>", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ZodRuntimeValidate_ValidatesJsonAndReturnsSchemaMismatchArm()
    {
        // The validation runs only for JSON responses (text/blob have no structured
        // schema) and surfaces parsed.error.issues + the raw data so consumers can
        // diagnose spec drift without losing the payload.
        var result = TypeScriptFetchApiClientExtractor.Generate(
            headerContent: null,
            hasRetry: false,
            zodRuntimeValidate: true);

        Assert.Contains("if (parseSchema && isJson) {", result, StringComparison.Ordinal);
        Assert.Contains("const parsed = parseSchema.safeParse(data);", result, StringComparison.Ordinal);
        Assert.Contains("return { status: 'schemaMismatch', issues: parsed.error.issues, data, response };", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ZodRuntimeValidate_EmitsSetStrictModeMethod()
    {
        // setStrictMode flips between the prod-friendly "return arm" path and the
        // dev-friendly "throw with issues" path. Default false matches the existing
        // ApiResult contract of errors-as-values.
        var result = TypeScriptFetchApiClientExtractor.Generate(
            headerContent: null,
            hasRetry: false,
            zodRuntimeValidate: true);

        Assert.Contains("private strictMode = false;", result, StringComparison.Ordinal);
        Assert.Contains("setStrictMode(enabled: boolean): void {", result, StringComparison.Ordinal);
        Assert.Contains("throw new Error(`Schema mismatch:", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ZodRuntimeValidateDisabled_OmitsAllValidationCode()
    {
        // Regression guard: flag off must produce zero zod-related output so the
        // existing 150+ client snapshots stay byte-identical.
        var result = TypeScriptFetchApiClientExtractor.Generate(
            headerContent: null,
            hasRetry: false,
            zodRuntimeValidate: false);

        Assert.DoesNotContain("ZodTypeAny", result, StringComparison.Ordinal);
        Assert.DoesNotContain("parseSchema", result, StringComparison.Ordinal);
        Assert.DoesNotContain("schemaMismatch", result, StringComparison.Ordinal);
        Assert.DoesNotContain("setStrictMode", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_RequestOptions_QueryAcceptsArrays()
    {
        // The query Record must accept arrays so that array params (e.g. tags: string[])
        // can be passed without a TypeScript type error.
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null);

        Assert.Contains(
            "query?: Record<string, string | number | boolean | (string | number | boolean)[] | undefined>;",
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_BuildUrl_UsesAppendForArrayValuesAndSetForScalars()
    {
        // Array query params must produce repeated keys (?tags=a&tags=b), not a single
        // joined or overwritten key (?tags=a%2Cb). This requires an Array.isArray branch
        // with searchParams.append per element, while scalars keep searchParams.set.
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null);

        Assert.Contains("Array.isArray(value)", result, StringComparison.Ordinal);
        Assert.Contains("url.searchParams.append(key, String(item));", result, StringComparison.Ordinal);
        Assert.Contains("url.searchParams.set(key, String(value));", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_BuildUrl_SkipsUndefinedBeforeArrayCheck()
    {
        // The undefined-guard must come before the Array.isArray branch so the
        // loop does not try to iterate undefined.
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null);

        // Scope the search to the buildUrl method definition so the assertion cannot be
        // satisfied by an unrelated 'continue;' elsewhere in the generated file
        // (e.g. the streaming/multipart parser).
        var buildUrlIdx = result.IndexOf("buildUrl(path: string,", StringComparison.Ordinal);
        Assert.True(buildUrlIdx >= 0, "buildUrl method must be generated.");
        var buildUrlBody = result.Substring(buildUrlIdx);

        var undefinedGuardIdx = buildUrlBody.IndexOf("value === undefined", StringComparison.Ordinal);
        var continueIdx = buildUrlBody.IndexOf("continue;", StringComparison.Ordinal);
        var arrayIdx = buildUrlBody.IndexOf("Array.isArray(value)", StringComparison.Ordinal);
        Assert.True(undefinedGuardIdx >= 0, "buildUrl must contain the 'value === undefined' guard.");
        Assert.True(continueIdx > undefinedGuardIdx, "The 'continue' must follow the undefined guard within buildUrl.");
        Assert.True(arrayIdx > continueIdx, "The undefined guard must appear before Array.isArray within buildUrl.");
    }

    // ========== SSE parser LF-only ==========
    [Fact]
    public void Generate_SseParser_StripsCarriageReturnChars()
    {
        // SSE spec mandates LF-only line endings; real-world servers may send CRLF.
        // The generated SSE parser must strip CR chars so events delimited by \r\n\n
        // or \r\n\r\n are still parsed correctly.
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null);

        Assert.Contains(".replace(/\\r/g, '')", result, StringComparison.Ordinal);
    }

    // ========== multipart incremental consumption ==========
    [Fact]
    public void Generate_MultipartParser_YieldsPartsIncrementally()
    {
        // The multipart branch must not buffer the entire body before yielding parts.
        // Instead it scans the rolling buffer for complete delimiter-bounded parts and
        // yields each one as soon as it is complete (incremental streaming).
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null);

        // Incremental approach: inner loop advances on each complete part found so far.
        Assert.Contains("buf.startsWith(delimiter)", result, StringComparison.Ordinal);

        // Must NOT buffer the whole body first.
        Assert.DoesNotContain("let all = ''", result, StringComparison.Ordinal);
    }

    // ========== SSE parser dedup ==========
    [Fact]
    public void Generate_SseParser_ExtractsParseEventStreamHelper()
    {
        // The SSE parsing block is identical in Axios and Fetch clients. It is extracted
        // to a module-level parseEventStream generator so the logic lives in one place
        // and the requestStream SSE branch delegates to it via yield*.
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null);

        Assert.Contains("async function* parseEventStream<T>(", result, StringComparison.Ordinal);
        Assert.Contains("yield* parseEventStream<T>(reader, decoder);", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_RequestStream_CancelsABodyTheConsumerStoppedReading()
    {
        // A consumer that breaks out of `for await` runs the generator's finally: the body must be cancelled
        // (not only unlocked) so the server sees the disconnect instead of sending until the process ends.
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null);

        Assert.Contains("async function releaseReader(reader: ReadableStreamDefaultReader<Uint8Array>, finished: boolean): Promise<void> {", result, StringComparison.Ordinal);
        Assert.Contains("await reader.cancel().catch(() => undefined);", result, StringComparison.Ordinal);
        Assert.Equal(3, result.Split("await releaseReader(reader, finished);").Length - 1);
        Assert.DoesNotContain("    reader.releaseLock();\n    }", result.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
    }
}