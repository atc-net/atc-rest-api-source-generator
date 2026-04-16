namespace Atc.CodeGeneration.TypeScript.Tests.CodeDocumentation;

public class JsDocCommentGeneratorTests
{
    [Fact]
    public void ShouldGenerateTags_DescriptionOnly_ReturnsTrue()
    {
        // Arrange
        var generator = new JsDocCommentGenerator();
        var comment = new JsDocComment("Fetches all pets from the API.");

        // Act
        var result = generator.ShouldGenerateTags(comment);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldGenerateTags_UndefinedDescription_ReturnsFalse()
    {
        // Arrange
        var generator = new JsDocCommentGenerator();
        var comment = new JsDocComment("Undefined description.");

        // Act
        var result = generator.ShouldGenerateTags(comment);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GenerateTags_DescriptionOnly_ProducesSingleLineComment()
    {
        // Arrange
        var generator = new JsDocCommentGenerator();
        var comment = new JsDocComment("Fetches all pets.");

        // Act
        var result = generator.GenerateTags(0, comment);

        // Assert
        Assert.Contains("/** Fetches all pets. */", result, StringComparison.Ordinal);
        Assert.DoesNotContain(" * @", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateTags_DescriptionAndReturns_ProducesMultiLineComment()
    {
        // Arrange
        var generator = new JsDocCommentGenerator();
        var comment = new JsDocComment(
            description: "Fetches all pets.",
            parameters: null,
            returns: "A list of pets.",
            isDeprecated: false,
            deprecatedMessage: null,
            example: null);

        // Act
        var result = generator.GenerateTags(2, comment);

        // Assert
        Assert.Contains("  /**", result, StringComparison.Ordinal);
        Assert.Contains("  * Fetches all pets.", result, StringComparison.Ordinal);
        Assert.Contains("  * @returns A list of pets.", result, StringComparison.Ordinal);
        Assert.Contains("  */", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateTags_Deprecated_IncludesDeprecatedTag()
    {
        // Arrange
        var generator = new JsDocCommentGenerator();
        var comment = new JsDocComment(
            description: null,
            parameters: null,
            returns: null,
            isDeprecated: true,
            deprecatedMessage: "Use newMethod instead.",
            example: null);

        // Act
        var result = generator.GenerateTags(0, comment);

        // Assert
        Assert.Contains("@deprecated Use newMethod instead.", result, StringComparison.Ordinal);
    }
}