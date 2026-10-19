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
    private const string UrlEncodedMediaType = "application/x-www-form-urlencoded";

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
    /// composition as an application/json part through <see cref="MultipartPartWriter.Json"/> - or, as its encoding
    /// names, a model as an XML part and a flat model as a URL-encoded part through <see cref="MultipartPartWriter.Part"/>
    /// (a list of one as a part per item, see <see cref="PartOf"/>). A field none of its media types carries is left out
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

            // An object, a map or a composition has no text: an application/json part, an XML or a URL-encoded part as
            // its encoding names (a list of one a part per item), or - for a media type that carries none - left out
            // (ATC_API_OPR037)
            if (property.Value.HasNoTextForm())
            {
                var part = PartOf(property.Value, property.Key, multipart);
                if (part.Encoding is MultipartPartEncoding.Json or MultipartPartEncoding.Xml or MultipartPartEncoding.UrlEncoded)
                {
                    AppendObjectParts(builder, property.Key, property.Value, requestAccess, propertyName, part, writer);
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
    /// How a field of a multipart form is sent. A value with a text form is a text (or file) part. A value with none
    /// (an object, a map or a composition, or a list of one - a part per item; see <c>HasNoTextForm</c>) is an
    /// <c>application/json</c> part, as OpenAPI encodes it, unless the form's <c>encoding</c> names other media types:
    /// of those, JSON is taken first, then XML for a model an XML body maps, then <c>application/x-www-form-urlencoded</c>
    /// for a flat model (each property one value, as an object in the query); a field none of them carries is
    /// <see cref="MultipartPartEncoding.None"/> (ATC_API_OPR037).
    /// </summary>
    /// <param name="field">The field's schema.</param>
    /// <param name="fieldName">The field's name, the key of its <c>encoding</c>.</param>
    /// <param name="multipart">The multipart/form-data media type, for its <c>encoding</c>.</param>
    /// <returns>The part's encoding and media type.</returns>
    public static MultipartPart PartOf(
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
            return new MultipartPart(MultipartPartEncoding.Text, MediaType: null);
        }

        // The encoding may list several media types (each with its parameters: application/xml; charset=utf-8)
        var mediaTypes = multipart?.Encoding is { } encoding &&
                         encoding.TryGetValue(fieldName, out var fieldEncoding) &&
                         !string.IsNullOrWhiteSpace(fieldEncoding.ContentType)
            ? fieldEncoding.ContentType!
                .Split(',')
                .Select(m => m.Split(';')[0].Trim())
                .Where(m => m.Length > 0)
                .ToList()
            : [];

        if (mediaTypes.Count == 0 || mediaTypes.Any(ErrorResponseShapeResolver.IsJsonMediaType))
        {
            return new MultipartPart(MultipartPartEncoding.Json, "application/json");
        }

        // A list of models is a part per item
        var value = field.IsOfType(JsonSchemaType.Array) && field.Items is { } items ? items : field;
        if (mediaTypes.FirstOrDefault(IsXmlMediaType) is { } xml &&
            XmlBodyHelper.IsMappedModel(value))
        {
            return new MultipartPart(MultipartPartEncoding.Xml, xml);
        }

        if (mediaTypes.Any(m => m.Equals(UrlEncodedMediaType, StringComparison.OrdinalIgnoreCase)) &&
            IsFlatModel(fieldName, value))
        {
            return new MultipartPart(MultipartPartEncoding.UrlEncoded, UrlEncodedMediaType);
        }

        return new MultipartPart(MultipartPartEncoding.None, MediaType: null);
    }

    /// <summary>
    /// Whether a field of a multipart form is sent as an <c>application/json</c> part (see <see cref="PartOf"/>).
    /// </summary>
    /// <param name="field">The field's schema.</param>
    /// <param name="fieldName">The field's name, the key of its <c>encoding</c>.</param>
    /// <param name="multipart">The multipart/form-data media type, for its <c>encoding</c>.</param>
    /// <returns><see langword="true"/> for a JSON part.</returns>
    public static bool IsJsonPart(
        IOpenApiSchema field,
        string fieldName,
        IOpenApiMediaType? multipart)
        => PartOf(field, fieldName, multipart).Encoding == MultipartPartEncoding.Json;

    /// <summary>
    /// Whether a field of a multipart form is an object part - JSON, XML or URL-encoded (see <see cref="PartOf"/>): a
    /// value the part's text is read into.
    /// </summary>
    /// <param name="field">The field's schema.</param>
    /// <param name="fieldName">The field's name, the key of its <c>encoding</c>.</param>
    /// <param name="multipart">The multipart/form-data media type, for its <c>encoding</c>.</param>
    /// <returns><see langword="true"/> for an object part.</returns>
    public static bool IsObjectPart(
        IOpenApiSchema field,
        string fieldName,
        IOpenApiMediaType? multipart)
        => PartOf(field, fieldName, multipart).Encoding is MultipartPartEncoding.Json or MultipartPartEncoding.Xml or MultipartPartEncoding.UrlEncoded;

    /// <summary>
    /// The properties of a field sent as an <c>application/x-www-form-urlencoded</c> part, each one value, as the
    /// part's text names them; null for any other field.
    /// </summary>
    /// <param name="field">The field's schema (a list of the model: its items).</param>
    /// <param name="fieldName">The field's name.</param>
    /// <returns>The properties, by wire name.</returns>
    public static IReadOnlyList<OpenApiParameterExtensions.QueryObjectProperty>? UrlEncodedPropertiesOf(
        IOpenApiSchema field,
        string fieldName)
    {
        if (field is null)
        {
            throw new ArgumentNullException(nameof(field));
        }

        var value = field.IsOfType(JsonSchemaType.Array) && field.Items is { } items ? items : field;
        return QueryObjectOf(fieldName, value)?.Properties;
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
    /// How a field of an operation's form body is sent when the operation declares a multipart/form-data body (see
    /// <see cref="PartOf"/>); null for a URL-encoded form.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="fieldName">The field's name.</param>
    /// <param name="field">The field's schema.</param>
    /// <returns>The part, or null.</returns>
    public static MultipartPart? MultipartPartOf(
        OpenApiOperation operation,
        string fieldName,
        IOpenApiSchema field)
        => FindMultipartFormData(operation) is { } multipart
            ? PartOf(field, fieldName, multipart)
            : null;

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
               !(IsMultipartFormData(formMediaType) && IsObjectPart(field, fieldName, form));
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

    // application/xml, text/xml and a structured syntax suffix (application/atom+xml)
    private static bool IsXmlMediaType(string mediaType)
        => mediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) ||
           mediaType.Equals("text/xml", StringComparison.OrdinalIgnoreCase) ||
           mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase);

    // A model whose properties are each one value: the object a query sends as form with explode, by the same rule
    private static bool IsFlatModel(
        string fieldName,
        IOpenApiSchema value)
        => QueryObjectOf(fieldName, value) is not null;

    private static OpenApiParameterExtensions.QueryObject? QueryObjectOf(
        string fieldName,
        IOpenApiSchema value)
        => new OpenApiParameter
        {
            Name = fieldName,
            In = ParameterLocation.Query,
            Style = ParameterStyle.Form,
            Explode = true,
            Schema = value,
        }.GetQueryObject();

    private static bool IsMultipartFormData(string mediaType)
        => mediaType.Split(';')[0].Trim().Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase);

    // An application/json part of the value, or one per item of a list
    private static void AppendObjectParts(
        StringBuilder builder,
        string fieldName,
        IOpenApiSchema field,
        string requestAccess,
        string propertyName,
        MultipartPart part,
        MultipartPartWriter writer)
    {
        if (field.IsOfType(JsonSchemaType.Array))
        {
            builder.AppendLine($"if ({requestAccess}?.{propertyName} is not null)");
            builder.AppendLine("{");
            builder.AppendLine(4, $"foreach (var item in {requestAccess}.{propertyName})");
            builder.AppendLine(4, "{");
            AppendObjectPart(builder, 8, fieldName, field, "item", part, writer);
            builder.AppendLine(4, "}");
            builder.AppendLine("}");
        }
        else
        {
            var variable = char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
            builder.AppendLine($"if ({requestAccess}?.{propertyName} is {{ }} {variable})");
            builder.AppendLine("{");
            AppendObjectPart(builder, 4, fieldName, field, variable, part, writer);
            builder.AppendLine("}");
        }

        builder.AppendLine();
    }

    // One part: the value as JSON, as XML (as an XML body of the model is written), or its fields URL-encoded
    private static void AppendObjectPart(
        StringBuilder builder,
        int indent,
        string fieldName,
        IOpenApiSchema field,
        string value,
        MultipartPart part,
        MultipartPartWriter writer)
    {
        switch (part.Encoding)
        {
            case MultipartPartEncoding.Xml:
                writer.Part(builder, indent, fieldName, $"{JsonPartValueType(field)}Xml.ToXml({value})", part.MediaType!);
                break;

            case MultipartPartEncoding.UrlEncoded:
            {
                var fields = value + "Fields";
                var model = field.IsOfType(JsonSchemaType.Array) && field.Items is { } items ? items : field;
                builder.AppendLine(indent, $"var {fields} = new List<KeyValuePair<string, string>>();");

                // The model's fields as a URL-encoded body of it has them, at this part's indent
                var fieldStatements = new StringBuilder();
                AppendUrlEncodedFields(fieldStatements, model, value, fields);
                foreach (var line in fieldStatements.ToString().TrimEnd().Split('\n'))
                {
                    var text = line.TrimEnd('\r');
                    builder.AppendLine(text.Length == 0 ? string.Empty : new string(' ', indent) + text);
                }

                writer.Part(builder, indent, fieldName, $"string.Join(\"&\", {fields}.Select(field => Uri.EscapeDataString(field.Key) + \"=\" + Uri.EscapeDataString(field.Value)))", part.MediaType!);
                break;
            }

            default:
                writer.Json(builder, indent, fieldName, value);
                break;
        }
    }
}