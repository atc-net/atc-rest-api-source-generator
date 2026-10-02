namespace Atc.CodeGeneration.CSharp.Tests.Content.Generators;

public class GenerateContentForRecordsTests
{
    [Fact]
    public void Generate_SimpleRecord_ProducesRecordDeclaration()
    {
        // Arrange
        var parameters = new RecordsParameters(
            HeaderContent: null,
            Namespace: "MyApp.Models",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters:
            [
                new RecordParameters(
                    DocumentationTags: null,
                    DeclarationModifier: DeclarationModifiers.PublicRecord,
                    Name: "Pet",
                    Parameters:
                    [
                        new ParameterBaseParameters(
                            Attributes: null,
                            GenericTypeName: null,
                            IsGenericListType: false,
                            TypeName: "string",
                            IsNullableType: false,
                            IsReferenceType: true,
                            Name: "Name",
                            DefaultValue: null),
                    ]),
            ]);

        // Act
        var generator = new GenerateContentForRecords(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        // Assert
        Assert.Contains("public record Pet", result, StringComparison.Ordinal);
        Assert.Contains("string Name", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_EmptyRecord_ProducesEmptyParens()
    {
        // Arrange
        var parameters = new RecordsParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters:
            [
                new RecordParameters(
                    DocumentationTags: null,
                    DeclarationModifier: DeclarationModifiers.PublicRecord,
                    Name: "Empty",
                    Parameters: null),
            ]);

        // Act
        var generator = new GenerateContentForRecords(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        // Assert
        Assert.Contains("public record Empty();", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithBaseType_IncludesInheritance()
    {
        // Arrange
        var parameters = new RecordsParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters:
            [
                new RecordParameters(
                    DocumentationTags: null,
                    DeclarationModifier: DeclarationModifiers.PublicRecord,
                    Name: "Dog",
                    Parameters:
                    [
                        new ParameterBaseParameters(
                            Attributes: null,
                            GenericTypeName: null,
                            IsGenericListType: false,
                            TypeName: "string",
                            IsNullableType: false,
                            IsReferenceType: true,
                            Name: "Breed",
                            DefaultValue: null),
                    ],
                    BaseTypeName: "Animal",
                    BaseConstructorArguments: ["Name"]),
            ]);

        // Act
        var generator = new GenerateContentForRecords(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        // Assert
        Assert.Contains(": Animal(Name)", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Generate_ParameterForwardedToTheBase_HasNoAttributes(
        bool hasBase)
    {
        // A parameter that is only forwarded to the base declares no property - the base has it, with its
        // attributes - so "[property: ...]" on it is ignored by the compiler with warning CS0657. Without a
        // base the same parameter is a property of its own and keeps them.
        static ParameterBaseParameters Parameter(string name)
            => new(
                Attributes: [new AttributeParameters("Required", null)],
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: "string",
                IsNullableType: false,
                IsReferenceType: true,
                Name: name,
                DefaultValue: null);

        var parameters = new RecordsParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters:
            [
                new RecordParameters(
                    DocumentationTags: null,
                    DeclarationModifier: DeclarationModifiers.PublicRecord,
                    Name: "Dog",
                    Parameters: [Parameter("Name"), Parameter("Breed")],
                    BaseTypeName: hasBase ? "Animal" : null,
                    BaseConstructorArguments: hasBase ? ["Name"] : null),
            ]);

        var result = new GenerateContentForRecords(new CodeDocumentationTagsGenerator(), parameters).Generate();

        Assert.Contains("[property: Required] string Breed", result, StringComparison.Ordinal);
        if (hasBase)
        {
            Assert.Contains("    string Name,", result, StringComparison.Ordinal);
            Assert.DoesNotContain("[property: Required] string Name", result, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("[property: Required] string Name", result, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Generate_MultipleRecords_ProducesAll()
    {
        // Arrange
        var parameters = new RecordsParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters:
            [
                new RecordParameters(
                    DocumentationTags: null,
                    DeclarationModifier: DeclarationModifiers.PublicRecord,
                    Name: "First",
                    Parameters: null),
                new RecordParameters(
                    DocumentationTags: null,
                    DeclarationModifier: DeclarationModifiers.PublicRecord,
                    Name: "Second",
                    Parameters: null),
            ]);

        // Act
        var generator = new GenerateContentForRecords(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        // Assert
        Assert.Contains("public record First();", result, StringComparison.Ordinal);
        Assert.Contains("public record Second();", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithNullableParameter_IncludesQuestionMark()
    {
        // Arrange
        var parameters = new RecordsParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters:
            [
                new RecordParameters(
                    DocumentationTags: null,
                    DeclarationModifier: DeclarationModifiers.PublicRecord,
                    Name: "Pet",
                    Parameters:
                    [
                        new ParameterBaseParameters(
                            Attributes: null,
                            GenericTypeName: null,
                            IsGenericListType: false,
                            TypeName: "string",
                            IsNullableType: true,
                            IsReferenceType: true,
                            Name: "Nickname",
                            DefaultValue: "null"),
                    ]),
            ]);

        // Act
        var generator = new GenerateContentForRecords(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        // Assert
        Assert.Contains("string?", result, StringComparison.Ordinal);
        Assert.Contains("Nickname", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_RecordWithJsonPropertyNameAttribute_ProducesPropertyTargetedAttribute()
    {
        // Arrange
        var parameters = new RecordsParameters(
            HeaderContent: null,
            Namespace: "MyApp.Models",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters:
            [
                new RecordParameters(
                    DocumentationTags: null,
                    DeclarationModifier: DeclarationModifiers.PublicRecord,
                    Name: "MyEnergyDataMarketDocumentResponse",
                    Parameters:
                    [
                        new ParameterBaseParameters(
                            Attributes: [new AttributeParameters("JsonPropertyName", "\"MyEnergyData_MarketDocument\"")],
                            GenericTypeName: null,
                            IsGenericListType: false,
                            TypeName: "MyEnergyDataMarketDocument",
                            IsNullableType: false,
                            IsReferenceType: true,
                            Name: "MyEnergyDataMarketDocument",
                            DefaultValue: null),
                    ]),
            ]);

        // Act
        var generator = new GenerateContentForRecords(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        // Assert
        Assert.Contains(
            "[property: JsonPropertyName(\"MyEnergyData_MarketDocument\")] MyEnergyDataMarketDocument MyEnergyDataMarketDocument",
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_RecordWithJsonPropertyNameAndValidationAttribute_ProducesCombinedAttributes()
    {
        // Arrange
        var parameters = new RecordsParameters(
            HeaderContent: null,
            Namespace: "MyApp.Models",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters:
            [
                new RecordParameters(
                    DocumentationTags: null,
                    DeclarationModifier: DeclarationModifiers.PublicRecord,
                    Name: "Order",
                    Parameters:
                    [
                        new ParameterBaseParameters(
                            Attributes:
                            [
                                new AttributeParameters("JsonPropertyName", "\"error_code\""),
                                new AttributeParameters("Required", null),
                            ],
                            GenericTypeName: null,
                            IsGenericListType: false,
                            TypeName: "string",
                            IsNullableType: false,
                            IsReferenceType: true,
                            Name: "ErrorCode",
                            DefaultValue: null),
                    ]),
            ]);

        // Act
        var generator = new GenerateContentForRecords(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        // Assert
        Assert.Contains("JsonPropertyName(\"error_code\")", result, StringComparison.Ordinal);
        Assert.Contains("Required", result, StringComparison.Ordinal);
        Assert.Contains("string ErrorCode", result, StringComparison.Ordinal);
    }
}