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
        var configs = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(document);

        // Assert
        Assert.NotNull(configs);
        Assert.True(configs.ContainsKey("Shape"));

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
        var configs = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(document);

        // Assert
        Assert.NotNull(configs);
        Assert.True(configs.ContainsKey("Notification"));

        var config = configs["Notification"];
        Assert.Equal("Notification", config.BaseTypeName);
        Assert.False(config.IsOneOf);
        Assert.False(config.IsDiscriminatorExplicit);
        Assert.False(config.UsesCustomConverter);
        Assert.Equal("type", config.DiscriminatorPropertyName);
    }

    [Fact]
    public void ExtractPolymorphicConfigs_AutoDetectedDiscriminator_UsesVerbatimSchemaNameAsValue()
    {
        // Arrange — anyOf with auto-detected discriminator and multi-word variant names.
        // The OpenAPI implicit-mapping convention uses the schema name verbatim as the
        // discriminator value, NOT a snake_case transformation of it.
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
                                    kind:
                                      type: string
                                    emailAddress:
                                      type: string
                                SmsNotification:
                                  type: object
                                  properties:
                                    kind:
                                      type: string
                                    phoneNumber:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var configs = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(document);

        // Assert
        Assert.NotNull(configs);
        var config = configs["Notification"];
        Assert.False(config.IsDiscriminatorExplicit);

        var email = config.Variants.First(v => string.Equals(v.TypeName, "EmailNotification", StringComparison.Ordinal));
        var sms = config.Variants.First(v => string.Equals(v.TypeName, "SmsNotification", StringComparison.Ordinal));
        Assert.Equal("EmailNotification", email.DiscriminatorValue);
        Assert.Equal("SmsNotification", sms.DiscriminatorValue);
    }

    // An auto-detected discriminator has no fallback: an unknown value is a JsonException, as with a declared one
    [Fact]
    public void ExtractPolymorphicConfigs_AutoDetectedDiscriminator_HasNoFallbackVariant()
    {
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
                                    kind:
                                      type: string
                                SmsNotification:
                                  type: object
                                  properties:
                                    kind:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var config = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(document)!["Notification"];

        Assert.Null(config.DefaultVariantTypeName);
        Assert.True(config.UsesDiscriminatorConverter);
        Assert.Contains("_ => throw new JsonException(", PolymorphicTypeExtractor.GenerateDiscriminatorFallbackConverter(config, "Test"), StringComparison.Ordinal);
    }

    // A variant that pins the property to a value of its own is read by that value, not by its schema name
    [Fact]
    public void ExtractPolymorphicConfigs_AutoDetectedPinnedDiscriminator_UsesThePinnedValues()
    {
        const string yaml = """
                            openapi: 3.1.0
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
                                Circle:
                                  type: object
                                  properties:
                                    shape:
                                      type: string
                                      enum: [circle]
                                Square:
                                  type: object
                                  properties:
                                    shape:
                                      const: square
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var config = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(document)!["Shape"];

        Assert.Equal("shape", config.DiscriminatorPropertyName);
        Assert.Equal("circle", config.Variants.Single(v => v.TypeName == "Circle").DiscriminatorValue);
        Assert.Equal("square", config.Variants.Single(v => v.TypeName == "Square").DiscriminatorValue);
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
        var configs = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(document);

        // Assert
        Assert.NotNull(configs);
        Assert.True(configs.ContainsKey("PaymentResult"));

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
        var configs = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(document);

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
        var configs = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(document);

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
        Assert.Contains("Info<SuccessPayment>", result, StringComparison.Ordinal);
        Assert.Contains("Info<FailedPayment>", result, StringComparison.Ordinal);
        Assert.Contains("catch (JsonException ex)", result, StringComparison.Ordinal);
        Assert.Contains("the body fits no variant", result, StringComparison.Ordinal);

        // Every variant is read strictly, and a body that fits two variants is ambiguous
        Assert.Contains("RespectRequiredConstructorParameters = true", result, StringComparison.Ordinal);
        Assert.Contains("UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow", result, StringComparison.Ordinal);
        Assert.Contains("Ambiguous {nameof(PaymentResult)}", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateDiscriminatorFallbackConverter_WithDefaultVariant_FallsBackToItForUnknownValues()
    {
        var config = new PolymorphicConfig
        {
            BaseTypeName = "Order",
            DiscriminatorPropertyName = "orderType",
            IsOneOf = true,
            IsDiscriminatorExplicit = true,
            DefaultVariantTypeName = "OnlineOrder",
            Variants =
            {
                new PolymorphicVariant { TypeName = "OnlineOrder", DiscriminatorValue = "online" },
                new PolymorphicVariant { TypeName = "InStoreOrder", DiscriminatorValue = "in_store" },
            },
        };

        var result = PolymorphicTypeExtractor.GenerateDiscriminatorFallbackConverter(
            config,
            "TestProject",
            pathSegment: null,
            additionalUsings: ["TestProject.Generated.Shared.Models"]);

        Assert.Contains("namespace TestProject.Generated.Models;", result, StringComparison.Ordinal);
        Assert.Contains("using System.Text.Json;", result, StringComparison.Ordinal);
        Assert.Contains("using TestProject.Generated.Shared.Models;", result, StringComparison.Ordinal);
        Assert.Contains("public sealed class OrderJsonConverter : JsonConverter<Order>", result, StringComparison.Ordinal);
        Assert.Contains("/// <see cref=\"OnlineOrder\"/> for unrecognized discriminator values.", result, StringComparison.Ordinal);

        // A discriminator that is no string is no known value; a body that is no object is a JsonException
        Assert.Contains("if (document.RootElement.TryGetProperty(\"orderType\", out var discriminatorElement) && discriminatorElement.ValueKind == JsonValueKind.String)", result, StringComparison.Ordinal);
        Assert.Contains("if (document.RootElement.ValueKind != JsonValueKind.Object)", result, StringComparison.Ordinal);
        Assert.Contains("\"online\" => JsonSerializer.Deserialize(rawText, Info<OnlineOrder>(options)),", result, StringComparison.Ordinal);
        Assert.Contains("\"in_store\" => JsonSerializer.Deserialize(rawText, Info<InStoreOrder>(options)),", result, StringComparison.Ordinal);
        Assert.Contains("_ => JsonSerializer.Deserialize(rawText, Info<OnlineOrder>(options)),", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Unknown 'orderType' value", result, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.Serialize(writer, value, options.GetTypeInfo(value.GetType()));", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateDiscriminatorFallbackConverter_WithoutDefaultVariant_ThrowsForUnknownValuesAndUsesSegmentNamespace()
    {
        var config = new PolymorphicConfig
        {
            BaseTypeName = "Order",
            DiscriminatorPropertyName = "orderType",
            IsOneOf = true,
            IsDiscriminatorExplicit = true,
            DiscriminatorDeclaredByVariants = true,
            Variants =
            {
                new PolymorphicVariant { TypeName = "OnlineOrder", DiscriminatorValue = "online" },
            },
        };

        var result = PolymorphicTypeExtractor.GenerateDiscriminatorFallbackConverter(config, "TestProject", "Orders");

        Assert.Contains("namespace TestProject.Generated.Orders.Models;", result, StringComparison.Ordinal);
        Assert.Contains("/// Dispatches on property 'orderType'; an unrecognized value is a <see cref=\"JsonException\"/>.", result, StringComparison.Ordinal);
        Assert.Contains("_ => throw new JsonException($\"Unknown 'orderType' value '{discriminatorValue}' for Order.\"),", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GetPolymorphicConfig_ExplicitDiscriminatorWithDefaultMapping_SetsMappedValuesAndFallbackVariant()
    {
        const string yaml = """
                            openapi: "3.2.0"
                            info:
                              title: T
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Order:
                                  oneOf:
                                    - $ref: '#/components/schemas/OnlineOrder'
                                    - $ref: '#/components/schemas/InStoreOrder'
                                  discriminator:
                                    propertyName: orderType
                                    mapping:
                                      online: '#/components/schemas/OnlineOrder'
                                      in_store: '#/components/schemas/InStoreOrder'
                                    defaultMapping: '#/components/schemas/OnlineOrder'
                                OnlineOrder:
                                  type: object
                                  properties:
                                    orderType:
                                      type: string
                                InStoreOrder:
                                  type: object
                                  properties:
                                    orderType:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var config = PolymorphicTypeExtractor.GetPolymorphicConfig("Order", document.Components!.Schemas!["Order"], document);

        Assert.NotNull(config);
        Assert.Equal("Order", config.BaseTypeName);
        Assert.Equal("orderType", config.DiscriminatorPropertyName);
        Assert.True(config.IsOneOf);
        Assert.True(config.IsDiscriminatorExplicit);
        Assert.False(config.UsesCustomConverter);
        Assert.Equal("OnlineOrder", config.DefaultVariantTypeName);

        // Both variants declare orderType as a property of their own, so STJ's [JsonPolymorphic] cannot be used.
        Assert.True(config.DiscriminatorDeclaredByVariants);
        Assert.True(config.UsesDiscriminatorConverter);

        Assert.Equal(2, config.Variants.Count);
        var online = config.Variants.Single(v => v.TypeName == "OnlineOrder");
        Assert.Equal("online", online.DiscriminatorValue);
        Assert.Equal("OnlineOrder", online.SchemaRefId);
        var inStore = config.Variants.Single(v => v.TypeName == "InStoreOrder");
        Assert.Equal("in_store", inStore.DiscriminatorValue);
    }

    [Fact]
    public void GetPolymorphicConfig_NoDiscriminatorAndNoCommonProperty_ReturnsUnionConfig()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
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
                                      type: string
                                FailedPayment:
                                  type: object
                                  properties:
                                    reason:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var config = PolymorphicTypeExtractor.GetPolymorphicConfig("PaymentResult", document.Components!.Schemas!["PaymentResult"], document);

        Assert.NotNull(config);
        Assert.Equal("PaymentResult", config.BaseTypeName);
        Assert.True(config.IsOneOf);
        Assert.False(config.IsDiscriminatorExplicit);
        Assert.True(config.UsesCustomConverter);
        Assert.Null(config.DefaultVariantTypeName);
        Assert.Equal(["SuccessPayment", "FailedPayment"], config.Variants.Select(v => v.TypeName), StringComparer.Ordinal);
    }

    [Fact]
    public void GetPolymorphicConfig_PlainObjectSchema_ReturnsNull()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        Assert.Null(PolymorphicTypeExtractor.GetPolymorphicConfig("Pet", document.Components!.Schemas!["Pet"], document));
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}