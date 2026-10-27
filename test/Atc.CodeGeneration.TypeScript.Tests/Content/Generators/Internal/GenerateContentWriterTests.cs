namespace Atc.CodeGeneration.TypeScript.Tests.Content.Generators.Internal;

public class GenerateContentWriterTests
{
    private static readonly string NL = Environment.NewLine;

    private readonly GenerateContentWriter writer = new(new JsDocCommentGenerator());

    [Fact]
    public void GenerateTopOfType_HeaderImportsAndDoc_WritesThemInOrder()
    {
        // Arrange
        var header = $"// auto-generated{NL}";
        var imports = new List<string> { "import { Pet } from './pet';" };

        // Act
        var result = writer.GenerateTopOfType(header, imports, new JsDocComment("A pet service."));

        // Assert
        Assert.Equal(
            $"// auto-generated{NL}import {{ Pet }} from './pet';{NL}{NL}/** A pet service. */{NL}",
            result);
    }

    [Fact]
    public void GenerateTopOfType_NothingGiven_ReturnsEmpty()
        => Assert.Equal(string.Empty, writer.GenerateTopOfType(headerContent: null, importStatements: null, documentationTags: null));

    [Fact]
    public void GenerateProperty_ReadonlyOptional_WritesKeywordAndQuestionMark()
    {
        // Arrange
        var parameters = new TypeScriptPropertyParameters(null, true, "string", true, "name", null);

        // Act
        var result = writer.GenerateProperty(parameters);

        // Assert
        Assert.Equal("  readonly name?: string;", result);
    }

    [Fact]
    public void GenerateProperty_WithDocAndDefault_WritesDocLineThenInitializer()
    {
        // Arrange
        var parameters = new TypeScriptPropertyParameters(new JsDocComment("The age."), false, "number", false, "age", "0");

        // Act
        var result = writer.GenerateProperty(parameters);

        // Assert
        Assert.Equal($"  /** The age. */{NL}  age: number = 0;", result);
    }

    [Fact]
    public void GenerateMethodSignature_GenericWithOptionalParameter_WritesFullSignature()
    {
        // Arrange
        var parameters = new TypeScriptMethodSignatureParameters(
            DocumentationTags: null,
            Name: "getPet",
            GenericTypeParameter: "T",
            ReturnType: "Promise<T>",
            Parameters:
            [
                new TypeScriptParameterParameters("id", "number", false, null),
                new TypeScriptParameterParameters("options", "RequestInit", true, null),
            ]);

        // Act
        var result = writer.GenerateMethodSignature(parameters);

        // Assert
        Assert.Equal("  getPet<T>(id: number, options?: RequestInit): Promise<T>;", result);
    }

    [Fact]
    public void GenerateMethodSignature_NoParametersNoReturnType_WritesBareSignature()
    {
        // Arrange
        var parameters = new TypeScriptMethodSignatureParameters(null, "reset", null, null, null);

        // Act
        var result = writer.GenerateMethodSignature(parameters);

        // Assert
        Assert.Equal("  reset();", result);
    }

    [Fact]
    public void GenerateMethod_AsyncWithBody_WritesAsyncKeywordAndIndentedBody()
    {
        // Arrange
        var parameters = new TypeScriptMethodParameters(
            DocumentationTags: null,
            TypeScriptModifiers.Async,
            Name: "getPet",
            GenericTypeParameter: null,
            ReturnType: "Promise<Pet>",
            Parameters: [new TypeScriptParameterParameters("id", "number", false, null)],
            Content: "return this.fetch(id);");

        // Act
        var result = writer.GenerateMethod(parameters);

        // Assert
        Assert.Equal($"  async getPet(id: number): Promise<Pet> {{{NL}    return this.fetch(id);{NL}  }}", result);
    }

    [Fact]
    public void GenerateMethod_NoBody_WritesEmptyBlock()
    {
        // Arrange
        var parameters = new TypeScriptMethodParameters(null, TypeScriptModifiers.None, "reset", null, "void", null, null);

        // Act
        var result = writer.GenerateMethod(parameters);

        // Assert
        Assert.Equal($"  reset(): void {{{NL}  }}", result);
    }

    [Fact]
    public void GenerateMethod_OptionalParameterWithDefault_OmitsQuestionMark()
    {
        // Arrange
        var parameters = new TypeScriptMethodParameters(
            DocumentationTags: null,
            TypeScriptModifiers.None,
            Name: "retry",
            GenericTypeParameter: null,
            ReturnType: null,
            Parameters: [new TypeScriptParameterParameters("retries", "number", true, "3")],
            Content: null);

        // Act
        var result = writer.GenerateMethod(parameters);

        // Assert
        Assert.Equal($"  retry(retries: number = 3) {{{NL}  }}", result);
    }

    [Fact]
    public void GenerateConstructor_NoParametersNoBody_WritesEmptyConstructor()
    {
        // Arrange
        var parameters = new TypeScriptConstructorParameters(null, null, null);

        // Act
        var result = writer.GenerateConstructor(parameters);

        // Assert
        Assert.Equal("  constructor() {}", result);
    }

    [Fact]
    public void GenerateConstructor_TwoPlainParametersWithBody_WritesOneLineParameterList()
    {
        // Arrange
        var parameters = new TypeScriptConstructorParameters(
            DocumentationTags: null,
            Parameters:
            [
                new TypeScriptConstructorParameterParameters(TypeScriptModifiers.None, false, "baseUrl", "string", false, null),
                new TypeScriptConstructorParameterParameters(TypeScriptModifiers.None, false, "timeout", "number", true, null),
            ],
            Content: "this.baseUrl = baseUrl;");

        // Act
        var result = writer.GenerateConstructor(parameters);

        // Assert
        Assert.Equal($"  constructor(baseUrl: string, timeout?: number) {{{NL}    this.baseUrl = baseUrl;{NL}  }}", result);
    }

    [Fact]
    public void GenerateConstructor_ReadonlyParameterProperty_WritesOneParameterPerLine()
    {
        // Arrange
        var parameters = new TypeScriptConstructorParameters(
            DocumentationTags: null,
            Parameters:
            [
                new TypeScriptConstructorParameterParameters(TypeScriptModifiers.None, true, "http", "HttpClient", false, null),
            ],
            Content: null);

        // Act
        var result = writer.GenerateConstructor(parameters);

        // Assert
        Assert.Equal($"  constructor({NL}    readonly http: HttpClient{NL}  ) {{}}", result);
    }
}