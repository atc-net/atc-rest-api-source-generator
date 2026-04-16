namespace Atc.CodeGeneration.TypeScript.Tests.Helpers;

public class TypeScriptModifiersHelperTests
{
    [Fact]
    public void Render_None_ReturnsEmptyString()
    {
        // Arrange
        var modifiers = TypeScriptModifiers.None;

        // Act
        var result = TypeScriptModifiersHelper.Render(modifiers);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Render_Export_ReturnsExport()
    {
        // Arrange
        var modifiers = TypeScriptModifiers.Export;

        // Act
        var result = TypeScriptModifiersHelper.Render(modifiers);

        // Assert
        Assert.Equal("export", result);
    }

    [Fact]
    public void Render_ExportAndAsync_ReturnsExportAsync()
    {
        // Arrange
        var modifiers = TypeScriptModifiers.Export | TypeScriptModifiers.Async;

        // Act
        var result = TypeScriptModifiersHelper.Render(modifiers);

        // Assert
        Assert.Equal("export async", result);
    }

    [Fact]
    public void Render_ExportDefault_ReturnsExportDefault()
    {
        // Arrange
        var modifiers = TypeScriptModifiers.ExportDefault;

        // Act
        var result = TypeScriptModifiersHelper.Render(modifiers);

        // Assert
        Assert.Equal("export default", result);
    }
}