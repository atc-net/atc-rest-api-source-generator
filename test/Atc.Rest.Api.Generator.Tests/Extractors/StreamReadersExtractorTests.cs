namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class StreamReadersExtractorTests
{
    [Theory]
    [InlineData("text/event-stream")]
    [InlineData("application/jsonl")]
    [InlineData("application/json-seq")]
    [InlineData("multipart/mixed")]
    public void DocumentRequiresStreamReaders_WireFramedStreaming_ReturnsTrue(
        string mediaType)
    {
        var document = OpenApiDocumentHelper.ParseYaml(BuildStreamingDocument(mediaType));

        Assert.True(StreamReadersExtractor.DocumentRequiresStreamReaders(document));
    }

    [Fact]
    public void DocumentRequiresStreamReaders_LegacyJsonArrayStreaming_ReturnsFalse()
    {
        var document = OpenApiDocumentHelper.ParseYaml(
            """
            openapi: 3.0.0
            info:
              title: Test
              version: 1.0.0
            paths:
              /events:
                get:
                  operationId: streamEvents
                  x-return-async-enumerable: true
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              $ref: '#/components/schemas/Event'
            components:
              schemas:
                Event:
                  type: object
                  properties:
                    id:
                      type: string
            """);

        Assert.False(StreamReadersExtractor.DocumentRequiresStreamReaders(document));
    }

    [Fact]
    public void DocumentRequiresStreamReaders_NoStreamingOperation_ReturnsFalse()
    {
        var document = OpenApiDocumentHelper.ParseYaml(
            """
            openapi: 3.0.0
            info:
              title: Test
              version: 1.0.0
            paths:
              /pets:
                get:
                  operationId: listPets
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              type: string
            """);

        Assert.False(StreamReadersExtractor.DocumentRequiresStreamReaders(document));
    }

    [Fact]
    public void GenerateContent_TypedClient_ReadsWithJsonSerializerOptions()
    {
        var content = StreamReadersExtractor.GenerateContent("MyApi");

        Assert.Contains("namespace MyApi.Generated.Streaming;", content, StringComparison.Ordinal);
        Assert.Contains("internal static class StreamReaders", content, StringComparison.Ordinal);
        Assert.Contains("using System.Text.Json;", content, StringComparison.Ordinal);
        Assert.DoesNotContain("using Atc.Rest.Client.Serialization;", content, StringComparison.Ordinal);
        Assert.DoesNotContain("IContractSerializer", content, StringComparison.Ordinal);

        Assert.Contains("(eventType, bytes) => ReadEvent(eventType, bytes, data => JsonSerializer.Deserialize(data, options.TypeInfo<T>())));", content, StringComparison.Ordinal);
        Assert.Contains("=> JsonSerializer.DeserializeAsyncEnumerable(new LineLimitedStream(stream), options.TypeInfo<T>(), topLevelValues: true, cancellationToken);", content, StringComparison.Ordinal);
        Assert.Contains("yield return JsonSerializer.Deserialize(record.ToArray(), options.TypeInfo<T>());", content, StringComparison.Ordinal);
        Assert.Contains("yield return JsonSerializer.Deserialize(body.ToString(), options.TypeInfo<T>());", content, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateContent_ShipsAllFourReaders()
    {
        var content = StreamReadersExtractor.GenerateContent("MyApi");

        Assert.Contains("ReadServerSentEventsAsync<T>(", content, StringComparison.Ordinal);
        Assert.Contains("ReadJsonLinesAsync<T>(", content, StringComparison.Ordinal);
        Assert.Contains("ReadJsonSequenceAsync<T>(", content, StringComparison.Ordinal);
        Assert.Contains("ReadMultipartMixedAsync<T>(", content, StringComparison.Ordinal);

        // JSON Text Sequence splits on the RS byte, never on LF; multipart stops at the closing delimiter
        Assert.Contains("if (value == 0x1E)", content, StringComparison.Ordinal);
        Assert.Contains("if (boundaryLine == 2)", content, StringComparison.Ordinal);
    }

    private static string BuildStreamingDocument(string mediaType)
        => $"""
            openapi: 3.2.0
            info:
              title: Test
              version: 1.0.0
            paths:
              /events:
                get:
                  operationId: streamEvents
                  responses:
                    '200':
                      description: OK
                      content:
                        {mediaType}:
                          itemSchema:
                            $ref: '#/components/schemas/Event'
            components:
              schemas:
                Event:
                  type: object
                  properties:
                    id:
                      type: string
            """;
}