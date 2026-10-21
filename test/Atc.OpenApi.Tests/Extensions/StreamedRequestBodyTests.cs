namespace Atc.OpenApi.Tests.Extensions;

/// <summary>
/// A request body sent as a stream of records (JSON Lines, JSON Text Sequences): the one accessor every layer reads it
/// through - its media types, its record, its count, whether a JSON array is offered beside it - and the body every
/// generator picks for the operation (the stream, before a JSON array offered beside it).
/// </summary>
public class StreamedRequestBodyTests
{
    [Theory]
    [InlineData("application/jsonl")]
    [InlineData("application/x-ndjson")]
    [InlineData("application/x-jsonlines")]
    [InlineData("application/json-seq")]
    [InlineData("application/geo+json-seq")]
    public void GetStreamedRequestBody_ASchemaUnderASequentialMediaType_IsTheRecord(
        string mediaType)
    {
        var operation = Parse($$"""
            requestBody:
              required: true
              content:
                {{mediaType}}:
                  schema:
                    $ref: '#/components/schemas/Reading'
            """);

        var streamed = operation.GetStreamedRequestBody();

        Assert.NotNull(streamed);
        Assert.Equal([mediaType], streamed.MediaTypes);
        Assert.Equal("Reading", ((OpenApiSchemaReference)streamed.ItemSchema).Reference.Id);
        Assert.True(streamed.IsRequired);
        Assert.False(streamed.AcceptsJsonArray);
        Assert.Null(streamed.MinItems);
        Assert.Null(streamed.MaxItems);
    }

    [Fact]
    public void GetStreamedRequestBody_AnArraySchema_ItsItemsAreTheRecordAndItsCountTheBodys()
    {
        var operation = Parse("""
            requestBody:
              content:
                application/json-seq:
                  schema:
                    type: array
                    minItems: 1
                    maxItems: 1000
                    items:
                      $ref: '#/components/schemas/Reading'
            """);

        var streamed = operation.GetStreamedRequestBody();

        Assert.NotNull(streamed);
        Assert.Equal("Reading", ((OpenApiSchemaReference)streamed.ItemSchema).Reference.Id);
        Assert.Equal(1, streamed.MinItems);
        Assert.Equal(1000, streamed.MaxItems);
        Assert.False(streamed.IsRequired);
        Assert.Equal(StreamingFraming.JsonSequence, streamed.PrimaryFraming);
    }

    [Fact]
    public void GetStreamedRequestBody_AnItemSchema_IsTheRecord()
    {
        var operation = Parse(
            """
            requestBody:
              content:
                application/jsonl:
                  itemSchema:
                    $ref: '#/components/schemas/Reading'
            """,
            "3.2.0");

        Assert.Equal("Reading", ((OpenApiSchemaReference)operation.GetStreamedRequestBody()!.ItemSchema).Reference.Id);
    }

    [Fact]
    public void GetStreamedRequestBody_JsonLinesBesideAJsonArray_TakesTheArrayToo_AndWinsAsTheBody()
    {
        var operation = Parse("""
            requestBody:
              content:
                application/json:
                  schema:
                    type: array
                    items:
                      $ref: '#/components/schemas/Reading'
                application/jsonl:
                  schema:
                    $ref: '#/components/schemas/Reading'
            """);

        var streamed = operation.GetStreamedRequestBody();
        var (schema, contentType) = operation.GetRequestBodySchemaWithContentType();

        Assert.NotNull(streamed);
        Assert.True(streamed.AcceptsJsonArray);
        Assert.Equal(["application/jsonl", "application/x-ndjson", "application/x-jsonlines", "application/json"], streamed.AcceptedMediaTypes);
        Assert.Equal("application/jsonl", contentType);
        Assert.Same(streamed.ItemSchema, schema);
    }

    [Fact]
    public void AcceptedMediaTypes_EveryJsonLinesNameOnce_InTheDeclaredOrder()
    {
        var operation = Parse("""
            requestBody:
              content:
                application/x-ndjson:
                  schema:
                    $ref: '#/components/schemas/Reading'
            """);

        Assert.Equal(["application/x-ndjson", "application/jsonl", "application/x-jsonlines"], operation.GetStreamedRequestBody()!.AcceptedMediaTypes);
    }

    [Theory]
    [InlineData("text/event-stream")]
    [InlineData("multipart/mixed")]
    [InlineData("application/json")]
    [InlineData("application/octet-stream")]
    public void GetStreamedRequestBody_AnyOtherBody_IsNull(string mediaType)
    {
        var operation = Parse($$"""
            requestBody:
              content:
                {{mediaType}}:
                  schema:
                    $ref: '#/components/schemas/Reading'
            """);

        Assert.Null(operation.GetStreamedRequestBody());
    }

    private static OpenApiOperation Parse(
        string requestBody,
        string version = "3.1.1")
    {
        var indented = string.Join("\n", requestBody.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => "      " + line));
        var document = OpenApiDocumentHelper.ParseYaml($$"""
            openapi: {{version}}
            info:
              title: Test
              version: 1.0.0
            paths:
              /readings:
                post:
                  operationId: importReadings
            {{indented}}
                  responses:
                    '202':
                      description: Accepted
            components:
              schemas:
                Reading:
                  type: object
                  properties:
                    deviceId:
                      type: string
            """);

        return document.Paths["/readings"].Operations!.Values.First();
    }
}