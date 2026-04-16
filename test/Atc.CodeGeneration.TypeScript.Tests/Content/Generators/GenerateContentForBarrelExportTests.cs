namespace Atc.CodeGeneration.TypeScript.Tests.Content.Generators;

public class GenerateContentForBarrelExportTests
{
    [Fact]
    public void Generate_WildcardExport_ProducesStarExport()
    {
        // Arrange
        var parameters = new TypeScriptBarrelExportParameters(
            HeaderContent: null,
            Exports:
            [
                new TypeScriptReExportParameters("./Pet", null, false),
                new TypeScriptReExportParameters("./Owner", null, false),
            ]);

        var generator = new GenerateContentForBarrelExport(parameters);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("export * from './Pet';", result, StringComparison.Ordinal);
        Assert.Contains("export * from './Owner';", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_NamedExport_ProducesNamedExport()
    {
        // Arrange
        var parameters = new TypeScriptBarrelExportParameters(
            HeaderContent: null,
            Exports:
            [
                new TypeScriptReExportParameters(
                    "./Pet",
                    ["Pet", "PetType"],
                    false),
            ]);

        var generator = new GenerateContentForBarrelExport(parameters);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains(
            "export { Pet, PetType } from './Pet';",
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_TypeOnlyExport_IncludesTypeKeyword()
    {
        // Arrange
        var parameters = new TypeScriptBarrelExportParameters(
            HeaderContent: null,
            Exports:
            [
                new TypeScriptReExportParameters("./Pet", null, true),
            ]);

        var generator = new GenerateContentForBarrelExport(parameters);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("export type * from './Pet';", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_TypeOnlyNamedExport_IncludesTypeKeyword()
    {
        // Arrange
        var parameters = new TypeScriptBarrelExportParameters(
            HeaderContent: null,
            Exports:
            [
                new TypeScriptReExportParameters(
                    "./Pet",
                    ["Pet"],
                    true),
            ]);

        var generator = new GenerateContentForBarrelExport(parameters);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains(
            "export type { Pet } from './Pet';",
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithHeaderContent_IncludesHeader()
    {
        // Arrange
        var parameters = new TypeScriptBarrelExportParameters(
            HeaderContent: "// Auto-generated barrel file\n",
            Exports:
            [
                new TypeScriptReExportParameters("./Pet", null, false),
            ]);

        var generator = new GenerateContentForBarrelExport(parameters);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.StartsWith("// Auto-generated", result, StringComparison.Ordinal);
    }
}