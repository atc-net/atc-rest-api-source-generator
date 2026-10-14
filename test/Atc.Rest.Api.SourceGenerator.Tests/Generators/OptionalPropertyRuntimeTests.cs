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
          /pets/summary:
            get:
              operationId: getPetSummary
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: object
                        required:
                          - count
                        properties:
                          count:
                            type: integer
                          note:
                            type: string
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

    /// <summary>
    /// Reads JSON that lacks a required property with the default web options (what the client uses): the value is
    /// the missing one (<c>null</c>, <c>0</c>), or - with <c>strictRequiredProperties</c> - a <c>JsonException</c>.
    /// </summary>
    private const string MissingRequiredProbeSource = """"
        using System.Text.Json;
        using ErrorBodies.Generated.Models;

        namespace ErrorBodies.RuntimeTest;

        public static class MissingRequiredProbe
        {
            public static List<string> Run()
            {
                var rows = new List<string>();
                // An inline response schema (no component): its required count, with the same rule.
                var summary = typeof(Pet).Assembly.GetTypes().Single(t => t.Name.Contains("Summary", StringComparison.Ordinal) && t.GetProperty("Count") is not null);
                foreach (var json in new[] { """{"count":2}""", """{"note":"x"}""" })
                {
                    try
                    {
                        var value = JsonSerializer.Deserialize(json, summary, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                        rows.Add($"{json} => count {summary.GetProperty("Count")!.GetValue(value)}");
                    }
                    catch (JsonException)
                    {
                        rows.Add($"{json} => THROWS");
                    }
                }

                foreach (var json in new[] { """{"id":1,"name":"Rex"}""", """{"id":1}""", """{"name":"Rex"}""" })
                {
                    try
                    {
                        var pet = JsonSerializer.Deserialize<Pet>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                        rows.Add($"{json} => {pet.Id} {pet.Name ?? "null"}");
                    }
                    catch (JsonException ex)
                    {
                        rows.Add($"{json} => THROWS {(ex.Message.Contains("'name'", StringComparison.Ordinal) ? "name" : ex.Message.Contains("'id'", StringComparison.Ordinal) ? "id" : ex.Message)}");
                    }
                }

                return rows;
            }
        }
        """";

    [Theory]
    [InlineData("EndpointPerOperation", false)]
    [InlineData("EndpointPerOperation", true)]
    [InlineData("TypedClient", false)]
    [InlineData("TypedClient", true)]
    public void RequiredProperties_AreEnforcedOnlyWithStrictRequiredProperties(
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
        sources.Add(("MissingRequiredProbe.cs", MissingRequiredProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var rows = (List<string>)assembly.GetType("ErrorBodies.RuntimeTest.MissingRequiredProbe", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [])!;

        Assert.Equal(
            strictRequiredProperties
                ? ["""{"count":2} => count 2""", """{"note":"x"} => THROWS""", """{"id":1,"name":"Rex"} => 1 Rex""", """{"id":1} => THROWS name""", """{"name":"Rex"} => THROWS id"""]
                : ["""{"count":2} => count 2""", """{"note":"x"} => count 0""", """{"id":1,"name":"Rex"} => 1 Rex""", """{"id":1} => 1 null""", """{"name":"Rex"} => 0 Rex"""],
            rows,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Specifications that list optional properties (and properties with a default, or readOnly) before required
    /// ones, in components, allOf inheritance and an inline response. C# requires every parameter with a default to
    /// come after those without (CS1737), so the generated records must reorder them, whatever the spec order.
    /// </summary>
    private const string OrderingYaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /items:
            post:
              operationId: createItem
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Item'
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Account'
          /items/{id}:
            get:
              operationId: getItem
              parameters:
                - name: id
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
                        $ref: '#/components/schemas/Dog'
          /items/summary:
            get:
              operationId: getItemSummary
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: object
                        required:
                          - total
                        properties:
                          note:
                            type: string
                          total:
                            type: integer
        components:
          schemas:
            Item:
              type: object
              required:
                - name
              properties:
                id:
                  type: string
                tag:
                  type: string
                  default: none
                name:
                  type: string
            Account:
              type: object
              required:
                - createdAt
                - name
              properties:
                createdAt:
                  type: string
                  format: date-time
                  readOnly: true
                id:
                  type: string
                name:
                  type: string
            Animal:
              type: object
              required:
                - kind
              properties:
                nickname:
                  type: string
                kind:
                  type: string
            Dog:
              allOf:
                - $ref: '#/components/schemas/Animal'
                - type: object
                  required:
                    - breed
                  properties:
                    age:
                      type: integer
                    breed:
                      type: string
        """;

    public static TheoryData<string> Generators()
        => ["Server", "EndpointPerOperation", "TypedClient"];

    [Theory]
    [MemberData(nameof(Generators))]
    public void OptionalBeforeRequiredInTheSpec_GeneratesValidRecords(
        string generator)
    {
        var sources = generator == "Server"
            ? ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, OrderingYaml)
            : ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", $$"""{ "generationMode": "{{generator}}" }""", includeMinimalApi: false, OrderingYaml);

        // Compiles: no parameter with a default before one without (CS1737), in any record.
        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: generator == "Server");
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));

        // And the order is the required ones first, the spec order kept within each group. (A server model names a
        // validated property by its JSON name, for the validation messages.)
        var item = sources.Single(s => s.HintName.EndsWith(".Item.g.cs", StringComparison.Ordinal)).Source;
        Assert.Matches(new Regex(@"record Item\(\s*\[property: Required(?:, global::System\.ComponentModel\.DataAnnotations\.Display\(Name = ""name""\))?\] string Name,\s*string\? Id = null,\s*string Tag = ""none""\);", RegexOptions.None, TimeSpan.FromSeconds(1)), item);
    }
}