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
            "responseType: options?.responseType === 'blob' ? 'blob' : options?.responseType === 'text' ? 'text' : 'json'",
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithRetry_ResponseVariableTypedAsOptional()
    {
        // The wrapped retry path assigns `response` from inside an async closure. TS's
        // control-flow analysis can't prove the closure ran before the variable is read
        // for handleResponse. The fix types `response` as `AxiosResponse<T> | undefined`
        // and adds a runtime guard — see issues/001-feedback.md "Out of scope" §.
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
}