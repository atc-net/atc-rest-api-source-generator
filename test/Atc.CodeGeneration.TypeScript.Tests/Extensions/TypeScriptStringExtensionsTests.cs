namespace Atc.CodeGeneration.TypeScript.Tests.Extensions;

public class TypeScriptStringExtensionsTests
{
    // ========== ToPascalCase Tests ==========
    [Theory]
    [InlineData("hello", "Hello")]
    [InlineData("helloWorld", "HelloWorld")]
    [InlineData("hello-world", "HelloWorld")]
    [InlineData("hello_world", "HelloWorld")]
    [InlineData("hello.world", "HelloWorld")]
    [InlineData("HELLO", "Hello")]
    [InlineData("", "")]
    public void ToPascalCase_ReturnsExpected(
        string input,
        string expected)
    {
        Assert.Equal(expected, input.ToPascalCase());
    }

    [Fact]
    public void ToPascalCase_SingleChar_ReturnsUpperCase()
    {
        Assert.Equal("A", "a".ToPascalCase());
    }

    [Fact]
    public void ToPascalCase_MultipleHyphens_SplitsAllWords()
    {
        Assert.Equal("OneTwoThree", "one-two-three".ToPascalCase());
    }

    [Fact]
    public void ToPascalCase_MixedSeparators_HandlesAll()
    {
        Assert.Equal("OneTwoThree", "one-two_three".ToPascalCase());
    }

    [Fact]
    public void ToPascalCase_CamelCaseInput_SplitsOnUpperCase()
    {
        // "helloWorld" -> splits at W -> "Hello" + "World" -> "HelloWorld"
        Assert.Equal("HelloWorld", "helloWorld".ToPascalCase());
    }

    // ========== ToCamelCase Tests ==========
    [Theory]
    [InlineData("Hello", "hello")]
    [InlineData("hello-world", "helloWorld")]
    [InlineData("hello_world", "helloWorld")]
    [InlineData("HelloWorld", "helloWorld")]
    [InlineData("HELLO", "hello")]
    [InlineData("", "")]
    public void ToCamelCase_ReturnsExpected(
        string input,
        string expected)
    {
        Assert.Equal(expected, input.ToCamelCase());
    }

    [Fact]
    public void ToCamelCase_SingleChar_ReturnsLowerCase()
    {
        Assert.Equal("a", "A".ToCamelCase());
    }

    [Fact]
    public void ToCamelCase_AlreadyCamelCase_PreservesFirstLower()
    {
        var result = "helloWorld".ToCamelCase();
        Assert.Equal('h', result[0]);
    }

    [Fact]
    public void ToCamelCase_MultipleHyphens_SplitsAllWords()
    {
        Assert.Equal("oneTwoThree", "one-two-three".ToCamelCase());
    }

    // ========== SplitIntoLines Tests ==========
    [Fact]
    public void SplitIntoLines_Empty_ReturnsEmptyArray()
    {
        Assert.Empty("".SplitIntoLines());
    }

    [Fact]
    public void SplitIntoLines_SingleLine_ReturnsSingleElement()
    {
        var result = "hello".SplitIntoLines();

        Assert.Single(result);
        Assert.Equal("hello", result[0]);
    }

    [Fact]
    public void SplitIntoLines_MultipleLines_SplitsCorrectly()
    {
        var result = "line1\nline2\nline3".SplitIntoLines();

        Assert.Equal(3, result.Length);
        Assert.Equal("line1", result[0]);
        Assert.Equal("line2", result[1]);
        Assert.Equal("line3", result[2]);
    }

    [Fact]
    public void SplitIntoLines_SkipsWhitespaceOnlyLines()
    {
        var result = "line1\n  \nline3".SplitIntoLines();

        Assert.Equal(2, result.Length);
        Assert.Equal("line1", result[0]);
        Assert.Equal("line3", result[1]);
    }

    [Fact]
    public void SplitIntoLines_CrLf_SplitsCorrectly()
    {
        var result = "line1\r\nline2".SplitIntoLines();

        Assert.Equal(2, result.Length);
    }

    // ========== SplitIntoLinesPreserveEmpty Tests ==========
    [Fact]
    public void SplitIntoLinesPreserveEmpty_Empty_ReturnsEmptyArray()
    {
        Assert.Empty("".SplitIntoLinesPreserveEmpty());
    }

    [Fact]
    public void SplitIntoLinesPreserveEmpty_PreservesEmptyLines()
    {
        var result = "line1\n\nline3".SplitIntoLinesPreserveEmpty();

        Assert.Equal(3, result.Length);
        Assert.Equal("", result[1]);
    }

    // ========== NormalizeForSourceOutput Tests ==========
    [Fact]
    public void NormalizeForSourceOutput_TrimsTrailingWhitespace()
    {
        Assert.Equal("hello", "hello   ".NormalizeForSourceOutput());
    }

    [Fact]
    public void NormalizeForSourceOutput_Empty_ReturnsEmpty()
    {
        Assert.Equal("", "".NormalizeForSourceOutput());
    }

    [Fact]
    public void NormalizeForSourceOutput_PreservesLeadingWhitespace()
    {
        Assert.Equal("  hello", "  hello  ".NormalizeForSourceOutput());
    }

    // ========== EnsureEnvironmentNewLines Tests ==========
    [Fact]
    public void EnsureEnvironmentNewLines_NormalizesLineEndings()
    {
        var result = "a\r\nb\nc\rd".EnsureEnvironmentNewLines();

        var lines = result.Split(Environment.NewLine);
        Assert.Equal(4, lines.Length);
    }

    [Fact]
    public void EnsureEnvironmentNewLines_Empty_ReturnsEmpty()
    {
        Assert.Equal("", "".EnsureEnvironmentNewLines());
    }

    // ========== ReplaceAt Tests ==========
    [Fact]
    public void ReplaceAt_ValidIndex_ReplacesCharacter()
    {
        Assert.Equal("hXllo", "hello".ReplaceAt(1, 'X'));
    }

    [Fact]
    public void ReplaceAt_FirstIndex_ReplacesFirst()
    {
        Assert.Equal("Xello", "hello".ReplaceAt(0, 'X'));
    }

    [Fact]
    public void ReplaceAt_LastIndex_ReplacesLast()
    {
        Assert.Equal("hellX", "hello".ReplaceAt(4, 'X'));
    }

    [Fact]
    public void ReplaceAt_NegativeIndex_Throws()
    {
        Assert.Throws<IndexOutOfRangeException>(() => "hello".ReplaceAt(-1, 'X'));
    }

    [Fact]
    public void ReplaceAt_IndexBeyondLength_Throws()
    {
        Assert.Throws<IndexOutOfRangeException>(() => "hello".ReplaceAt(10, 'X'));
    }

    [Fact]
    public void ReplaceAt_Empty_Throws()
    {
        Assert.Throws<IndexOutOfRangeException>(() => "".ReplaceAt(0, 'X'));
    }

    // ========== EnsureEndsWithDot Tests ==========
    [Fact]
    public void EnsureEndsWithDot_NoDot_AppendsDot()
    {
        Assert.Equal("hello.", "hello".EnsureEndsWithDot());
    }

    [Fact]
    public void EnsureEndsWithDot_AlreadyHasDot_ReturnsSame()
    {
        Assert.Equal("hello.", "hello.".EnsureEndsWithDot());
    }

    [Fact]
    public void EnsureEndsWithDot_Empty_ReturnsDot()
    {
        Assert.Equal(".", "".EnsureEndsWithDot());
    }
}