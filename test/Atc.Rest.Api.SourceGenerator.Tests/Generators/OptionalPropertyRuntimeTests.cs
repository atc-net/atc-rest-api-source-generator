namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// An optional property (not in <c>required</c>) may be absent: the generated record reads JSON without it under the
/// strictest System.Text.Json options (<c>RespectRequiredConstructorParameters</c> and
/// <c>RespectNullableAnnotations</c>), and code can create it with the required values only.
/// </summary>
public sealed class OptionalPropertyRuntimeTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPet
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Pet'
        components:
          schemas:
            Pet:
              type: object
              required:
                - id
                - name
              properties:
                tag:
                  type: string
                id:
                  type: integer
                  format: int64
                owner:
                  $ref: '#/components/schemas/Owner'
                toys:
                  type: array
                  items:
                    type: string
                age:
                  type: integer
                kind:
                  type: string
                  default: dog
                name:
                  type: string
            Owner:
              type: object
              properties:
                name:
                  type: string
        """;

    /// <summary>
    /// Reads JSON with and without the optional properties under the strict options, and creates the record from the
    /// required values alone (this only compiles when every optional parameter has a C# default).
    /// </summary>
    private const string ProbeSource = """"
        using System.Text.Json;
        using ErrorBodies.Generated.Models;

        namespace ErrorBodies.RuntimeTest;

        public static class Probe
        {
            public static List<string> Run()
            {
                var strict = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    RespectRequiredConstructorParameters = true,
                    RespectNullableAnnotations = true,
                };

                var rows = new List<string>();
                foreach (var json in new[]
                {
                    """{"id":1,"name":"Rex"}""",
                    """{"id":1,"name":"Rex","tag":null,"owner":null,"toys":null,"age":null}""",
                    """{"id":1,"name":"Rex","tag":"good","owner":{"name":"Ann"},"toys":["ball"],"age":3,"kind":"cat"}""",
                    """{"name":"Rex"}""",
                })
                {
                    try
                    {
                        var pet = JsonSerializer.Deserialize<Pet>(json, strict)!;
                        rows.Add($"{json} => {pet.Id} {pet.Name} tag={pet.Tag ?? "null"} owner={pet.Owner?.Name ?? "null"} toys={(pet.Toys is null ? "null" : pet.Toys.Count.ToString())} age={pet.Age?.ToString() ?? "null"} kind={pet.Kind}");
                    }
                    catch (JsonException)
                    {
                        rows.Add($"{json} => THROWS");
                    }
                }

                var created = new Pet(Id: 7, Name: "Max");
                rows.Add($"created => {created.Id} {created.Name} tag={created.Tag ?? "null"} kind={created.Kind}");
                return rows;
            }
        }
        """";

    [Fact]
    public void OptionalProperties_MayBeAbsent_UnderTheStrictestJsonOptions()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, Yaml);
        sources.Add(("Probe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var rows = (List<string>)assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [])!;

        Assert.Equal(
            [
                """{"id":1,"name":"Rex"} => 1 Rex tag=null owner=null toys=null age=null kind=dog""",
                """{"id":1,"name":"Rex","tag":null,"owner":null,"toys":null,"age":null} => 1 Rex tag=null owner=null toys=null age=null kind=dog""",
                """{"id":1,"name":"Rex","tag":"good","owner":{"name":"Ann"},"toys":["ball"],"age":3,"kind":"cat"} => 1 Rex tag=good owner=Ann toys=1 age=3 kind=cat""",

                // A required property is still required.
                """{"name":"Rex"} => THROWS""",
                "created => 7 Max tag=null kind=dog",
            ],
            rows,
            StringComparer.Ordinal);
    }
}