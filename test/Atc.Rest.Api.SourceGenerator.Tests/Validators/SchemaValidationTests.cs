namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
public class SchemaValidationTests
{
    private const string TestFilePath = "test.yaml";

    // ========== SCH001: Missing title on array type ==========

    [Fact]
    public void Validate_ArrayTypeMissingTitle_ReportsSCH001()
    {
        // Arrange
        var document = ParseYaml(CreateArraySchemaYaml(title: null));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ArrayTitleMissing);
        Assert.NotNull(sch001);
        Assert.Contains("Pets", sch001.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ArrayTypeHasTitle_NoSCH001()
    {
        // Arrange
        var document = ParseYaml(CreateArraySchemaYaml(title: "Pets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ArrayTitleMissing);
        Assert.Null(sch001);
    }

    // ========== SCH002: Array type title not starting with uppercase ==========

    [Fact]
    public void Validate_ArrayTypeTitleLowercase_ReportsSCH002()
    {
        // Arrange
        var document = ParseYaml(CreateArraySchemaYaml(title: "pets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch002 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ArrayTitleNotUppercase);
        Assert.NotNull(sch002);
        Assert.Contains("pets", sch002.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ArrayTypeTitleUppercase_NoSCH002()
    {
        // Arrange
        var document = ParseYaml(CreateArraySchemaYaml(title: "Pets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch002 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ArrayTitleNotUppercase);
        Assert.Null(sch002);
    }

    // ========== SCH003: Missing title on object type ==========

    [Fact]
    public void Validate_ObjectTypeMissingTitle_ReportsSCH003()
    {
        // Arrange
        var document = ParseYaml(CreateObjectSchemaYaml(title: null));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch003 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ObjectTitleMissing);
        Assert.NotNull(sch003);
        Assert.Contains("Pet", sch003.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ObjectTypeHasTitle_NoSCH003()
    {
        // Arrange
        var document = ParseYaml(CreateObjectSchemaYaml(title: "Pet"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch003 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ObjectTitleMissing);
        Assert.Null(sch003);
    }

    // ========== SCH004: Object type title not starting with uppercase ==========

    [Fact]
    public void Validate_ObjectTypeTitleLowercase_ReportsSCH004()
    {
        // Arrange
        var document = ParseYaml(CreateObjectSchemaYaml(title: "pet"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch004 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ObjectTitleNotUppercase);
        Assert.NotNull(sch004);
        Assert.Contains("pet", sch004.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ObjectTypeTitleUppercase_NoSCH004()
    {
        // Arrange
        var document = ParseYaml(CreateObjectSchemaYaml(title: "Pet"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch004 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ObjectTitleNotUppercase);
        Assert.Null(sch004);
    }

    // ========== SCH005: Implicit object definition in array property not supported ==========

    [Fact]
    public void Validate_ArrayPropertyWithImplicitObject_ReportsSCH005()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithArrayPropertyHavingImplicitObjectYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch005 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ImplicitArrayObjectNotSupported);
        Assert.NotNull(sch005);
        Assert.Contains("children", sch005.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ArrayPropertyWithReference_NoSCH005()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithArrayPropertyHavingReferenceYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch005 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ImplicitArrayObjectNotSupported);
        Assert.Null(sch005);
    }

    // ========== SCH006: Object name not using correct casing style ==========

    [Fact]
    public void Validate_ObjectNameNotPascalCase_ReportsSCH006()
    {
        // Arrange
        var document = ParseYaml(CreateObjectSchemaWithNameYaml(schemaName: "petModel"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch006 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ObjectNameCasing);
        Assert.NotNull(sch006);
        Assert.Contains("petModel", sch006.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ObjectNamePascalCase_NoSCH006()
    {
        // Arrange
        var document = ParseYaml(CreateObjectSchemaWithNameYaml(schemaName: "PetModel"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch006 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ObjectNameCasing);
        Assert.Null(sch006);
    }

    // ========== SCH007: Object property name not using correct casing style ==========

    [Fact]
    public void Validate_PropertyNameNotCamelCase_ReportsSCH007()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithPropertyNameYaml(propertyName: "FirstName"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch007 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PropertyNameCasing);
        Assert.NotNull(sch007);
        Assert.Contains("FirstName", sch007.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PropertyNameCamelCase_NoSCH007()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithPropertyNameYaml(propertyName: "firstName"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch007 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PropertyNameCasing);
        Assert.Null(sch007);
    }

    // ========== SCH008: Enum name not using correct casing style ==========

    [Fact]
    public void Validate_EnumNameNotPascalCase_ReportsSCH008()
    {
        // Arrange
        var document = ParseYaml(CreateEnumSchemaWithNameYaml(schemaName: "petStatus"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch008 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.EnumNameCasing);
        Assert.NotNull(sch008);
        Assert.Contains("petStatus", sch008.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_EnumNamePascalCase_NoSCH008()
    {
        // Arrange
        var document = ParseYaml(CreateEnumSchemaWithNameYaml(schemaName: "PetStatus"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch008 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.EnumNameCasing);
        Assert.Null(sch008);
    }

    // ========== SCH009: Array property missing data type specification ==========

    [Fact]
    public void Validate_ArrayPropertyItemsMissingType_ReportsSCH009()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithArrayPropertyMissingTypeYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch009 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ArrayPropertyMissingType);
        Assert.NotNull(sch009);
        Assert.Contains("tags", sch009.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ArrayPropertyItemsHasType_NoSCH009()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithArrayPropertyHavingTypeYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch009 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ArrayPropertyMissingType);
        Assert.Null(sch009);
    }

    // ========== SCH010: Implicit object definition on property not supported ==========

    [Fact]
    public void Validate_PropertyWithImplicitObject_ReportsSCH010()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithImplicitObjectPropertyYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch010 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ImplicitObjectNotSupported);
        Assert.NotNull(sch010);
        Assert.Contains("address", sch010.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PropertyWithReference_NoSCH010()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithReferencePropertyYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch010 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ImplicitObjectNotSupported);
        Assert.Null(sch010);
    }

    // ========== SCH011: Array property missing items specification ==========

    [Fact]
    public void Validate_ArrayPropertyMissingItems_ReportsSCH011()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithArrayPropertyMissingItemsYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch011 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ArrayPropertyMissingItems);
        Assert.NotNull(sch011);
        Assert.Contains("tags", sch011.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ArrayPropertyHasItems_NoSCH011()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithArrayPropertyHavingItemsYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch011 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ArrayPropertyMissingItems);
        Assert.Null(sch011);
    }

    // ========== SCH012: Missing key/name for object property ==========

    [Fact]
    public void Validate_PropertyWithEmptyName_ReportsSCH012()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithEmptyPropertyNameYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch012 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PropertyKeyMissing);
        Assert.NotNull(sch012);
    }

    [Fact]
    public void Validate_PropertyWithValidName_NoSCH012()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithPropertyNameYaml(propertyName: "name"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch012 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PropertyKeyMissing);
        Assert.Null(sch012);
    }

    // ========== SCH013: Schema reference does not exist ==========

    [Fact]
    public void Validate_InvalidSchemaReference_ReportsSCH013()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithInvalidReferenceYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch013 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.InvalidSchemaReference);
        Assert.NotNull(sch013);
        Assert.Contains("NonExistent", sch013.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ValidSchemaReference_NoSCH013()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithReferencePropertyYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch013 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.InvalidSchemaReference);
        Assert.Null(sch013);
    }

    // ========== SCH014: Multiple non-null types (OpenAPI 3.1) ==========

    [Fact]
    public void Validate_MultipleNonNullTypes_ReportsSCH014()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithMultipleNonNullTypesYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch014 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.MultipleNonNullTypes);
        Assert.NotNull(sch014);
    }

    [Fact]
    public void Validate_SingleType_NoSCH014()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithPropertyNameYaml(propertyName: "name"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch014 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.MultipleNonNullTypes);
        Assert.Null(sch014);
    }

    // ========== SCH015: $ref with sibling properties ==========

    [Fact]
    public void Validate_RefWithSiblingProperties_ReportsSCH015()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithRefAndSiblingPropertiesYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch015 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RefWithSiblingProperties);
        Assert.NotNull(sch015);
    }

    [Fact]
    public void Validate_RefWithoutSiblingProperties_NoSCH015()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithReferencePropertyYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch015 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RefWithSiblingProperties);
        Assert.Null(sch015);
    }

    // ========== SCH016: Schema uses const value ==========

    [Fact]
    public void Validate_SchemaUsesConst_ReportsSCH016()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithConstValueYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch016 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.SchemaUsesConstValue);
        Assert.NotNull(sch016);
    }

    [Fact]
    public void Validate_SchemaWithoutConst_NoSCH016()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithPropertyNameYaml(propertyName: "name"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch016 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.SchemaUsesConstValue);
        Assert.Null(sch016);
    }

    // ========== SCH017: Schema uses unevaluatedProperties ==========

    [Fact]
    public void Validate_SchemaUsesUnevaluatedProperties_ReportsSCH017()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithUnevaluatedPropertiesYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch017 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.UnevaluatedPropertiesNotSupported);
        Assert.NotNull(sch017);
        Assert.Contains("unevaluatedProperties", sch017.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_SchemaWithoutUnevaluatedProperties_NoSCH017()
    {
        // Arrange
        var document = ParseYaml(CreateSchemaWithPropertyNameYaml(propertyName: "name"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sch017 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.UnevaluatedPropertiesNotSupported);
        Assert.Null(sch017);
    }

    // ========== Helper Methods ==========

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, TestFilePath, out var document)
            ? document
            : null;

    private static string CreateArraySchemaYaml(string? title)
    {
        var titleLine = title != null ? $"title: {title}" : string.Empty;
        return $$"""

                 openapi: 3.0.0
                 info:
                   title: Test API
                   version: 1.0.0
                 paths: {}
                 components:
                   schemas:
                     Pets:
                       type: array
                       {{titleLine}}
                       items:
                         type: string

                 """;
    }

    private static string CreateObjectSchemaYaml(string? title)
    {
        var titleLine = title != null ? $"title: {title}" : string.Empty;
        return $$"""

                 openapi: 3.0.0
                 info:
                   title: Test API
                   version: 1.0.0
                 paths: {}
                 components:
                   schemas:
                     Pet:
                       type: object
                       {{titleLine}}
                       properties:
                         id:
                           type: integer

                 """;
    }

    private static string CreateSchemaWithArrayPropertyHavingImplicitObjectYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths: {}
           components:
             schemas:
               Parent:
                 type: object
                 title: Parent
                 properties:
                   children:
                     type: array
                     items:
                       type: object
                       properties:
                         name:
                           type: string

           """;

    private static string CreateSchemaWithArrayPropertyHavingReferenceYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths: {}
           components:
             schemas:
               Child:
                 type: object
                 title: Child
                 properties:
                   name:
                     type: string
               Parent:
                 type: object
                 title: Parent
                 properties:
                   children:
                     type: array
                     items:
                       $ref: '#/components/schemas/Child'

           """;

    private static string CreateObjectSchemaWithNameYaml(string schemaName)
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
                   title: {{schemaName}}
                   properties:
                     id:
                       type: integer

             """;

    private static string CreateSchemaWithPropertyNameYaml(string propertyName)
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
                   title: Pet
                   properties:
                     {{propertyName}}:
                       type: string

             """;

    private static string CreateEnumSchemaWithNameYaml(string schemaName)
        => $$"""

             openapi: 3.0.0
             info:
               title: Test API
               version: 1.0.0
             paths: {}
             components:
               schemas:
                 {{schemaName}}:
                   type: string
                   enum:
                     - Available
                     - Pending

             """;

    private static string CreateSchemaWithArrayPropertyMissingTypeYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths: {}
           components:
             schemas:
               Pet:
                 type: object
                 title: Pet
                 properties:
                   tags:
                     type: array
                     items: {}

           """;

    private static string CreateSchemaWithArrayPropertyHavingTypeYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths: {}
           components:
             schemas:
               Pet:
                 type: object
                 title: Pet
                 properties:
                   tags:
                     type: array
                     items:
                       type: string

           """;

    private static string CreateSchemaWithImplicitObjectPropertyYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths: {}
           components:
             schemas:
               Pet:
                 type: object
                 title: Pet
                 properties:
                   address:
                     type: object
                     properties:
                       street:
                         type: string

           """;

    private static string CreateSchemaWithReferencePropertyYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths: {}
           components:
             schemas:
               Address:
                 type: object
                 title: Address
                 properties:
                   street:
                     type: string
               Pet:
                 type: object
                 title: Pet
                 properties:
                   address:
                     $ref: '#/components/schemas/Address'

           """;

    private static string CreateSchemaWithArrayPropertyMissingItemsYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths: {}
           components:
             schemas:
               Pet:
                 type: object
                 title: Pet
                 properties:
                   tags:
                     type: array

           """;

    private static string CreateSchemaWithArrayPropertyHavingItemsYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths: {}
           components:
             schemas:
               Pet:
                 type: object
                 title: Pet
                 properties:
                   tags:
                     type: array
                     items:
                       type: string

           """;

    private static string CreateSchemaWithEmptyPropertyNameYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths: {}
           components:
             schemas:
               Pet:
                 type: object
                 title: Pet
                 properties:
                   "":
                     type: string

           """;

    private static string CreateSchemaWithInvalidReferenceYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths: {}
           components:
             schemas:
               Pet:
                 type: object
                 title: Pet
                 properties:
                   owner:
                     $ref: '#/components/schemas/NonExistent'

           """;

    private static string CreateSchemaWithMultipleNonNullTypesYaml()
        => """

           openapi: 3.1.0
           info:
             title: Test API
             version: 1.0.0
           paths: {}
           components:
             schemas:
               Pet:
                 type: object
                 title: Pet
                 properties:
                   value:
                     type:
                       - string
                       - integer

           """;

    private static string CreateSchemaWithRefAndSiblingPropertiesYaml()
        => """

           openapi: 3.1.0
           info:
             title: Test API
             version: 1.0.0
           paths: {}
           components:
             schemas:
               Address:
                 type: object
                 title: Address
                 properties:
                   street:
                     type: string
               Pet:
                 type: object
                 title: Pet
                 properties:
                   address:
                     $ref: '#/components/schemas/Address'
                     description: The pet's home address
                     deprecated: true

           """;

    private static string CreateSchemaWithConstValueYaml()
        => """

           openapi: 3.1.0
           info:
             title: Test API
             version: 1.0.0
           paths: {}
           components:
             schemas:
               Pet:
                 type: object
                 title: Pet
                 properties:
                   type:
                     const: "pet"

           """;

    private static string CreateSchemaWithUnevaluatedPropertiesYaml()
        => """

           openapi: 3.1.0
           info:
             title: Test API
             version: 1.0.0
           paths: {}
           components:
             schemas:
               Base:
                 type: object
                 title: Base
                 properties:
                   id:
                     type: integer
               Pet:
                 type: object
                 title: Pet
                 unevaluatedProperties: false
                 allOf:
                   - $ref: '#/components/schemas/Base'
                   - type: object
                     properties:
                       name:
                         type: string

           """;
}