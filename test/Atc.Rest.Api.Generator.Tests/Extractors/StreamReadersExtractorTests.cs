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

        Assert.Contains("(eventType, bytes) => JsonSerializer.Deserialize<T>(bytes, options));", content, StringComparison.Ordinal);
        Assert.Contains("=> JsonSerializer.DeserializeAsyncEnumerable<T>(stream, topLevelValues: true, options, cancellationToken);", content, StringComparison.Ordinal);
        Assert.Contains("yield return JsonSerializer.Deserialize<T>(record.ToArray(), options);", content, StringComparison.Ordinal);
        Assert.Contains("yield return JsonSerializer.Deserialize<T>(body.ToString(), options);", content, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateContent_PerOperationClient_ReadsWithContractSerializer()
    {
        var content = StreamReadersExtractor.GenerateContent("MyApi", perOperation: true);

        Assert.Contains("using Atc.Rest.Client.Serialization;", content, StringComparison.Ordinal);
        Assert.DoesNotContain("using System.Text.Json;", content, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializerOptions", content, StringComparison.Ordinal);

        Assert.Contains("(eventType, bytes) => serializer.Deserialize<T>(bytes.ToArray()));", content, StringComparison.Ordinal);
        Assert.Contains("public static async IAsyncEnumerable<T?> ReadJsonLinesAsync<T>(", content, StringComparison.Ordinal);
        Assert.Contains("yield return serializer.Deserialize<T>(line);", content, StringComparison.Ordinal);
        Assert.Contains("yield return serializer.Deserialize<T>(record.ToArray());", content, StringComparison.Ordinal);
        Assert.Contains("yield return serializer.Deserialize<T>(body.ToString());", content, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateContent_BothVariants_ShipAllFourReaders()
    {
        foreach (var perOperation in new[] { false, true })
        {
            var content = StreamReadersExtractor.GenerateContent("MyApi", perOperation);

            Assert.Contains("ReadServerSentEventsAsync<T>(", content, StringComparison.Ordinal);
            Assert.Contains("ReadJsonLinesAsync<T>(", content, StringComparison.Ordinal);
            Assert.Contains("ReadJsonSequenceAsync<T>(", content, StringComparison.Ordinal);
            Assert.Contains("ReadMultipartMixedAsync<T>(", content, StringComparison.Ordinal);

            // JSON Text Sequence splits on the RS byte, never on LF; multipart stops at the closing delimiter
            Assert.Contains("if (buffer[i] == 0x1E)", content, StringComparison.Ordinal);
            Assert.Contains("if (line.StartsWith(delimiter + \"--\", StringComparison.Ordinal))", content, StringComparison.Ordinal);
        }
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