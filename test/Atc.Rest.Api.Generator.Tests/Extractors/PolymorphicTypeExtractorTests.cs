namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class PolymorphicTypeExtractorTests
{
    [Fact]
    public void ExtractPolymorphicConfigs_WithExplicitDiscriminator_ReturnsConfig()
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
                                Shape:
                                  oneOf:
                                    - $ref: '#/components/schemas/Circle'
                                    - $ref: '#/components/schemas/Square'
                                  discriminator:
                                    propertyName: shapeType
                                    mapping:
                                      circle: '#/components/schemas/Circle'
                                      square: '#/components/schemas/Square'
                                Circle:
                                  type: object
                                  properties:
                                    shapeType:
                                      type: string
                                    radius:
                                      type: number
                                Square:
                                  type: object
                                  properties:
                                    shapeType:
                                      type: string
                                    sideLength:
                                      type: number
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var configs = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(document!);

        // Assert
        Assert.NotNull(configs);
        Assert.True(configs!.ContainsKey("Shape"));

        var config = configs["Shape"];
        Assert.Equal("Shape", config.BaseTypeName);
        Assert.Equal("shapeType", config.DiscriminatorPropertyName);
        Assert.True(config.IsOneOf);
        Assert.True(config.IsDiscriminatorExplicit);
        Assert.False(config.UsesCustomConverter);
        Assert.Equal(2, config.Variants.Count);
        Assert.Contains(config.Variants, v => v.TypeName == "Circle");
        Assert.Contains(config.Variants, v => v.TypeName == "Square");
    }

    [Fact]
    public void ExtractPolymorphicConfigs_WithAutoDetectedDiscriminator_ReturnsConfig()
    {
        // Arrange — anyOf where all variants share a common string property "type"
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Notification:
                                  anyOf:
                                    - $ref: '#/components/schemas/EmailNotification'
                                    - $ref: '#/components/schemas/SmsNotification'
                                EmailNotification:
                                  type: object
                                  properties:
                                    type:
                                      type: string
                                    emailAddress:
                                      type: string
                                SmsNotification:
                                  type: object
                                  properties:
                                    type:
                                      type: string
                                    phoneNumber:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var configs = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(document!);

        // Assert
        Assert.NotNull(configs);
        Assert.True(configs!.ContainsKey("Notification"));

        var config = configs["Notification"];
        Assert.Equal("Notification", config.BaseTypeName);
        Assert.False(config.IsOneOf);
        Assert.False(config.IsDiscriminatorExplicit);
        Assert.False(config.UsesCustomConverter);
        Assert.Equal("type", config.DiscriminatorPropertyName);
    }

    [Fact]
    public void ExtractPolymorphicConfigs_WithoutDiscriminator_ReturnsUnionConfig()
    {
        // Arrange — oneOf with no common string property
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                PaymentResult:
                                  oneOf:
                                    - $ref: '#/components/schemas/SuccessPayment'
                                    - $ref: '#/components/schemas/FailedPayment'
                                SuccessPayment:
                                  type: object
                                  properties:
                                    transactionId:
                                      type: integer
                                FailedPayment:
                                  type: object
                                  properties:
                                    errorCode:
                                      type: integer
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var configs = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(document!);

        // Assert
        Assert.NotNull(configs);
        Assert.True(configs!.ContainsKey("PaymentResult"));

        var config = configs["PaymentResult"];
        Assert.True(config.UsesCustomConverter);
        Assert.True(config.IsOneOf);
        Assert.False(config.IsDiscriminatorExplicit);
        Assert.Equal(2, config.Variants.Count);
    }

    [Fact]
    public void ExtractPolymorphicConfigs_NoSchemas_ReturnsNull()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var configs = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(document!);

        // Assert
        Assert.Null(configs);
    }

    [Fact]
    public void ExtractPolymorphicConfigs_NoPolymorphicSchemas_ReturnsNull()
    {
        // Arrange — plain object schemas only
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                User:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                                Order:
                                  type: object
                                  properties:
                                    total:
                                      type: number
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var configs = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(document!);

        // Assert
        Assert.Null(configs);
    }

    [Fact]
    public void GetBaseTypeForVariant_DiscriminatorConfig_ReturnsBaseType()
    {
        // Arrange
        var configs = new Dictionary<string, PolymorphicConfig>(StringComparer.Ordinal)
        {
            ["Shape"] = new PolymorphicConfig
            {
                BaseTypeName = "Shape",
                DiscriminatorPropertyName = "shapeType",
                IsOneOf = true,
                IsDiscriminatorExplicit = true,
                Variants =
                {
                    new PolymorphicVariant { TypeName = "Circle", SchemaRefId = "Circle", DiscriminatorValue = "circle" },
                    new PolymorphicVariant { TypeName = "Square", SchemaRefId = "Square", DiscriminatorValue = "square" },
                },
            },
        };

        // Act
        var result = PolymorphicTypeExtractor.GetBaseTypeForVariant("Circle", configs);

        // Assert
        Assert.Equal("Shape", result);
    }

    [Fact]
    public void GetBaseTypeForVariant_UnionConfig_ReturnsNull()
    {
        // Arrange — union type with UsesCustomConverter = true skips variant lookup
        var configs = new Dictionary<string, PolymorphicConfig>(StringComparer.Ordinal)
        {
            ["PaymentResult"] = new PolymorphicConfig
            {
                BaseTypeName = "PaymentResult",
                UsesCustomConverter = true,
                Variants =
                {
                    new PolymorphicVariant { TypeName = "SuccessPayment", SchemaRefId = "SuccessPayment" },
                    new PolymorphicVariant { TypeName = "FailedPayment", SchemaRefId = "FailedPayment" },
                },
            },
        };

        // Act
        var result = PolymorphicTypeExtractor.GetBaseTypeForVariant("SuccessPayment", configs);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetBaseTypeForVariant_UnknownSchema_ReturnsNull()
    {
        // Arrange
        var configs = new Dictionary<string, PolymorphicConfig>(StringComparer.Ordinal)
        {
            ["Shape"] = new PolymorphicConfig
            {
                BaseTypeName = "Shape",
                DiscriminatorPropertyName = "shapeType",
                Variants =
                {
                    new PolymorphicVariant { TypeName = "Circle", SchemaRefId = "Circle", DiscriminatorValue = "circle" },
                },
            },
        };

        // Act
        var result = PolymorphicTypeExtractor.GetBaseTypeForVariant("Triangle", configs);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetPolymorphicVariantSchemaNames_ReturnsAllVariants()
    {
        // Arrange
        var configs = new Dictionary<string, PolymorphicConfig>(StringComparer.Ordinal)
        {
            ["Shape"] = new PolymorphicConfig
            {
                BaseTypeName = "Shape",
                Variants =
                {
                    new PolymorphicVariant { TypeName = "Circle", SchemaRefId = "Circle" },
                    new PolymorphicVariant { TypeName = "Square", SchemaRefId = "Square" },
                },
            },
            ["Animal"] = new PolymorphicConfig
            {
                BaseTypeName = "Animal",
                Variants =
                {
                    new PolymorphicVariant { TypeName = "Dog", SchemaRefId = "Dog" },
                },
            },
        };

        // Act
        var result = PolymorphicTypeExtractor.GetPolymorphicVariantSchemaNames(configs);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("Circle", result);
        Assert.Contains("Square", result);
        Assert.Contains("Dog", result);
    }

    [Fact]
    public void GetPolymorphicVariantSchemaNames_NullConfigs_ReturnsEmpty()
    {
        // Act
        var result = PolymorphicTypeExtractor.GetPolymorphicVariantSchemaNames(null);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GeneratePolymorphicBaseType_ProducesExpectedCode()
    {
        // Arrange
        var config = new PolymorphicConfig
        {
            BaseTypeName = "Shape",
            DiscriminatorPropertyName = "shapeType",
            IsOneOf = true,
            IsDiscriminatorExplicit = true,
            Variants =
            {
                new PolymorphicVariant { TypeName = "Circle", DiscriminatorValue = "circle" },
                new PolymorphicVariant { TypeName = "Square", DiscriminatorValue = "square" },
            },
        };

        // Act
        var result = PolymorphicTypeExtractor.GeneratePolymorphicBaseType(config, "TestProject");

        // Assert
        Assert.Contains("[JsonPolymorphic(TypeDiscriminatorPropertyName = \"shapeType\")]", result, StringComparison.Ordinal);
        Assert.Contains("[JsonDerivedType(typeof(Circle), \"circle\")]", result, StringComparison.Ordinal);
        Assert.Contains("[JsonDerivedType(typeof(Square), \"square\")]", result, StringComparison.Ordinal);
        Assert.Contains("public abstract record Shape;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateUnionBaseType_ProducesExpectedCode()
    {
        // Arrange
        var config = new PolymorphicConfig
        {
            BaseTypeName = "PaymentResult",
            IsOneOf = true,
            UsesCustomConverter = true,
            Variants =
            {
                new PolymorphicVariant { TypeName = "SuccessPayment" },
                new PolymorphicVariant { TypeName = "FailedPayment" },
            },
        };

        // Act
        var result = PolymorphicTypeExtractor.GenerateUnionBaseType(config, "TestProject");

        // Assert
        Assert.Contains("[JsonConverter(typeof(PaymentResultJsonConverter))]", result, StringComparison.Ordinal);
        Assert.Contains("public sealed record PaymentResult(object Value)", result, StringComparison.Ordinal);
        Assert.Contains("public static implicit operator PaymentResult(SuccessPayment value)", result, StringComparison.Ordinal);
        Assert.Contains("public static implicit operator PaymentResult(FailedPayment value)", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateUnionConverter_ProducesExpectedCode()
    {
        // Arrange
        var config = new PolymorphicConfig
        {
            BaseTypeName = "PaymentResult",
            UsesCustomConverter = true,
            Variants =
            {
                new PolymorphicVariant { TypeName = "SuccessPayment" },
                new PolymorphicVariant { TypeName = "FailedPayment" },
            },
        };

        // Act
        var result = PolymorphicTypeExtractor.GenerateUnionConverter(config, "TestProject");

        // Assert
        Assert.Contains("JsonConverter<PaymentResult>", result, StringComparison.Ordinal);
        Assert.Contains("Deserialize<SuccessPayment>", result, StringComparison.Ordinal);
        Assert.Contains("Deserialize<FailedPayment>", result, StringComparison.Ordinal);
        Assert.Contains("catch (JsonException ex)", result, StringComparison.Ordinal);
        Assert.Contains("no matching variant found", result, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}