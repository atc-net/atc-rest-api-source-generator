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
        Assert.Contains("fields.Add(new KeyValuePair<string, string>(\"count\", global::System.FormattableString.Invariant($\"{count}\")));", code, StringComparison.Ordinal);
        Assert.Contains("\"ratio\", ((global::System.IFormattable?)ratio)?.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture)!", code, StringComparison.Ordinal);
        Assert.Contains("\"flag\", (flag ? \"true\" : \"false\")", code, StringComparison.Ordinal);
        Assert.Contains("\"when\", ((global::System.IFormattable?)when)?.ToString(\"yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK\", global::System.Globalization.CultureInfo.InvariantCulture)!", code, StringComparison.Ordinal);
        Assert.Contains("\"color\", color.ToWireValue()", code, StringComparison.Ordinal);
        Assert.Contains("foreach (var item in tags)", code, StringComparison.Ordinal);
        Assert.Contains("fields.Add(new KeyValuePair<string, string>(\"tags\", item));", code, StringComparison.Ordinal);

        // A file has no URL-encoded form; it is left out (ATC_API_OPR031 reports it)
        Assert.DoesNotContain("\"file\"", code, StringComparison.Ordinal);
    }
}