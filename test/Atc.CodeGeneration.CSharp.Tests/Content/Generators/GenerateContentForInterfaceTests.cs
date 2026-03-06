namespace Atc.CodeGeneration.CSharp.Tests.Content.Generators;

public class GenerateContentForInterfaceTests
{
    [Fact]
    public void Generate_SimpleInterface_ProducesInterfaceDeclaration()
    {
        var parameters = new InterfaceParameters(
            HeaderContent: null,
            Namespace: "MyApp.Contracts",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            InterfaceTypeName: "IPetService",
            InheritedInterfaceTypeName: null,
            Properties: null,
            Methods: null);

        var generator = new GenerateContentForInterface(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("public IPetService", result, StringComparison.Ordinal);
        Assert.Contains("namespace MyApp.Contracts;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithInheritedInterface_IncludesBase()
    {
        var parameters = new InterfaceParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            InterfaceTypeName: "IPetService",
            InheritedInterfaceTypeName: "IService",
            Properties: null,
            Methods: null);

        var generator = new GenerateContentForInterface(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("IPetService : IService", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithMethod_IncludesMethodSignature()
    {
        var parameters = new InterfaceParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            InterfaceTypeName: "IPetService",
            InheritedInterfaceTypeName: null,
            Properties: null,
            Methods:
            [
                new MethodParameters(
                    DocumentationTags: null,
                    Attributes: null,
                    DeclarationModifier: DeclarationModifiers.None,
                    ReturnGenericTypeName: "Task",
                    ReturnTypeName: "Pet",
                    Name: "GetPetAsync",
                    Parameters: null,
                    AlwaysBreakDownParameters: false,
                    UseExpressionBody: false,
                    Content: null),
            ]);

        var generator = new GenerateContentForInterface(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("GetPetAsync", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithDocumentation_IncludesXmlDocs()
    {
        var parameters = new InterfaceParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: new CodeDocumentationTags("Defines pet operations."),
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            InterfaceTypeName: "IPetService",
            InheritedInterfaceTypeName: null,
            Properties: null,
            Methods: null);

        var generator = new GenerateContentForInterface(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("/// <summary>", result, StringComparison.Ordinal);
        Assert.Contains("Defines pet operations.", result, StringComparison.Ordinal);
    }
}