// ReSharper disable once CheckNamespace
namespace System;

[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
[SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "OK.")]
public static class StringBuilderExtensions
{
    extension(StringBuilder sb)
    {
        public void AppendDeclarationModifier(DeclarationModifiers declarationModifier)
            => sb.AppendDeclarationModifier(0, declarationModifier);

        public void AppendDeclarationModifier(
            int indentSpaces,
            DeclarationModifiers declarationModifier)
        {
            if (declarationModifier == DeclarationModifiers.None)
            {
                return;
            }

            sb.Append(indentSpaces, EnumDescriptionHelper.GetDescription(declarationModifier));
            sb.Append(' ');
        }

        public void AppendTypeAndName(
            string? genericTypeName,
            string typeName,
            bool isNullableType,
            string name)
            => sb.AppendTypeAndName(
                0,
                genericTypeName,
                typeName,
                isNullableType,
                name,
                defaultValue: null);

        public void AppendTypeAndName(
            int indentSpaces,
            string? genericTypeName,
            string typeName,
            bool isNullableType,
            string name)
            => sb.AppendTypeAndName(
                indentSpaces,
                genericTypeName,
                typeName,
                isNullableType,
                name,
                defaultValue: null);

        public void AppendTypeAndName(
            int indentSpaces,
            string? genericTypeName,
            string typeName,
            bool isNullableType,
            string name,
            string? defaultValue)
        {
            // Build the type part
            string typeText;
            if (string.IsNullOrEmpty(genericTypeName))
            {
                typeText = isNullableType ? $"{typeName}?" : typeName;
            }
            else
            {
                typeText = isNullableType
                    ? $"{genericTypeName}<{typeName}>?"
                    : $"{genericTypeName}<{typeName}>";
            }

            // Only add space and name if name is not empty (implicit operators have empty name)
            if (string.IsNullOrEmpty(name))
            {
                sb.Append(indentSpaces, typeText);
            }
            else
            {
                sb.Append(indentSpaces, $"{typeText} {name}");
            }

            if (string.IsNullOrEmpty(defaultValue))
            {
                return;
            }

            if (string.IsNullOrEmpty(genericTypeName) &&
                typeName.Equals("string", StringComparison.Ordinal) &&
                !defaultValue.Equals("null", StringComparison.Ordinal))
            {
                sb.Append($" = \"{defaultValue}\"");
            }
            else
            {
                sb.Append($" = {defaultValue}");
            }
        }

        public void AppendAttributesAsLines(
            int indentSpaces,
            bool usePropertyPrefix,
            IList<AttributeParameters> attributes)
        {
            foreach (var item in attributes)
            {
                sb.AppendAttribute(indentSpaces, usePropertyPrefix, item);
                sb.AppendLine();
            }
        }

        public void AppendAttribute(
            bool usePropertyPrefix,
            AttributeParameters attribute)
            => sb.AppendAttribute(
                indentSpaces: 0,
                usePropertyPrefix,
                attribute.Name,
                attribute.Content);

        public void AppendAttribute(
            int indentSpaces,
            bool usePropertyPrefix,
            AttributeParameters attribute)
            => sb.AppendAttribute(
                indentSpaces,
                usePropertyPrefix,
                attribute.Name,
                attribute.Content);

        public void AppendAttribute(
            int indentSpaces,
            bool usePropertyPrefix,
            string name,
            string? content)
        {
            if (usePropertyPrefix)
            {
                sb.Append(
                    indentSpaces,
                    string.IsNullOrEmpty(content)
                        ? $"[property: {name}]"
                        : $"[property: {name}({content})]");
            }
            else
            {
                sb.Append(
                    indentSpaces,
                    string.IsNullOrEmpty(content)
                        ? $"[{name}]"
                        : $"[{name}({content})]");
            }
        }

        public void AppendAttributes(
            int indentSpaces,
            bool usePropertyPrefix,
            bool mergeIntoOne,
            IList<AttributeParameters> attributes)
        {
            if (mergeIntoOne)
            {
                for (var i = 0; i < attributes.Count; i++)
                {
                    if (i == 0)
                    {
                        sb.Append(
                            indentSpaces,
                            usePropertyPrefix
                                ? "[property: "
                                : "[");
                    }

                    var attribute = attributes[i];

                    sb.Append(string.IsNullOrEmpty(attribute.Content)
                        ? attribute.Name
                        : $"{attribute.Name}({attribute.Content})");

                    if (i == attributes.Count - 1)
                    {
                        sb.Append(']');
                    }
                    else
                    {
                        sb.Append(", ");
                    }
                }
            }
            else
            {
                foreach (var attribute in attributes)
                {
                    AppendAttribute(sb, indentSpaces, usePropertyPrefix, attribute);
                }
            }
        }

        public void AppendInputParameter(
            int indentSpaces,
            bool usePropertyPrefix,
            IList<AttributeParameters>? attributes,
            string? genericTypeName,
            string typeName,
            bool isNullableType,
            string name,
            string? defaultValue,
            bool useCommaForEndChar)
        {
            if (attributes is not null &&
                attributes.Count > 0)
            {
                switch (attributes.Count)
                {
                    case 1:
                        sb.AppendAttribute(indentSpaces, usePropertyPrefix, attributes[0]);
                        indentSpaces = 1;
                        break;
                    case > 1:
                        sb.AppendAttributes(indentSpaces, usePropertyPrefix, mergeIntoOne: true, attributes);
                        indentSpaces = 1;
                        break;
                }
            }

            sb.AppendTypeAndName(indentSpaces, genericTypeName, typeName, isNullableType, name, defaultValue);

            if (useCommaForEndChar)
            {
                sb.AppendLine(",");
            }
            else
            {
                sb.Append(')');
            }
        }

        public void AppendContent(string content)
            => sb.AppendContent(0, content);

        public void AppendContent(
            int indentSpaces,
            string content)
        {
            var lines = content
                .EnsureEnvironmentNewLines()
                .Split([Environment.NewLine], StringSplitOptions.None);

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

        public void AppendContentAsExpressionBody(
            int indentSpaces,
            string content)
        {
            var lines = content
                .EnsureEnvironmentNewLines()
                .Split([Environment.NewLine], StringSplitOptions.None);

            if (lines.Length == 1)
            {
                sb.Append(indentSpaces, $"=> {lines[0]};");
            }
            else
            {
                var linesLength = lines.Length;
                if (string.IsNullOrEmpty(lines[lines.Length - 1]))
                {
                    linesLength--;
                }

                for (var i = 0; i < linesLength; i++)
                {
                    var line = lines[i];
                    if (i == 0)
                    {
                        sb.AppendLine(indentSpaces, $"=> {line}");
                    }
                    else if (i == linesLength - 1)
                    {
                        sb.Append(indentSpaces, $"{line};");
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