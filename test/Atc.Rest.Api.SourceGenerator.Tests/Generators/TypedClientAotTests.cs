namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A typed client is trim- and AOT-safe: every JSON read and write goes through the <c>JsonTypeInfo</c> its options
/// resolve - from the consumer's <c>JsonSerializerContext</c> in an AOT app - and the OAuth token response is read
/// from the JSON document. The scenarios cover the verbs, a media-typed JSON body, the Result style, streaming and
/// OAuth.
/// </summary>
[Trait("Category", "CompilationGate")]
[Collection(nameof(GeneratedCodeAotGateTests))]
public sealed class TypedClientAotTests
{
    public static TheoryData<string> Scenarios
        => ["Demo", "AsyncEnumerableHooks", "StreamingItemSchema", "HttpMethods", "TypedClientResultStyle", "SecurityStandard"];

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task TheTypedClient_HasNoTrimOrAotWarning(string scenario)
    {
        var sources = GeneratorSnapshotHarness.RunRawForCompilation(scenario, "Client-Typed");

        var problems = await AotAnalyzerHarness.AnalyzeAsync(sources, includeMinimalApi: false);

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}