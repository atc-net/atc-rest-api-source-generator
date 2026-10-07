namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A problem a handler passes to a result factory is the problem the client reads, with the factory's status: a
/// <c>ValidationProblemDetails</c> keeps its <c>errors</c> (passed to <c>InternalServerError</c> or to a declared
/// problem overload, which take a <c>ProblemDetails</c>), and a problem type of the handler's own keeps its properties.
/// The factories used to rebuild a new problem from detail, instance, title, type and extensions only.
/// </summary>
[Collection(nameof(ProblemPassThroughWireTests))]
[CollectionDefinition(nameof(ProblemPassThroughWireTests), DisableParallelization = true)]
public sealed class ProblemPassThroughWireTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /things/{kind}:
            get:
              operationId: getThing
              parameters:
                - name: kind
                  in: path
                  required: true
                  schema: { type: string }
              responses:
                '200':
                  description: The thing
                  content:
                    application/json:
                      schema: { type: string }
                '400':
                  description: Invalid
                '409':
                  description: Out of stock
                  content:
                    application/problem+json:
                      schema:
                        $ref: '#/components/schemas/ProblemDetails'
                '500':
                  description: Failed
        components:
          schemas:
            ProblemDetails:
              type: object
              properties:
                type: { type: string }
                title: { type: string }
                status: { type: integer }
                detail: { type: string }
                instance: { type: string }
        """;

    private const string HandlerSource = """"
        using Microsoft.AspNetCore.Mvc;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class OutOfStockProblem : ProblemDetails
        {
            public int Remaining { get; set; }
        }

        public sealed class GetThingHandlerImpl : IGetThingHandler
        {
            public System.Threading.Tasks.Task<GetThingResult> ExecuteAsync(GetThingParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                var errors = new ValidationProblemDetails(new Dictionary<string, string[]> { ["name"] = ["Name is required."] }) { Detail = "invalid" };
                return System.Threading.Tasks.Task.FromResult(parameters.Kind switch
                {
                    "bad" => GetThingResult.BadRequest(errors),
                    "conflict-validation" => GetThingResult.Conflict(errors),
                    "conflict-own" => GetThingResult.Conflict(new OutOfStockProblem { Detail = "gone", Remaining = 3 }),
                    "failed" => GetThingResult.InternalServerError(errors),
                    _ => GetThingResult.Ok("thing"),
                });
            }
        }

        public static class ThingRegistration
        {
            public static void Register(IServiceCollection services)
                => services.AddScoped<IGetThingHandler, GetThingHandlerImpl>();
        }
        """";

    [Theory]
    [InlineData("bad", 400)]
    [InlineData("conflict-validation", 409)]
    [InlineData("failed", 500)]
    public async Task AValidationProblem_KeepsItsErrors_WithTheFactorysStatus(
        string kind,
        int expectedStatus)
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.ThingRegistration");

        var (status, _, body) = await host.SendRawAsync(HttpMethod.Get, "/things/" + kind, accept: "application/json");

        Assert.True(status == expectedStatus, $"{status}: {body}");
        var problem = JsonNode.Parse(body)!;
        Assert.Equal("Name is required.", (string?)problem["errors"]?["name"]?[0]);
        Assert.Equal("invalid", (string?)problem["detail"]);
        Assert.Equal(expectedStatus, (int?)problem["status"]);
    }

    [Fact]
    public async Task AProblemOfTheHandlersOwn_KeepsItsProperties()
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.ThingRegistration");

        var (status, _, body) = await host.SendRawAsync(HttpMethod.Get, "/things/conflict-own", accept: "application/json");

        Assert.True(status == 409, $"{status}: {body}");
        var problem = JsonNode.Parse(body)!;
        Assert.Equal(3, (int?)problem["remaining"]);
        Assert.Equal("gone", (string?)problem["detail"]);
        Assert.Equal(409, (int?)problem["status"]);
    }
}