// ReSharper disable MergeIntoPattern
// ReSharper disable ConvertIfStatementToSwitchStatement
namespace Atc.CodeGeneration.CSharp.Content.Generators;

public class GenerateContentForClass : IContentGenerator
{
    private readonly ICodeDocumentationTagsGenerator codeDocumentationTagsGenerator;
    private readonly ClassParameters parameters;

    public GenerateContentForClass(
        ICodeDocumentationTagsGenerator codeDocumentationTagsGenerator,
        ClassParameters parameters)
    {
        this.codeDocumentationTagsGenerator = codeDocumentationTagsGenerator;
        this.parameters = parameters;
    }

    public string Generate()
    {
        var contentWriter = new GenerateContentWriter(codeDocumentationTagsGenerator);

        var sb = new StringBuilder();
        contentWriter.AppendTopOfType(
            sb,
            parameters.HeaderContent,
            parameters.Namespace,
            parameters.DocumentationTags,
            parameters.Attributes);

        sb.Append($"{EnumDescriptionHelper.GetDescription(parameters.DeclarationModifier)} {parameters.ClassTypeName}");
        if (!string.IsNullOrEmpty(parameters.GenericTypeName))
        {
            sb.Append($"<{parameters.GenericTypeName}>");
        }

        if (!string.IsNullOrEmpty(parameters.InheritedClassTypeName) ||
            !string.IsNullOrEmpty(parameters.InheritedInterfaceTypeName))
        {
            sb.Append(" : ");

            if (!string.IsNullOrEmpty(parameters.InheritedClassTypeName) &&
                !string.IsNullOrEmpty(parameters.InheritedInterfaceTypeName))
            {
                sb.Append($"{parameters.InheritedClassTypeName}, {parameters.InheritedInterfaceTypeName}");
            }
            else if (!string.IsNullOrEmpty(parameters.InheritedClassTypeName))
            {
                sb.Append(parameters.InheritedClassTypeName);
            }
            else if (!string.IsNullOrEmpty(parameters.InheritedInterfaceTypeName))
            {
                sb.Append(parameters.InheritedInterfaceTypeName);
            }
        }

        sb.AppendLine();
        sb.AppendLine("{");

        var isFirstEntry = true;
        if (parameters.Constants is not null)
        {
            foreach (var constant in parameters.Constants)
            {
                var formattedValue = IsStringType(constant.TypeName)
                    ? $"\"{constant.Value}\""
                    : constant.Value;
                sb.AppendLine($"    {constant.AccessModifier} const {constant.TypeName} {constant.Name} = {formattedValue};");
                isFirstEntry = false;
            }
        }

        if (parameters.AdditionalFieldDeclarations is not null)
        {
            if (!isFirstEntry)
            {
                sb.AppendLine();
            }

            foreach (var declaration in parameters.AdditionalFieldDeclarations)
            {
                sb.AppendLine($"    {declaration}");
            }

            isFirstEntry = false;
        }

        if (parameters.Constructors is not null)
        {
            var lengthBefore = sb.Length;
            contentWriter.AppendPrivateReadonlyMembersToConstructor(sb, parameters.Constructors);
            if (sb.Length > lengthBefore)
            {
                if (!isFirstEntry)
                {
                    // Readonly members were appended — insert blank line before them
                    sb.Insert(lengthBefore, Environment.NewLine);
                }

                isFirstEntry = false;
            }

            foreach (var constructorParameters in parameters.Constructors)
            {
                if (!isFirstEntry)
                {
                    sb.AppendLine();
                }

                contentWriter.AppendConstructor(sb, constructorParameters);
                sb.AppendLine();

                isFirstEntry = false;
            }
        }

        if (parameters.Properties is not null)
        {
            foreach (var propertyParameters in parameters.Properties)
            {
                if (!isFirstEntry)
                {
                    sb.AppendLine();
                }

                contentWriter.AppendProperty(sb, propertyParameters);
                sb.AppendLine();

                isFirstEntry = false;
            }
        }

        if (parameters.Methods is not null)
        {
            foreach (var methodParameters in parameters.Methods)
            {
                if (!isFirstEntry)
                {
                    sb.AppendLine();
                }

                contentWriter.AppendMethod(sb, methodParameters);
                sb.AppendLine();

                isFirstEntry = false;
            }
        }

        if (parameters.GenerateToStringMethod &&
            parameters.Properties is not null)
        {
            if (!isFirstEntry)
            {
                sb.AppendLine();
            }

            contentWriter.AppendMethodToString(sb, parameters.Properties);
            sb.AppendLine();
        }

        sb.Append('}');

        return sb.ToString();
    }

    private static bool IsStringType(string typeName)
        => typeName.Equals("string", StringComparison.OrdinalIgnoreCase);
}