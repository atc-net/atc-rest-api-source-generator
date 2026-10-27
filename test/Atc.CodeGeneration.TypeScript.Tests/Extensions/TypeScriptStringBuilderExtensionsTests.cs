namespace Atc.CodeGeneration.TypeScript.Tests.Extensions;

public class TypeScriptStringBuilderExtensionsTests
{
    private static readonly string NL = Environment.NewLine;

    [Theory]
    [InlineData(TypeScriptModifiers.Export, "export ")]
    [InlineData(TypeScriptModifiers.Export | TypeScriptModifiers.Abstract, "export abstract ")]
    [InlineData(TypeScriptModifiers.None, "")]
    public void AppendModifiers_WritesKeywordsAndTrailingSpace(
        TypeScriptModifiers modifiers,
        string expected)
    {
        // Arrange
        var sb = new StringBuilder();

        // Act
        sb.AppendModifiers(modifiers);

        // Assert
        Assert.Equal(expected, sb.ToString());
    }

    [Fact]
    public void AppendContent_MultiLineWithTrailingBreak_IndentsLinesAndDropsTrailingBreak()
    {
        // Arrange
        var sb = new StringBuilder();

        // Act
        sb.AppendContent(2, "const a = 1;\nreturn a;\n");

        // Assert
        Assert.Equal($"  const a = 1;{NL}  return a;", sb.ToString());
    }

    [Fact]
    public void AppendContent_EmptyLineInside_KeepsItUnindented()
    {
        // Arrange
        var sb = new StringBuilder();

        // Act
        sb.AppendContent(4, "a\n\nb");

        // Assert
        Assert.Equal($"    a{NL}{NL}    b", sb.ToString());
    }
}