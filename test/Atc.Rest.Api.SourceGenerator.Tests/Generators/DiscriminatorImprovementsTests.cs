namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>Tests for ATC_API_SCH020 and OpenAPI 3.2 discriminator improvements.</summary>
public class DiscriminatorImprovementsTests
{
    [Fact]
    public void DiscriminatorWithoutPropertyName_WhenAutoDetectFails_SCH020_ProducesWarningDiagnostic()
    {
        // A polymorphic schema with a discriminator block that has no propertyName,
        // and variants with no common string property (auto-detect fails) should emit ATC_API_SCH020.
        const string yaml = """
            openapi: "3.2.0"
            info:
              title: SCH020 Test
              version: 1.0.0
            paths:
              /shapes:
                get:
                  operationId: listShapes
                  tags:
                    - shapes
                  responses:
                    "200":
                      description: OK
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/Shape'
            components:
              schemas:
                Shape:
                  oneOf:
                    - $ref: '#/components/schemas/Circle'
                    - $ref: '#/components/schemas/Square'
                  discriminator:
                    mapping:
                      circle: '#/components/schemas/Circle'
                      square: '#/components/schemas/Square'
                Circle:
                  type: object
                  properties:
                    radius:
                      type: number
                Square:
                  type: object
                  properties:
                    sideLength:
                      type: number
            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Standard,
            doc,
            [],
            "sch020-test.yaml");

        Assert.True(
            diagnostics.Any(d => d.RuleId == Generator.RuleIdentifiers.DiscriminatorMissingPropertyName),
            "Expected ATC_API_SCH020 warning for discriminator block without propertyName when auto-detect fails.");
    }

    [Fact]
    public void DiscriminatorWithoutPropertyName_WhenAutoDetectSucceeds_NoDiagnostic()
    {
        // When propertyName is absent but all variants share a common string property,
        // auto-detect succeeds and ATC_API_SCH020 should NOT be emitted.
        const string yaml = """
            openapi: "3.2.0"
            info:
              title: No SCH020 Test
              version: 1.0.0
            paths:
              /events:
                get:
                  operationId: listEvents
                  tags:
                    - events
                  responses:
                    "200":
                      description: OK
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/Event'
            components:
              schemas:
                Event:
                  oneOf:
                    - $ref: '#/components/schemas/UserEvent'
                    - $ref: '#/components/schemas/OrderEvent'
                  discriminator:
                    mapping:
                      user: '#/components/schemas/UserEvent'
                      order: '#/components/schemas/OrderEvent'
                UserEvent:
                  type: object
                  properties:
                    eventType:
                      type: string
                    userId:
                      type: string
                OrderEvent:
                  type: object
                  properties:
                    eventType:
                      type: string
                    orderId:
                      type: string
            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Standard,
            doc,
            [],
            "no-sch020-test.yaml");

        Assert.False(
            diagnostics.Any(d => d.RuleId == Generator.RuleIdentifiers.DiscriminatorMissingPropertyName),
            "ATC_API_SCH020 should not be emitted when auto-detect finds a common discriminator property.");
    }
}