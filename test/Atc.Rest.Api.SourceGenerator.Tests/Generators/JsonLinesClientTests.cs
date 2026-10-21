namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The C# clients send a streamed request body from an <c>IAsyncEnumerable&lt;T&gt;</c> - one record per line (JSON Lines)
/// or per record separator (JSON Text Sequences) - and read a streamed answer on any verb: the generated server on
/// Kestrel, called by the generated TypedClient and EndpointPerOperation clients.
/// </summary>
public sealed class JsonLinesClientTests
{
    private const string Records = """
        public static class ReadingSource
        {
            public static async System.Collections.Generic.IAsyncEnumerable<Reading> Of(params string[] deviceIds)
            {
                foreach (var deviceId in deviceIds)
                {
                    await System.Threading.Tasks.Task.Yield();
                    yield return new Reading(deviceId, 1);
                }
            }

            public static async System.Collections.Generic.IAsyncEnumerable<Reading> WithAnInvalidSecond()
            {
                yield return new Reading("a", 1);
                await System.Threading.Tasks.Task.Yield();
                yield return new Reading("b", 500);
            }

            public static async System.Threading.Tasks.Task<string> JoinAsync(System.Collections.Generic.IAsyncEnumerable<Reading> readings)
            {
                var ids = new System.Collections.Generic.List<string>();
                await foreach (var reading in readings)
                {
                    ids.Add(reading.DeviceId);
                }

                return string.Join(",", ids);
            }
        }
        """;

    private const string TypedProbe = $$"""
        namespace ErrorBodies.RuntimeTest;

        {{Records}}

        public static class JsonLinesProbe
        {
            public static async System.Threading.Tasks.Task<System.Collections.Generic.List<string>> RunAsync(System.Uri baseAddress)
            {
                var client = new ErrorBodiesClient(new System.Net.Http.HttpClient { BaseAddress = baseAddress });
                var rows = new System.Collections.Generic.List<string>
                {
                    "import " + await client.ImportReadingsAsync(new ImportReadingsParameters(ReadingSource.Of("a", "b", "c"))),
                    "list " + await client.ImportReadingsAsync(new ImportReadingsParameters(System.Linq.AsyncEnumerable.ToAsyncEnumerable(new[] { new Reading("x", 1) }))),
                    "sequence " + await client.ImportReadingSequenceAsync(new ImportReadingSequenceParameters(ReadingSource.Of("a", "b"))),
                    "either " + await client.ImportReadingsAnyFormatAsync(new ImportReadingsAnyFormatParameters(ReadingSource.Of("a"))),
                    "optional " + await client.UpdateReadingsAsync(new UpdateReadingsParameters(null)),
                    "normalize " + await ReadingSource.JoinAsync(client.NormalizeReadingsAsync(new NormalizeReadingsParameters(ReadingSource.Of("a", "b")))),
                    "export " + await ReadingSource.JoinAsync(client.ListReadingsAsync()),
                };

                try
                {
                    await client.ImportReadingsAsync(new ImportReadingsParameters(ReadingSource.WithAnInvalidSecond()));
                    rows.Add("invalid accepted");
                }
                catch (System.Net.Http.HttpRequestException ex)
                {
                    rows.Add("invalid " + (int?)ex.StatusCode);
                }

                // The content is sent once: a second send (a retry, a redirect) would read the source again
                var content = global::ErrorBodies.Generated.Streaming.StreamedRequestContent.Create(ReadingSource.Of("a"), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web), "application/jsonl");
                using var first = new System.IO.MemoryStream();
                await content.CopyToAsync(first);
                try
                {
                    await content.CopyToAsync(new System.IO.MemoryStream());
                    rows.Add("second send allowed");
                }
                catch (System.InvalidOperationException)
                {
                    rows.Add("second send refused, first was " + System.Text.Encoding.UTF8.GetString(first.ToArray()).TrimEnd('\n'));
                }

                return rows;
            }
        }
        """;

    private const string EndpointProbe = $$"""
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        {{Records}}

        public static class JsonLinesProbe
        {
            public static async System.Threading.Tasks.Task<System.Collections.Generic.List<string>> RunAsync(System.Uri baseAddress)
            {
                var services = new ServiceCollection();
                services.AddHttpClient(Constants.HttpClientName, c => c.BaseAddress = baseAddress);
                services.AddErrorBodiesEndpoints();
                using var provider = services.BuildServiceProvider();

                var import = await provider.GetRequiredService<IImportReadingsEndpoint>().ExecuteAsync(new ImportReadingsParameters(ReadingSource.Of("a", "b", "c")));
                var sequence = await provider.GetRequiredService<IImportReadingSequenceEndpoint>().ExecuteAsync(new ImportReadingSequenceParameters(ReadingSource.Of("a", "b")));
                var optional = await provider.GetRequiredService<IUpdateReadingsEndpoint>().ExecuteAsync(new UpdateReadingsParameters(null));
                var invalid = await provider.GetRequiredService<IImportReadingsEndpoint>().ExecuteAsync(new ImportReadingsParameters(ReadingSource.WithAnInvalidSecond()));
                var normalize = await provider.GetRequiredService<INormalizeReadingsEndpoint>().ExecuteAsync(new NormalizeReadingsParameters(ReadingSource.Of("a", "b")));
                return
                [
                    "import " + import.OkContent,
                    "sequence " + sequence.OkContent,
                    "optional " + optional.OkContent,
                    "normalize " + await ReadingSource.JoinAsync(normalize.Content!),
                    "invalid " + (int)invalid.StatusCode,
                ];
            }
        }
        """;

    [Fact]
    public async Task TypedClient_SendsAndReadsRecords()
    {
        await using var host = await StartServerAsync();

        var rows = await RunAsync(JsonLinesClientMarker("TypedClient"), TypedProbe, host.BaseAddress);

        Assert.Equal(
            [
                "import 3 a,b,c",
                "list 1 x",
                "sequence 2 a,b",
                "either 1 a",
                "optional 0 ",
                "normalize A,B",
                "export a,b",
                "invalid 400",
                "second send refused, first was {\"deviceId\":\"a\",\"value\":1}",
            ],
            rows);
    }

    [Fact]
    public async Task EndpointPerOperationClient_SendsAndReadsRecords()
    {
        await using var host = await StartServerAsync();

        var rows = await RunAsync(ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), EndpointProbe, host.BaseAddress);

        Assert.Equal(
            [
                "import 3 a,b,c",
                "sequence 2 a,b",
                "optional 0 ",
                "normalize A,B",
                "invalid 400",
            ],
            rows);
    }

    [Theory]
    [InlineData("TypedClient", "Throw")]
    [InlineData("TypedClient", "Result")]
    [InlineData("EndpointPerOperation", "Throw")]
    public void Clients_CompileWithoutWarnings(string mode, string style)
    {
        var marker = mode == "EndpointPerOperation"
            ? ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails")
            : $$"""{ "generationMode": "TypedClient", "typedClientResultStyle": "{{style}}" }""";
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, JsonLinesRequestBodyTests.Yaml);

        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSourcesForWarnings(sources));
    }

    private static string JsonLinesClientMarker(string mode)
        => $$"""{ "generationMode": "{{mode}}", "typedClientResultStyle": "Throw" }""";

    private static Task<XmlTestHost> StartServerAsync()
        => XmlTestHost.StartAsync(
            JsonLinesRequestBodyTests.Yaml,
            JsonLinesRequestBodyTests.HandlerSource,
            "ErrorBodies.RuntimeTest.ReadingRegistration");

    private static async Task<List<string>> RunAsync(
        string marker,
        string probe,
        Uri baseAddress)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, JsonLinesRequestBodyTests.Yaml);

        // The probe sees every generated namespace
        var usings = sources
            .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
            .Where(ns => ns.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Select(ns => $"global using {ns};");
        sources.Add(("JsonLinesProbe.cs", string.Join(Environment.NewLine, usings) + Environment.NewLine + probe));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        return await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.JsonLinesProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [baseAddress])!;
    }
}
