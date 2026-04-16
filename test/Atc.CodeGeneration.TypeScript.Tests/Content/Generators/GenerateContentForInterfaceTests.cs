namespace Atc.CodeGeneration.TypeScript.Tests.Content.Generators;

public class GenerateContentForInterfaceTests
{
    [Fact]
    public void Generate_SimpleInterface_ProducesCorrectOutput()
    {
        // Arrange
        var parameters = new TypeScriptInterfaceParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "Pet",
            ExtendsTypeName: null,
            ImportStatements: null,
            Properties:
            [
                new TypeScriptPropertyParameters(null, false, "string", false, "name", null),
                new TypeScriptPropertyParameters(null, false, "number", false, "age", null),
            ],
            Methods: null);

        var writer = new GenerateContentWriter(new JsDocCommentGenerator());
        var generator = new GenerateContentForInterface(writer, parameters);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("export interface Pet {", result, StringComparison.Ordinal);
        Assert.Contains("name: string;", result, StringComparison.Ordinal);
        Assert.Contains("age: number;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithExtends_IncludesExtendsClause()
    {
        // Arrange
        var parameters = new TypeScriptInterfaceParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "Dog",
            ExtendsTypeName: "Pet",
            ImportStatements: null,
            Properties: null,
            Methods: null);

        var writer = new GenerateContentWriter(new JsDocCommentGenerator());
        var generator = new GenerateContentForInterface(writer, parameters);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("interface Dog extends Pet {", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithImports_IncludesImportStatements()
    {
        // Arrange
        var parameters = new TypeScriptInterfaceParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "Pet",
            ExtendsTypeName: null,
            ImportStatements: ["import { Owner } from './Owner';"],
            Properties:
            [
                new TypeScriptPropertyParameters(null, false, "Owner", false, "owner", null),
            ],
            Methods: null);

        var writer = new GenerateContentWriter(new JsDocCommentGenerator());
        var generator = new GenerateContentForInterface(writer, parameters);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("import { Owner } from './Owner';", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithOptionalProperty_IncludesQuestionMark()
    {
        // Arrange
        var parameters = new TypeScriptInterfaceParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "Pet",
            ExtendsTypeName: null,
            ImportStatements: null,
            Properties:
            [
                new TypeScriptPropertyParameters(null, false, "string", true, "nickname", null),
            ],
            Methods: null);

        var writer = new GenerateContentWriter(new JsDocCommentGenerator());
        var generator = new GenerateContentForInterface(writer, parameters);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("nickname?: string;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithReadonlyProperty_IncludesReadonly()
    {
        // Arrange
        var parameters = new TypeScriptInterfaceParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "Pet",
            ExtendsTypeName: null,
            ImportStatements: null,
            Properties:
            [
                new TypeScriptPropertyParameters(null, true, "string", false, "id", null),
            ],
            Methods: null);

        var writer = new GenerateContentWriter(new JsDocCommentGenerator());
        var generator = new GenerateContentForInterface(writer, parameters);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("readonly id: string;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithMethodSignature_IncludesMethod()
    {
        // Arrange
        var parameters = new TypeScriptInterfaceParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "PetService",
            ExtendsTypeName: null,
            ImportStatements: null,
            Properties: null,
            Methods:
            [
                new TypeScriptMethodSignatureParameters(
                    null,
                    "getPet",
                    null,
                    "Pet",
                    [new TypeScriptParameterParameters("id", "string", false, null)]),
            ]);

        var writer = new GenerateContentWriter(new JsDocCommentGenerator());
        var generator = new GenerateContentForInterface(writer, parameters);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("getPet(id: string): Pet;", result, StringComparison.Ordinal);
    }
}