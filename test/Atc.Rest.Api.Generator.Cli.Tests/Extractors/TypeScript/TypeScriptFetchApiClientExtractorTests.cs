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
}