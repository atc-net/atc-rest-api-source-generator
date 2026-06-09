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
    public void Generate_ConvertDates_AnchorsIsoDateRegexToWholeString()
    {
        // The reviver must only convert strings that are ENTIRELY an ISO datetime.
        // An end-anchored regex prevents free-text fields like
        // "2026-06-01T12:30:45Z [INFO] hello" (which merely start with a date) from
        // being silently turned into Date objects.
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null, convertDates: true);

        Assert.Contains(
            "const ISO_DATE_RE = /^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}(:\\d{2})?(\\.\\d+)?(Z|[+-]\\d{2}:\\d{2})?$/;",
            result,
            StringComparison.Ordinal);

        // Regression guard for the buggy prefix-only regex.
        Assert.DoesNotContain("/^\\d{4}-\\d{2}-\\d{2}(T\\d{2}:\\d{2})/;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ConvertDates_GuardsAgainstInvalidDate()
    {
        // Even when the regex matches, new Date(value) can yield an Invalid Date.
        // The reviver must return the original string in that case rather than a
        // corrupt Date object.
        var result = TypeScriptFetchApiClientExtractor.Generate(headerContent: null, convertDates: true);

        Assert.Contains("const parsed = new Date(value);", result, StringComparison.Ordinal);
        Assert.Contains(
            "return Number.isNaN(parsed.getTime()) ? value : parsed;",
            result,
            StringComparison.Ordinal);

        // Regression guard: the old reviver returned the Date unconditionally.
        Assert.DoesNotContain("    return new Date(value);", result, StringComparison.Ordinal);
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

        var continueIdx = result.IndexOf("continue;", StringComparison.Ordinal);
        var arrayIdx = result.IndexOf("Array.isArray(value)", StringComparison.Ordinal);
        Assert.True(continueIdx > 0 && arrayIdx > continueIdx, "The 'continue' (undefined guard) must appear before Array.isArray.");
    }
}