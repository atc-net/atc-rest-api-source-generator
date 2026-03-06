namespace Atc.CodeGeneration.TypeScript.Tests.Content.Generators;

public class GenerateContentForTypeAliasTests
{
    [Fact]
    public void Generate_SimpleAlias_ProducesCorrectOutput()
    {
        var parameters = new TypeScriptTypeAliasParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "PetId",
            GenericTypeParameter: null,
            ImportStatements: null,
            Definition: "string");

        var generator = new GenerateContentForTypeAlias(
            new JsDocCommentGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("export type PetId = string;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_UnionType_ProducesCorrectOutput()
    {
        var parameters = new TypeScriptTypeAliasParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "Status",
            GenericTypeParameter: null,
            ImportStatements: null,
            Definition: "'active' | 'inactive' | 'pending'");

        var generator = new GenerateContentForTypeAlias(
            new JsDocCommentGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains(
            "export type Status = 'active' | 'inactive' | 'pending';",
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithGenericTypeParameter_IncludesGeneric()
    {
        var parameters = new TypeScriptTypeAliasParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "ApiResult",
            GenericTypeParameter: "T",
            ImportStatements: null,
            Definition: "{ data: T; error?: string }");

        var generator = new GenerateContentForTypeAlias(
            new JsDocCommentGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("export type ApiResult<T>", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithImports_IncludesImportStatements()
    {
        var parameters = new TypeScriptTypeAliasParameters(
            HeaderContent: null,
            DocumentationTags: null,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "PetOrError",
            GenericTypeParameter: null,
            ImportStatements: ["import { Pet } from './Pet';"],
            Definition: "Pet | Error");

        var generator = new GenerateContentForTypeAlias(
            new JsDocCommentGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("import { Pet } from './Pet';", result, StringComparison.Ordinal);
        Assert.Contains("export type PetOrError = Pet | Error;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithDocumentation_IncludesJsDoc()
    {
        var parameters = new TypeScriptTypeAliasParameters(
            HeaderContent: null,
            DocumentationTags: new JsDocComment("A unique identifier for pets."),
            Modifiers: TypeScriptModifiers.Export,
            TypeName: "PetId",
            GenericTypeParameter: null,
            ImportStatements: null,
            Definition: "string");

        var generator = new GenerateContentForTypeAlias(
            new JsDocCommentGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("/** A unique identifier for pets. */", result, StringComparison.Ordinal);
    }
}