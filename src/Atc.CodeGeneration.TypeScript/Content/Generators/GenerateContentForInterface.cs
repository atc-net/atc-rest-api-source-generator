namespace Atc.CodeGeneration.TypeScript.Content.Generators;

/// <summary>
/// Writes a complete TypeScript interface file: header, imports, JSDoc, the interface declaration with its
/// <c>extends</c> clause, then properties and method signatures separated by blank lines.
/// </summary>
public class GenerateContentForInterface : IContentGenerator
{
    private readonly GenerateContentWriter writer;
    private readonly TypeScriptInterfaceParameters parameters;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateContentForInterface"/> class.
    /// </summary>
    /// <param name="writer">Renders the file top and the members.</param>
    /// <param name="parameters">What the interface is made of.</param>
    public GenerateContentForInterface(
        GenerateContentWriter writer,
        TypeScriptInterfaceParameters parameters)
    {
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    /// <summary>
    /// Renders the interface file.
    /// </summary>
    /// <returns>The TypeScript source of the interface, ending in a line break.</returns>
    public string Generate()
    {
        var sb = new StringBuilder();

        sb.Append(writer.GenerateTopOfType(
            parameters.HeaderContent,
            parameters.ImportStatements,
            parameters.DocumentationTags));

        sb.AppendModifiers(parameters.Modifiers);
        sb.Append($"interface {parameters.TypeName}");

        if (!string.IsNullOrEmpty(parameters.ExtendsTypeName))
        {
            sb.Append($" extends {parameters.ExtendsTypeName}");
        }

        sb.AppendLine(" {");

        var hasContent = false;

        if (parameters.Properties is not null)
        {
            for (var i = 0; i < parameters.Properties.Count; i++)
            {
                if (i > 0)
                {
                    sb.AppendLine();
                }

                sb.Append(writer.GenerateProperty(parameters.Properties[i]));
                sb.AppendLine();
                hasContent = true;
            }
        }

        if (parameters.Methods is not null)
        {
            for (var i = 0; i < parameters.Methods.Count; i++)
            {
                if (hasContent || i > 0)
                {
                    sb.AppendLine();
                }

                sb.Append(writer.GenerateMethodSignature(parameters.Methods[i]));
                sb.AppendLine();
            }
        }

        sb.Append('}');
        sb.AppendLine();

        return sb.ToString();
    }
}