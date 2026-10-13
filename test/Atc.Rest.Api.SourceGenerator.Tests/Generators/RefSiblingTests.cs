namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// OpenAPI 3.1 lets a <c>$ref</c> have siblings: a property that references a component says what it is here
/// (<c>description</c>), that it is on its way out (<c>deprecated</c>), or what it is when left out
/// (<c>default</c>). The sibling is the property's, not the component's: the generated property carries it, and
/// another property that references the same component does not.
/// </summary>
public class RefSiblingTests
{
    private const string Yaml = """
        openapi: 3.1.0
        info:
          title: Orders
          version: 1.0.0
        paths:
          /orders:
            get:
              operationId: listOrders
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Order'
        components:
          schemas:
            Order:
              type: object
              required: [billing]
              properties:
                billing:
                  $ref: '#/components/schemas/Address'
                  description: Where the invoice goes
                shipping:
                  $ref: '#/components/schemas/Address'
                  description: Where the parcel goes
                  deprecated: true
                priority:
                  $ref: '#/components/schemas/Priority'
                  default: high
                plain:
                  $ref: '#/components/schemas/Address'
            Address:
              type: object
              description: A postal address
              properties:
                street:
                  type: string
            Priority:
              type: string
              enum: [low, high]
        """;

    [Fact]
    public void Server_AndClient_GiveAReferencingPropertyItsOwnSiblings()
    {
        foreach (var (generator, marker) in new (IIncrementalGenerator, string)[] { (new ApiServerGenerator(), ".atc-rest-api-server"), (new ApiClientGenerator(), ".atc-rest-api-client") })
        {
            var order = OrderModel(generator, marker);

            // Its own description, not the component's
            Assert.Contains("Where the invoice goes", order, StringComparison.Ordinal);
            Assert.Contains("Where the parcel goes", order, StringComparison.Ordinal);

            // Deprecated here only: one [Obsolete], on Shipping
            Assert.Single(Regex.Matches(order, "Obsolete", RegexOptions.None, TimeSpan.FromSeconds(1)));
            Assert.Matches(new Regex(@"Obsolete[^\n]*\s+public Address\?? Shipping", RegexOptions.None, TimeSpan.FromSeconds(1)), order);

            // Its own default - on the server; a client's string enum is an extensible struct, whose members are no
            // constants a parameter could default to
            Assert.Matches(
                generator is ApiServerGenerator
                    ? new Regex(@"Priority\?? Priority = Priority\.High", RegexOptions.None, TimeSpan.FromSeconds(1))
                    : new Regex(@"Priority\? Priority = null", RegexOptions.None, TimeSpan.FromSeconds(1)),
                order);
        }
    }

    /// <summary>
    /// OpenAPI 3.0 says the opposite: whatever stands next to a <c>$ref</c> is ignored. The same specification as a
    /// 3.0 document gives the property no default.
    /// </summary>
    [Fact]
    public void Server_InAnOpenApi30Document_IgnoresADefaultNextToAReference()
    {
        var order = OrderModel(new ApiServerGenerator(), ".atc-rest-api-server", Yaml.Replace("openapi: 3.1.0", "openapi: 3.0.3", StringComparison.Ordinal));

        Assert.Matches(new Regex(@"Priority\? Priority = null", RegexOptions.None, TimeSpan.FromSeconds(1)), order);
    }

    private static string OrderModel(
        IIncrementalGenerator generator,
        string markerFile,
        string? yaml = null)
    {
        var compilation = CSharpCompilation.Create(
            "Orders.Host",
            [],
            CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi: true),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(
            [
                new CompilationVerificationHarness.InMemoryAdditionalText(yaml is null ? "Orders.yaml" : "Orders30.yaml", yaml ?? Yaml),
                new CompilationVerificationHarness.InMemoryAdditionalText(markerFile, "{}"),
            ])
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out _, CancellationToken.None);

        var sources = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(t => t.ToString()));
        var start = sources.IndexOf("record Order(", StringComparison.Ordinal);
        Assert.True(start >= 0, "no Order record generated");
        var docs = sources.LastIndexOf("/// <summary>", start, StringComparison.Ordinal);
        var end = sources.IndexOf("// <auto-generated />", start, StringComparison.Ordinal);
        return sources.Substring(docs, (end < 0 ? sources.Length : end) - docs);
    }
}