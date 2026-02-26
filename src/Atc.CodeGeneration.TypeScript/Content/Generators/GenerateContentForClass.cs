namespace Atc.CodeGeneration.TypeScript.Content.Generators;

public class GenerateContentForClass : IContentGenerator
{
    private readonly GenerateContentWriter writer;
    private readonly TypeScriptClassParameters parameters;

    public GenerateContentForClass(
        GenerateContentWriter writer,
        TypeScriptClassParameters parameters)
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
        sb.Append($"class {parameters.TypeName}");

        if (!string.IsNullOrEmpty(parameters.GenericTypeParameter))
        {
            sb.Append($"<{parameters.GenericTypeParameter}>");
        }

        if (!string.IsNullOrEmpty(parameters.ExtendsTypeName))
        {
            sb.Append($" extends {parameters.ExtendsTypeName}");
        }

        if (parameters.ImplementsTypeNames is not null && parameters.ImplementsTypeNames.Any())
        {
            sb.Append($" implements {string.Join(", ", parameters.ImplementsTypeNames)}");
        }

        sb.AppendLine(" {");

        var memberIndex = 0;

        if (parameters.Constructors is not null)
        {
            foreach (var constructor in parameters.Constructors)
            {
                if (memberIndex > 0)
                {
                    sb.AppendLine();
                }

                sb.Append(writer.GenerateConstructor(constructor));
                sb.AppendLine();
                memberIndex++;
            }
        }

        if (parameters.Properties is not null)
        {
            foreach (var property in parameters.Properties)
            {
                if (memberIndex > 0)
                {
                    sb.AppendLine();
                }

                sb.Append(writer.GenerateProperty(property));
                sb.AppendLine();
                memberIndex++;
            }
        }

        if (parameters.Methods is not null)
        {
            foreach (var method in parameters.Methods)
            {
                if (memberIndex > 0)
                {
                    sb.AppendLine();
                }

                sb.Append(writer.GenerateMethod(method));
                sb.AppendLine();
                memberIndex++;
            }
        }

        sb.Append('}');
        sb.AppendLine();

        return sb.ToString();
    }
}
