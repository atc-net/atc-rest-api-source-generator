namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A property that is required and nullable must be present and may be null (OpenAPI: <c>required</c> is about the
/// key, <c>nullable</c> / a <c>null</c> type about the value). The server's model takes an explicit null and refuses a
/// body without the key; a client reads a response without it unless <c>strictRequiredProperties</c> is on.
/// </summary>
public sealed class RequiredNullablePropertyRuntimeTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /profiles:
            post:
              operationId: createProfile
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Profile'
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Profile'
        components:
          schemas:
            Profile:
              type: object
              required:
                - name
                - bio
              properties:
                name:
                  type: string
                bio:
                  type: string
                  nullable: true
        """;

    /// <summary>
    /// Reads the three bodies with the web defaults, and validates the model as the ValidationFilter does
    /// (DataAnnotations, every property).
    /// </summary>
    private const string ProbeSource = """"
        using System.ComponentModel.DataAnnotations;
        using System.Text.Json;
        using ErrorBodies.Generated.Models;

        namespace ErrorBodies.RuntimeTest;

        public static class Probe
        {
            public static List<string> Run()
            {
                var rows = new List<string>();
                foreach (var json in new[] { """{"name":"Ann","bio":"x"}""", """{"name":"Ann","bio":null}""", """{"name":"Ann"}""" })
                {
                    try
                    {
                        var profile = JsonSerializer.Deserialize<Profile>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                        var results = new List<ValidationResult>();
                        var valid = Validator.TryValidateObject(profile, new ValidationContext(profile), results, validateAllProperties: true);
                        rows.Add($"{json} => bio={profile.Bio ?? "null"} {(valid ? "valid" : "invalid: " + string.Join(", ", results.SelectMany(r => r.MemberNames)))}");
                    }
                    catch (JsonException)
                    {
                        rows.Add($"{json} => THROWS");
                    }
                }

                return rows;
            }
        }
        """";

    [Fact]
    public void Server_RequiredNullableProperty_TakesNullAndRefusesAMissingKey()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", "{}", includeMinimalApi: false, Yaml);
        sources.Add(("Probe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var rows = (List<string>)assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [])!;

        Assert.Equal(
            [
                """{"name":"Ann","bio":"x"} => bio=x valid""",
                """{"name":"Ann","bio":null} => bio=null valid""",
                """{"name":"Ann"} => THROWS""",
            ],
            rows,
            StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("TypedClient", false)]
    [InlineData("TypedClient", true)]
    [InlineData("EndpointPerOperation", false)]
    [InlineData("EndpointPerOperation", true)]
    public void Client_RequiredNullableProperty_IsEnforcedOnlyWithStrictRequiredProperties(
        string generationMode,
        bool strictRequiredProperties)
    {
        var marker = $$"""
            {
              "generationMode": "{{generationMode}}",
              "strictRequiredProperties": {{(strictRequiredProperties ? "true" : "false")}}
            }
            """;
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("Probe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var rows = (List<string>)assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [])!;

        Assert.Equal(
            strictRequiredProperties ? """{"name":"Ann"} => THROWS""" : """{"name":"Ann"} => bio=null valid""",
            rows[2]);
        Assert.Equal("""{"name":"Ann","bio":null} => bio=null valid""", rows[1]);
    }
}