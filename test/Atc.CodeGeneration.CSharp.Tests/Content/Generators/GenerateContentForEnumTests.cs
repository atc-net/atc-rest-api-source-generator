namespace Atc.CodeGeneration.CSharp.Tests.Content.Generators;

public class GenerateContentForEnumTests
{
    [Fact]
    public void Generate_SimpleEnum_ProducesEnumDeclaration()
    {
        // Arrange
        var parameters = new EnumParameters(
            HeaderContent: null,
            Namespace: "MyApp.Models",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            EnumTypeName: "PetStatus",
            UseFlags: false,
            Values:
            [
                new EnumValueParameters(null, null, "Active", null, null),
                new EnumValueParameters(null, null, "Inactive", null, null),
            ]);

        // Act
        var generator = new GenerateContentForEnum(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        // Assert
        Assert.Contains("public enum PetStatus", result, StringComparison.Ordinal);
        Assert.Contains("Active,", result, StringComparison.Ordinal);
        Assert.Contains("Inactive,", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithExplicitValues_IncludesValues()
    {
        // Arrange
        var parameters = new EnumParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            EnumTypeName: "Priority",
            UseFlags: false,
            Values:
            [
                new EnumValueParameters(null, null, "Low", null, 0),
                new EnumValueParameters(null, null, "Medium", null, 1),
                new EnumValueParameters(null, null, "High", null, 2),
            ]);

        // Act
        var generator = new GenerateContentForEnum(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        // Assert
        Assert.Contains("Low = 0,", result, StringComparison.Ordinal);
        Assert.Contains("Medium = 1,", result, StringComparison.Ordinal);
        Assert.Contains("High = 2,", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithFlags_AddsFlagsAttribute()
    {
        // Arrange
        var parameters = new EnumParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: new List<AttributeParameters>(),
            DeclarationModifier: DeclarationModifiers.Public,
            EnumTypeName: "Permissions",
            UseFlags: true,
            Values:
            [
                new EnumValueParameters(null, null, "None", null, 0),
                new EnumValueParameters(null, null, "Read", null, 1),
                new EnumValueParameters(null, null, "Write", null, 2),
            ]);

        // Act
        var generator = new GenerateContentForEnum(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        // Assert
        Assert.Contains("[Flags]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithEnumMember_AddsJsonConverterAndEnumMemberAttribute()
    {
        // Arrange
        var parameters = new EnumParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            EnumTypeName: "Color",
            UseFlags: false,
            Values:
            [
                new EnumValueParameters(null, null, "DarkRed", "dark-red", null),
                new EnumValueParameters(null, null, "LightBlue", "light-blue", null),
            ]);

        // Act
        var generator = new GenerateContentForEnum(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        // Assert
        Assert.Contains("[JsonConverter(typeof(JsonStringEnumConverter))]", result, StringComparison.Ordinal);
        Assert.Contains("[EnumMember(Value = \"dark-red\")]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithDocumentation_IncludesXmlDocs()
    {
        // Arrange
        var parameters = new EnumParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: new CodeDocumentationTags("Status of a pet."),
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            EnumTypeName: "PetStatus",
            UseFlags: false,
            Values:
            [
                new EnumValueParameters(null, null, "Active", null, null),
            ]);

        // Act
        var generator = new GenerateContentForEnum(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        // Assert
        Assert.Contains("/// <summary>", result, StringComparison.Ordinal);
        Assert.Contains("Status of a pet.", result, StringComparison.Ordinal);
    }
}