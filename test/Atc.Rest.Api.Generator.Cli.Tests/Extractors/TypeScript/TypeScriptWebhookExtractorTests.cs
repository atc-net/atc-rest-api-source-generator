namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptWebhookExtractorTests
{
    [Fact]
    public void Generate_OperationWithCallback_EmitsTypeAliasReferencingComponentSchema()
    {
        // Every callback whose body references a named component schema produces
        // a `<OperationId><CallbackName>Payload = <SchemaName>` alias. Consumers can
        // then type their webhook handler against the alias instead of duplicating
        // the payload shape.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: T, version: '1' }
                            paths:
                              /subscriptions:
                                post:
                                  operationId: createSubscription
                                  requestBody:
                                    required: true
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/SubscriptionRequest'
                                  callbacks:
                                    onEvent:
                                      '{$request.body#/callbackUrl}':
                                        post:
                                          requestBody:
                                            required: true
                                            content:
                                              application/json:
                                                schema:
                                                  $ref: '#/components/schemas/EventPayload'
                                          responses:
                                            '200': { description: OK }
                                  responses:
                                    '201': { description: Created }
                            components:
                              schemas:
                                SubscriptionRequest:
                                  type: object
                                EventPayload:
                                  type: object
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var content = TypeScriptWebhookExtractor.Generate(doc!, headerContent: null);

        Assert.NotNull(content);
        Assert.Contains("import type { EventPayload } from '../models';", content, StringComparison.Ordinal);
        Assert.Contains("export type CreateSubscriptionOnEventPayload = EventPayload;", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_NoCallbacks_ReturnsNull()
    {
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: T, version: '1' }
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  responses:
                                    '200': { description: OK }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var content = TypeScriptWebhookExtractor.Generate(doc!, headerContent: null);

        Assert.Null(content);
    }

    [Fact]
    public void Generate_CallbackWithInlineBodySchema_Skipped()
    {
        // Inline (non-$ref) callback body schemas have no canonical name to alias.
        // Skipping is safer than synthesizing — the model emitter doesn't produce a
        // type for inline shapes either.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: T, version: '1' }
                            paths:
                              /subscriptions:
                                post:
                                  operationId: createSubscription
                                  callbacks:
                                    onEvent:
                                      '{$request.body#/callbackUrl}':
                                        post:
                                          requestBody:
                                            required: true
                                            content:
                                              application/json:
                                                schema:
                                                  type: object
                                                  properties:
                                                    id: { type: string }
                                          responses:
                                            '200': { description: OK }
                                  responses:
                                    '201': { description: Created }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var content = TypeScriptWebhookExtractor.Generate(doc!, headerContent: null);

        Assert.Null(content);
    }

    [Fact]
    public void Generate_MultipleCallbacksOnOneOperation_AllAliasesEmitted()
    {
        // An operation may declare more than one callback name; each gets its own
        // alias so consumers can disambiguate handlers.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: T, version: '1' }
                            paths:
                              /subscriptions:
                                post:
                                  operationId: createSubscription
                                  callbacks:
                                    onCreated:
                                      '{$request.body#/callbackUrl}':
                                        post:
                                          requestBody:
                                            content:
                                              application/json:
                                                schema:
                                                  $ref: '#/components/schemas/CreatedEvent'
                                          responses:
                                            '200': { description: OK }
                                    onUpdated:
                                      '{$request.body#/callbackUrl}':
                                        post:
                                          requestBody:
                                            content:
                                              application/json:
                                                schema:
                                                  $ref: '#/components/schemas/UpdatedEvent'
                                          responses:
                                            '200': { description: OK }
                                  responses:
                                    '201': { description: Created }
                            components:
                              schemas:
                                CreatedEvent:
                                  type: object
                                UpdatedEvent:
                                  type: object
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var content = TypeScriptWebhookExtractor.Generate(doc!, headerContent: null);

        Assert.NotNull(content);
        Assert.Contains("export type CreateSubscriptionOnCreatedPayload = CreatedEvent;", content, StringComparison.Ordinal);
        Assert.Contains("export type CreateSubscriptionOnUpdatedPayload = UpdatedEvent;", content, StringComparison.Ordinal);

        // Both component schemas land in a single grouped import line.
        Assert.Contains("import type { CreatedEvent, UpdatedEvent } from '../models';", content, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}