namespace Atc.CodeGeneration.TypeScript.Content.Generators;

/// <summary>
/// Writes a complete TypeScript class file: header, imports, JSDoc, the class declaration with its
/// <c>extends</c> and <c>implements</c> clauses, then constructors, properties and methods separated by
/// blank lines.
/// </summary>
public class GenerateContentForClass : IContentGenerator
{
    private readonly GenerateContentWriter writer;
    private readonly TypeScriptClassParameters parameters;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateContentForClass"/> class.
    /// </summary>
    /// <param name="writer">Renders the file top and the members.</param>
    /// <param name="parameters">What the class is made of.</param>
    public GenerateContentForClass(
        GenerateContentWriter writer,
        TypeScriptClassParameters parameters)
    {
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    /// <summary>
    /// Renders the class file.
    /// </summary>
    /// <returns>The TypeScript source of the class, ending in a line break.</returns>
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