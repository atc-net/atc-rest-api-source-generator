// ReSharper disable once CheckNamespace
namespace System;

/// <summary>
/// <see cref="StringBuilder"/> extensions that append TypeScript modifiers and indented bodies.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
[SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "OK.")]
public static class TypeScriptStringBuilderExtensions
{
    /// <param name="sb">The builder to append to.</param>
    extension(StringBuilder sb)
    {
        /// <summary>
        /// Appends the modifiers' keywords followed by a space; <see cref="TypeScriptModifiers.None"/> appends nothing.
        /// </summary>
        /// <param name="modifiers">The modifiers to write.</param>
        public void AppendModifiers(TypeScriptModifiers modifiers)
        {
            var rendered = TypeScriptModifiersHelper.Render(modifiers);
            if (!string.IsNullOrEmpty(rendered))
            {
                sb.Append(rendered);
                sb.Append(' ');
            }
        }

        /// <summary>
        /// Appends a multi-line body with every non-empty line indented; a trailing empty line is dropped and the
        /// last line gets no line break.
        /// </summary>
        /// <param name="indentSpaces">The number of spaces before each line.</param>
        /// <param name="content">The body text, with any line-ending style.</param>
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