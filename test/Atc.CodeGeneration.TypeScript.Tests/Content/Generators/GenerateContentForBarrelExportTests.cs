namespace Atc.CodeGeneration.TypeScript.Tests.Content.Generators;

public class GenerateContentForBarrelExportTests
{
    [Fact]
    public void Generate_WildcardExport_ProducesStarExport()
    {
        var parameters = new TypeScriptBarrelExportParameters(
            HeaderContent: null,
            Exports:
            [
                new TypeScriptReExportParameters("./Pet", null, false),
                new TypeScriptReExportParameters("./Owner", null, false),
            ]);

        var generator = new GenerateContentForBarrelExport(parameters);

        var result = generator.Generate();

        Assert.Contains("export * from './Pet';", result, StringComparison.Ordinal);
        Assert.Contains("export * from './Owner';", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_NamedExport_ProducesNamedExport()
    {
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

        var result = generator.Generate();

        Assert.Contains(
            "export { Pet, PetType } from './Pet';",
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_TypeOnlyExport_IncludesTypeKeyword()
    {
        var parameters = new TypeScriptBarrelExportParameters(
            HeaderContent: null,
            Exports:
            [
                new TypeScriptReExportParameters("./Pet", null, true),
            ]);

        var generator = new GenerateContentForBarrelExport(parameters);

        var result = generator.Generate();

        Assert.Contains("export type * from './Pet';", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_TypeOnlyNamedExport_IncludesTypeKeyword()
    {
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

        var result = generator.Generate();

        Assert.Contains(
            "export type { Pet } from './Pet';",
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithHeaderContent_IncludesHeader()
    {
        var parameters = new TypeScriptBarrelExportParameters(
            HeaderContent: "// Auto-generated barrel file\n",
            Exports:
            [
                new TypeScriptReExportParameters("./Pet", null, false),
            ]);

        var generator = new GenerateContentForBarrelExport(parameters);

        var result = generator.Generate();

        Assert.StartsWith("// Auto-generated", result, StringComparison.Ordinal);
    }
}