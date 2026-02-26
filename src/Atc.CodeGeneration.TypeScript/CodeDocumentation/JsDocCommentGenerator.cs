namespace Atc.CodeGeneration.TypeScript.CodeDocumentation;

public class JsDocCommentGenerator : IJsDocCommentGenerator
{
    public bool ShouldGenerateTags(JsDocComment jsDocComment)
    {
        if (jsDocComment is null)
        {
            throw new ArgumentNullException(nameof(jsDocComment));
        }

        if (jsDocComment.Parameters is not null ||
            jsDocComment.Returns is not null ||
            jsDocComment.IsDeprecated ||
            jsDocComment.Example is not null)
        {
            return true;
        }

        return !string.IsNullOrEmpty(jsDocComment.Description) &&
               !jsDocComment.Description.StartsWith(Constants.UndefinedDescription, StringComparison.Ordinal);
    }

    public string GenerateTags(
        ushort indentSpaces,
        JsDocComment jsDocComment)
    {
        if (jsDocComment is null)
        {
            throw new ArgumentNullException(nameof(jsDocComment));
        }

        if (!ShouldGenerateTags(jsDocComment))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var indent = indentSpaces > 0 ? new string(' ', indentSpaces) : string.Empty;

        var hasMultipleEntries = HasMultipleEntries(jsDocComment);

        if (!hasMultipleEntries && !string.IsNullOrEmpty(jsDocComment.Description))
        {
            sb.AppendLine($"{indent}/** {jsDocComment.Description} */");
            return sb.ToString();
        }

        sb.AppendLine($"{indent}/**");

        if (!string.IsNullOrEmpty(jsDocComment.Description))
        {
            var lines = jsDocComment.Description
                .EnsureEnvironmentNewLines()
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                sb.AppendLine($"{indent} * {line}");
            }
        }

        if (jsDocComment.Parameters is not null && jsDocComment.Parameters.Any())
        {
            foreach (var param in jsDocComment.Parameters)
            {
                sb.AppendLine($"{indent} * @param {param.Key} {param.Value}");
            }
        }

        if (!string.IsNullOrEmpty(jsDocComment.Returns))
        {
            sb.AppendLine($"{indent} * @returns {jsDocComment.Returns}");
        }

        if (jsDocComment.IsDeprecated)
        {
            if (!string.IsNullOrEmpty(jsDocComment.DeprecatedMessage))
            {
                sb.AppendLine($"{indent} * @deprecated {jsDocComment.DeprecatedMessage}");
            }
            else
            {
                sb.AppendLine($"{indent} * @deprecated");
            }
        }

        if (!string.IsNullOrEmpty(jsDocComment.Example))
        {
            sb.AppendLine($"{indent} * @example");
            var exampleLines = jsDocComment.Example
                .EnsureEnvironmentNewLines()
                .Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            foreach (var line in exampleLines)
            {
                sb.AppendLine($"{indent} * {line}");
            }
        }

        sb.AppendLine($"{indent} */");

        return sb.ToString();
    }

    private static bool HasMultipleEntries(JsDocComment jsDocComment)
    {
        var count = 0;
        if (!string.IsNullOrEmpty(jsDocComment.Description))
        {
            count++;
        }

        if (jsDocComment.Parameters is not null && jsDocComment.Parameters.Any())
        {
            count++;
        }

        if (!string.IsNullOrEmpty(jsDocComment.Returns))
        {
            count++;
        }

        if (jsDocComment.IsDeprecated)
        {
            count++;
        }

        if (!string.IsNullOrEmpty(jsDocComment.Example))
        {
            count++;
        }

        return count > 1;
    }
}
