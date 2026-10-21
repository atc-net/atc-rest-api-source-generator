namespace Atc.Rest.Api.Generator.Tests;

public class StreamingMediaTypeTests
{
    [Theory]
    [InlineData("text/event-stream", StreamingFraming.ServerSentEvents)]
    [InlineData("text/event-stream; charset=utf-8", StreamingFraming.ServerSentEvents)]
    [InlineData("application/jsonl", StreamingFraming.JsonLines)]
    [InlineData("application/x-ndjson", StreamingFraming.JsonLines)]
    [InlineData("application/x-jsonlines", StreamingFraming.JsonLines)]
    [InlineData("APPLICATION/JSONL; charset=utf-8", StreamingFraming.JsonLines)]
    [InlineData("application/geo+json-seq", StreamingFraming.JsonSequence)]
    [InlineData("application/json-seq", StreamingFraming.JsonSequence)]
    [InlineData("multipart/mixed", StreamingFraming.MultipartMixed)]
    [InlineData("application/json", StreamingFraming.JsonArray)]
    [InlineData("", StreamingFraming.JsonArray)]
    public void Classify_MediaType_ReturnsFraming(
        string mediaType,
        StreamingFraming expected)
        => Assert.Equal(expected, StreamingMediaType.Classify(mediaType));

    [Fact]
    public void ItemSchemaOf_StreamingMediaTypeWithArraySchema_ReturnsTheItems()
    {
        var items = new OpenApiSchema { Title = "Event" };
        var schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = items };

        var result = StreamingMediaType.ItemSchemaOf("application/jsonl", schema);

        Assert.Same(items, result);
    }

    [Fact]
    public void ItemSchemaOf_StreamingMediaTypeWithElementSchema_ReturnsTheSchemaItself()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Object };

        var result = StreamingMediaType.ItemSchemaOf("text/event-stream", schema);

        Assert.Same(schema, result);
    }

    [Fact]
    public void ItemSchemaOf_PlainJsonMediaType_ReturnsNull()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema() };

        Assert.Null(StreamingMediaType.ItemSchemaOf("application/json", schema));
    }

    [Fact]
    public void ItemSchemaOf_NullSchema_ReturnsNull()
        => Assert.Null(StreamingMediaType.ItemSchemaOf("application/jsonl", schema: null));

    [Fact]
    public void ItemSchemaOf_ParsedDocument_UnwrapsTheReferencedElement()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(
            """
            openapi: 3.0.0
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
                        application/jsonl:
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

        var media = doc.Paths["/events"].Operations!.Values.First().Responses!["200"].Content!["application/jsonl"];

        var result = StreamingMediaType.ItemSchemaOf("application/jsonl", media.Schema);

        var reference = Assert.IsType<OpenApiSchemaReference>(result);
        Assert.Equal("Event", reference.Reference.Id);
    }
}