namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The endpoint metadata the runtime OpenAPI document is built from: a download that declares several binary media
/// types lists them all; a streamed response (an OpenAPI 3.2 <c>itemSchema</c>) names its item type under its stream
/// media type; an <c>x-return-async-enumerable</c> operation keeps its JSON array metadata.
/// </summary>
public sealed class ResponseMetadataTests
{
    private const string Yaml = """
        openapi: 3.2.0
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /documents/{id}:
            get:
              operationId: getDocument
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: The document
                  content:
                    application/pdf: { schema: { type: string, format: binary } }
                    image/png: { schema: { type: string, format: binary } }
                    image/jpeg: { schema: { type: string, format: binary } }
          /events:
            get:
              operationId: streamEvents
              responses:
                '200':
                  description: A stream of events
                  content:
                    text/event-stream:
                      itemSchema:
                        $ref: '#/components/schemas/Event'
          /items:
            get:
              operationId: listItems
              x-return-async-enumerable: true
              responses:
                '200':
                  description: The items, streamed
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Event'
        components:
          schemas:
            Event:
              title: Event
              type: object
              required: [name]
              properties:
                name:
                  type: string
        """;

    [Fact]
    public void EndpointMetadata_NamesEveryFileType_TheStreamItem_AndKeepsTheAsyncEnumerableArray()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        var endpoints = string.Join(Environment.NewLine, sources.Where(s => s.HintName.Contains("EndpointDefinition", StringComparison.Ordinal)).Select(s => s.Source));

        Assert.Contains(".Produces(StatusCodes.Status200OK, typeof(System.IO.Stream), \"application/pdf\", \"image/png\", \"image/jpeg\")", endpoints, StringComparison.Ordinal);
        Assert.Contains(".Produces<Event>(StatusCodes.Status200OK, \"text/event-stream\")", endpoints, StringComparison.Ordinal);
        Assert.Contains(".Produces<List<Event>>()", endpoints, StringComparison.Ordinal);
        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true));
    }
}