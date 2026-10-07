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

    /// <summary>
    /// A reference the generators cannot follow - to another file or a URL, or a pointer into a component (a property
    /// of a schema) - is an error saying the form is not supported, not a "missing component" (the component may well
    /// exist, and SCH013 named only the pointer's last segment).
    /// </summary>
    [Theory]
    [InlineData("other.yaml#/components/schemas/Thing")]
    [InlineData("https://schemas.contoso.example/thing.yaml#/components/schemas/Thing")]
    [InlineData("#/components/schemas/Thing/properties/name")]
    public void Validate_AnUnsupportedReferenceForm_IsAnErrorSayingSo_UnderEveryStrategy(
        string reference)
    {
        var document = Parse(
            $$"""
            paths:
              /things:
                get:
                  operationId: listThings
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema: { $ref: '{{reference}}' }
            components:
              schemas:
                Thing: { type: object, properties: { name: { type: string } } }
            """);

        foreach (var strategy in (ValidateSpecificationStrategy[])[ValidateSpecificationStrategy.None, ValidateSpecificationStrategy.Standard, ValidateSpecificationStrategy.Strict])
        {
            var diagnostics = OpenApiDocumentValidator.Validate(strategy, document, [], TestFilePath);

            var error = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.UnsupportedReferenceForm);
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
            Assert.Contains(reference, error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.InvalidSchemaReference);
        }
    }

    /// <summary>
    /// An OpenAPI 3.1 <c>$dynamicRef</c> names no component the generators can type: an error saying so, rather than a
    /// property typed after the anchor.
    /// </summary>
    [Fact]
    public void Validate_ADynamicReference_IsAnUnsupportedReferenceForm()
    {
        var document = Parse(
            """
            paths:
              /trees:
                get:
                  operationId: getTree
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema: { $ref: '#/components/schemas/Tree' }
            components:
              schemas:
                Tree:
                  $dynamicAnchor: node
                  type: object
                  properties:
                    children:
                      type: array
                      items: { $dynamicRef: '#node' }
            """);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Standard, document, [], TestFilePath);

        var error = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.UnsupportedReferenceForm);
        Assert.Contains("#node", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The reader's warnings reach the author as warnings (under Standard and Strict) - except the two kinds a rule of
    /// its own already reports as an error with its place: a missing schema (SCH013) and a reference cycle.
    /// </summary>
    [Fact]
    public void Validate_TheReadersWarnings_AreReported_ExceptThoseARuleCovers()
    {
        var document = Parse(
            """
            paths:
              /things:
                get:
                  operationId: listThings
                  responses:
                    '200': { description: OK }
            """);
        IList<OpenApiError> warnings =
        [
            new("#/paths/~1things", "Something the reader found odd"),
            new("#/components/schemas/A", "Circular reference detected while resolving reference: #/components/schemas/B"),
            new("#/paths", "The schema reference '#/components/schemas/Missing' does not point to an existing schema."),
        ];

        var standard = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Standard, document, [], TestFilePath, diagnosticWarnings: warnings);
        var none = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.None, document, [], TestFilePath, diagnosticWarnings: warnings);

        var warning = Assert.Single(standard, d => d.RuleId == RuleIdentifiers.OpenApiCoreWarning);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("Something the reader found odd", warning.Message, StringComparison.Ordinal);
        Assert.Equal("#/paths/~1things", warning.Context);
        Assert.DoesNotContain(none, d => d.RuleId == RuleIdentifiers.OpenApiCoreWarning);
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