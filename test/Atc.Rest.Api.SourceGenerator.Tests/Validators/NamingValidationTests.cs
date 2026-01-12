namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

public class NamingValidationTests
{
    private const string TestFilePath = "test.yaml";

    [Fact]
    public void Validate_OperationIdStartsWithUppercase_ReportsNAM001()
    {
        // Arrange
        var document = ParseYaml(CreateOperationYaml(operationId: "ListPets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var nam001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationIdMustBeCamelCase);
        Assert.NotNull(nam001);
        Assert.Contains("ListPets", nam001.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_OperationIdStartsWithLowercase_NoNAM001()
    {
        // Arrange
        var document = ParseYaml(CreateOperationYaml(operationId: "listPets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var nam001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationIdMustBeCamelCase);
        Assert.Null(nam001);
    }

    [Fact]
    public void Validate_ModelNameNotPascalCase_ReportsNAM002()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaYaml(schemaName: "petModel"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var nam002 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ModelNameMustBePascalCase);
        Assert.NotNull(nam002);
        Assert.Contains("petModel", nam002.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ModelNamePascalCase_NoNAM002()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaYaml(schemaName: "PetModel"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var nam002 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ModelNameMustBePascalCase);
        Assert.Null(nam002);
    }

    [Fact]
    public void Validate_PropertyNameNotCamelCase_ReportsNAM003()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithPropertyYaml(propertyName: "FirstName"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var nam003 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PropertyNameMustBeCamelCase);
        Assert.NotNull(nam003);
        Assert.Contains("FirstName", nam003.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PropertyNameCamelCase_NoNAM003()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithPropertyYaml(propertyName: "firstName"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var nam003 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PropertyNameMustBeCamelCase);
        Assert.Null(nam003);
    }

    [Fact]
    public void Validate_ParameterNameNotCamelCase_ReportsNAM004()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithParameterYaml(parameterName: "PetId"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var nam004 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ParameterNameMustBeCamelCase);
        Assert.NotNull(nam004);
        Assert.Contains("PetId", nam004.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ParameterNameCamelCase_NoNAM004()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithParameterYaml(parameterName: "petId"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var nam004 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ParameterNameMustBeCamelCase);
        Assert.Null(nam004);
    }

    [Fact]
    public void Validate_HeaderParameterHyphenatedName_NoNAM004()
    {
        // Arrange - Header parameters use HTTP header naming conventions (hyphenated), not camelCase
        var document = ParseYaml(CreateOperationWithHeaderParameterYaml(parameterName: "x-continuation"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert - Header parameters should be excluded from NAM004
        var nam004 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ParameterNameMustBeCamelCase);
        Assert.Null(nam004);
    }

    [Fact]
    public void Validate_HeaderParameterPascalCase_NoNAM004()
    {
        // Arrange - Even PascalCase header names should not trigger NAM004
        var document = ParseYaml(CreateOperationWithHeaderParameterYaml(parameterName: "X-Request-Id"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert - Header parameters should be excluded from NAM004
        var nam004 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ParameterNameMustBeCamelCase);
        Assert.Null(nam004);
    }

    [Fact]
    public void Validate_QueryParameterNotCamelCase_ReportsNAM004()
    {
        // Arrange - Query parameters (unlike headers) should still require camelCase
        var document = ParseYaml(CreateOperationWithQueryParameterYaml(parameterName: "PageSize"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert - Query parameters should still trigger NAM004
        var nam004 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ParameterNameMustBeCamelCase);
        Assert.NotNull(nam004);
        Assert.Contains("PageSize", nam004.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_EnumValueNotValidCasing_ReportsNAM005()
    {
        // Arrange
        var document = ParseYaml(CreateEnumSchemaYaml(enumValues: ["available", "pending"]));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var nam005 = diagnostics
            .Where(d => d.RuleId == Generator.RuleIdentifiers.EnumValueCasing)
            .ToList();
        Assert.Equal(2, nam005.Count);
    }

    [Fact]
    public void Validate_EnumValuePascalCase_NoNAM005()
    {
        // Arrange
        var document = ParseYaml(CreateEnumSchemaYaml(enumValues: ["Available", "Pending"]));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var nam005 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.EnumValueCasing);
        Assert.Null(nam005);
    }

    [Fact]
    public void Validate_EnumValueUpperSnakeCase_NoNAM005()
    {
        // Arrange
        var document = ParseYaml(CreateEnumSchemaYaml(enumValues: ["AVAILABLE", "PENDING_APPROVAL"]));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var nam005 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.EnumValueCasing);
        Assert.Null(nam005);
    }

    [Fact]
    public void Validate_TagNameNotKebabCase_ReportsNAM006()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithTagYaml(tagName: "PetStore"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var nam006 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.TagNameMustBeKebabCase);
        Assert.NotNull(nam006);
        Assert.Contains("PetStore", nam006.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_TagNameKebabCase_NoNAM006()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithTagYaml(tagName: "pet-store"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var nam006 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.TagNameMustBeKebabCase);
        Assert.Null(nam006);
    }

    [Fact]
    public void Validate_TagNameWithConsecutiveUppercase_SuggestsCorrectKebabCase()
    {
        // Arrange - "QW IoT Nexus" should suggest "qw-iot-nexus", NOT "q-w-iot-nexus"
        var document = ParseYaml(CreateOperationWithTagYaml(tagName: "QW IoT Nexus"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var nam006 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.TagNameMustBeKebabCase);
        Assert.NotNull(nam006);
        Assert.Contains("QW IoT Nexus", nam006.Message, StringComparison.Ordinal);

        // Verify the suggestion is correct (consecutive uppercase "KL" should become "kl", not "k-l")
        Assert.NotNull(nam006.Suggestions);
        Assert.Single(nam006.Suggestions);
        Assert.Contains("qw-iot-nexus", nam006.Suggestions[0], StringComparison.Ordinal);
        Assert.DoesNotContain("q-w-iot-nexus", nam006.Suggestions[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NoneStrategy_SkipsAllValidation()
    {
        // Arrange - Invalid casing
        var document = ParseYaml(CreateOperationYaml(operationId: "ListPets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.None,
            document,
            [],
            TestFilePath);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Validate_StandardStrategy_SkipsNamingValidation()
    {
        // Arrange - Invalid casing
        var document = ParseYaml(CreateOperationYaml(operationId: "ListPets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Standard,
            document,
            [],
            TestFilePath);

        // Assert - NAM001 should NOT be reported in Standard mode
        var nam001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationIdMustBeCamelCase);
        Assert.Null(nam001);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, TestFilePath, out var document)
            ? document
            : null;

    private static string CreateOperationYaml(string operationId)
        => $"""

            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /pets:
                get:
                  operationId: {operationId}
                  responses:
                    '200':
                      description: Success

            """;

    private static string CreateSchemaYaml(string schemaName)
        => $$"""

             openapi: 3.0.0
             info:
               title: Test API
               version: 1.0.0
             paths: {}
             components:
               schemas:
                 {{schemaName}}:
                   type: object

             """;

    private static string CreateSchemaWithPropertyYaml(string propertyName)
        => $$"""

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
                     {{propertyName}}:
                       type: string

             """;

    private static string CreateOperationWithParameterYaml(string parameterName)
        => $$"""

             openapi: 3.0.0
             info:
               title: Test API
               version: 1.0.0
             paths:
               /pets/{id}:
                 get:
                   operationId: getPet
                   parameters:
                     - name: {{parameterName}}
                       in: path
                       required: true
                       schema:
                         type: string
                   responses:
                     '200':
                       description: Success

             """;

    private static string CreateOperationWithHeaderParameterYaml(
        string parameterName)
        => $$"""

             openapi: 3.0.0
             info:
               title: Test API
               version: 1.0.0
             paths:
               /pets:
                 get:
                   operationId: getPets
                   parameters:
                     - name: {{parameterName}}
                       in: header
                       schema:
                         type: string
                   responses:
                     '200':
                       description: Success

             """;

    private static string CreateOperationWithQueryParameterYaml(
        string parameterName)
        => $$"""

             openapi: 3.0.0
             info:
               title: Test API
               version: 1.0.0
             paths:
               /pets:
                 get:
                   operationId: getPets
                   parameters:
                     - name: {{parameterName}}
                       in: query
                       schema:
                         type: integer
                   responses:
                     '200':
                       description: Success

             """;

    private static string CreateEnumSchemaYaml(string[] enumValues)
    {
        var enumStr = string.Join("\n        - ", enumValues);
        return $$"""

                 openapi: 3.0.0
                 info:
                   title: Test API
                   version: 1.0.0
                 paths: {}
                 components:
                   schemas:
                     Status:
                       type: string
                       enum:
                         - {{enumStr}}

                 """;
    }

    private static string CreateOperationWithTagYaml(string tagName)
        => $"""

            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            tags:
              - name: {tagName}
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags:
                    - {tagName}
                  responses:
                    '200':
                      description: Success

            """;
}