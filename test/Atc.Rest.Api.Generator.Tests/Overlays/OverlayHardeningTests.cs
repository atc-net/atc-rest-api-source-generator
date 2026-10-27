namespace Atc.Rest.Api.Generator.Tests.Overlays;

/// <summary>
/// The overlay engine on hostile input: a YAML alias bomb, a file of a hundred thousand blank lines, a JSONPath nested
/// past any reason or with a number no double holds, a copy of a node into itself, and a regular expression that
/// backtracks for seconds. Each is a reported problem or a quick answer - never an exception out of the engine, a
/// hang, or a selection that is silently wrong.
/// </summary>
public sealed class OverlayHardeningTests
{
    [Fact]
    public void Read_AnAliasBomb_IsAFormatException_AndIsOverlayDoesNotThrow()
    {
        // 10^5 nodes through five levels of aliases
        var bomb = """
            overlay: 1.2.0
            a: &a [x, x, x, x, x, x, x, x, x, x]
            b: &b [*a, *a, *a, *a, *a, *a, *a, *a, *a, *a]
            c: &c [*b, *b, *b, *b, *b, *b, *b, *b, *b, *b]
            d: &d [*c, *c, *c, *c, *c, *c, *c, *c, *c, *c]
            e: [*d, *d, *d, *d, *d, *d, *d, *d, *d, *d]
            """;

        var isOverlay = OverlayDocumentReader.IsOverlay(bomb);
        var read = Record.Exception(() => OverlayDocumentReader.Read(bomb));

        // Either the reader takes it (it is an overlay, read) or refuses it as a document it cannot read - never a crash
        Assert.True(read is null or FormatException, read?.GetType().FullName);
        Assert.Equal(read is null, isOverlay);
    }

    [Fact]
    public void IsOverlay_AHundredThousandBlankLines_AnswersAtOnce()
    {
        var blank = new string('\n', 100_000);
        var stopwatch = Stopwatch.StartNew();

        var specification = OverlayDocumentReader.IsOverlay(blank + "openapi: 3.0.3\ninfo: { title: x, version: '1' }\npaths: {}\n");
        var overlay = OverlayDocumentReader.IsOverlay(blank + "overlay: 1.2.0\ninfo: { title: x, version: '1' }\nactions: []\n");

        stopwatch.Stop();
        Assert.False(specification);
        Assert.True(overlay);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"took {stopwatch.Elapsed}");
    }

    [Theory]
    [InlineData("""{"overlay":"1.0.0","info":{"title":"Contoso","version":"1"},"actions":[]}""", true)]
    [InlineData("""{ "overlay": "1.0.0", "info": { "title": "Contoso", "version": "1" }, "actions": [] }""", true)]
    [InlineData("""{"info":{"title":"Contoso","version":"1"},"overlay":"1.0.0","actions":[]}""", true)]
    [InlineData("""{'overlay':'1.0.0','info':{'title':'Contoso','version':'1'},'actions':[]}""", true)]
    [InlineData("""{"openapi":"3.0.3","info":{"title":"Contoso","version":"1"},"paths":{},"x-overlay":{"overlay":"1"}}""", false)]
    [InlineData("""{"openapi":"3.0.3","overlay":"1.0.0","info":{"title":"Contoso","version":"1"},"paths":{}}""", false)]
    public void IsOverlay_JsonOnOneLine_IsToldByItsKeys(
        string content,
        bool expected)
        => Assert.Equal(expected, OverlayDocumentReader.IsOverlay(content));

    [Fact]
    public void Parse_AQueryNestedPastTheLimit_IsAJsonPathException()
    {
        var query = "$[?" + new string('(', 100) + "@.a == 1" + new string(')', 100) + "]";

        var exception = Assert.Throws<JsonPathException>(() => JsonPathQuery.Parse(query));

        Assert.Contains("nest", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ANumberNoDoubleHolds_IsAJsonPathException()
    {
        var exception = Assert.Throws<JsonPathException>(() => JsonPathQuery.Parse("$[?@.a == 1e999]"));

        Assert.Contains("1e999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Copy_ANodeIntoItself_IsNoException_AndMergesACopyOfIt()
    {
        var document = OverlayDocumentReader.Read("""
            openapi: 3.2.0
            paths:
              /a: { get: { tags: [t], responses: { '200': { description: OK } } } }
            """)!;
        var overlay = OverlayDocumentReader.Read("""
            overlay: 1.2.0
            info: { title: Copy into itself, version: 1.0.0 }
            actions:
              - target: $.paths
                copy: $.paths
            """);

        var result = OverlayApplier.Apply(document, overlay);

        // The merge rules on a copy of the node: objects merge, arrays concatenate, primitives are replaced
        Assert.False(result.HasErrors, string.Join("; ", result.Problems.Select(p => p.Message)));
        Assert.Equal("""{"/a":{"get":{"tags":["t","t"],"responses":{"200":{"description":"OK"}}}}}""", result.Document!["paths"]!.ToJsonString());
    }

    [Fact]
    public void Match_ARegularExpressionThatTimesOut_IsAnError_NotAFalseSelection()
    {
        // (a|aa)*. on fifty a's and two '!': the a's and one '!' match, the second '!' fails the full match, and every
        // way of splitting the a's is retried before the engine gives up - exponential, past the second allowed (a
        // missing literal or a wrong last character would be caught before matching; this is not)
        var document = OverlayDocumentReader.Read("""
            openapi: 3.2.0
            paths:
              /a: { get: { operationId: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!!' } }
            """)!;
        var overlay = OverlayDocumentReader.Read("""
            overlay: 1.2.0
            info: { title: A catastrophic regex, version: 1.0.0 }
            actions:
              - target: $.paths.*[?match(@.operationId, '(a|aa)*.')]
                update: { deprecated: true }
            """);

        var result = OverlayApplier.Apply(document, overlay);

        var problem = Assert.Single(result.Problems);
        Assert.True(problem.IsError, problem.Message);
        Assert.Contains("(a|aa)*.", problem.Message, StringComparison.Ordinal);
        Assert.Contains("time", problem.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegexCache_IsBounded()
    {
        var document = OverlayDocumentReader.Read("openapi: 3.2.0\npaths:\n  /a: { get: { operationId: x } }\n")!;
        for (var i = 0; i < 300; i++)
        {
            JsonPathQuery.Parse($"$.paths.*[?match(@.operationId, 'p{i}')]").Select(document);
        }

        var evaluator = typeof(JsonPathQuery).Assembly.GetType("Atc.Rest.Api.Generator.Overlays.JsonPathEvaluator", throwOnError: true)!;
        var cache = (System.Collections.ICollection)evaluator.GetField("RegexCache", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Assert.True(cache.Count <= 256, $"{cache.Count} cached regular expressions");
    }
}