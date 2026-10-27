namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class FormBodyHelperTests
{
    private const string FormYaml = """
        openapi: 3.0.0
        info: { title: T, version: 1.0.0 }
        paths: {}
        components:
          schemas:
            Color:
              type: string
              enum: [red, dark-blue]
            Form:
              type: object
              required: [name]
              properties:
                name: { type: string }
                count: { type: integer, format: int32 }
                ratio: { type: number, format: double }
                flag: { type: boolean }
                when: { type: string, format: date-time }
                color: { $ref: '#/components/schemas/Color' }
                tags: { type: array, items: { type: string } }
                file: { type: string, format: binary }
        """;

    [Fact]
    public void AppendUrlEncodedFields_WritesEveryScalarAsItsWireText()
    {
        // The text a form field carries is the wire text (a number and a date in the invariant culture, an enum by its
        // wire name, a boolean lowercase), never ToString(): a Danish machine writes 1,5 and 04-03-2026.
        var document = OpenApiDocumentHelper.ParseYaml(FormYaml);
        var schema = document.Components!.Schemas!["Form"];
        var sb = new StringBuilder();

        FormBodyHelper.AppendUrlEncodedFields(sb, schema, "parameters.Request", "fields");
        var code = sb.ToString();

        Assert.Contains("if (parameters.Request?.Name is { } name)", code, StringComparison.Ordinal);
        Assert.Contains("fields.Add(new KeyValuePair<string, string>(\"name\", name));", code, StringComparison.Ordinal);
        Assert.Contains("fields.Add(new KeyValuePair<string, string>(\"count\", ((global::System.IFormattable?)count)?.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture)!));", code, StringComparison.Ordinal);
        Assert.Contains("\"ratio\", ((global::System.IFormattable?)ratio)?.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture)!", code, StringComparison.Ordinal);
        Assert.Contains("\"flag\", (flag ? \"true\" : \"false\")", code, StringComparison.Ordinal);
        Assert.Contains("\"when\", ((global::System.IFormattable?)when)?.ToString(\"yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK\", global::System.Globalization.CultureInfo.InvariantCulture)!", code, StringComparison.Ordinal);
        Assert.Contains("\"color\", color.ToWireValue()", code, StringComparison.Ordinal);
        Assert.Contains("foreach (var item in tags)", code, StringComparison.Ordinal);
        Assert.Contains("fields.Add(new KeyValuePair<string, string>(\"tags\", item));", code, StringComparison.Ordinal);

        // A file has no URL-encoded form; it is left out (ATC_API_OPR031 reports it)
        Assert.DoesNotContain("\"file\"", code, StringComparison.Ordinal);
    }

    [Fact]
    public void AppendMultipartFields_WritesFilesFileListsScalarListsAndScalarsThroughTheWriter()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Color:
                  type: string
                  enum: [red, dark-blue]
                Upload:
                  type: object
                  properties:
                    file: { type: string, format: binary }
                    attachments: { type: array, items: { type: string, format: binary } }
                    tags: { type: array, items: { type: string } }
                    count: { type: integer, format: int32 }
                    color: { $ref: '#/components/schemas/Color' }
                    address: { $ref: '#/components/schemas/Address' }
                    addresses: { type: array, items: { $ref: '#/components/schemas/Address' } }
                Address:
                  type: object
                  properties:
                    street: { type: string }
            """);
        var schema = document.Components!.Schemas!["Upload"];
        var sb = new StringBuilder();
        var writer = new MultipartPartWriter(
            File: (builder, indent, name, expression) => builder.AppendLine(new string(' ', indent) + "content.Add(" + expression + ", \"" + name + "\");"),
            Text: (builder, indent, name, expression) => builder.AppendLine(new string(' ', indent) + "content.Add(new StringContent(" + expression + "), \"" + name + "\");"),
            Json: (builder, indent, name, expression) => builder.AppendLine(new string(' ', indent) + "content.Add(Json(" + expression + "), \"" + name + "\");"),
            Part: (builder, indent, name, expression, mediaType) => builder.AppendLine(new string(' ', indent) + "content.Add(Part(" + expression + ", \"" + mediaType + "\"), \"" + name + "\");"));

        FormBodyHelper.AppendMultipartFields(sb, schema, multipart: null, "parameters.Request", writer);
        var code = sb.ToString();

        // A file: one part
        Assert.Contains(
            "if (parameters.Request?.File is not null)" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    content.Add(parameters.Request.File, \"file\");" + Environment.NewLine +
            "}" + Environment.NewLine,
            code,
            StringComparison.Ordinal);

        // A list of files: one part per file
        Assert.Contains(
            "if (parameters.Request?.Attachments is not null)" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    foreach (var fileItem in parameters.Request.Attachments)" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        content.Add(fileItem, \"attachments\");" + Environment.NewLine +
            "    }" + Environment.NewLine +
            "}" + Environment.NewLine,
            code,
            StringComparison.Ordinal);

        // A list of scalars: one text part per item
        Assert.Contains(
            "if (parameters.Request?.Tags is not null)" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    foreach (var item in parameters.Request.Tags)" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        content.Add(new StringContent(item), \"tags\");" + Environment.NewLine +
            "    }" + Environment.NewLine +
            "}" + Environment.NewLine,
            code,
            StringComparison.Ordinal);

        // A scalar: its wire text; an enum: its wire name
        Assert.Contains(
            "if (parameters.Request?.Count is { } count)" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    content.Add(new StringContent(((global::System.IFormattable?)count)?.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture)!), \"count\");" + Environment.NewLine +
            "}" + Environment.NewLine,
            code,
            StringComparison.Ordinal);
        Assert.Contains("content.Add(new StringContent(color.ToWireValue()), \"color\");", code, StringComparison.Ordinal);

        // An object: an application/json part; a list of objects: one per item
        Assert.Contains(
            "if (parameters.Request?.Address is { } address)" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    content.Add(Json(address), \"address\");" + Environment.NewLine +
            "}" + Environment.NewLine,
            code,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (parameters.Request?.Addresses is not null)" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    foreach (var item in parameters.Request.Addresses)" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        content.Add(Json(item), \"addresses\");" + Environment.NewLine +
            "    }" + Environment.NewLine +
            "}" + Environment.NewLine,
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AppendMultipartFields_NullWriter_Throws()
    {
        var document = OpenApiDocumentHelper.ParseYaml(FormYaml);
        var schema = document.Components!.Schemas!["Form"];

        Assert.Throws<ArgumentNullException>(() => FormBodyHelper.AppendMultipartFields(new StringBuilder(), schema, multipart: null, "parameters.Request", null!));
    }

    [Theory]
    [InlineData("value", "string", false, "value")]
    [InlineData("flag", "bool", false, "(flag ? \"true\" : \"false\")")]
    [InlineData("count", "int", false, "((global::System.IFormattable?)count)?.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture)!")]
    [InlineData("id", "Guid", false, "global::System.FormattableString.Invariant($\"{id}\")")]
    [InlineData("ratio", "double", false, "((global::System.IFormattable?)ratio)?.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture)!")]
    [InlineData("price", "decimal", false, "((global::System.IFormattable?)price)?.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture)!")]
    [InlineData("when", "DateTimeOffset", false, "((global::System.IFormattable?)when)?.ToString(\"yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK\", global::System.Globalization.CultureInfo.InvariantCulture)!")]
    [InlineData("day", "DateOnly", false, "((global::System.IFormattable?)day)?.ToString(\"yyyy-MM-dd\", global::System.Globalization.CultureInfo.InvariantCulture)!")]
    [InlineData("color", "Color", true, "color.ToWireValue()")]
    public void WireText_ReturnsExpectedExpression(
        string valueExpression,
        string csharpType,
        bool isEnum,
        string expected)
    {
        Assert.Equal(expected, FormBodyHelper.WireText(valueExpression, csharpType, isEnum));
    }
}