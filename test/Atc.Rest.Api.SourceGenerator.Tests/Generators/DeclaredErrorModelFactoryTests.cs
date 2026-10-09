namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A handler's factory for an error status whose specification declares a model: called without the model, the
/// response has no body of its own and the host writes its ProblemDetails. A client reading the declared model then finds
/// it empty - unless the model can hold that ProblemDetails (it has a <c>title</c>, <c>status</c> or <c>detail</c>). So the
/// model is required, except for such a model, where the call without it stays.
/// </summary>
public sealed class DeclaredErrorModelFactoryTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /things/{thingId}:
            get:
              operationId: getThing
              parameters:
                - name: thingId
                  in: path
                  required: true
                  schema: { type: string }
              responses:
                '204':
                  description: Found
                '400':
                  description: Bad Request
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/ApiError' }
                '404':
                  description: Not Found
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/ApiError' }
                '409':
                  description: Conflict
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/ApiProblem' }
                '429':
                  description: Too Many Requests
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/ApiError' }
                '503':
                  description: Unavailable
                  content:
                    application/json:
                      schema: { type: string }
        components:
          schemas:
            ApiError:
              type: object
              properties:
                code: { type: string }
                message: { type: string }
            ApiProblem:
              type: object
              properties:
                title: { type: string }
                detail: { type: string }
                code: { type: string }
        """;

    [Theory]
    [InlineData("Thing.NotFound()", false)]
    [InlineData("Thing.BadRequest()", false)]
    [InlineData("Thing.TooManyRequests()", false)]
    [InlineData("Thing.TooManyRequests(30)", false)]
    [InlineData("Thing.NotFound(new ApiError())", true)]
    [InlineData("Thing.BadRequest(new ApiError())", true)]
    [InlineData("Thing.TooManyRequests(new ApiError(), 30)", true)]
    [InlineData("Thing.Conflict()", true)]
    [InlineData("Thing.Conflict(new ApiProblem())", true)]
    [InlineData("Thing.ServiceUnavailable()", true)]
    public void Server_DeclaredModel_IsRequired_UnlessItCanHoldAProblemDetails(
        string call,
        bool compiles)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        var apiError = ModelConstructorDefaults(sources, "ApiError");
        var apiProblem = ModelConstructorDefaults(sources, "ApiProblem");
        sources.Add((
            "Call.cs",
            $$"""
              using ErrorBodies.Generated.Models;
              using Thing = ErrorBodies.Generated.Results.GetThingResult;

              namespace ErrorBodies.CompatCheck;

              public static class Call
              {
                  public static object Make()
                      => {{call.Replace("new ApiError()", $"new ApiError({apiError})", StringComparison.Ordinal).Replace("new ApiProblem()", $"new ApiProblem({apiProblem})", StringComparison.Ordinal)}};
              }
              """));

        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true);

        Assert.True(compiles == (errors.Count == 0), $"{call}: {string.Join(Environment.NewLine, errors)}");
    }

    // The arguments of a model's positional constructor, each the default of its type
    private static string ModelConstructorDefaults(
        List<(string HintName, string Source)> sources,
        string model)
    {
        var source = sources.Single(s => s.Source.Contains($"record {model}(", StringComparison.Ordinal)).Source;
        var start = source.IndexOf($"record {model}(", StringComparison.Ordinal) + model.Length + 8;
        var count = source[start..source.IndexOf(')', start)].Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
        return string.Join(", ", Enumerable.Repeat("default", count));
    }
}