namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class PaginationTypeResolverTests
{
    private const string PaginationYaml = """
        openapi: 3.0.0
        info: { title: T, version: 1.0.0 }
        paths: {}
        components:
          schemas:
            PaginatedResult:
              type: object
              properties:
                pageSize: { type: integer }
                continuationToken: { type: string }
            Pet:
              type: object
              properties:
                name: { type: string }
            Pets:
              type: array
              items: { $ref: '#/components/schemas/Pet' }
            PetPage:
              allOf:
                - $ref: '#/components/schemas/PaginatedResult'
                - type: object
                  properties:
                    items:
                      type: array
                      items: { $ref: '#/components/schemas/Pet' }
            PetPageThroughAlias:
              allOf:
                - $ref: '#/components/schemas/PaginatedResult'
                - type: object
                  properties:
                    results: { $ref: '#/components/schemas/Pets' }
            InlinePage:
              allOf:
                - $ref: '#/components/schemas/PaginatedResult'
                - type: object
                  properties:
                    items:
                      type: array
                      items:
                        type: object
                        properties:
                          id: { type: integer }
            Dog:
              allOf:
                - $ref: '#/components/schemas/Pet'
                - type: object
                  properties:
                    barks: { type: boolean }
            Tags:
              type: array
              items: { type: string }
            PetMatrix:
              type: array
              items: { $ref: '#/components/schemas/Pets' }
            Anything:
              type: array
        """;

    [Theory]
    [InlineData("PaginationResult", true)]
    [InlineData("PaginatedResult", true)]
    [InlineData("PagedResult", true)]
    [InlineData("PagedResultOfPets", true)]
    [InlineData("Pet", false)]
    [InlineData("Page", false)]
    [InlineData(null, false)]
    public void IsPaginationBaseType_ReturnsExpectedResult(
        string? typeName,
        bool expected)
    {
        Assert.Equal(expected, PaginationTypeResolver.IsPaginationBaseType(typeName!));
    }

    [Fact]
    public void GetAllOfSchemaTypeName_PaginationBaseWithItemsArray_ReturnsGenericOfTheItem()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PaginationYaml);
        var allOf = document.Components!.Schemas!["PetPage"].AllOf!;

        var result = PaginationTypeResolver.GetAllOfSchemaTypeName(allOf, document, registry: null, _ => "Unexpected");

        Assert.Equal("PaginatedResult<Pet>", result);
    }

    [Fact]
    public void GetAllOfSchemaTypeName_ResultsReferencingAnArrayAlias_UnwrapsTheAlias()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PaginationYaml);
        var allOf = document.Components!.Schemas!["PetPageThroughAlias"].AllOf!;

        var result = PaginationTypeResolver.GetAllOfSchemaTypeName(allOf, document, registry: null, _ => "Unexpected");

        Assert.Equal("PaginatedResult<Pet>", result);
    }

    [Fact]
    public void GetAllOfSchemaTypeName_InlineItemSchema_AsksTheCallerForTheItemType()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PaginationYaml);
        var allOf = document.Components!.Schemas!["InlinePage"].AllOf!;

        var result = PaginationTypeResolver.GetAllOfSchemaTypeName(allOf, document, registry: null, inline => inline.Properties!.ContainsKey("id") ? "InlinePageItem" : "Wrong");

        Assert.Equal("PaginatedResult<InlinePageItem>", result);
    }

    [Fact]
    public void GetAllOfSchemaTypeName_PlainInheritance_ReturnsTheReferencedBase()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PaginationYaml);
        var allOf = document.Components!.Schemas!["Dog"].AllOf!;

        var result = PaginationTypeResolver.GetAllOfSchemaTypeName(allOf, document, registry: null, _ => "Unexpected");

        Assert.Equal("Pet", result);
    }

    [Fact]
    public void GetAllOfSchemaTypeName_NoReference_ReturnsObject()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PaginationYaml);

        var result = PaginationTypeResolver.GetAllOfSchemaTypeName([], document, registry: null, _ => "Unexpected");

        Assert.Equal("object", result);
    }

    [Fact]
    public void GetArrayItemTypeName_ReferencedModel_ReturnsItsName()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PaginationYaml);
        var array = (OpenApiSchema)document.Components!.Schemas!["Pets"];

        var result = PaginationTypeResolver.GetArrayItemTypeName(array, document, registry: null, _ => "Unexpected");

        Assert.Equal("Pet", result);
    }

    [Fact]
    public void GetArrayItemTypeName_ItemIsAnArrayAlias_UnwrapsToItsElement()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PaginationYaml);
        var array = (OpenApiSchema)document.Components!.Schemas!["PetMatrix"];

        var result = PaginationTypeResolver.GetArrayItemTypeName(array, document, registry: null, _ => "Unexpected");

        Assert.Equal("Pet", result);
    }

    [Fact]
    public void GetArrayItemTypeName_InlineItem_AsksTheCallerForTheItemType()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PaginationYaml);
        var array = (OpenApiSchema)document.Components!.Schemas!["Tags"];

        var result = PaginationTypeResolver.GetArrayItemTypeName(array, document, registry: null, inline => inline.Type == JsonSchemaType.String ? "string" : "Wrong");

        Assert.Equal("string", result);
    }

    [Fact]
    public void GetArrayItemTypeName_NoItems_ReturnsObject()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PaginationYaml);
        var array = (OpenApiSchema)document.Components!.Schemas!["Anything"];

        var result = PaginationTypeResolver.GetArrayItemTypeName(array, document, registry: null, _ => "Unexpected");

        Assert.Equal("object", result);
    }

    [Fact]
    public void GetArrayItemTypeName_NullArguments_Throw()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PaginationYaml);
        var array = (OpenApiSchema)document.Components!.Schemas!["Pets"];

        Assert.Throws<ArgumentNullException>(() => PaginationTypeResolver.GetArrayItemTypeName(null!, document, null, _ => "x"));
        Assert.Throws<ArgumentNullException>(() => PaginationTypeResolver.GetArrayItemTypeName(array, null!, null, _ => "x"));
        Assert.Throws<ArgumentNullException>(() => PaginationTypeResolver.GetArrayItemTypeName(array, document, null, null!));
    }
}