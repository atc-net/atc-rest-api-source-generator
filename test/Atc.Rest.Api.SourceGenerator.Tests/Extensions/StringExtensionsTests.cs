namespace Atc.Rest.Api.SourceGenerator.Tests.Extensions;

[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
public class StringExtensionsTests
{
    // ========== SplitIntoLinesPreserveEmpty Tests ==========

    [Fact]
    public void SplitIntoLinesPreserveEmpty_WithBlankLines_PreservesAllLines()
    {
        // Arrange
        var input = "line1\n\nline3\n\nline5";

        // Act
        var result = input.SplitIntoLinesPreserveEmpty();

        // Assert
        Assert.Equal(5, result.Length);
        Assert.Equal("line1", result[0]);
        Assert.Equal("", result[1]);       // Empty line preserved
        Assert.Equal("line3", result[2]);
        Assert.Equal("", result[3]);       // Empty line preserved
        Assert.Equal("line5", result[4]);
    }

    [Fact]
    public void SplitIntoLinesPreserveEmpty_WithWindowsLineEndings_SplitsCorrectly()
    {
        // Arrange
        var input = "line1\r\n\r\nline3";

        // Act
        var result = input.SplitIntoLinesPreserveEmpty();

        // Assert
        Assert.Equal(3, result.Length);
        Assert.Equal("line1", result[0]);
        Assert.Equal("", result[1]);       // Empty line preserved
        Assert.Equal("line3", result[2]);
    }

    [Fact]
    public void SplitIntoLinesPreserveEmpty_WithOnlyContent_ReturnsAllLines()
    {
        // Arrange
        var input = "line1\nline2\nline3";

        // Act
        var result = input.SplitIntoLinesPreserveEmpty();

        // Assert
        Assert.Equal(3, result.Length);
        Assert.All(result, line => Assert.False(string.IsNullOrEmpty(line)));
    }

    [Fact]
    public void SplitIntoLinesPreserveEmpty_WithEmptyString_ReturnsEmptyArray()
    {
        // Arrange
        var input = string.Empty;

        // Act
        var result = input.SplitIntoLinesPreserveEmpty();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void SplitIntoLinesPreserveEmpty_WithNull_ReturnsEmptyArray()
    {
        // Arrange
        string? input = null;

        // Act
        var result = input!.SplitIntoLinesPreserveEmpty();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void SplitIntoLinesPreserveEmpty_GeneratedClassContent_PreservesMethodSpacing()
    {
        // Arrange - simulate generated class content with blank lines between methods
        var input = """
            public class TestResult : IResult
            {
                private readonly IResult innerResult;

                public static TestResult Ok(string response)
                    => new(Results.Ok(response));

                public Task ExecuteAsync(HttpContext httpContext)
                    => innerResult.ExecuteAsync(httpContext);

                public static IResult ToIResult(TestResult result)
                    => result;
            }
            """;

        // Act
        var result = input.SplitIntoLinesPreserveEmpty();

        // Assert - verify blank lines are preserved
        var blankLineCount = result.Count(line => string.IsNullOrWhiteSpace(line));
        Assert.True(blankLineCount >= 3, "Should preserve blank lines between members");
    }

    // ========== SplitIntoLines vs SplitIntoLinesPreserveEmpty Comparison ==========

    [Fact]
    public void SplitIntoLines_RemovesEmptyLines()
    {
        // Arrange
        var input = "line1\n\nline3";

        // Act
        var result = input.SplitIntoLines();

        // Assert - SplitIntoLines removes empty lines
        Assert.Equal(2, result.Length);
        Assert.Equal("line1", result[0]);
        Assert.Equal("line3", result[1]);
    }

    [Fact]
    public void SplitIntoLinesPreserveEmpty_KeepsEmptyLines()
    {
        // Arrange
        var input = "line1\n\nline3";

        // Act
        var result = input.SplitIntoLinesPreserveEmpty();

        // Assert - SplitIntoLinesPreserveEmpty keeps empty lines
        Assert.Equal(3, result.Length);
        Assert.Equal("line1", result[0]);
        Assert.Equal("", result[1]);
        Assert.Equal("line3", result[2]);
    }
}