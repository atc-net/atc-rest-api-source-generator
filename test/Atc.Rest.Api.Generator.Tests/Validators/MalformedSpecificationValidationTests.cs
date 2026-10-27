namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// A specification the OpenAPI reader accepts but that holds a value where an object belongs (a media type or a
/// schema written as a string) is validated, not crashed on: the validator reports where, it does not throw.
/// </summary>
public class MalformedSpecificationValidationTests
{
    private const string TestFilePath = "malformed.yaml";

    private const string MediaTypeAsString = """
        openapi: 3.0.3
        info: { title: T, version: 1.0.0 }
        paths:
          /events:
            get:
              operationId: listEvents
              responses:
                '200':
                  description: OK
                  content:
                    application/json: text
        """;

    private const string SchemaAsString = """
        openapi: 3.0.3
        info: { title: T, version: 1.0.0 }
        paths:
          /events:
            get:
              operationId: listEvents
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Page'
        components:
          schemas:
            Page: text
            Event:
              type: object
              properties:
                page:
                  $ref: '#/components/schemas/Page'
        """;

    public static TheoryData<string, string> Malformed
        => new()
        {
            { MediaTypeAsString, "#/paths//events/get/responses/200/content/application/json" },
            { SchemaAsString, "#/components/schemas/Page" },
        };

    [Theory]
    [MemberData(nameof(Malformed))]
    public void Validate_AValueWhereAnObjectBelongs_IsAnErrorNamingThePlace(
        string yaml,
        string expectedPointer)
    {
        var (document, diagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yaml, TestFilePath + yaml.Length);
        Assert.NotNull(document);

        foreach (var strategy in (ValidateSpecificationStrategy[])[ValidateSpecificationStrategy.None, ValidateSpecificationStrategy.Standard, ValidateSpecificationStrategy.Strict])
        {
            var diagnostics = OpenApiDocumentValidator.Validate(strategy, document, diagnostic?.Errors ?? [], TestFilePath);

            var error = Assert.Single(diagnostics);
            Assert.Equal(RuleIdentifiers.OpenApiCoreError, error.RuleId);
            Assert.Contains(expectedPointer, error.Message, StringComparison.Ordinal);
        }
    }
}