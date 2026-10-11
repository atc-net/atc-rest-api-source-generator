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
    /// URL-encoded form and is left out (ATC_API_OPR031 reports it), as is a field that is an object, a map or a
    /// composition, which has no text (ATC_API_OPR037).
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
            // A file has no URL-encoded form; an object, a map or a composition has no text (ATC_API_OPR037)
            var (isFile, _) = property.Value.GetFileUploadInfo();
            if (isFile || property.Value.HasNoTextForm())
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
    /// by one, a list of scalars as one text part per item, a scalar as its wire text, and an object, a map or a
    /// composition as an application/json part through <see cref="MultipartPartWriter.Json"/> (a list of one as a part
    /// per item, see <see cref="IsJsonPart"/>). A field whose encoding names another media type is left out
    /// (ATC_API_OPR037).
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="schema">The form model's schema (a component).</param>
    /// <param name="multipart">The multipart/form-data media type, for its <c>encoding</c>.</param>
    /// <param name="requestAccess">The expression that reads the model, e.g. <c>parameters.Request</c>.</param>
    /// <param name="writer">How a part is added.</param>
    public static void AppendMultipartFields(
        StringBuilder builder,
        IOpenApiSchema schema,
        IOpenApiMediaType? multipart,
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

            // An object, a map or a composition has no text: an application/json part (a list of one a part per
            // item), or - when its encoding names another media type - left out (ATC_API_OPR037)
            if (property.Value.HasNoTextForm())
            {
                if (IsJsonPart(property.Value, property.Key, multipart))
                {
                    AppendJsonParts(builder, property.Key, property.Value, requestAccess, propertyName, writer);
                }

                continue;
            }

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
    /// Whether a field of a multipart form is sent as an <c>application/json</c> part: a value with no text form (an
    /// object, a map or a composition, or a list of one - a part per item; see <c>HasNoTextForm</c>), which OpenAPI
    /// encodes as JSON unless the form's <c>encoding</c> gives the field another media type.
    /// </summary>
    /// <param name="field">The field's schema.</param>
    /// <param name="fieldName">The field's name, the key of its <c>encoding</c>.</param>
    /// <param name="multipart">The multipart/form-data media type, for its <c>encoding</c>.</param>
    /// <returns><see langword="true"/> for a JSON part.</returns>
    public static bool IsJsonPart(
        IOpenApiSchema field,
        string fieldName,
        IOpenApiMediaType? multipart)
    {
        if (field is null)
        {
            throw new ArgumentNullException(nameof(field));
        }

        if (!field.HasNoTextForm())
        {
            return false;
        }

        // The encoding may list several media types; any JSON one is what the generated code writes
        return multipart?.Encoding is not { } encoding ||
               !encoding.TryGetValue(fieldName, out var fieldEncoding) ||
               string.IsNullOrWhiteSpace(fieldEncoding.ContentType) ||
               fieldEncoding.ContentType!.Split(',').Any(ErrorResponseShapeResolver.IsJsonMediaType);
    }

    /// <summary>
    /// Whether a field of an operation's form body is sent as an application/json part: the operation declares a
    /// multipart/form-data body, and the field is a JSON part of it (see <see cref="IsJsonPart"/>). A server bound field
    /// by field reads such a field as JSON.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="fieldName">The field's name.</param>
    /// <param name="field">The field's schema.</param>
    /// <returns><see langword="true"/> for a JSON part.</returns>
    public static bool IsMultipartJsonPart(
        OpenApiOperation operation,
        string fieldName,
        IOpenApiSchema field)
        => FindMultipartFormData(operation) is { } multipart &&
           IsJsonPart(field, fieldName, multipart);

    /// <summary>
    /// The C# type of the value one JSON part carries: the field's type, or its item's for a list of one (a part per
    /// item).
    /// </summary>
    /// <param name="field">The field's schema.</param>
    /// <returns>The C# type, without <c>?</c>.</returns>
    public static string JsonPartValueType(IOpenApiSchema field)
    {
        if (field is null)
        {
            throw new ArgumentNullException(nameof(field));
        }

        // A map is the model's Dictionary<string, TValue>
        var value = field.IsOfType(JsonSchemaType.Array) && field.Items is { } items ? items : field;
        return (value.GetDictionaryTypeString(isRequired: true) ?? value.ToCSharpType(isRequired: true, registry: null)).TrimEnd('?');
    }

    /// <summary>
    /// Whether a field of the form body the generated code sends (see <c>GetFormBodyMediaType</c>) has no encoding
    /// there (ATC_API_OPR037): a value with no text form in a URL-encoded form, or in a multipart form whose
    /// <c>encoding</c> gives it a media type other than JSON (see <see cref="IsJsonPart"/>).
    /// </summary>
    /// <param name="formMediaType">The form's media type, as the document declares it.</param>
    /// <param name="form">The form's media type object, for its <c>encoding</c>.</param>
    /// <param name="fieldName">The field's name.</param>
    /// <param name="field">The field's schema.</param>
    /// <returns><see langword="true"/> when the field cannot be sent.</returns>
    public static bool HasNoFormEncoding(
        string formMediaType,
        IOpenApiMediaType? form,
        string fieldName,
        IOpenApiSchema field)
    {
        if (formMediaType is null)
        {
            throw new ArgumentNullException(nameof(formMediaType));
        }

        if (field is null)
        {
            throw new ArgumentNullException(nameof(field));
        }

        return field.HasNoTextForm() &&
               !(IsMultipartFormData(formMediaType) && IsJsonPart(field, fieldName, form));
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
        => content?.Keys.FirstOrDefault(IsMultipartFormData);

    /// <summary>
    /// The multipart/form-data media type object of an operation's request body (see
    /// <see cref="FindMultipartFormDataMediaType"/>), for its <c>encoding</c>.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The media type object, or <see langword="null"/> when the body has no multipart/form-data.</returns>
    public static IOpenApiMediaType? FindMultipartFormData(
        OpenApiOperation operation)
        => operation?.RequestBody?.Content is { } content &&
           FindMultipartFormDataMediaType(content) is { } mediaType
            ? content[mediaType]
            : null;

    private static bool IsMultipartFormData(string mediaType)
        => mediaType.Split(';')[0].Trim().Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase);

    // An application/json part of the value, or one per item of a list
    private static void AppendJsonParts(
        StringBuilder builder,
        string fieldName,
        IOpenApiSchema field,
        string requestAccess,
        string propertyName,
        MultipartPartWriter writer)
    {
        if (field.IsOfType(JsonSchemaType.Array))
        {
            builder.AppendLine($"if ({requestAccess}?.{propertyName} is not null)");
            builder.AppendLine("{");
            builder.AppendLine(4, $"foreach (var item in {requestAccess}.{propertyName})");
            builder.AppendLine(4, "{");
            writer.Json(builder, 8, fieldName, "item");
            builder.AppendLine(4, "}");
            builder.AppendLine("}");
        }
        else
        {
            var variable = char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
            builder.AppendLine($"if ({requestAccess}?.{propertyName} is {{ }} {variable})");
            builder.AppendLine("{");
            writer.Json(builder, 4, fieldName, variable);
            builder.AppendLine("}");
        }

        builder.AppendLine();
    }
}