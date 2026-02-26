namespace Atc.CodeGeneration.TypeScript.Content.Generators.Internal;

[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "OK.")]
public class GenerateContentWriter
{
    private const int IndentSize = 2;

    private readonly IJsDocCommentGenerator jsDocCommentGenerator;

    public GenerateContentWriter(
        IJsDocCommentGenerator jsDocCommentGenerator)
    {
        this.jsDocCommentGenerator = jsDocCommentGenerator;
    }

    public string GenerateTopOfType(
        string? headerContent,
        IList<string>? importStatements,
        JsDocComment? documentationTags)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(headerContent))
        {
            sb.Append(headerContent);
        }

        if (importStatements is not null && importStatements.Any())
        {
            foreach (var importStatement in importStatements)
            {
                sb.AppendLine(importStatement);
            }

            sb.AppendLine();
        }

        if (documentationTags is not null)
        {
            sb.Append(jsDocCommentGenerator.GenerateTags(0, documentationTags));
        }

        return sb.ToString();
    }

    public string GenerateProperty(
        TypeScriptPropertyParameters parameters)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        var sb = new StringBuilder();

        if (parameters.DocumentationTags is not null)
        {
            sb.Append(jsDocCommentGenerator.GenerateTags(IndentSize, parameters.DocumentationTags));
        }

        sb.Append(IndentSize, string.Empty);
        if (parameters.IsReadonly)
        {
            sb.Append("readonly ");
        }

        sb.Append(parameters.Name);
        if (parameters.IsOptional)
        {
            sb.Append('?');
        }

        sb.Append($": {parameters.TypeAnnotation}");

        if (!string.IsNullOrEmpty(parameters.DefaultValue))
        {
            sb.Append($" = {parameters.DefaultValue}");
        }

        sb.Append(';');

        return sb.ToString();
    }

    public string GenerateMethodSignature(
        TypeScriptMethodSignatureParameters parameters)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        var sb = new StringBuilder();

        if (parameters.DocumentationTags is not null)
        {
            sb.Append(jsDocCommentGenerator.GenerateTags(IndentSize, parameters.DocumentationTags));
        }

        sb.Append(IndentSize, parameters.Name);

        if (!string.IsNullOrEmpty(parameters.GenericTypeParameter))
        {
            sb.Append($"<{parameters.GenericTypeParameter}>");
        }

        sb.Append('(');
        AppendParameterList(sb, parameters.Parameters);
        sb.Append(')');

        if (!string.IsNullOrEmpty(parameters.ReturnType))
        {
            sb.Append($": {parameters.ReturnType}");
        }

        sb.Append(';');

        return sb.ToString();
    }

    public string GenerateMethod(
        TypeScriptMethodParameters parameters)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        var sb = new StringBuilder();

        if (parameters.DocumentationTags is not null)
        {
            sb.Append(jsDocCommentGenerator.GenerateTags(IndentSize, parameters.DocumentationTags));
        }

        sb.Append(IndentSize, string.Empty);
        if ((parameters.Modifiers & TypeScriptModifiers.Async) == TypeScriptModifiers.Async)
        {
            sb.Append("async ");
        }

        sb.Append(parameters.Name);

        if (!string.IsNullOrEmpty(parameters.GenericTypeParameter))
        {
            sb.Append($"<{parameters.GenericTypeParameter}>");
        }

        sb.Append('(');
        AppendParameterList(sb, parameters.Parameters);
        sb.Append(')');

        if (!string.IsNullOrEmpty(parameters.ReturnType))
        {
            sb.Append($": {parameters.ReturnType}");
        }

        if (string.IsNullOrEmpty(parameters.Content))
        {
            sb.AppendLine(" {");
            sb.Append(IndentSize, "}");
        }
        else
        {
            sb.AppendLine(" {");
            sb.AppendContent(IndentSize * 2, parameters.Content);
            sb.AppendLine();
            sb.Append(IndentSize, "}");
        }

        return sb.ToString();
    }

    public string GenerateConstructor(
        TypeScriptConstructorParameters parameters)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        var sb = new StringBuilder();

        if (parameters.DocumentationTags is not null)
        {
            sb.Append(jsDocCommentGenerator.GenerateTags(IndentSize, parameters.DocumentationTags));
        }

        sb.Append(IndentSize, "constructor(");

        if (parameters.Parameters is not null && parameters.Parameters.Any())
        {
            var hasShorthandParams = parameters.Parameters.Any(p =>
                p.AccessModifier != TypeScriptModifiers.None || p.IsReadonly);

            if (hasShorthandParams || parameters.Parameters.Count > 2)
            {
                sb.AppendLine();
                for (var i = 0; i < parameters.Parameters.Count; i++)
                {
                    var param = parameters.Parameters[i];
                    sb.Append(IndentSize * 2, string.Empty);

                    if (param.AccessModifier != TypeScriptModifiers.None)
                    {
                        sb.Append(TypeScriptModifiersHelper.Render(param.AccessModifier));
                        sb.Append(' ');
                    }

                    if (param.IsReadonly)
                    {
                        sb.Append("readonly ");
                    }

                    sb.Append(param.Name);
                    if (param.IsOptional)
                    {
                        sb.Append('?');
                    }

                    sb.Append($": {param.TypeAnnotation}");

                    if (!string.IsNullOrEmpty(param.DefaultValue))
                {
                        sb.Append($" = {param.DefaultValue}");
                    }

                    if (i < parameters.Parameters.Count - 1)
                    {
                        sb.AppendLine(",");
                    }
                    else
                    {
                        sb.AppendLine();
                    }
                }

                sb.Append(IndentSize, ")");
            }
            else
            {
                AppendConstructorParameterList(sb, parameters.Parameters);
                sb.Append(')');
            }
        }
        else
        {
            sb.Append(')');
        }

        if (string.IsNullOrEmpty(parameters.Content))
        {
            sb.Append(" {}");
        }
        else
        {
            sb.AppendLine(" {");
            sb.AppendContent(IndentSize * 2, parameters.Content);
            sb.AppendLine();
            sb.Append(IndentSize, "}");
        }

        return sb.ToString();
    }

    private static void AppendParameterList(
        StringBuilder sb,
        IList<TypeScriptParameterParameters>? parameters)
    {
        if (parameters is null || !parameters.Any())
        {
            return;
        }

        for (var i = 0; i < parameters.Count; i++)
        {
            var param = parameters[i];
            sb.Append(param.Name);
            if (param.IsOptional && string.IsNullOrEmpty(param.DefaultValue))
            {
                sb.Append('?');
            }

            sb.Append($": {param.TypeAnnotation}");

            if (!string.IsNullOrEmpty(param.DefaultValue))
            {
                sb.Append($" = {param.DefaultValue}");
            }

            if (i < parameters.Count - 1)
            {
                sb.Append(", ");
            }
        }
    }

    private static void AppendConstructorParameterList(
        StringBuilder sb,
        IList<TypeScriptConstructorParameterParameters> parameters)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            var param = parameters[i];
            sb.Append(param.Name);
            if (param.IsOptional && string.IsNullOrEmpty(param.DefaultValue))
            {
                sb.Append('?');
            }

            sb.Append($": {param.TypeAnnotation}");

            if (!string.IsNullOrEmpty(param.DefaultValue))
            {
                sb.Append($" = {param.DefaultValue}");
            }

            if (i < parameters.Count - 1)
            {
                sb.Append(", ");
            }
        }
    }
}
