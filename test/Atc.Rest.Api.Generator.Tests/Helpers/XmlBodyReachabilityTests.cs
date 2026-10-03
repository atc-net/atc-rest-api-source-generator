namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class XmlBodyReachabilityTests
{
    // The XML request body carries Pet (and through it Tag and Owner); the XML response carries Invoice (and through
    // its oneOf and dictionary values Line and Note). Receipt is JSON only and must not get an XML mapping.
    private const string Yaml = """
        openapi: 3.0.0
        info: { title: T, version: 1.0.0 }
        paths:
          /pets:
            post:
              operationId: createPet
              requestBody:
                content:
                  application/xml:
                    schema: { $ref: '#/components/schemas/Pet' }
              responses:
                '201':
                  description: Created
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Receipt' }
          /invoices:
            get:
              operationId: listInvoices
              responses:
                '200':
                  description: OK
                  content:
                    text/xml:
                      schema:
                        type: array
                        items: { $ref: '#/components/schemas/Invoice' }
          /notes:
            post:
              operationId: createNote
              requestBody:
                content:
                  application/xml:
                    schema: { type: string }
              responses:
                '201': { description: Created }
        components:
          schemas:
            Pet:
              type: object
              properties:
                name: { type: string }
                tags:
                  type: array
                  items: { $ref: '#/components/schemas/Tag' }
                owner: { $ref: '#/components/schemas/Owner' }
            Tag:
              type: object
              properties:
                label: { type: string }
            Owner:
              type: object
              properties:
                name: { type: string }
            Receipt:
              type: object
              properties:
                id: { type: string }
            Invoice:
              type: object
              properties:
                lines:
                  type: object
                  additionalProperties: { $ref: '#/components/schemas/Line' }
                attachment:
                  oneOf:
                    - $ref: '#/components/schemas/Note'
                    - $ref: '#/components/schemas/Tag'
            Line:
              type: object
              properties:
                amount: { type: number }
            Note:
              type: object
              properties:
                text: { type: string }
        """;

    [Fact]
    public void Collect_WalksRequestAndResponseXmlBodies()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);

        var result = XmlBodyReachability.Collect(document);

        Assert.Equal(
            ["Invoice", "Line", "Note", "Owner", "Pet", "Tag"],
            result.OrderBy(s => s, StringComparer.Ordinal),
            StringComparer.Ordinal);
        Assert.DoesNotContain("Receipt", result);
    }

    [Fact]
    public void Collect_NoXmlBody_ReturnsEmpty()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema: { $ref: '#/components/schemas/Pet' }
            components:
              schemas:
                Pet:
                  type: object
                  properties:
                    name: { type: string }
            """);

        Assert.Empty(XmlBodyReachability.Collect(document));
    }

    [Fact]
    public void CollectRequestBodies_IgnoresResponses()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);

        var result = XmlBodyReachability.CollectRequestBodies(document);

        Assert.Equal(["Owner", "Pet", "Tag"], result.OrderBy(s => s, StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public void IsXmlModelBody_XmlMediaTypeWithModelSchema_ReturnsTrue()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var petSchema = document.Paths["/pets"].Operations![HttpMethod.Post].RequestBody!.Content!["application/xml"].Schema;
        var invoiceList = document.Paths["/invoices"].Operations![HttpMethod.Get].Responses!["200"].Content!["text/xml"].Schema;

        Assert.True(XmlBodyReachability.IsXmlModelBody("application/xml", petSchema));
        Assert.True(XmlBodyReachability.IsXmlModelBody("text/xml", invoiceList));
        Assert.True(XmlBodyReachability.IsXmlModelBody("application/atom+xml; charset=utf-8", petSchema));
    }

    [Fact]
    public void IsXmlModelBody_StringSchemaOrNonXmlMediaType_ReturnsFalse()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var petSchema = document.Paths["/pets"].Operations![HttpMethod.Post].RequestBody!.Content!["application/xml"].Schema;
        var textSchema = document.Paths["/notes"].Operations![HttpMethod.Post].RequestBody!.Content!["application/xml"].Schema;

        Assert.False(XmlBodyReachability.IsXmlModelBody("application/xml", textSchema));
        Assert.False(XmlBodyReachability.IsXmlModelBody("application/xml", schema: null));
        Assert.False(XmlBodyReachability.IsXmlModelBody("application/json", petSchema));
    }
}