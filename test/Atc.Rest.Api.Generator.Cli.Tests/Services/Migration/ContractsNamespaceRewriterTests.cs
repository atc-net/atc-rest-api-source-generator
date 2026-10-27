namespace Atc.Rest.Api.Generator.Cli.Tests.Services.Migration;

/// <summary>
/// The references to the old generator's contracts (<c>{Project}.Api.Generated.Contracts[.{Segment}]</c>, which held
/// models, handlers, parameters and results together) become the source generator's namespaces, decided by the type
/// each reference names - from the specification, as the generator lays them out.
/// </summary>
public sealed class ContractsNamespaceRewriterTests
{
    private const string Spec = """
        openapi: 3.0.1
        info:
          title: Contoso
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: listPets
              parameters:
                - name: limit
                  in: query
                  schema:
                    type: integer
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Pet'
          /owners/{ownerId}:
            get:
              operationId: getOwner
              parameters:
                - name: ownerId
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Owner'
        components:
          schemas:
            Pet:
              type: object
              properties:
                address:
                  $ref: '#/components/schemas/Address'
            Owner:
              type: object
              properties:
                address:
                  $ref: '#/components/schemas/Address'
            Address:
              type: object
              properties:
                street:
                  type: string
        """;

    private static readonly ContractsNamespaceMap Map = ContractsNamespaceMap.Build(OpenApiDocumentHelper.ParseYaml(Spec), "Contoso");

    [Fact]
    public void GlobalUsingOfASegment_BecomesTheNamespacesThatSegmentHas()
    {
        var result = ContractsNamespaceRewriter.Rewrite("global using Contoso.Api.Generated.Contracts.Pets;\n", Map);

        Assert.Equal(
            "global using Contoso.Api.Contracts.Generated.Pets.Handlers;\n" +
            "global using Contoso.Api.Contracts.Generated.Pets.Models;\n" +
            "global using Contoso.Api.Contracts.Generated.Pets.Parameters;\n" +
            "global using Contoso.Api.Contracts.Generated.Pets.Results;\n",
            result.Content);
    }

    [Fact]
    public void UsingOfTheContractsRoot_BecomesTheSharedModels()
    {
        var result = ContractsNamespaceRewriter.Rewrite("using Contoso.Api.Generated.Contracts;\nnamespace Contoso.Domain;\n", Map);

        Assert.Equal("using Contoso.Api.Contracts.Generated.Models;\nnamespace Contoso.Domain;\n", result.Content);
    }

    [Theory]
    [InlineData("Contoso.Api.Generated.Contracts.Pets.Pet", "Contoso.Api.Contracts.Generated.Pets.Models.Pet")]
    [InlineData("Contoso.Api.Generated.Contracts.Pets.IListPetsHandler", "Contoso.Api.Contracts.Generated.Pets.Handlers.IListPetsHandler")]
    [InlineData("Contoso.Api.Generated.Contracts.Pets.ListPetsParameters", "Contoso.Api.Contracts.Generated.Pets.Parameters.ListPetsParameters")]
    [InlineData("Contoso.Api.Generated.Contracts.Owners.GetOwnerResult.Ok(owner)", "Contoso.Api.Contracts.Generated.Owners.Results.GetOwnerResult.Ok(owner)")]
    [InlineData("Contoso.Api.Generated.Contracts.Address", "Contoso.Api.Contracts.Generated.Models.Address")]
    [InlineData("global::Contoso.Api.Generated.Contracts.Owners.Owner", "global::Contoso.Api.Contracts.Generated.Owners.Models.Owner")]
    public void QualifiedReference_GoesToTheNamespaceOfItsType(
        string old,
        string expected)
        => Assert.Equal($"var x = {expected};", ContractsNamespaceRewriter.Rewrite($"var x = {old};", Map).Content);

    [Fact]
    public void RelativeReference_StaysRelative()
    {
        // Written inside namespace Contoso: Api.Generated.Contracts... resolved against it, and so does the new name
        var result = ContractsNamespaceRewriter.Rewrite("var pet = new Api.Generated.Contracts.Pets.Pet();", Map);

        Assert.Equal("var pet = new Api.Contracts.Generated.Pets.Models.Pet();", result.Content);
    }

    [Fact]
    public void ReferenceAlreadyNamingAKind_IsNotDoubled()
    {
        var result = ContractsNamespaceRewriter.Rewrite("Contoso.Api.Generated.Contracts.Pets.Models.Pet pet;", Map);

        Assert.Equal("Contoso.Api.Contracts.Generated.Pets.Models.Pet pet;", result.Content);
    }

    [Fact]
    public void HandWrittenNamespaceInTheOldProject_IsLeftAlone()
    {
        const string source = "global using Contoso.Api.Generated.Extensions;\nContoso.Api.Generated.Extensions.Helper.Run();";

        var result = ContractsNamespaceRewriter.Rewrite(source, Map);

        Assert.Equal(source, result.Content);
        Assert.Empty(result.Replacements);
    }

    [Fact]
    public void SingleSegmentSpecification_HasNoSegmentInItsNamespaces()
    {
        var single = ContractsNamespaceMap.Build(OpenApiDocumentHelper.ParseYaml(Spec.Replace("/owners/{ownerId}", "/pets/{ownerId}", StringComparison.Ordinal)), "Contoso");

        var result = ContractsNamespaceRewriter.Rewrite("var x = Contoso.Api.Generated.Contracts.Pets.Pet.Empty;", single);

        Assert.Equal("var x = Contoso.Api.Contracts.Generated.Models.Pet.Empty;", result.Content);
    }

    [Fact]
    public void Replacements_AreListedOncePerDistinctChange()
    {
        var result = ContractsNamespaceRewriter.Rewrite("Contoso.Api.Generated.Contracts.Pets.Pet a; Contoso.Api.Generated.Contracts.Pets.Pet b;", Map);

        Assert.Single(result.Replacements);
    }
}