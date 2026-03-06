namespace Atc.CodeGeneration.CSharp.Tests.Content.Generators;

public class GenerateContentForRecordsTests
{
    [Fact]
    public void Generate_SimpleRecord_ProducesRecordDeclaration()
    {
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

        var generator = new GenerateContentForRecords(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("public record Pet", result, StringComparison.Ordinal);
        Assert.Contains("string Name", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_EmptyRecord_ProducesEmptyParens()
    {
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

        var generator = new GenerateContentForRecords(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("public record Empty();", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithBaseType_IncludesInheritance()
    {
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

        var generator = new GenerateContentForRecords(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains(": Animal(Name)", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_MultipleRecords_ProducesAll()
    {
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

        var generator = new GenerateContentForRecords(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("public record First();", result, StringComparison.Ordinal);
        Assert.Contains("public record Second();", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithNullableParameter_IncludesQuestionMark()
    {
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

        var generator = new GenerateContentForRecords(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("string?", result, StringComparison.Ordinal);
        Assert.Contains("Nickname", result, StringComparison.Ordinal);
    }
}