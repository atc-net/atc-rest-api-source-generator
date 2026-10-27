namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// A component schema whose type is only <c>null</c> (<c>type: ['null']</c> in OpenAPI 3.1) has no values and no C#
/// type: nothing is generated for it, while every property that references it names a type that does not exist. An
/// error (<c>ATC_API_SCH024</c>) under every strategy. A nullable type (<c>['string', 'null']</c>) is fine.
/// </summary>
[RuleTest("ATC_API_SCH024")]
public class NullOnlyTypeValidatorTests
{
    private const string TestFilePath = "null-only.yaml";

    [Theory]
    [InlineData("type: ['null']")]
    [InlineData("type: ['null']\n      enum: [Low, High]")]
    [InlineData("type: 'null'")]
    public void Validate_AComponentSchemaTypedOnlyNull_IsAnError_UnderEveryStrategy(
        string type)
    {
        var document = Parse(
            $$"""
            components:
              schemas:
                Priority:
                  {{type}}
                Ticket:
                  type: object
                  properties:
                    priority: { $ref: '#/components/schemas/Priority' }
            """);

        foreach (var strategy in (ValidateSpecificationStrategy[])[ValidateSpecificationStrategy.None, ValidateSpecificationStrategy.Standard, ValidateSpecificationStrategy.Strict])
        {
            var diagnostics = OpenApiDocumentValidator.Validate(strategy, document, [], TestFilePath);

            var error = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.SchemaTypeOnlyNull);
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
            Assert.Contains("Priority", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Validate_ANullableTypeAndANullableProperty_AreNoError()
    {
        var document = Parse(
            """
            components:
              schemas:
                Priority:
                  type: ['string', 'null']
                  enum: [Low, High, null]
                Ticket:
                  type: object
                  properties:
                    priority: { $ref: '#/components/schemas/Priority' }
                    note: { type: ['string', 'null'] }
            """);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Strict, document, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.SchemaTypeOnlyNull);
    }

    private static OpenApiDocument Parse(string body)
    {
        const string head = """
            openapi: 3.1.0
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /tickets:
                get:
                  operationId: listTickets
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema: { $ref: '#/components/schemas/Ticket' }

            """;
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(head + body, TestFilePath);
        Assert.NotNull(document);
        return document;
    }
}