namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// How a field of a multipart form is sent (<see cref="FormBodyHelper.PartOf"/>), swept over the media types an
/// <c>encoding</c> can name and the shapes a field can have: JSON first, then XML for a model an XML body maps, then
/// URL-encoded for a flat model; any other media type leaves an object field without an encoding (ATC_API_OPR037).
/// </summary>
public sealed class FormBodyHelperPartTests
{
    private const string Yaml = """
        openapi: 3.1.0
        info:
          title: Parts
          version: 1.0.0
        paths:
          /upload:
            post:
              operationId: upload
              requestBody:
                content:
                  multipart/form-data:
                    schema:
                      $ref: '#/components/schemas/Form'
              responses:
                '204':
                  description: Done
        components:
          schemas:
            Flat:
              type: object
              required: [name]
              properties:
                name:
                  type: string
                count:
                  type: integer
            Nested:
              type: object
              properties:
                flat:
                  $ref: '#/components/schemas/Flat'
            Either:
              oneOf:
                - $ref: '#/components/schemas/Flat'
                - $ref: '#/components/schemas/Nested'
            Form:
              type: object
              properties:
                flat:
                  $ref: '#/components/schemas/Flat'
                flats:
                  type: array
                  items:
                    $ref: '#/components/schemas/Flat'
                nested:
                  $ref: '#/components/schemas/Nested'
                map:
                  type: object
                  additionalProperties:
                    type: string
                either:
                  $ref: '#/components/schemas/Either'
                note:
                  type: string
        """;

    public static TheoryData<string, string?, MultipartPartEncoding, string?> Cases()
        => new()
        {
            // No encoding, or one without a media type: JSON, as OpenAPI encodes an object
            { "flat", null, MultipartPartEncoding.Json, "application/json" },
            { "flat", " ", MultipartPartEncoding.Json, "application/json" },
            { "map", null, MultipartPartEncoding.Json, "application/json" },
            { "either", null, MultipartPartEncoding.Json, "application/json" },

            // JSON, in any spelling, and first of several
            { "flat", "application/json", MultipartPartEncoding.Json, "application/json" },
            { "flat", "application/problem+json", MultipartPartEncoding.Json, "application/json" },
            { "flat", "application/xml, application/json", MultipartPartEncoding.Json, "application/json" },
            { "flat", "application/x-www-form-urlencoded, application/json; charset=utf-8", MultipartPartEncoding.Json, "application/json" },

            // XML for a model an XML body maps - alone, in a list (a part per item), nested; with parameters
            { "flat", "application/xml", MultipartPartEncoding.Xml, "application/xml" },
            { "flat", "text/xml", MultipartPartEncoding.Xml, "text/xml" },
            { "flat", "application/atom+xml", MultipartPartEncoding.Xml, "application/atom+xml" },
            { "flat", "Application/XML; charset=utf-8", MultipartPartEncoding.Xml, "Application/XML" },
            { "flats", "application/xml", MultipartPartEncoding.Xml, "application/xml" },
            { "nested", "application/xml", MultipartPartEncoding.Xml, "application/xml" },
            { "flat", "application/x-www-form-urlencoded, application/xml", MultipartPartEncoding.Xml, "application/xml" },

            // XML for what no XML body maps: a map, a composition
            { "map", "application/xml", MultipartPartEncoding.None, null },
            { "either", "application/xml", MultipartPartEncoding.None, null },

            // URL-encoded for a flat model only
            { "flat", "application/x-www-form-urlencoded", MultipartPartEncoding.UrlEncoded, "application/x-www-form-urlencoded" },
            { "flats", "application/x-www-form-urlencoded", MultipartPartEncoding.UrlEncoded, "application/x-www-form-urlencoded" },
            { "nested", "application/x-www-form-urlencoded", MultipartPartEncoding.None, null },
            { "map", "application/x-www-form-urlencoded", MultipartPartEncoding.None, null },
            { "either", "application/x-www-form-urlencoded", MultipartPartEncoding.None, null },

            // A media type that carries no object
            { "flat", "text/plain", MultipartPartEncoding.None, null },
            { "flat", "application/octet-stream", MultipartPartEncoding.None, null },
            { "flat", "image/png, text/plain", MultipartPartEncoding.None, null },

            // A value with a text form is a text part, whatever its encoding says
            { "note", null, MultipartPartEncoding.Text, null },
            { "note", "application/json", MultipartPartEncoding.Text, null },
            { "note", "text/plain", MultipartPartEncoding.Text, null },
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void PartOf_ReturnsTheEncodingTheFieldIsSentWith(
        string fieldName,
        string? contentType,
        MultipartPartEncoding expectedEncoding,
        string? expectedMediaType)
    {
        var form = OpenApiDocumentHelper.ParseYaml(Yaml).Components!.Schemas!["Form"];
        var field = form.Properties![fieldName];
        var multipart = new OpenApiMediaType
        {
            Encoding = contentType is null
                ? null
                : new Dictionary<string, OpenApiEncoding>(StringComparer.Ordinal) { [fieldName] = new OpenApiEncoding { ContentType = contentType } },
        };

        var part = FormBodyHelper.PartOf(field, fieldName, multipart);

        Assert.Equal(expectedEncoding, part.Encoding);
        Assert.Equal(expectedMediaType, part.MediaType);

        // An object part is no OPR037; a field none of the media types carries is
        var noEncoding = FormBodyHelper.HasNoFormEncoding("multipart/form-data", multipart, fieldName, field);
        Assert.Equal(expectedEncoding == MultipartPartEncoding.None, noEncoding);
    }

    [Fact]
    public void UrlEncodedPropertiesOf_AreTheFlatModelsProperties()
    {
        var form = OpenApiDocumentHelper.ParseYaml(Yaml).Components!.Schemas!["Form"];

        var properties = FormBodyHelper.UrlEncodedPropertiesOf(form.Properties!["flats"], "flats");

        Assert.NotNull(properties);
        Assert.Equal(["name", "count"], properties.Select(p => p.WireName), StringComparer.Ordinal);
        Assert.Equal([true, false], properties.Select(p => p.Required));
        Assert.Null(FormBodyHelper.UrlEncodedPropertiesOf(form.Properties["nested"], "nested"));
    }
}