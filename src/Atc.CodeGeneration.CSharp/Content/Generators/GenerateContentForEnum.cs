// ReSharper disable MergeIntoPattern
namespace Atc.CodeGeneration.CSharp.Content.Generators;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "OK.")]
public class GenerateContentForEnum : IContentGenerator
{
    private readonly ICodeDocumentationTagsGenerator codeDocumentationTagsGenerator;
    private readonly EnumParameters parameters;

    public GenerateContentForEnum(
        ICodeDocumentationTagsGenerator codeDocumentationTagsGenerator,
        EnumParameters parameters)
    {
        this.codeDocumentationTagsGenerator = codeDocumentationTagsGenerator;
        this.parameters = parameters;
    }

    public string Generate()
    {
        var contentWriter = new GenerateContentWriter(codeDocumentationTagsGenerator);

        var useJsonStringEnumConverter = UseJsonStringEnumConverter(parameters.Values);

        var sb = new StringBuilder();
        contentWriter.AppendTopOfType(
            sb,
            parameters.HeaderContent,
            parameters.Namespace,
            parameters.DocumentationTags,
            GetAttributeParametersList());

        if (useJsonStringEnumConverter)
        {
            sb.AppendLine("[JsonConverter(typeof(JsonStringEnumConverter))]");
        }

        sb.Append($"{EnumDescriptionHelper.GetDescription(parameters.DeclarationModifier)} enum ");

        // A value outside the range of an int needs a 64-bit enum - an int enum would silently hold another number
        var needsLong = parameters.Values.Any(v => v.Value is < int.MinValue or > int.MaxValue);
        sb.AppendLine(needsLong ? $"{parameters.EnumTypeName} : long" : $"{parameters.EnumTypeName}");

        sb.AppendLine("{");
        foreach (var parametersValue in parameters.Values)
        {
            if (parametersValue.DescriptionAttribute is not null)
            {
                sb.AppendAttribute(4, usePropertyPrefix: false, parametersValue.DescriptionAttribute);
                sb.AppendLine();
            }

            var sbLine = new StringBuilder();

            // Add EnumMember attribute if EnumMemberValue is specified
            if (!string.IsNullOrEmpty(parametersValue.EnumMemberValue))
            {
                // [EnumMember] names the wire value for whoever reads it by reflection; System.Text.Json does not
                // read it and takes the wire value from [JsonStringEnumMemberName]
                var wireValue = CSharpLiteralHelper.ToStringLiteral(parametersValue.EnumMemberValue!);
                sbLine.AppendLine($"[EnumMember(Value = {wireValue})]");
                sbLine.Append(4, $"[JsonStringEnumMemberName({wireValue})]");
                sbLine.AppendLine();
                sbLine.Append(4, parametersValue.Name);
            }
            else
            {
                sbLine.Append(parametersValue.Name);
            }

            if (parametersValue.Value is not null)
            {
                sbLine.Append($" = {parametersValue.Value}");
            }

            sbLine.Append(',');
            sb.AppendLine(4, sbLine.ToString());

            if (useJsonStringEnumConverter &&
                parameters.Values.Last() != parametersValue)
            {
                sb.AppendLine();
            }
        }

        sb.Append('}');

        return sb.ToString();
    }

    private IList<AttributeParameters>? GetAttributeParametersList()
    {
        var attributeParameters = parameters.Attributes?.ToList();
        if (parameters.UseFlags &&
            attributeParameters is not null &&
            attributeParameters.Find(x => x.Name == "Flags") is null)
        {
            attributeParameters.Add(
                new AttributeParameters(
                    Name: "Flags",
                    Content: null));
        }

        return attributeParameters;
    }

    private static bool UseJsonStringEnumConverter(
        IList<EnumValueParameters> parametersValues)
        => parametersValues.Any(x => !string.IsNullOrEmpty(x.EnumMemberValue));
}