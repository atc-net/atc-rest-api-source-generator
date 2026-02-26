// ReSharper disable once CheckNamespace
namespace System;

[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
[SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "OK.")]
public static class TypeScriptStringBuilderExtensions
{
    extension(StringBuilder sb)
    {
        internal void Append(
            int indentSpaces,
            string value)
        {
            if (indentSpaces > 0)
            {
                sb.Append(new string(' ', indentSpaces));
            }

            sb.Append(value);
        }

        internal void AppendLine(
            int indentSpaces,
            string value)
        {
            if (indentSpaces > 0)
            {
                sb.Append(new string(' ', indentSpaces));
            }

            sb.AppendLine(value);
        }

        public void AppendModifiers(TypeScriptModifiers modifiers)
        {
            var rendered = TypeScriptModifiersHelper.Render(modifiers);
            if (!string.IsNullOrEmpty(rendered))
            {
                sb.Append(rendered);
                sb.Append(' ');
            }
        }

        public void AppendModifiers(
            int indentSpaces,
            TypeScriptModifiers modifiers)
        {
            var rendered = TypeScriptModifiersHelper.Render(modifiers);
            if (!string.IsNullOrEmpty(rendered))
            {
                sb.Append(indentSpaces, rendered);
                sb.Append(' ');
            }
            else if (indentSpaces > 0)
            {
                sb.Append(new string(' ', indentSpaces));
            }
        }

        public void AppendContent(
            int indentSpaces,
            string content)
        {
            var lines = content
                .EnsureEnvironmentNewLines()
                .Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            var linesLength = lines.Length;
            if (string.IsNullOrEmpty(lines[lines.Length - 1]))
            {
                linesLength--;
            }

            for (var i = 0; i < linesLength; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line))
                {
                    sb.AppendLine();
                }
                else
                {
                    if (i == linesLength - 1)
                    {
                        sb.Append(indentSpaces, line);
                    }
                    else
                    {
                        sb.AppendLine(indentSpaces, line);
                    }
                }
            }
        }
    }
}
