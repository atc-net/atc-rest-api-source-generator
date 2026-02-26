namespace Atc.CodeGeneration.TypeScript.Content.Generators;

public class GenerateContentForInterface : IContentGenerator
{
    private readonly GenerateContentWriter writer;
    private readonly TypeScriptInterfaceParameters parameters;

    public GenerateContentForInterface(
        GenerateContentWriter writer,
        TypeScriptInterfaceParameters parameters)
    {
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

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
