namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Tests for SchemaExtractor regarding JsonPropertyName attributes on record parameters
/// for OpenAPI property keys with underscores, dots, hyphens or non-standard casing.
/// </summary>
public class SchemaExtractorJsonPropertyNameTests
{
    [Fact]
    public void ExtractForSchemas_WithUnderscoreAndDotProperties_EmitsJsonPropertyNameAttributes()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
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

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "MyEnergyDataMarketDocument" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.NotNull(record.Parameters);

        AssertJsonPropertyName(record.Parameters, "MRid", "mRID");
        AssertJsonPropertyName(record.Parameters, "SenderMarketParticipantName", "sender_MarketParticipant.name");
        AssertJsonPropertyName(record.Parameters, "PeriodTimeInterval", "period.timeInterval");

        var createdDateTime = record.Parameters.First(p => p.Name == "CreatedDateTime");
        Assert.True(
            createdDateTime.Attributes is null ||
            createdDateTime.Attributes.All(a => !string.Equals(a.Name, "JsonPropertyName", StringComparison.Ordinal)),
            "Standard camelCase property should not have a JsonPropertyName attribute");

        Assert.Contains("using System.Text.Json.Serialization;", result.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithStandardProperties_DoesNotEmitJsonPropertyNameAttributes()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                                    errorText:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.NotNull(record.Parameters);
        Assert.All(
            record.Parameters,
            p => Assert.True(
                p.Attributes is null ||
                p.Attributes.All(a => !string.Equals(a.Name, "JsonPropertyName", StringComparison.Ordinal))));

        Assert.DoesNotContain("using System.Text.Json.Serialization;", result.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithUnderscoreAndValidation_CombinesAttributes()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Order:
                                  type: object
                                  required:
                                    - error_code
                                  properties:
                                    error_code:
                                      type: string
                                      maxLength: 10
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Order" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.NotNull(record.Parameters);

        var parameter = record.Parameters.First(p => p.Name == "ErrorCode");
        Assert.NotNull(parameter.Attributes);
        Assert.Equal("JsonPropertyName", parameter.Attributes[0].Name);
        Assert.True(parameter.Attributes.Count > 1, "Validation attributes should be preserved alongside JsonPropertyName");
    }

    private static void AssertJsonPropertyName(
        IList<ParameterBaseParameters> parameters,
        string propertyName,
        string expectedJsonKey)
    {
        var parameter = parameters.First(p => p.Name == propertyName);
        Assert.NotNull(parameter.Attributes);
        var attribute = parameter.Attributes.First(a => string.Equals(a.Name, "JsonPropertyName", StringComparison.Ordinal));
        Assert.Equal($"\"{expectedJsonKey}\"", attribute.Content);
    }
}