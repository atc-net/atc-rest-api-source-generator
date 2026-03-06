namespace Atc.CodeGeneration.TypeScript.Tests.Content.Generators;

public class GenerateContentForEnumTests
{
    [Fact]
    public void Generate_SimpleEnum_ProducesCorrectOutput()
    {
        var parameters = new TypeScriptEnumParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "Color",
            IsConstEnum: false,
            Values:
            [
                new TypeScriptEnumValueParameters(null, "Red", "'red'"),
                new TypeScriptEnumValueParameters(null, "Green", "'green'"),
                new TypeScriptEnumValueParameters(null, "Blue", "'blue'"),
            ]);

        var generator = new GenerateContentForEnum(
            new JsDocCommentGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("export enum Color {", result, StringComparison.Ordinal);
        Assert.Contains("Red = 'red'", result, StringComparison.Ordinal);
        Assert.Contains("Green = 'green'", result, StringComparison.Ordinal);
        Assert.Contains("Blue = 'blue'", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ConstEnum_IncludesConstKeyword()
    {
        var parameters = new TypeScriptEnumParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "Direction",
            IsConstEnum: true,
            Values:
            [
                new TypeScriptEnumValueParameters(null, "Up", "'up'"),
            ]);

        var generator = new GenerateContentForEnum(
            new JsDocCommentGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("export const enum Direction {", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithDocumentation_IncludesJsDoc()
    {
        var parameters = new TypeScriptEnumParameters(
            HeaderContent: null,
            DocumentationTags: new JsDocComment("Represents a color."),
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "Color",
            IsConstEnum: false,
            Values:
            [
                new TypeScriptEnumValueParameters(null, "Red", "'red'"),
            ]);

        var generator = new GenerateContentForEnum(
            new JsDocCommentGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("/** Represents a color. */", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithHeaderContent_IncludesHeader()
    {
        var header = "// Auto-generated\n";
        var parameters = new TypeScriptEnumParameters(
            HeaderContent: header,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.None,
            TypeName: "Status",
            IsConstEnum: false,
            Values:
            [
                new TypeScriptEnumValueParameters(null, "Active", "'active'"),
            ]);

        var generator = new GenerateContentForEnum(
            new JsDocCommentGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.StartsWith("// Auto-generated", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ValuesWithoutExplicitValue_OmitsAssignment()
    {
        var parameters = new TypeScriptEnumParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "Direction",
            IsConstEnum: false,
            Values:
            [
                new TypeScriptEnumValueParameters(null, "Up", null),
                new TypeScriptEnumValueParameters(null, "Down", null),
            ]);

        var generator = new GenerateContentForEnum(
            new JsDocCommentGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("Up,", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Up =", result, StringComparison.Ordinal);
    }
}