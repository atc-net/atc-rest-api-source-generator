// ReSharper disable ForCanBeConvertedToForeach
namespace Atc.CodeGeneration.CSharp.Content.Generators;

public class GenerateContentForRecords : IContentGenerator
{
    private readonly ICodeDocumentationTagsGenerator codeDocumentationTagsGenerator;
    private readonly RecordsParameters parameters;

    public GenerateContentForRecords(
        ICodeDocumentationTagsGenerator codeDocumentationTagsGenerator,
        RecordsParameters parameters)
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

        for (var i = 0; i < parameters.Parameters.Count; i++)
        {
            var recordParameters = parameters.Parameters[i];

            if (recordParameters.DocumentationTags is not null)
            {
                sb.Append(codeDocumentationTagsGenerator.GenerateTags(0, recordParameters.DocumentationTags));
            }

            if (recordParameters.Attributes is not null)
            {
                foreach (var attribute in recordParameters.Attributes)
                {
                    sb.AppendAttribute(usePropertyPrefix: false, attribute);
                    sb.AppendLine();
                }
            }

            if (recordParameters.Parameters is not null &&
                recordParameters.Parameters.Any(x => x.IsGenericListType &&
                                                     x.TypeName.Equals("T", StringComparison.Ordinal)))
            {
                sb.Append($"{EnumDescriptionHelper.GetDescription(recordParameters.DeclarationModifier)} {recordParameters.Name}<T>");
            }
            else
            {
                sb.Append($"{EnumDescriptionHelper.GetDescription(recordParameters.DeclarationModifier)} {recordParameters.Name}");
            }

            if (recordParameters.Parameters is null ||
                !recordParameters.Parameters.Any())
            {
                sb.Append("();");
            }
            else
            {
                sb.AppendLine("(");
                const int indentSpaces = 4;

                for (var j = 0; j < recordParameters.Parameters.Count; j++)
                {
                    var item = recordParameters.Parameters[j];
                    var useCommaForEndChar = j != recordParameters.Parameters.Count - 1;
                    sb.AppendInputParameter(
                        indentSpaces,
                        usePropertyPrefix: true,
                        item.Attributes,
                        item.GenericTypeName,
                        item.TypeName,
                        item.IsNullableType,
                        item.Name,
                        item.DefaultValue,
                        useCommaForEndChar);
                }
            }

            if (recordParameters.Parameters is not null &&
                recordParameters.Parameters.Any())
            {
                // BaseConstructorArguments distinguishes three cases. Null means the base takes no
                // constructor arguments - the abstract base of a oneOf/anyOf schema, say - so the
                // clause is written without parentheses. A non-empty list is forwarded as the base
                // constructor call. An empty list means the caller could not resolve the base's
                // arguments, and emitting a parameterless call would not compile against a
                // positional record, so the clause is dropped as it always has been.
                if (!string.IsNullOrEmpty(recordParameters.BaseTypeName) &&
                    recordParameters.BaseConstructorArguments is not { Count: 0 })
                {
                    sb.Append(" : ");
                    sb.Append(recordParameters.BaseTypeName);

                    if (recordParameters.BaseConstructorArguments is { Count: > 0 })
                    {
                        sb.Append('(');
                        sb.Append(string.Join(", ", recordParameters.BaseConstructorArguments));
                        sb.Append(')');
                    }
                }

                sb.Append(';');
            }

            if (i == parameters.Parameters.Count - 1)
            {
                continue;
            }

            sb.AppendLine();
            sb.AppendLine();
        }

        return sb.ToString();
    }
}