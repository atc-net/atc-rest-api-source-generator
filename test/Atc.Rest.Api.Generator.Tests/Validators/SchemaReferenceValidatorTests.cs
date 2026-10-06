namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// A <c>$ref</c> that names a component that is not there is an error (<c>ATC_API_SCH013</c>) under every strategy,
/// wherever it sits: a media type's schema or item schema, an <c>allOf</c>/<c>oneOf</c>/<c>anyOf</c> member, a
/// property, the items of an array, <c>additionalProperties</c>, a parameter's schema, a header, or a reference to a
/// parameter, response or request body. The generators would otherwise emit code that names a type that is never
/// generated.
/// </summary>
public class SchemaReferenceValidatorTests
{
    private const string TestFilePath = "references.yaml";

    public static TheoryData<string, string> DanglingReferences
        => new()
        {
            {
                "allOf member of an inline response schema",
                """
                paths:
                  /things:
                    get:
                      operationId: listThings
                      responses:
                        '200':
                          description: OK
                          content:
                            application/json:
                              schema:
                                allOf:
                                  - $ref: '#/components/schemas/Missing'
                                  - type: object
                                    properties:
                                      items: { type: array, items: { $ref: '#/components/schemas/Thing' } }
                components:
                  schemas:
                    Thing: { type: object, properties: { name: { type: string } } }
                """
            },
            {
                "items of a component array alias",
                """
                paths:
                  /things:
                    get:
                      operationId: listThings
                      responses:
                        '200':
                          description: OK
                          content:
                            application/json:
                              schema: { $ref: '#/components/schemas/Things' }
                components:
                  schemas:
                    Things: { type: array, items: { $ref: '#/components/schemas/Missing' } }
                """
            },
            {
                "items of an array property",
                """
                paths:
                  /things:
                    get:
                      operationId: listThings
                      responses:
                        '200':
                          description: OK
                          content:
                            application/json:
                              schema: { $ref: '#/components/schemas/Thing' }
                components:
                  schemas:
                    Thing:
                      type: object
                      properties:
                        tags: { type: array, items: { $ref: '#/components/schemas/Missing' } }
                """
            },
            {
                "oneOf member of a component schema",
                """
                paths:
                  /things:
                    get:
                      operationId: listThings
                      responses:
                        '200':
                          description: OK
                          content:
                            application/json:
                              schema: { $ref: '#/components/schemas/Pet' }
                components:
                  schemas:
                    Pet:
                      oneOf:
                        - $ref: '#/components/schemas/Missing'
                        - $ref: '#/components/schemas/Dog'
                    Dog: { type: object, properties: { bark: { type: boolean } } }
                """
            },
            {
                "additionalProperties of a dictionary schema",
                """
                paths:
                  /things:
                    get:
                      operationId: listThings
                      responses:
                        '200':
                          description: OK
                          content:
                            application/json:
                              schema: { $ref: '#/components/schemas/Lookup' }
                components:
                  schemas:
                    Lookup: { type: object, additionalProperties: { $ref: '#/components/schemas/Missing' } }
                """
            },
            {
                "itemSchema of a streaming media type",
                """
                paths:
                  /things:
                    get:
                      operationId: streamThings
                      responses:
                        '200':
                          description: OK
                          content:
                            application/jsonl:
                              itemSchema: { $ref: '#/components/schemas/Missing' }
                components:
                  schemas:
                    Thing: { type: object, properties: { name: { type: string } } }
                """
            },
            {
                "schema of a path-level parameter",
                """
                paths:
                  /things/{id}:
                    parameters:
                      - name: id
                        in: path
                        required: true
                        schema: { $ref: '#/components/schemas/Missing' }
                    get:
                      operationId: getThing
                      responses:
                        '200': { description: OK }
                components:
                  schemas:
                    Thing: { type: object, properties: { name: { type: string } } }
                """
            },
            {
                "schema of a response header",
                """
                paths:
                  /things:
                    get:
                      operationId: listThings
                      responses:
                        '200':
                          description: OK
                          headers:
                            X-Total: { schema: { $ref: '#/components/schemas/Missing' } }
                components:
                  schemas:
                    Thing: { type: object, properties: { name: { type: string } } }
                """
            },
            {
                "schema of a request body",
                """
                paths:
                  /things:
                    post:
                      operationId: createThing
                      requestBody:
                        content:
                          application/json:
                            schema: { $ref: '#/components/schemas/Missing' }
                      responses:
                        '201': { description: Created }
                components:
                  schemas:
                    Thing: { type: object, properties: { name: { type: string } } }
                """
            },
            {
                "reference to a component parameter",
                """
                paths:
                  /things:
                    get:
                      operationId: listThings
                      parameters:
                        - $ref: '#/components/parameters/Missing'
                      responses:
                        '200': { description: OK }
                components:
                  schemas:
                    Thing: { type: object, properties: { name: { type: string } } }
                """
            },
            {
                "reference to a component response",
                """
                paths:
                  /things:
                    get:
                      operationId: listThings
                      responses:
                        '200': { $ref: '#/components/responses/Missing' }
                components:
                  schemas:
                    Thing: { type: object, properties: { name: { type: string } } }
                """
            },
            {
                "reference to a component request body",
                """
                paths:
                  /things:
                    post:
                      operationId: createThing
                      requestBody: { $ref: '#/components/requestBodies/Missing' }
                      responses:
                        '201': { description: Created }
                components:
                  schemas:
                    Thing: { type: object, properties: { name: { type: string } } }
                """
            },
        };

    [Theory]
    [MemberData(nameof(DanglingReferences))]
    public void Validate_ADanglingReference_IsAnErrorNamingIt_UnderEveryStrategy(
        string site,
        string yaml)
    {
        var document = Parse(yaml);

        foreach (var strategy in (ValidateSpecificationStrategy[])[ValidateSpecificationStrategy.None, ValidateSpecificationStrategy.Standard, ValidateSpecificationStrategy.Strict])
        {
            var diagnostics = OpenApiDocumentValidator.Validate(strategy, document, [], TestFilePath);

            var error = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.InvalidSchemaReference);
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
            Assert.Contains("Missing", error.Message, StringComparison.Ordinal);
            Assert.True(error.Context is not null && error.Context.Length > 0, $"{site} ({strategy}): the error says where");
        }
    }

    [Fact]
    public void Validate_EveryReferenceResolves_IsNoError()
    {
        var document = Parse(
            """
            paths:
              /things/{id}:
                parameters:
                  - $ref: '#/components/parameters/Id'
                get:
                  operationId: getThing
                  responses:
                    '200': { $ref: '#/components/responses/ThingResponse' }
                put:
                  operationId: updateThing
                  requestBody: { $ref: '#/components/requestBodies/ThingBody' }
                  responses:
                    '200':
                      description: OK
                      headers:
                        X-Total: { schema: { $ref: '#/components/schemas/Count' } }
                      content:
                        application/json:
                          schema:
                            allOf:
                              - $ref: '#/components/schemas/Thing'
                              - type: object
                                properties:
                                  parent: { $ref: '#/components/schemas/Thing' }
            components:
              parameters:
                Id: { name: id, in: path, required: true, schema: { type: string } }
              responses:
                ThingResponse:
                  description: OK
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Thing' }
              requestBodies:
                ThingBody:
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Thing' }
              schemas:
                Count: { type: integer }
                Thing:
                  type: object
                  properties:
                    name: { type: string }
                    tags: { type: array, items: { $ref: '#/components/schemas/Tag' } }
                    lookup: { type: object, additionalProperties: { $ref: '#/components/schemas/Tag' } }
                    parent: { $ref: '#/components/schemas/Thing' }
                Tag: { type: object, properties: { label: { type: string } } }
            """);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Strict, document, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.InvalidSchemaReference);
    }

    private static OpenApiDocument Parse(string body)
    {
        const string head = """
            openapi: 3.2.0
            info: { title: Contoso, version: 1.0.0 }

            """;
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(head + body, TestFilePath);
        Assert.NotNull(document);
        return document;
    }
}