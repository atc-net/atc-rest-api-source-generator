namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// A parameter whose value the generated code cannot bind or send - an object other than a flat model in the query
/// (as <c>deepObject</c> or <c>form</c> with <c>explode</c>): a model in a header, a cookie or the path, a nested
/// object or an array property, an inline object, <c>form</c> without <c>explode</c>, an array of objects, a
/// <c>oneOf</c>/<c>anyOf</c>/<c>allOf</c> without a type - is <c>ATC_API_OPR037</c> under every strategy, naming the
/// operation and the parameter. A warning (a client leaves the parameter out; a server generator raises it to an
/// error), and an error in the path (no client could build the URL). The old <c>ATC_API_OPR026</c> warning
/// ("emitting default form serialization") is not reported for it: the code it promised did not compile.
/// </summary>
public class ParameterBindingValidatorTests
{
    private const string TestFilePath = "parameters.yaml";

    public static TheoryData<string, string> Unbindable => new()
    {
        { "in: header", "{ $ref: '#/components/schemas/Filter' }" },
        { "in: cookie", "{ $ref: '#/components/schemas/Filter' }" },
        { "in: path", "{ $ref: '#/components/schemas/Filter' }" },
        { "in: query", "{ $ref: '#/components/schemas/Pageable' }" },
        { "in: query, style: form, explode: false", "{ $ref: '#/components/schemas/Filter' }" },
        { "in: query", "{ type: object, properties: { name: { type: string } } }" },
        { "in: query", "{ type: array, items: { $ref: '#/components/schemas/Filter' } }" },
        { "in: query", "{ oneOf: [ { type: string }, { $ref: '#/components/schemas/Filter' } ] }" },
        { "in: query", "{ anyOf: [ { $ref: '#/components/schemas/Filter' } ] }" },
    };

    public static TheoryData<string, string> Bindable => new()
    {
        { "in: query", "{ type: string }" },
        { "in: query", "{ type: integer, format: int32 }" },
        { "in: query", "{ type: string, format: uri }" },
        { "in: query", "{}" },
        { "in: query", "{ $ref: '#/components/schemas/Color' }" },
        { "in: query", "{ nullable: true, allOf: [ { $ref: '#/components/schemas/Color' }, {} ] }" },
        { "in: query", "{ type: array, items: { type: string } }" },
        { "in: query", "{ type: array, items: { $ref: '#/components/schemas/Color' } }" },
        { "in: query", "{ $ref: '#/components/schemas/Filter' }" },
        { "in: query, style: deepObject, explode: true", "{ $ref: '#/components/schemas/Filter' }" },
        { "in: query", "{ allOf: [ { $ref: '#/components/schemas/Filter' } ] }" },
        { "in: header", "{ type: string, format: uuid }" },
        { "in: path", "{ type: string }" },
    };

    [Theory]
    [MemberData(nameof(Unbindable))]
    public void Validate_AParameterWhoseValueCannotBeBound_IsReported_UnderEveryStrategy(
        string declaration,
        string schema)
    {
        var document = Parse(Operation(declaration, schema));

        foreach (var strategy in (ValidateSpecificationStrategy[])[ValidateSpecificationStrategy.None, ValidateSpecificationStrategy.Standard, ValidateSpecificationStrategy.Strict])
        {
            var diagnostics = OpenApiDocumentValidator.Validate(strategy, document, [], TestFilePath);

            var diagnostic = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.ParameterValueNotBindable);
            Assert.Equal(declaration == "in: path" ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("'filter'", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains("listThings", diagnostic.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.ParameterSerializationNotSupported);
        }
    }

    [Theory]
    [MemberData(nameof(Bindable))]
    public void Validate_AParameterWhoseValueIsBound_IsNotReported(
        string declaration,
        string schema)
    {
        var document = Parse(Operation(declaration, schema));

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Strict, document, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.ParameterValueNotBindable);
    }

    [Fact]
    public void Validate_AParameterWithAContentMap_IsNotReported()
    {
        var document = Parse(
            """
            paths:
              /things:
                get:
                  operationId: listThings
                  parameters:
                    - name: filter
                      in: query
                      content:
                        application/json:
                          schema: { $ref: '#/components/schemas/Pageable' }
                  responses:
                    '200': { description: OK }
            components:
              schemas:
                Pageable:
                  type: object
                  properties:
                    sort: { type: object, properties: { by: { type: string } } }
            """);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Strict, document, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.ParameterValueNotBindable);
    }

    [Fact]
    public void Validate_APathLevelParameterWhoseValueCannotBeBound_IsReportedForEachOperation()
    {
        var document = Parse(
            """
            paths:
              /things:
                parameters:
                  - name: filter
                    in: query
                    schema: { $ref: '#/components/schemas/Pageable' }
                get:
                  operationId: listThings
                  responses:
                    '200': { description: OK }
                delete:
                  operationId: deleteThings
                  responses:
                    '204': { description: No Content }
            components:
              schemas:
                Pageable:
                  type: object
                  properties:
                    sort: { type: object, properties: { by: { type: string } } }
            """);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.None, document, [], TestFilePath);

        var reported = diagnostics.Where(d => d.RuleId == RuleIdentifiers.ParameterValueNotBindable).ToList();
        Assert.Equal(2, reported.Count);
        Assert.Contains(reported, e => e.Message.Contains("listThings", StringComparison.Ordinal));
        Assert.Contains(reported, e => e.Message.Contains("deleteThings", StringComparison.Ordinal));
    }

    private static string Operation(
        string declaration,
        string schema)
        => $$"""
            paths:
              {{(declaration == "in: path" ? "/things/{filter}" : "/things")}}:
                get:
                  operationId: listThings
                  parameters:
                    - { name: filter, required: true, {{declaration}}, schema: {{schema}} }
                  responses:
                    '200': { description: OK }
            components:
              schemas:
                Filter:
                  type: object
                  properties:
                    name: { type: string }
                Pageable:
                  type: object
                  properties:
                    page: { type: integer }
                    sort: { $ref: '#/components/schemas/Filter' }
                Color:
                  type: string
                  enum: [red, green]
            """;

    private static OpenApiDocument Parse(string body)
    {
        const string head = """
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }

            """;
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(head + body, TestFilePath);
        Assert.NotNull(document);
        return document;
    }
}