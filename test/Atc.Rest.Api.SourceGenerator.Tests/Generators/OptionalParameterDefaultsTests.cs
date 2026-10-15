namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// An optional parameter of an operation is <c>= null</c> in its <c>{Operation}Parameters</c> record, as an optional
/// property of a model is: a caller names what it sends (<c>new ListEventsParameters(LastEventId: "2")</c>) instead of
/// writing out every optional parameter. Required parameters come first, as C# needs.
/// </summary>
public class OptionalParameterDefaultsTests
{
    private const string Yaml = """
        openapi: 3.1.1
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /feeds/{feedId}/events:
            get:
              operationId: listEvents
              parameters:
                - name: thingId
                  in: query
                  schema:
                    type: string
                    format: uuid
                - name: feedId
                  in: path
                  required: true
                  schema:
                    type: string
                - name: heartbeatSeconds
                  in: query
                  schema:
                    type: integer
                    format: int32
                - name: limit
                  in: query
                  schema:
                    type: integer
                    format: int32
                    default: 50
                - name: lastEventId
                  in: header
                  schema:
                    type: string
              responses:
                '204':
                  description: No events
        """;

    [Theory]
    [InlineData("Server")]
    [InlineData("EndpointPerOperation")]
    [InlineData("TypedClient")]
    public void AnOptionalParameter_IsNullUnlessNamed(string side)
    {
        var sources = side == "Server"
            ? ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", "{}", includeMinimalApi: false, Yaml)
            : ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", $$"""{ "generationMode": "{{side}}" }""", includeMinimalApi: false, Yaml);
        var usings = string.Join(
            Environment.NewLine,
            sources
                .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
                .Where(ns => ns.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Select(ns => $"using {ns};"));
        sources.Add(("Probe.cs", usings + Environment.NewLine + """
            namespace ErrorBodies.RuntimeTest;

            public static class Probe
            {
                public static string Describe()
                {
                    var onlyRequired = new ListEventsParameters("feed-1");
                    var named = new ListEventsParameters("feed-1", LastEventId: "2");
                    return $"{onlyRequired.FeedId}|{onlyRequired.ThingId?.ToString() ?? "null"}|{onlyRequired.HeartbeatSeconds?.ToString() ?? "null"}|{onlyRequired.Limit}|{named.LastEventId}";
                }
            }
            """));

        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var described = (string)assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!.GetMethod("Describe")!.Invoke(null, [])!;

        // The required path parameter first; the optional ones null, the one with a default its default
        Assert.Equal("feed-1|null|null|50|2", described);
    }
}