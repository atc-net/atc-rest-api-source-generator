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
    /// Appends the statements that add every field of the multipart form model read through
    /// <paramref name="requestAccess"/>: a file through <see cref="MultipartPartWriter.File"/>, a list of files one
    /// by one, a list of scalars as one text part per item, a scalar as its wire text.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="schema">The form model's schema (a component).</param>
    /// <param name="requestAccess">The expression that reads the model, e.g. <c>parameters.Request</c>.</param>
    /// <param name="writer">How a part is added.</param>
    public static void AppendMultipartFields(
        StringBuilder builder,
        IOpenApiSchema schema,
        string requestAccess,
        MultipartPartWriter writer)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (schema is null)
        {
            throw new ArgumentNullException(nameof(schema));
        }

        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        foreach (var property in schema.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            var propertyName = property.Key.ToPascalCaseForDotNet();
            var access = $"{requestAccess}.{propertyName}";
            var (isFile, isFileList) = property.Value.GetFileUploadInfo();
            var isList = property.Value.IsOfType(JsonSchemaType.Array);
            var isEnum = WireValueHelper.IsStringEnum(property.Value);

            if (isFile && isFileList)
            {
                builder.AppendLine($"if ({requestAccess}?.{propertyName} is not null)");
                builder.AppendLine("{");
                builder.AppendLine(4, $"foreach (var fileItem in {access})");
                builder.AppendLine(4, "{");
                writer.File(builder, 8, property.Key, "fileItem");
                builder.AppendLine(4, "}");
                builder.AppendLine("}");
            }
            else if (isFile)
            {
                builder.AppendLine($"if ({requestAccess}?.{propertyName} is not null)");
                builder.AppendLine("{");
                writer.File(builder, 4, property.Key, access);
                builder.AppendLine("}");
            }
            else if (isList)
            {
                var itemType = property.Value.Items?.ToCSharpType(isRequired: true, registry: null).TrimEnd('?') ?? "string";
                builder.AppendLine($"if ({requestAccess}?.{propertyName} is not null)");
                builder.AppendLine("{");
                builder.AppendLine(4, $"foreach (var item in {access})");
                builder.AppendLine(4, "{");
                writer.Text(builder, 8, property.Key, WireText("item", itemType, isEnum));
                builder.AppendLine(4, "}");
                builder.AppendLine("}");
            }
            else
            {
                var variable = char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
                var valueType = property.Value.ToCSharpType(isRequired: true, registry: null).TrimEnd('?');
                builder.AppendLine($"if ({requestAccess}?.{propertyName} is {{ }} {variable})");
                builder.AppendLine("{");
                writer.Text(builder, 4, property.Key, WireText(variable, valueType, isEnum));
                builder.AppendLine("}");
            }

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

    /// <summary>
    /// The model schema of a form body: the component a reference names. An inline model has no generated type (the
    /// request is an <c>object</c>; validation reports ATC_API_OPR017), so it has no fields to send either.
    /// </summary>
    /// <param name="schema">The form body's schema.</param>
    /// <param name="openApiDoc">The document the components are in.</param>
    /// <returns>The component schema, or <see langword="null"/> for an inline schema or a reference the document does
    /// not have.</returns>
    public static IOpenApiSchema? ResolveModelSchema(
        IOpenApiSchema? schema,
        OpenApiDocument openApiDoc)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (schema is not OpenApiSchemaReference schemaRef)
        {
            return null;
        }

        var schemaId = schemaRef.Reference?.Id;
        return !string.IsNullOrEmpty(schemaId) &&
               openApiDoc.Components?.Schemas is not null &&
               openApiDoc.Components.Schemas.TryGetValue(schemaId!, out var component)
            ? component
            : null;
    }

    /// <summary>
    /// The multipart/form-data media type of a request body as the document declares it: with a parameter (e.g.
    /// <c>; charset=utf-8</c>) and in any case.
    /// </summary>
    /// <param name="content">The request body's content.</param>
    /// <returns>The declared key, or <see langword="null"/> when the body has no multipart/form-data.</returns>
    public static string? FindMultipartFormDataMediaType(
        IDictionary<string, IOpenApiMediaType>? content)
        => content?.Keys.FirstOrDefault(key => key.Split(';')[0].Trim().Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase));
}