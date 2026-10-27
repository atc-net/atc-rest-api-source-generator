namespace Atc.CodeGeneration.TypeScript.Content.Generators.Internal;

/// <summary>
/// Renders the pieces the class and interface generators share - file top, properties, method signatures,
/// methods and constructors - as strings indented two spaces for a member of a top-level type.
/// </summary>
[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "OK.")]
public class GenerateContentWriter
{
    private const int IndentSize = 2;

    private readonly IJsDocCommentGenerator jsDocCommentGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateContentWriter"/> class.
    /// </summary>
    /// <param name="jsDocCommentGenerator">Renders the JSDoc of the members written.</param>
    public GenerateContentWriter(
        IJsDocCommentGenerator jsDocCommentGenerator)
    {
        this.jsDocCommentGenerator = jsDocCommentGenerator;
    }

    /// <summary>
    /// Renders the header, the import lines followed by a blank line, and the type's JSDoc.
    /// </summary>
    /// <param name="headerContent">Text written first, verbatim; null or empty writes nothing.</param>
    /// <param name="importStatements">The import lines, or null for none.</param>
    /// <param name="documentationTags">The JSDoc of the type, or null for none.</param>
    /// <returns>The text that precedes the type declaration.</returns>
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

    /// <summary>
    /// Renders a property as <c>[readonly ]name[?]: type[ = default];</c> preceded by its JSDoc.
    /// </summary>
    /// <param name="parameters">The property's name, type, flags and default.</param>
    /// <returns>The property line without a trailing line break.</returns>
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

    /// <summary>
    /// Renders an interface method signature as <c>name&lt;T&gt;(params): returnType;</c> preceded by its JSDoc.
    /// </summary>
    /// <param name="parameters">The method's name, type parameter, parameters and return type.</param>
    /// <returns>The signature line without a trailing line break.</returns>
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

    /// <summary>
    /// Renders a class method as <c>[async ]name&lt;T&gt;(params): returnType { body }</c> preceded by its JSDoc;
    /// only <see cref="TypeScriptModifiers.Async"/> is honoured from the modifiers.
    /// </summary>
    /// <param name="parameters">The method's name, modifiers, parameters, return type and body.</param>
    /// <returns>The method text ending in the closing brace, without a trailing line break.</returns>
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

    /// <summary>
    /// Renders a constructor preceded by its JSDoc. Parameters go one per line when any is a
    /// <c>private</c>/<c>readonly</c> parameter property or there are more than two, else on one line;
    /// no body gives <c>{}</c>.
    /// </summary>
    /// <param name="parameters">The constructor's parameters and body.</param>
    /// <returns>The constructor text ending in the closing brace, without a trailing line break.</returns>
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