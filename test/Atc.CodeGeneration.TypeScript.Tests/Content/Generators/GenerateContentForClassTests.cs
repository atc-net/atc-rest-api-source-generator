namespace Atc.CodeGeneration.TypeScript.Tests.Content.Generators;

public class GenerateContentForClassTests
{
    [Fact]
    public void Generate_SimpleClass_ProducesCorrectOutput()
    {
        // Arrange
        var parameters = new TypeScriptClassParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "PetService",
            GenericTypeParameter: null,
            ExtendsTypeName: null,
            ImplementsTypeNames: null,
            ImportStatements: null,
            Constructors: null,
            Properties: null,
            Methods:
            [
                new TypeScriptMethodParameters(
                    null,
                    TypeScriptModifiers.Async,
                    "getPets",
                    null,
                    "Promise<Pet[]>",
                    null,
                    "return [];"),
            ]);

        var writer = new GenerateContentWriter(new JsDocCommentGenerator());
        var generator = new GenerateContentForClass(writer, parameters);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("export class PetService {", result, StringComparison.Ordinal);
        Assert.Contains("async getPets(): Promise<Pet[]>", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithExtends_IncludesExtendsClause()
    {
        // Arrange
        var parameters = new TypeScriptClassParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "Dog",
            GenericTypeParameter: null,
            ExtendsTypeName: "Animal",
            ImplementsTypeNames: null,
            ImportStatements: null,
            Constructors: null,
            Properties: null,
            Methods: null);

        var writer = new GenerateContentWriter(new JsDocCommentGenerator());
        var generator = new GenerateContentForClass(writer, parameters);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("class Dog extends Animal {", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithImplements_IncludesImplementsClause()
    {
        // Arrange
        var parameters = new TypeScriptClassParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "PetService",
            GenericTypeParameter: null,
            ExtendsTypeName: null,
            ImplementsTypeNames: ["IPetService", "IDisposable"],
            ImportStatements: null,
            Constructors: null,
            Properties: null,
            Methods: null);

        var writer = new GenerateContentWriter(new JsDocCommentGenerator());
        var generator = new GenerateContentForClass(writer, parameters);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains(
            "implements IPetService, IDisposable",
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithGenericType_IncludesTypeParameter()
    {
        // Arrange
        var parameters = new TypeScriptClassParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "Repository",
            GenericTypeParameter: "T",
            ExtendsTypeName: null,
            ImplementsTypeNames: null,
            ImportStatements: null,
            Constructors: null,
            Properties: null,
            Methods: null);

        var writer = new GenerateContentWriter(new JsDocCommentGenerator());
        var generator = new GenerateContentForClass(writer, parameters);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("class Repository<T> {", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithConstructor_IncludesConstructor()
    {
        // Arrange
        var parameters = new TypeScriptClassParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "PetService",
            GenericTypeParameter: null,
            ExtendsTypeName: null,
            ImplementsTypeNames: null,
            ImportStatements: null,
            Constructors:
            [
                new TypeScriptConstructorParameters(
                    null,
                    [
                        new TypeScriptConstructorParameterParameters(
                            TypeScriptModifiers.None,
                            false,
                            "apiUrl",
                            "string",
                            false,
                            null),
                    ],
                    "this.url = apiUrl;"),
            ],
            Properties: null,
            Methods: null);

        var writer = new GenerateContentWriter(new JsDocCommentGenerator());
        var generator = new GenerateContentForClass(writer, parameters);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("constructor(apiUrl: string)", result, StringComparison.Ordinal);
        Assert.Contains("this.url = apiUrl;", result, StringComparison.Ordinal);
    }
}