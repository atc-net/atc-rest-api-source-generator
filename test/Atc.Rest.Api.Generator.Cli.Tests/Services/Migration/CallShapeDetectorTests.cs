namespace Atc.Rest.Api.Generator.Cli.Tests.Services.Migration;

/// <summary>
/// Best-effort detection of code the old generator's call shapes leave behind: a result factory called with an
/// argument it no longer takes (or a string where it takes the declared model), and properties renamed to PascalCase.
/// </summary>
public sealed class CallShapeDetectorTests
{
    private const string Spec = """
        openapi: 3.0.1
        info:
          title: Contoso
          version: 1.0.0
        paths:
          /pets:
            post:
              operationId: createPet
              requestBody:
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Pet'
              responses:
                '201':
                  description: Created
                '400':
                  description: Invalid
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/PetError'
        components:
          schemas:
            Pet:
              type: object
              properties:
                isVaccinated:
                  type: boolean
                name:
                  type: string
            PetError:
              type: object
              properties:
                reason:
                  type: string
        """;

    private static readonly OpenApiDocument Document = OpenApiDocumentHelper.ParseYaml(Spec);

    [Fact]
    public void SuccessFactoryCalledWithAnArgument_ItNoLongerTakes_IsReported()
    {
        var findings = CallShapeDetector.Detect(Document, [("Handlers/CreatePetHandler.cs", "return CreatePetResult.Created(\"done\");")]);

        var finding = Assert.Single(findings);
        Assert.StartsWith("Handlers/CreatePetHandler.cs:1:", finding, StringComparison.Ordinal);
        Assert.Contains("CreatePetResult.Created", finding, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorFactoryCalledWithAString_WhereTheSpecificationDeclaresAModel_IsReported()
    {
        var findings = CallShapeDetector.Detect(Document, [("CreatePetHandler.cs", "\n\nreturn CreatePetResult.BadRequest(ex.Message);")]);

        var finding = Assert.Single(findings);
        Assert.StartsWith("CreatePetHandler.cs:3:", finding, StringComparison.Ordinal);
        Assert.Contains("PetError", finding, StringComparison.Ordinal);
    }

    [Fact]
    public void PropertyKeptInTheSpecificationsCasing_IsReportedWithItsNewName()
    {
        var findings = CallShapeDetector.Detect(Document, [("Mapper.cs", "var v = request.isVaccinated;")]);

        var finding = Assert.Single(findings);
        Assert.Contains("isVaccinated → IsVaccinated", finding, StringComparison.Ordinal);
    }

    [Fact]
    public void CallsThatFitTheNewShapes_AreNotReported()
    {
        const string code = "return CreatePetResult.Created(); return CreatePetResult.BadRequest(new PetError(\"x\")); var n = pet.Name;";

        Assert.Empty(CallShapeDetector.Detect(Document, [("Ok.cs", code)]));
    }
}