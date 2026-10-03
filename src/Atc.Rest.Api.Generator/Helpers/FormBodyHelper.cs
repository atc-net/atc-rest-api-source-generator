namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// The fields of a form request body (<c>application/x-www-form-urlencoded</c>, <c>multipart/form-data</c>) as the
/// generated C# clients write them: a scalar as its wire text (a number and a date in the invariant culture, an enum
/// by its wire name, a boolean lowercase - never <c>ToString()</c>, which a Danish machine renders as <c>1,5</c>),
/// a list as one field per item. Shared by the typed and the per-operation client, so both send what the server
/// binds.
/// </summary>
public static class FormBodyHelper
{
    /// <summary>
    /// Appends the statements that add every non-file field of the form model read through
    /// <paramref name="requestAccess"/> to <paramref name="fieldsVariable"/>, a
    /// <c>List&lt;KeyValuePair&lt;string, string&gt;&gt;</c> (what <c>FormUrlEncodedContent</c> takes). A file has no
    /// URL-encoded form and is left out; ATC_API_OPR031 reports it.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="schema">The form model's schema (a component).</param>
    /// <param name="requestAccess">The expression that reads the model, e.g. <c>parameters.Request</c>.</param>
    /// <param name="fieldsVariable">The name of the list the fields are added to.</param>
    public static void AppendUrlEncodedFields(
        StringBuilder builder,
        IOpenApiSchema schema,
        string requestAccess,
        string fieldsVariable)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (schema is null)
        {
            throw new ArgumentNullException(nameof(schema));
        }

        foreach (var property in schema.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            var (isFile, _) = property.Value.GetFileUploadInfo();
            if (isFile)
            {
                continue;
            }

            var propertyName = property.Key.ToPascalCaseForDotNet();
            var variable = char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
            var csharpType = property.Value.ToCSharpType(isRequired: true, registry: null).TrimEnd('?');
            var isEnum = WireValueHelper.IsStringEnum(property.Value);

            builder.AppendLine($"if ({requestAccess}?.{propertyName} is {{ }} {variable})");
            builder.AppendLine("{");
            if (csharpType.StartsWith("List<", StringComparison.Ordinal))
            {
                var itemType = csharpType.Substring("List<".Length, csharpType.Length - "List<".Length - 1);
                builder.AppendLine(4, $"foreach (var item in {variable})");
                builder.AppendLine(4, "{");
                builder.AppendLine(8, $"{fieldsVariable}.Add(new KeyValuePair<string, string>(\"{property.Key}\", {WireText("item", itemType, isEnum)}));");
                builder.AppendLine(4, "}");
            }
            else
            {
                builder.AppendLine(4, $"{fieldsVariable}.Add(new KeyValuePair<string, string>(\"{property.Key}\", {WireText(variable, csharpType, isEnum)}));");
            }

            builder.AppendLine("}");
            builder.AppendLine();
        }
    }

    /// <summary>
    /// A C# expression of type <c>string</c> giving the wire text of a non-null value of <paramref name="csharpType"/>.
    /// </summary>
    /// <param name="valueExpression">The expression that reads the value.</param>
    /// <param name="csharpType">The value's C# type, without <c>?</c>.</param>
    /// <param name="isEnum">Whether the value is a generated string enum (written with <c>ToWireValue()</c>).</param>
    /// <returns>The expression.</returns>
    public static string WireText(
        string valueExpression,
        string csharpType,
        bool isEnum)
    {
        if (isEnum)
        {
            return $"{valueExpression}.ToWireValue()";
        }

        return csharpType switch
        {
            "string" => valueExpression,
            "bool" => $"({valueExpression} ? \"true\" : \"false\")",
            _ => WireValueHelper.TryBuildWireExpression(valueExpression, csharpType) is { } wireValue
                ? wireValue + "!"
                : $"global::System.FormattableString.Invariant($\"{{{valueExpression}}}\")",
        };
    }
}