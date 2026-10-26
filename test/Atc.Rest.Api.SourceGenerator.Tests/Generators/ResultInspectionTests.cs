namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A handler test reads what an operation's result answers - <c>StatusCode</c>, <c>Value</c> and <c>Problem</c> on the
/// generated <c>{Operation}Result</c> - without casting to the framework's result types, whatever the result is wrapped
/// in (a <c>Location</c> header, a response header, a JSON type info).
/// </summary>
public sealed class ResultInspectionTests
{
    private const string Yaml = """
        openapi: 3.1.0
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
                '201':
                  description: Created
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Order'
                '400':
                  description: Bad Request
                '409':
                  description: Conflict
          /orders/{orderId}:
            parameters:
              - name: orderId
                in: path
                required: true
                schema:
                  type: string
            get:
              operationId: getOrder
              responses:
                '200':
                  description: OK
                  headers:
                    ETag:
                      schema:
                        type: string
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Order'
                '404':
                  description: Not Found
            delete:
              operationId: deleteOrder
              responses:
                '204':
                  description: No Content
        webhooks:
          orderShipped:
            post:
              operationId: onOrderShipped
              requestBody:
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Order'
              responses:
                '200':
                  description: OK
                '400':
                  description: Bad Request
        components:
          schemas:
            Order:
              type: object
              properties:
                id:
                  type: string
        """;

    private const string ProbeSource = """"
        namespace ErrorBodies.RuntimeTest;

        public static class ResultProbe
        {
            public static string[] Run()
            {
                var order = new Order(Id: "o-1");
                return
                [
                    Describe(CreateOrderResult.Created(order, "/orders/o-1")),
                    Describe(CreateOrderResult.Conflict("The order exists.")),
                    Describe(GetOrderResult.Ok(order, eTag: "\"v1\"")),
                    Describe(GetOrderResult.NotFound()),
                    Describe(DeleteOrderResult.NoContent()),
                ];
            }

            private static string Describe(object result)
            {
                dynamic r = result;
                int? status = r.StatusCode;
                object? value = r.Value;
                Microsoft.AspNetCore.Mvc.ProblemDetails? problem = r.Problem;
                var asInterface = ((Microsoft.AspNetCore.Http.IStatusCodeHttpResult)result).StatusCode;
                return $"{status}|{asInterface}|{(value is Order o ? "Order " + o.Id : value?.GetType().Name ?? "-")}|{problem?.Status}:{problem?.Detail ?? "-"}";
            }
        }
        """";

    [Fact]
    public void AResult_ReadsItsStatusValueAndProblem_ThroughAnyWrapper()
    {
        // Arrange
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        sources.Add(("ResultProbe.cs", WithUsings(sources, ProbeSource)));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);

        // Act
        var described = (string[])assembly.GetType("ErrorBodies.RuntimeTest.ResultProbe", throwOnError: true)!.GetMethod("Run")!.Invoke(null, null)!;

        // Assert
        Assert.Equal(
            [
                "201|201|Order o-1|:-",
                "409|409|ProblemDetails|409:The order exists.",
                "200|200|Order o-1|:-",
                "404|404|ProblemDetails|404:-",
                "204|204|-|:-",
            ],
            described);
    }

    [Fact]
    public void AWebhookResult_ReadsItsStatusValueAndProblem()
    {
        // Arrange
        const string probe = """
            namespace ErrorBodies.RuntimeTest;

            public static class WebhookProbe
            {
                public static string Run()
                {
                    var result = OnOrderShippedWebhookResult.BadRequest();
                    return $"{result.StatusCode}|{result.Problem?.Detail}|{((Microsoft.AspNetCore.Http.IValueHttpResult)result).Value?.GetType().Name}";
                }
            }
            """;
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        sources.Add(("WebhookProbe.cs", WithUsings(sources, probe)));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);

        // Act
        var described = (string)assembly.GetType("ErrorBodies.RuntimeTest.WebhookProbe", throwOnError: true)!.GetMethod("Run")!.Invoke(null, null)!;

        // Assert
        Assert.StartsWith("400||", described, StringComparison.Ordinal);
    }

    // The probe sees every generated namespace
    private static string WithUsings(
        List<(string HintName, string Source)> sources,
        string probe)
        => string.Join(
               Environment.NewLine,
               sources
                   .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
                   .Where(ns => ns.Length > 0)
                   .Distinct(StringComparer.Ordinal)
                   .Select(ns => $"global using {ns};")) +
           Environment.NewLine +
           probe;
}