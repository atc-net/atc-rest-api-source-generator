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
        // the parse so the discriminated 'parseError' ApiResult arm fires instead.
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null);

        Assert.Contains(
            "return { status: 'parseError', error: parseError as Error, response };",
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithRetry_PassesPerAttemptSignalToFetch()
    {
        // Retry path must hand fetch the per-attempt signal so policy.timeoutMs can
        // actually cancel an in-flight request. Spreading init then overriding signal
        // keeps caller-supplied init fields intact.
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null, convertDates: false, hasRetry: true);

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
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null, convertDates: false, hasRetry: false);

        Assert.DoesNotContain("retryWithBackoff", result, StringComparison.Ordinal);
    }
}