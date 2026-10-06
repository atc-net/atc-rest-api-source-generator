namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptAxiosApiClientExtractorTests
{
    [Fact]
    public void Generate_RequestOptions_IncludesTextInResponseTypeUnion()
    {
        var result = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null);

        Assert.Contains("responseType?: 'json' | 'blob' | 'text';", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_AxiosConfig_ForwardsTextResponseType()
    {
        // Axios picks the wire-level parser from its responseType config. For raw text
        // bodies the generated config must select 'text' so axios does NOT JSON.parse.
        var result = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null);

        Assert.Contains(
            "responseType: options?.responseType === 'blob' ? 'arraybuffer' : options?.responseType === 'text' ? 'text' : 'json'",
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithRetry_ResponseVariableTypedAsOptional()
    {
        // The wrapped retry path assigns `response` from inside an async closure. TS's
        // control-flow analysis can't prove the closure ran before the variable is read
        // for handleResponse. The fix types `response` as `AxiosResponse<T> | undefined`
        // and adds a runtime guard.
        var result = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null, hasRetry: true);

        Assert.Contains("let response: AxiosResponse<T> | undefined;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithRetry_GuardsAgainstUnassignedResponse()
    {
        // The guard transforms a static TS2454 into a runtime exception with a clear
        // message — better than the previous "response is undefined" deep in handleResponse.
        var result = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null, hasRetry: true);

        Assert.Contains(
            "throw new Error('retryWithBackoff resolved without executing the request');",
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithoutRetry_DoesNotEmitRetryGuard()
    {
        // The non-retry branch assigns `response` synchronously; the guard would be dead
        // code there. The retry path is the only place that needs it.
        var result = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null, hasRetry: false);

        Assert.DoesNotContain("retryWithBackoff resolved", result, StringComparison.Ordinal);
        Assert.DoesNotContain("let response: AxiosResponse<T> | undefined;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_HandleResponse_SurfacesParseErrorArm()
    {
        // The Axios variant uses axios's built-in JSON parsing (via responseType: 'json'),
        // but the parsed body can still be invalid — Axios reports the parse error via
        // the response in a separate path. Either way the generated handleResponse needs
        // to surface the discriminated 'parseError' arm when response.data is not the
        // expected shape and a JSON parse failure can be detected.
        var result = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null);

        Assert.Contains("status: 'parseError'", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithRetry_DoRequestAcceptsPerAttemptSignal()
    {
        // Each retry attempt needs a fresh AbortSignal so policy.timeoutMs actually
        // cancels axios. doRequest accepts an optional attemptSignal and falls back to
        // options?.signal for the non-retry call site (which still passes no argument).
        var result = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null, hasRetry: true);

        Assert.Contains("const doRequest = (attemptSignal?: AbortSignal)", result, StringComparison.Ordinal);
        Assert.Contains("signal: attemptSignal ?? options?.signal", result, StringComparison.Ordinal);
        Assert.Contains("await doRequest(attemptSignal)", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_StreamHeadersLoop_GuardsAgainstUndefinedValue()
    {
        // RequestOptions.headers accepts `string | number | boolean | undefined` (axios
        // gives the caller flexibility), but Headers.set requires a string. The loop now
        // skips undefined and coerces with String(...) — matching the same guard used in
        // the axios request() method and the fetch requestStream().
        var result = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null);

        Assert.Contains(
            "if (value !== undefined) {",
            result,
            StringComparison.Ordinal);
        Assert.Contains(
            "headers.set(key, String(value));",
            result,
            StringComparison.Ordinal);

        // Regression-guard for the original buggy emission shape.
        Assert.DoesNotContain("headers.set(key, value);", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ZodRuntimeValidate_ImportsZodTypeAnyAndValidatesAxiosData()
    {
        // Axios already parsed the JSON into response.data; the validation runs on
        // that directly. We re-wrap parsed.data into the response when validation
        // succeeds so the typed `data: T` field on the ApiResult arm reflects the
        // Zod-parsed shape rather than the raw parse.
        var result = TypeScriptAxiosApiClientExtractor.Generate(
            headerContent: null,
            hasRetry: false,
            zodRuntimeValidate: true);

        Assert.Contains("import type { ZodTypeAny } from 'zod';", result, StringComparison.Ordinal);
        Assert.Contains("parseSchema?: ZodTypeAny;", result, StringComparison.Ordinal);
        Assert.Contains("if (parseSchema && expectsJson) {", result, StringComparison.Ordinal);
        Assert.Contains("parseSchema.safeParse(response.data);", result, StringComparison.Ordinal);
        Assert.Contains("return { status: 'schemaMismatch', issues: parsed.error.issues, data: response.data, response };", result, StringComparison.Ordinal);
        Assert.Contains("setStrictMode(enabled: boolean): void {", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ZodRuntimeValidateDisabled_AxiosOutputUnchanged()
    {
        // Regression guard: flag off means byte-identical Axios output. Lock the
        // four signature tokens that would change under the flag.
        var result = TypeScriptAxiosApiClientExtractor.Generate(
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
        var result = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null);

        Assert.Contains(
            "query?: Record<string, string | number | boolean | (string | number | boolean)[] | undefined>;",
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_AxiosCreate_ConfiguresParamsSerializerForRepeatedKeys()
    {
        // Axios 1.x default serializes arrays as tags[]=a&tags[]=b.
        // ASP.NET Core expects repeated keys (tags=a&tags=b), so we configure
        // paramsSerializer: { indexes: null } to suppress the bracket notation.
        var result = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null);

        Assert.Contains("paramsSerializer: { indexes: null },", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_BuildUrl_UsesAppendForArrayValuesAndSetForScalars()
    {
        // The streaming path uses buildUrl (not axios params). Array query params must
        // produce repeated keys (?tags=a&tags=b) there too.
        var result = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null);

        Assert.Contains("Array.isArray(value)", result, StringComparison.Ordinal);
        Assert.Contains("url.searchParams.append(key, String(item));", result, StringComparison.Ordinal);
        Assert.Contains("url.searchParams.set(key, String(value));", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_BuildUrl_SkipsUndefinedBeforeArrayCheck()
    {
        // The undefined-guard must come before the Array.isArray branch so the
        // loop does not try to iterate undefined.
        var result = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null);

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
        var result = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null);

        // Must strip CRs in the SSE branch (before or after decoding)
        Assert.Contains(".replace(/\\r/g, '')", result, StringComparison.Ordinal);
    }

    // ========== multipart incremental consumption ==========
    [Fact]
    public void Generate_MultipartParser_YieldsPartsIncrementally()
    {
        // The multipart branch must not buffer the entire body before yielding parts.
        // Instead it scans the rolling buffer for complete delimiter-bounded parts and
        // yields each one as soon as it is complete (incremental streaming).
        var result = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null);

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
        var result = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null);

        Assert.Contains("async function* parseEventStream<T>(", result, StringComparison.Ordinal);
        Assert.Contains("yield* parseEventStream<T>(reader, decoder);", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_RequestStream_CancelsABodyTheConsumerStoppedReading()
    {
        // A consumer that breaks out of `for await` runs the generator's finally: the body must be cancelled
        // (not only unlocked) so the server sees the disconnect instead of sending until the process ends.
        var result = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null);

        Assert.Contains("async function releaseReader(reader: ReadableStreamDefaultReader<Uint8Array>, finished: boolean): Promise<void> {", result, StringComparison.Ordinal);
        Assert.Contains("await reader.cancel().catch(() => undefined);", result, StringComparison.Ordinal);
        Assert.Equal(3, result.Split("await releaseReader(reader, finished);").Length - 1);
        Assert.DoesNotContain("    reader.releaseLock();\n    }", result.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
    }
}