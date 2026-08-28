namespace Atc.Rest.Api.Generator.IntegrationTests;

/// <summary>
/// End-to-end verification that OpenAPI property keys with underscores, dots or
/// non-standard casing produce records annotated with [property: JsonPropertyName("...")]
/// so that System.Text.Json can map them correctly.
/// </summary>
public class JsonPropertyNameGenerationTests
{
    private const string Yaml = """
                                openapi: 3.0.0
                                info:
                                  title: Eloverblik Test API
                                  version: 1.0.0
                                paths: {}
                                components:
                                  schemas:
                                    MyEnergyDataMarketDocumentResponse:
                                      type: object
                                      properties:
                                        success:
                                          type: boolean
                                        errorText:
                                          type: string
                                          nullable: true
                                        MyEnergyData_MarketDocument:
                                          $ref: "#/components/schemas/MyEnergyDataMarketDocument"
                                    MyEnergyDataMarketDocument:
                                      type: object
                                      properties:
                                        mRID:
                                          type: string
                                          nullable: true
                                        createdDateTime:
                                          type: string
                                          nullable: true
                                        sender_MarketParticipant.name:
                                          type: string
                                          nullable: true
                                        period.timeInterval:
                                          type: string
                                          nullable: true
                                """;

    [Fact]
    public void GenerateModels_WithNonStandardPropertyKeys_EmitsJsonPropertyNameAttributes()
    {
        // Arrange
        var document = ParseYaml();

        // Act
        var models = CodeGenerationService
            .GenerateModels(document, "EloverblikTest", CodeGenerationService.GeneratorType.Client)
            .ToList();

        // Assert
        var document1 = models.Single(m => m.TypeName == "MyEnergyDataMarketDocument");
        Assert.Contains(
            """[property: JsonPropertyName("mRID")] string? MRid""",
            document1.Content,
            StringComparison.Ordinal);
        Assert.Contains(
            """[property: JsonPropertyName("sender_MarketParticipant.name")] string? SenderMarketParticipantName""",
            document1.Content,
            StringComparison.Ordinal);
        Assert.Contains(
            """[property: JsonPropertyName("period.timeInterval")] string? PeriodTimeInterval""",
            document1.Content,
            StringComparison.Ordinal);

        // Standard camelCase key needs no attribute
        Assert.Contains("string? CreatedDateTime", document1.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonPropertyName(\"createdDateTime\")", document1.Content, StringComparison.Ordinal);

        var response = models.Single(m => m.TypeName == "MyEnergyDataMarketDocumentResponse");
        Assert.Contains(
            """[property: JsonPropertyName("MyEnergyData_MarketDocument")] MyEnergyDataMarketDocument MyEnergyDataMarketDocument""",
            response.Content,
            StringComparison.Ordinal);
        Assert.DoesNotContain("JsonPropertyName(\"success\")", response.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateModels_WithNonStandardPropertyKeys_IncludesJsonSerializationUsing()
    {
        // Arrange
        var document = ParseYaml();

        // Act
        var models = CodeGenerationService
            .GenerateModels(document, "EloverblikTest", CodeGenerationService.GeneratorType.Client)
            .ToList();

        // Assert
        var document1 = models.Single(m => m.TypeName == "MyEnergyDataMarketDocument");
        Assert.Contains("System.Text.Json.Serialization", document1.RequiredUsings, StringComparer.Ordinal);
    }

    private static OpenApiDocument ParseYaml()
    {
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(Yaml, "test.yaml");
        Assert.NotNull(document);
        return document!;
    }
}