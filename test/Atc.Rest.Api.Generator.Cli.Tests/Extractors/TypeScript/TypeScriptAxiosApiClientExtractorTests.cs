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
}