namespace Atc.Rest.Api.Generator.Cli.Tests.Services.Migration;

public class SpecificationValidatorTests
{
    // ========== OpenApiVersion display format ==========
    [Fact]
    public void Validate_OpenApi30Spec_ReturnsVersionAs30()
    {
        var specPath = CreateTempSpec("openapi: 3.0.0");

        try
        {
            var result = SpecificationValidator.Validate(specPath);

            Assert.True(result.IsValid, string.Join(", ", result.ValidationErrors));
            Assert.Equal("3.0", result.OpenApiVersion);
        }
        finally
        {
            File.Delete(specPath);
        }
    }

    [Fact]
    public void Validate_OpenApi31Spec_ReturnsVersionAs31()
    {
        var specPath = CreateTempSpec("openapi: 3.1.0");

        try
        {
            var result = SpecificationValidator.Validate(specPath);

            Assert.True(result.IsValid, string.Join(", ", result.ValidationErrors));
            Assert.Equal("3.1", result.OpenApiVersion);
        }
        finally
        {
            File.Delete(specPath);
        }
    }

    private static string CreateTempSpec(string openapiLine)
    {
        var path = Path.GetTempFileName() + ".yaml";
        File.WriteAllText(path, $"""
            {openapiLine}
            info:
              title: Test
              version: 1.0.0
            paths:
              /health:
                get:
                  operationId: getHealth
                  responses:
                    '200':
                      description: OK
            """);
        return path;
    }
}