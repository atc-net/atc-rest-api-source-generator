namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class BuiltInErrorContractNamesTests
{
    [Theory]
    [InlineData("ProblemDetails", true)]
    [InlineData("ValidationProblemDetails", true)]
    [InlineData("problemDetails", false)]
    [InlineData("ApiError", false)]
    public void IsReserved_ReturnsExpectedResult(
        string schemaName,
        bool expected)
    {
        Assert.Equal(expected, BuiltInErrorContractNames.IsReserved(schemaName));
    }

    [Fact]
    public void ReferencedByOtherSchemas_AllOfBaseAndArrayItem_ReturnsBothReservedNames()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                ProblemDetails:
                  type: object
                  properties:
                    title: { type: string }
                ValidationProblemDetails:
                  allOf:
                    - $ref: '#/components/schemas/ProblemDetails'
                    - type: object
                      properties:
                        errors: { type: object }
                ApiError:
                  allOf:
                    - $ref: '#/components/schemas/ProblemDetails'
                    - type: object
                      properties:
                        code: { type: string }
                Batch:
                  type: object
                  properties:
                    failures:
                      type: array
                      items: { $ref: '#/components/schemas/ValidationProblemDetails' }
                Plain:
                  type: object
                  properties:
                    name: { type: string }
            """);

        var result = BuiltInErrorContractNames.ReferencedByOtherSchemas(document);

        Assert.Equal(2, result.Count);
        Assert.Contains("ProblemDetails", result);
        Assert.Contains("ValidationProblemDetails", result);
    }

    [Fact]
    public void ReferencedByOtherSchemas_OnlyReservedSchemasReferenceEachOther_ReturnsEmpty()
    {
        // ValidationProblemDetails extending ProblemDetails is a reference from a reserved schema, which is never
        // materialized itself, so it does not make ProblemDetails a model
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                ProblemDetails:
                  type: object
                  properties:
                    title: { type: string }
                ValidationProblemDetails:
                  allOf:
                    - $ref: '#/components/schemas/ProblemDetails'
                Pet:
                  type: object
                  properties:
                    name: { type: string }
            """);

        var result = BuiltInErrorContractNames.ReferencedByOtherSchemas(document);

        Assert.Empty(result);
    }

    [Fact]
    public void ReferencedByOtherSchemas_NoComponents_ReturnsEmpty()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            """);

        var result = BuiltInErrorContractNames.ReferencedByOtherSchemas(document);

        Assert.Empty(result);
    }

    [Fact]
    public void AppendFrameworkAliases_WritesOneAliasPerNameInOrdinalOrder()
    {
        var sb = new StringBuilder();

        BuiltInErrorContractNames.AppendFrameworkAliases(sb, ["ValidationProblemDetails", "ProblemDetails"]);

        var expected =
            "using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;" + Environment.NewLine +
            "using ValidationProblemDetails = Microsoft.AspNetCore.Mvc.ValidationProblemDetails;" + Environment.NewLine;
        Assert.Equal(expected, sb.ToString());
    }

    [Fact]
    public void AppendFrameworkAliases_NoNames_WritesNothing()
    {
        var sb = new StringBuilder();

        BuiltInErrorContractNames.AppendFrameworkAliases(sb, []);

        Assert.Equal(string.Empty, sb.ToString());
    }
}