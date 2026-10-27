namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A nullable reference to a polymorphic component (<c>nullable: true</c> + <c>allOf: [$ref]</c>, the OpenAPI 3.0
/// spelling) is that component's type, made nullable: the property reads the variant the discriminator names, and
/// null - not an untyped <c>object?</c> holding a JsonElement.
/// </summary>
public sealed class NullablePolymorphicReferenceRuntimeTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /orders:
            post:
              operationId: createOrder
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Order'
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Order'
        components:
          schemas:
            Order:
              type: object
              properties:
                payment:
                  nullable: true
                  allOf:
                    - $ref: '#/components/schemas/PaymentMethod'
            PaymentMethod:
              oneOf:
                - $ref: '#/components/schemas/Card'
                - $ref: '#/components/schemas/Invoice'
              discriminator:
                propertyName: kind
                mapping:
                  card: '#/components/schemas/Card'
                  invoice: '#/components/schemas/Invoice'
            Card:
              type: object
              required:
                - kind
                - number
              properties:
                kind:
                  type: string
                number:
                  type: string
            Invoice:
              type: object
              required:
                - kind
                - reference
              properties:
                kind:
                  type: string
                reference:
                  type: string
        """;

    /// <summary>
    /// Reads three bodies with the web defaults and the generated converters, and names the type the property holds.
    /// </summary>
    private const string ProbeSource = """"
        using System.Reflection;
        using System.Text.Json;
        using ErrorBodies.Generated.Models;

        namespace ErrorBodies.RuntimeTest;

        public static class Probe
        {
            public static List<string> Run()
            {
                var rows = new List<string>
                {
                    "type=" + typeof(Order).GetProperty("Payment")!.PropertyType.Name,
                };

                foreach (var json in new[] { """{"payment":{"kind":"card","number":"4111"}}""", """{"payment":{"kind":"invoice","reference":"R-7"}}""", """{"payment":null}""" })
                {
                    var order = JsonSerializer.Deserialize<Order>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                    rows.Add($"{json} => {order.Payment?.GetType().Name ?? "null"}");
                }

                return rows;
            }
        }
        """";

    [Fact]
    public void Server_NullableAllOfReferenceToAPolymorphicType_IsThatTypeMadeNullable()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", "{}", includeMinimalApi: false, Yaml);

        AssertReadsTheVariants(sources);
    }

    [Theory]
    [InlineData("TypedClient")]
    [InlineData("EndpointPerOperation")]
    public void Client_NullableAllOfReferenceToAPolymorphicType_IsThatTypeMadeNullable(
        string generationMode)
    {
        var marker = $$"""{ "generationMode": "{{generationMode}}" }""";
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);

        AssertReadsTheVariants(sources);
    }

    private static void AssertReadsTheVariants(
        List<(string HintName, string Source)> sources)
    {
        sources.Add(("Probe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var rows = (List<string>)assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [])!;

        Assert.Equal(
            [
                "type=PaymentMethod",
                """{"payment":{"kind":"card","number":"4111"}} => Card""",
                """{"payment":{"kind":"invoice","reference":"R-7"}} => Invoice""",
                """{"payment":null} => null""",
            ],
            rows,
            StringComparer.Ordinal);
    }
}