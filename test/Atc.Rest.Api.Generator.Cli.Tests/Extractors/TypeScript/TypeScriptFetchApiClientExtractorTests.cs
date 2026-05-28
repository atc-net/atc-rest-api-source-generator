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

    [Fact]
    public void Generate_ZodRuntimeValidate_ImportsZodTypeAnyAndAddsParseSchemaOption()
    {
        // Wires the schema in via RequestOptions.parseSchema. ZodTypeAny is the broad
        // parent type — any z.object/z.array/primitive schema fits. Type-only import
        // keeps the bundle clean for consumers that never narrow into schemaMismatch.
        var result = TypeScriptFetchApiClientExtractor.Generate(
            headerContent: null,
            convertDates: false,
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
            convertDates: false,
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
            convertDates: false,
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
            convertDates: false,
            hasRetry: false,
            zodRuntimeValidate: false);

        Assert.DoesNotContain("ZodTypeAny", result, StringComparison.Ordinal);
        Assert.DoesNotContain("parseSchema", result, StringComparison.Ordinal);
        Assert.DoesNotContain("schemaMismatch", result, StringComparison.Ordinal);
        Assert.DoesNotContain("setStrictMode", result, StringComparison.Ordinal);
    }
}