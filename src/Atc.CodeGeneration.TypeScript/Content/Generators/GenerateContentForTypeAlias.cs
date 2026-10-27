namespace Atc.CodeGeneration.TypeScript.Content.Generators;

/// <summary>
/// Writes a complete TypeScript type alias file: header, imports, JSDoc and
/// <c>export type Name&lt;T&gt; = definition;</c>.
/// </summary>
public class GenerateContentForTypeAlias : IContentGenerator
{
    private readonly TypeScriptTypeAliasParameters parameters;
    private readonly IJsDocCommentGenerator jsDocCommentGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateContentForTypeAlias"/> class.
    /// </summary>
    /// <param name="jsDocCommentGenerator">Renders the JSDoc of the alias.</param>
    /// <param name="parameters">What the alias is made of.</param>
    public GenerateContentForTypeAlias(
        IJsDocCommentGenerator jsDocCommentGenerator,
        TypeScriptTypeAliasParameters parameters)
    {
        this.jsDocCommentGenerator = jsDocCommentGenerator ?? throw new ArgumentNullException(nameof(jsDocCommentGenerator));
        this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    /// <summary>
    /// Renders the type alias file.
    /// </summary>
    /// <returns>The TypeScript source of the alias, ending in a line break.</returns>
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