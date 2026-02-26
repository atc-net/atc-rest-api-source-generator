namespace Atc.CodeGeneration.TypeScript.Content.Generators;

public class GenerateContentForTypeAlias : IContentGenerator
{
    private readonly TypeScriptTypeAliasParameters parameters;
    private readonly IJsDocCommentGenerator jsDocCommentGenerator;

    public GenerateContentForTypeAlias(
        IJsDocCommentGenerator jsDocCommentGenerator,
        TypeScriptTypeAliasParameters parameters)
    {
        this.jsDocCommentGenerator = jsDocCommentGenerator ?? throw new ArgumentNullException(nameof(jsDocCommentGenerator));
        this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public string Generate()
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(parameters.HeaderContent))
        {
            sb.Append(parameters.HeaderContent);
        }

        if (parameters.ImportStatements is not null && parameters.ImportStatements.Any())
        {
            foreach (var importStatement in parameters.ImportStatements)
            {
                sb.AppendLine(importStatement);
            }

            sb.AppendLine();
        }

        if (parameters.DocumentationTags is not null)
        {
            sb.Append(jsDocCommentGenerator.GenerateTags(0, parameters.DocumentationTags));
        }

        sb.AppendModifiers(parameters.Modifiers);
        sb.Append($"type {parameters.TypeName}");

        if (!string.IsNullOrEmpty(parameters.GenericTypeParameter))
        {
            sb.Append($"<{parameters.GenericTypeParameter}>");
        }

        sb.Append($" = {parameters.Definition};");
        sb.AppendLine();

        return sb.ToString();
    }
}
