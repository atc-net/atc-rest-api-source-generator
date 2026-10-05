namespace Atc.Rest.Api.Generator.Tests.Overlays;

/// <summary>
/// The JSONPath an Overlay's targets are written in is RFC 9535, and the Overlay Specification requires it in full.
/// Every case of the official compliance test suite (jsonpath-standard/jsonpath-compliance-test-suite, commit
/// 9d1a415, BSD-2 - <c>TestData/jsonpath-cts.LICENSE</c>) passes: a valid query selects exactly the expected nodes
/// at exactly the expected locations, and an invalid one is refused when it is parsed.
/// </summary>
public sealed class JsonPathComplianceTests
{
    private static readonly Lazy<JsonArray> Cases = new(() =>
        (JsonArray)JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Overlays", "TestData", "jsonpath-cts.json")))!["tests"]!);

    public static TheoryData<int, string> CaseNames()
    {
        var data = new TheoryData<int, string>();
        for (var i = 0; i < Cases.Value.Count; i++)
        {
            data.Add(i, (string)Cases.Value[i]!["name"]!);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Case_SelectsWhatTheStandardSays(
        int index,
        string name)
    {
        var testCase = Cases.Value[index]!.AsObject();
        var selector = (string)testCase["selector"]!;

        if (testCase["invalid_selector"] is not null)
        {
            var exception = Assert.Throws<JsonPathException>(() => JsonPathQuery.Parse(selector));
            Assert.False(string.IsNullOrEmpty(exception.Message), name);
            return;
        }

        var matches = JsonPathQuery.Parse(selector).Select(testCase["document"]?.DeepClone());
        var values = new JsonArray([.. matches.Select(m => m.Value?.DeepClone())]);
        var paths = matches.Select(m => m.NormalizedPath).ToList();

        if (testCase["result"] is JsonArray expected)
        {
            Assert.True(JsonNode.DeepEquals(expected, values), $"{name}: expected {expected.ToJsonString()}, got {values.ToJsonString()}");
            Assert.Equal(((JsonArray)testCase["result_paths"]!).Select(p => (string)p!), paths, StringComparer.Ordinal);
            return;
        }

        // Several orders are correct (an object's members have no order)
        var alternatives = (JsonArray)testCase["results"]!;
        var alternativePaths = (JsonArray)testCase["results_paths"]!;
        var matched = Enumerable.Range(0, alternatives.Count).FirstOrDefault(
            i => JsonNode.DeepEquals(alternatives[i], values) &&
                 ((JsonArray)alternativePaths[i]!).Select(p => (string)p!).SequenceEqual(paths, StringComparer.Ordinal),
            -1);
        Assert.True(matched >= 0, $"{name}: got {values.ToJsonString()} at {string.Join(", ", paths)}");
    }
}