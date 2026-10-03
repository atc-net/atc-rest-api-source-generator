namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class XmlBodyHelperTests
{
    // createPet: XML-only model body; listPets: XML-only list with a named root; createOwner: a model offered as
    // JSON and XML (two XML media types); createTags: a list offered as JSON and XML; createNote: XML text (no
    // model); createMixed: JSON and XML carry different models (no alternative).
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
                    application/xml:
                      schema: { $ref: '#/components/schemas/Pet' }
                '400':
                  description: Bad request
                  content:
                    application/problem+xml:
                      schema: { $ref: '#/components/schemas/Problem' }
                default:
                  description: Error
                  content:
                    application/problem+json:
                      schema: { $ref: '#/components/schemas/Problem' }
            get:
              operationId: listPets
              responses:
                '200':
                  description: OK
                  content:
                    application/xml:
                      schema:
                        type: array
                        xml: { name: pets }
                        items: { $ref: '#/components/schemas/Pet' }
          /owners:
            post:
              operationId: createOwner
              requestBody:
                content:
                  application/json:
                    schema: { $ref: '#/components/schemas/Owner' }
                  application/xml:
                    schema: { $ref: '#/components/schemas/Owner' }
                  text/xml:
                    schema: { $ref: '#/components/schemas/Owner' }
              responses:
                '201': { description: Created }
          /tags:
            post:
              operationId: createTags
              requestBody:
                content:
                  application/json:
                    schema:
                      type: array
                      items: { $ref: '#/components/schemas/Tag' }
                  application/xml:
                    schema:
                      type: array
                      items: { $ref: '#/components/schemas/Tag' }
              responses:
                '201': { description: Created }
          /notes:
            post:
              operationId: createNote
              requestBody:
                content:
                  application/xml:
                    schema: { type: string }
              responses:
                '201': { description: Created }
          /mixed:
            post:
              operationId: createMixed
              requestBody:
                content:
                  application/json:
                    schema: { $ref: '#/components/schemas/Owner' }
                  application/xml:
                    schema: { $ref: '#/components/schemas/Pet' }
              responses:
                '201': { description: Created }
        components:
          schemas:
            Pet:
              type: object
              properties:
                id: { type: integer }
                name: { type: string }
                tag: { $ref: '#/components/schemas/Tag' }
            Owner:
              type: object
              properties:
                name: { type: string }
            Tag:
              type: object
              properties:
                label: { type: string }
            Problem:
              type: object
              properties:
                detail: { type: string }
            Color:
              type: string
              enum: [red, blue]
            Shape:
              oneOf:
                - $ref: '#/components/schemas/Pet'
                - $ref: '#/components/schemas/Owner'
            Catalog:
              type: object
              properties:
                pet: { $ref: '#/components/schemas/Pet' }
                color: { $ref: '#/components/schemas/Color' }
                shape: { $ref: '#/components/schemas/Shape' }
                pets:
                  type: array
                  items: { $ref: '#/components/schemas/Pet' }
                inline:
                  type: object
                  properties:
                    x: { type: string }
        """;

    [Fact]
    public void FindXmlOnlyBody_XmlModel_ReturnsSingleBody()
    {
        var content = RequestContent("/pets", HttpMethod.Post);

        var result = XmlBodyHelper.FindXmlOnlyBody(content);

        Assert.Equal(new XmlBody("application/xml", IsList: false, ListRootName: null), result);
        Assert.False(result!.IsAlternative);
    }

    [Fact]
    public void FindXmlOnlyBody_ArrayOfModelsWithXmlName_ReturnsListBodyWithRootName()
    {
        var content = ResponseContent("/pets", HttpMethod.Get, "200");

        var result = XmlBodyHelper.FindXmlOnlyBody(content);

        Assert.Equal(new XmlBody("application/xml", IsList: true, ListRootName: "pets"), result);
    }

    [Fact]
    public void FindXmlOnlyBody_JsonDeclaredToo_ReturnsNull()
        => Assert.Null(XmlBodyHelper.FindXmlOnlyBody(RequestContent("/owners", HttpMethod.Post)));

    [Fact]
    public void FindXmlOnlyBody_XmlStringIsText_ReturnsNull()
        => Assert.Null(XmlBodyHelper.FindXmlOnlyBody(RequestContent("/notes", HttpMethod.Post)));

    [Fact]
    public void FindXmlOnlyBody_NullOrEmptyContent_ReturnsNull()
    {
        Assert.Null(XmlBodyHelper.FindXmlOnlyBody(content: null));
        Assert.Null(XmlBodyHelper.FindXmlOnlyBody(new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)));
    }

    [Fact]
    public void FindXmlAlternative_SameModelAsJson_ListsEveryXmlMediaType()
    {
        var content = RequestContent("/owners", HttpMethod.Post);

        var result = XmlBodyHelper.FindXmlAlternative(content);

        Assert.NotNull(result);
        Assert.Equal("application/xml", result.MediaType);
        Assert.False(result.IsList);
        Assert.True(result.IsAlternative);
        Assert.Equal(["application/xml", "text/xml"], result.AlternativeMediaTypes);
    }

    [Fact]
    public void FindXmlAlternative_ListOfSameModel_IsListAlternative()
    {
        var content = RequestContent("/tags", HttpMethod.Post);

        var result = XmlBodyHelper.FindXmlAlternative(content);

        Assert.NotNull(result);
        Assert.True(result.IsList);
        Assert.Null(result.ListRootName);
        Assert.Equal(["application/xml"], result.AlternativeMediaTypes);
    }

    [Fact]
    public void FindXmlAlternative_XmlOnlyBody_ReturnsNull()
        => Assert.Null(XmlBodyHelper.FindXmlAlternative(RequestContent("/pets", HttpMethod.Post)));

    [Fact]
    public void FindXmlAlternative_DifferentModelThanJson_ReturnsNull()
        => Assert.Null(XmlBodyHelper.FindXmlAlternative(RequestContent("/mixed", HttpMethod.Post)));

    [Fact]
    public void ClientRequestBody_XmlOnly_IsSentAsXmlRegardlessOfPreference()
    {
        var operation = Operation("/pets", HttpMethod.Post);

        Assert.Equal(new XmlBody("application/xml", IsList: false, ListRootName: null), XmlBodyHelper.ClientRequestBody(operation, preferXml: false));
        Assert.Equal(new XmlBody("application/xml", IsList: false, ListRootName: null), XmlBodyHelper.ClientRequestBody(operation, preferXml: true));
    }

    [Fact]
    public void ClientRequestBody_JsonAndXml_OnlyWithPreferXml()
    {
        var operation = Operation("/owners", HttpMethod.Post);

        Assert.Null(XmlBodyHelper.ClientRequestBody(operation, preferXml: false));

        var preferred = XmlBodyHelper.ClientRequestBody(operation, preferXml: true);
        Assert.NotNull(preferred);
        Assert.True(preferred.IsAlternative);
        Assert.Equal("application/xml", preferred.MediaType);
    }

    [Fact]
    public void ServerBoundRequestBody_XmlOnlyAndAlternative_AreBound_DifferentModelIsNot()
    {
        var xmlOnly = XmlBodyHelper.ServerBoundRequestBody(RequestContent("/pets", HttpMethod.Post));
        Assert.NotNull(xmlOnly);
        Assert.False(xmlOnly.IsAlternative);

        var alternative = XmlBodyHelper.ServerBoundRequestBody(RequestContent("/owners", HttpMethod.Post));
        Assert.NotNull(alternative);
        Assert.True(alternative.IsAlternative);

        Assert.Null(XmlBodyHelper.ServerBoundRequestBody(RequestContent("/mixed", HttpMethod.Post)));
        Assert.Null(XmlBodyHelper.ServerBoundRequestBody(content: null));
    }

    [Fact]
    public void ServerBoundRequestModels_CollectsHowEachModelIsBound()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);

        var result = XmlBodyHelper.ServerBoundRequestModels(document);

        Assert.Equal(3, result.Count);
        Assert.Equal(new ServerBoundModel(AcceptsJson: false, BoundAlone: true, BoundInList: false), result["Pet"]);
        Assert.Equal(new ServerBoundModel(AcceptsJson: true, BoundAlone: true, BoundInList: false), result["Owner"]);
        Assert.Equal(new ServerBoundModel(AcceptsJson: false, BoundAlone: false, BoundInList: true), result["Tag"]);
    }

    [Fact]
    public void ModelTypeName_ModelAndList()
    {
        var single = RequestContent("/pets", HttpMethod.Post);
        var list = ResponseContent("/pets", HttpMethod.Get, "200");

        Assert.Equal("Pet", XmlBodyHelper.ModelTypeName(single, XmlBodyHelper.FindXmlOnlyBody(single)!));
        Assert.Equal("List<Pet>", XmlBodyHelper.ModelTypeName(list, XmlBodyHelper.FindXmlOnlyBody(list)!));
    }

    [Fact]
    public void ModelTypeName_ConflictingName_IsFullyQualified()
    {
        var list = ResponseContent("/pets", HttpMethod.Get, "200");
        var registry = TypeConflictRegistry.ForSegment(new HashSet<string>(StringComparer.Ordinal) { "Pet" }, "TestApi", "Pets");

        var result = XmlBodyHelper.ModelTypeName(list, XmlBodyHelper.FindXmlOnlyBody(list)!, registry);

        Assert.Equal("List<TestApi.Generated.Pets.Models.Pet>", result);
    }

    [Fact]
    public void ItemTypeName_ListBody_IsTheItemModel()
    {
        var list = ResponseContent("/pets", HttpMethod.Get, "200");

        Assert.Equal("Pet", XmlBodyHelper.ItemTypeName(list, XmlBodyHelper.FindXmlOnlyBody(list)!));
    }

    [Fact]
    public void ErrorMediaTypes_ReturnsMediaTypesOf4xx5xxAndDefaultInOrder()
    {
        var operation = Operation("/pets", HttpMethod.Post);

        var result = XmlBodyHelper.ErrorMediaTypes(operation).ToList();

        Assert.Equal(["application/problem+xml", "application/problem+json"], result);
    }

    [Fact]
    public void ErrorMediaTypes_NoErrorResponses_ReturnsEmpty()
        => Assert.Empty(XmlBodyHelper.ErrorMediaTypes(Operation("/pets", HttpMethod.Get)));

    [Fact]
    public void AcceptFor_XmlOnlyBody_AsksForXmlThenDeclaredErrorsThenProblemJson()
    {
        var bodies = new[] { new XmlBody("application/xml", IsList: false, ListRootName: null) };

        var result = XmlBodyHelper.AcceptFor(bodies, ["application/problem+xml", "application/problem+json"], preferXml: false);

        Assert.Equal("application/xml, application/problem+xml, application/problem+json", result);
    }

    [Fact]
    public void AcceptFor_AlternativeBody_PreferXml_AsksForXmlWithWeightedProblems()
    {
        var bodies = new[]
        {
            new XmlBody("application/xml", IsList: false, ListRootName: null) { AlternativeMediaTypes = ["application/xml", "text/xml"] },
        };

        var result = XmlBodyHelper.AcceptFor(bodies, ["application/problem+json"], preferXml: true);

        Assert.Equal("application/xml, text/xml, application/problem+xml;q=0.9, application/problem+json;q=0.8", result);
    }

    [Fact]
    public void AcceptFor_AlternativeBody_PreferJson_AsksForJson()
    {
        var bodies = new[]
        {
            new XmlBody("application/xml", IsList: false, ListRootName: null) { AlternativeMediaTypes = ["application/xml"] },
        };

        var result = XmlBodyHelper.AcceptFor(bodies, ["application/problem+xml"], preferXml: false, ["application/pdf"]);

        Assert.Equal("application/json, application/pdf, application/problem+json", result);
    }

    [Fact]
    public void AcceptFor_NoXmlBodyButFile_AsksForJsonAndTheFile()
    {
        var result = XmlBodyHelper.AcceptFor([], [], preferXml: false, ["application/pdf"]);

        Assert.Equal("application/json, application/pdf, application/problem+json", result);
    }

    [Fact]
    public void AcceptFor_NoXmlBodyNoFile_ReturnsNull()
        => Assert.Null(XmlBodyHelper.AcceptFor([], ["application/problem+json"], preferXml: true));

    [Theory]
    [InlineData("pet", true)]
    [InlineData("color", false)]
    [InlineData("shape", false)]
    [InlineData("pets", false)]
    [InlineData("inline", false)]
    public void IsMappedModel_OnlyAReferenceToAnObjectRecord(
        string property,
        bool expected)
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var schema = document.Components!.Schemas!["Catalog"].Properties![property];

        Assert.Equal(expected, XmlBodyHelper.IsMappedModel(schema));
    }

    [Fact]
    public void IsMappedModel_Null_ReturnsFalse()
        => Assert.False(XmlBodyHelper.IsMappedModel(schema: null));

    [Fact]
    public void ToUtf8BytesExpression_SingleModel_UsesTheCompanion()
    {
        var body = new XmlBody("application/xml", IsList: false, ListRootName: null);

        Assert.Equal("PetXml.ToUtf8Bytes(request)", XmlBodyHelper.ToUtf8BytesExpression("Pet", body, "request"));
        Assert.Equal("PetXml.ToUtf8Bytes(request)", XmlBodyHelper.ToUtf8BytesExpression("Pet?", body, "request"));
    }

    [Fact]
    public void ToUtf8BytesExpression_List_UnwrapsTheItemTypeAndPassesTheRootName()
    {
        Assert.Equal(
            "PetXml.ListToUtf8Bytes(items, \"pets\")",
            XmlBodyHelper.ToUtf8BytesExpression("List<Pet>", new XmlBody("application/xml", IsList: true, ListRootName: "pets"), "items"));
        Assert.Equal(
            "PetXml.ListToUtf8Bytes(items)",
            XmlBodyHelper.ToUtf8BytesExpression("Pet[]", new XmlBody("application/xml", IsList: true, ListRootName: null), "items"));
    }

    [Fact]
    public void ReadMethod_ModelAndList()
    {
        Assert.Equal("PetXml.Read", XmlBodyHelper.ReadMethod("Pet", new XmlBody("application/xml", IsList: false, ListRootName: null)));
        Assert.Equal("PetXml.ReadList", XmlBodyHelper.ReadMethod("List<Pet>", new XmlBody("application/xml", IsList: true, ListRootName: null)));
    }

    private static OpenApiOperation Operation(
        string path,
        HttpMethod method)
        => OpenApiDocumentHelper.ParseYaml(Yaml).Paths[path].Operations![method];

    private static IDictionary<string, IOpenApiMediaType> RequestContent(
        string path,
        HttpMethod method)
        => Operation(path, method).RequestBody!.Content!;

    private static IDictionary<string, IOpenApiMediaType> ResponseContent(
        string path,
        HttpMethod method,
        string statusCode)
        => Operation(path, method).Responses![statusCode].Content!;
}