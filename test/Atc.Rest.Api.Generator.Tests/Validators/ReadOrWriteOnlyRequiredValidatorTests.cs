namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// A property listed in <c>required</c> that is <c>readOnly</c> or <c>writeOnly</c> is generated optional when its
/// schema is used in requests and responses alike - one record serves both. <c>ATC_API_SCH029</c> (Warning, every
/// strategy) says so, naming the schema and the property. A schema used in the one direction the property is always
/// there in keeps it required and reports nothing; so does a schema no operation uses.
/// </summary>
[RuleTest("ATC_API_SCH029")]
public class ReadOrWriteOnlyRequiredValidatorTests
{
    private const string TestFilePath = "readonly.yaml";

    private static readonly ValidateSpecificationStrategy[] Strategies =
    [
        ValidateSpecificationStrategy.None,
        ValidateSpecificationStrategy.Standard,
        ValidateSpecificationStrategy.Strict,
    ];

    [Fact]
    public void Validate_ReadOnlyRequiredProperty_OfASchemaUsedBothWays_IsAWarningNamingIt()
    {
        var document = Parse(BothWays("id: { type: string, readOnly: true }", "id"));

        foreach (var strategy in Strategies)
        {
            var warning = Assert.Single(Sch029(strategy, document));
            Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
            Assert.Contains("'Pet.id'", warning.Message, StringComparison.Ordinal);
            Assert.Contains("readOnly", warning.Message, StringComparison.Ordinal);
            Assert.Contains("requests", warning.Message, StringComparison.Ordinal);
            Assert.Equal(SpecSuppressions.Schema("Pet"), warning.Scope);
        }
    }

    [Fact]
    public void Validate_WriteOnlyRequiredProperty_OfASchemaUsedBothWays_IsAWarningNamingIt()
    {
        var document = Parse(BothWays("password: { type: string, writeOnly: true }", "password"));

        var warning = Assert.Single(Sch029(ValidateSpecificationStrategy.Standard, document));
        Assert.Contains("'Pet.password'", warning.Message, StringComparison.Ordinal);
        Assert.Contains("writeOnly", warning.Message, StringComparison.Ordinal);
        Assert.Contains("responses", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ReadOnlyRequiredProperty_OfAResponseOnlySchema_ReportsNothing()
    {
        var document = Parse("""
            openapi: 3.0.3
            info: { title: T, version: 1.0.0 }
            paths:
              /pets:
                get:
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema: { $ref: '#/components/schemas/Pet' }
            components:
              schemas:
                Pet:
                  type: object
                  required: [id, name]
                  properties:
                    id: { type: string, readOnly: true }
                    name: { type: string }
            """);

        foreach (var strategy in Strategies)
        {
            Assert.Empty(Sch029(strategy, document));
        }
    }

    [Fact]
    public void Validate_ReadOnlyRequiredProperty_OfASchemaNoOperationUses_ReportsNothing()
    {
        var document = Parse("""
            openapi: 3.0.3
            info: { title: T, version: 1.0.0 }
            paths:
              /health:
                get:
                  responses:
                    '204':
                      description: OK
            components:
              schemas:
                Pet:
                  type: object
                  required: [id]
                  properties:
                    id: { type: string, readOnly: true }
            """);

        Assert.Empty(Sch029(ValidateSpecificationStrategy.Strict, document));
    }

    /// <summary>
    /// What a webhook references counts as used both ways (the API sends the request and reads the response), so a
    /// schema only a webhook payload carries reports its required readOnly property.
    /// </summary>
    [Fact]
    public void Validate_ReadOnlyRequiredProperty_OfASchemaOnlyAWebhookUses_IsAWarning()
    {
        var document = Parse("""
            openapi: 3.1.0
            info: { title: T, version: 1.0.0 }
            paths:
              /health:
                get:
                  responses:
                    '204':
                      description: OK
            webhooks:
              thingCreated:
                post:
                  requestBody:
                    content:
                      application/json:
                        schema: { $ref: '#/components/schemas/Thing' }
                  responses:
                    '200':
                      description: OK
            components:
              schemas:
                Thing:
                  type: object
                  required: [id]
                  properties:
                    id: { type: string, readOnly: true }
            """);

        var warning = Assert.Single(Sch029(ValidateSpecificationStrategy.Standard, document));
        Assert.Contains("'Thing.id'", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ReadOnlyProperty_NotListedInRequired_ReportsNothing()
    {
        var document = Parse(BothWays("id: { type: string, readOnly: true }", "name"));

        Assert.Empty(Sch029(ValidateSpecificationStrategy.Standard, document));
    }

    [Fact]
    public void Validate_XAtcSuppressOnTheSchema_SwitchesTheRuleOff()
    {
        var yaml = BothWays("id: { type: string, readOnly: true }", "id")
            .Replace("    Pet:\n", "    Pet:\n      x-atc-suppress: [ATC_API_SCH029]\n", StringComparison.Ordinal);
        Assert.Contains("x-atc-suppress", yaml, StringComparison.Ordinal);

        Assert.Empty(Sch029(ValidateSpecificationStrategy.Standard, Parse(yaml)));
    }

    [Fact]
    public void Validate_SuppressRules_SwitchesTheRuleOff()
    {
        var warning = Assert.Single(Sch029(ValidateSpecificationStrategy.Standard, Parse(BothWays("id: { type: string, readOnly: true }", "id"))));

        Assert.Null(RuleSeverityOverrides.From(null, ["ATC_API_SCH029"]).Apply(warning));
        Assert.Equal(
            DiagnosticSeverity.Error,
            RuleSeverityOverrides.From(new Dictionary<string, string>(StringComparer.Ordinal) { ["ATC_API_SCH029"] = "error" }, null).Apply(warning)!.Severity);
    }

    /// <summary>
    /// A derived schema used in responses only inherits from a base used both ways: its inherited required readOnly
    /// property is reported once, on the base that declares it - and its own, decided by the base's use too, on it.
    /// </summary>
    [Fact]
    public void Validate_AllOfInheritance_ReportsEachPropertyOnTheSchemaThatDeclaresIt()
    {
        var document = Parse("""
            openapi: 3.0.3
            info: { title: T, version: 1.0.0 }
            paths:
              /animals:
                post:
                  requestBody:
                    content:
                      application/json:
                        schema: { $ref: '#/components/schemas/Animal' }
                  responses:
                    '204': { description: Created }
              /dogs:
                get:
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema: { $ref: '#/components/schemas/Dog' }
            components:
              schemas:
                Animal:
                  type: object
                  required: [id]
                  properties:
                    id: { type: string, readOnly: true }
                Dog:
                  allOf:
                    - $ref: '#/components/schemas/Animal'
                    - type: object
                      required: [tagged]
                      properties:
                        tagged: { type: boolean, readOnly: true }
            """);

        var messages = Sch029(ValidateSpecificationStrategy.Standard, document).Select(d => d.Message).ToList();

        Assert.Equal(2, messages.Count);
        Assert.Single(messages, m => m.Contains("'Animal.id'", StringComparison.Ordinal));
        Assert.Single(messages, m => m.Contains("'Dog.tagged'", StringComparison.Ordinal) && m.Contains("'Animal'", StringComparison.Ordinal));
    }

    private static string BothWays(
        string property,
        string required)
        => $$"""
            openapi: 3.0.3
            info: { title: T, version: 1.0.0 }
            paths:
              /pets:
                post:
                  requestBody:
                    content:
                      application/json:
                        schema: { $ref: '#/components/schemas/Pet' }
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema: { $ref: '#/components/schemas/Pet' }
            components:
              schemas:
                Pet:
                  type: object
                  required: [{{required}}]
                  properties:
                    {{property}}
                    name: { type: string }

            """.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static OpenApiDocument Parse(string yaml)
        => OpenApiDocumentHelper.ParseYaml(yaml);

    private static List<DiagnosticMessage> Sch029(
        ValidateSpecificationStrategy strategy,
        OpenApiDocument document)
        => OpenApiDocumentValidator.Validate(strategy, document, [], TestFilePath)
            .Where(d => d.RuleId == RuleIdentifiers.RequiredReadOrWriteOnlyPropertyGeneratedOptional)
            .ToList();
}