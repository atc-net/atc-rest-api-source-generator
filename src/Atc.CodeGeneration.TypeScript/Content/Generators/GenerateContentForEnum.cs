namespace Atc.CodeGeneration.TypeScript.Content.Generators;

/// <summary>
/// Writes a complete TypeScript enum file: header, JSDoc, <c>export [const] enum Name {</c> and one member per
/// line with its optional value and JSDoc.
/// </summary>
public class GenerateContentForEnum : IContentGenerator
{
    private readonly IJsDocCommentGenerator jsDocCommentGenerator;
    private readonly TypeScriptEnumParameters parameters;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateContentForEnum"/> class.
    /// </summary>
    /// <param name="jsDocCommentGenerator">Renders the JSDoc of the enum and its members.</param>
    /// <param name="parameters">What the enum is made of.</param>
    public GenerateContentForEnum(
        IJsDocCommentGenerator jsDocCommentGenerator,
        TypeScriptEnumParameters parameters)
    {
        this.jsDocCommentGenerator = jsDocCommentGenerator ?? throw new ArgumentNullException(nameof(jsDocCommentGenerator));
        this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    /// <summary>
    /// Renders the enum file.
    /// </summary>
    /// <returns>The TypeScript source of the enum, ending in a line break.</returns>
    public string Generate()
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(parameters.HeaderContent))
        {
            sb.Append(parameters.HeaderContent);
        }

        if (parameters.DocumentationTags is not null)
        {
            sb.Append(jsDocCommentGenerator.GenerateTags(0, parameters.DocumentationTags));
        }

        sb.AppendModifiers(parameters.Modifiers);
        if (parameters.IsConstEnum)
        {
            sb.Append("const ");
        }

        sb.AppendLine($"enum {parameters.TypeName} {{");

        for (var i = 0; i < parameters.Values.Count; i++)
        {
            var value = parameters.Values[i];

            if (value.DocumentationTags is not null)
            {
                if (i > 0)
                {
                    sb.AppendLine();
                }

                sb.Append(jsDocCommentGenerator.GenerateTags(2, value.DocumentationTags));
            }

            sb.Append(2, value.Name);

            if (!string.IsNullOrEmpty(value.Value))
            {
                sb.Append($" = {value.Value}");
            }

            if (i < parameters.Values.Count - 1)
            {
                sb.AppendLine(",");
            }
            else
            {
                sb.AppendLine();
            }
        }

        sb.Append('}');
        sb.AppendLine();

        return sb.ToString();
    }
}