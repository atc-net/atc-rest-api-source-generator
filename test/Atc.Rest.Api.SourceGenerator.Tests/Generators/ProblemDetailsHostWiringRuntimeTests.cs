namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Runtime characterization of what <c>AddProblemDetails()</c> and <c>UseStatusCodePages()</c> change
/// for the error responses a generated API can produce outside its handlers' typed bodies: framework
/// binding failures, unmatched routes, wrong methods, and handler results with an empty body.
/// </summary>
/// <remarks>
/// Input for the decision whether the generated <c>Add{Project}Api()</c> / <c>Map{Project}Api()</c>
/// should wire these up.
/// </remarks>
public class ProblemDetailsHostWiringRuntimeTests
{
    [Fact(Explicit = true)]
    public async Task Report_HostWiringMatrix()
    {
        var path = ErrorResponseBodyRuntimeTests.HandRunOutputPath("ERRORBODY_WIRING_REPORT_FILE", "errorbody-wiring.md");

        var sb = new StringBuilder();
        sb.AppendLine("| AddProblemDetails | UseStatusCodePages | Case | Status | Content-Type | Body |");
        sb.AppendLine("|---|---|---|---|---|---|");

        foreach (var (addProblemDetails, useStatusCodePages) in new[] { (false, false), (true, false), (false, true), (true, true) })
        {
            foreach (var (name, status, contentType, body) in await RunAsync(addProblemDetails, useStatusCodePages))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {addProblemDetails} | {useStatusCodePages} | {name} | {status} | {contentType} | `{body.Replace("|", "\\|", StringComparison.Ordinal)}` |");
            }
        }

        await File.WriteAllTextAsync(path, sb.ToString(), TestContext.Current.CancellationToken);
    }

    private static async Task<List<(string Name, int Status, string ContentType, string Body)>> RunAsync(
        bool addProblemDetails,
        bool useStatusCodePages)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        if (addProblemDetails)
        {
            builder.Services.AddProblemDetails();
        }

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        if (useStatusCodePages)
        {
            app.UseStatusCodePages();
        }

        app.MapPut("/items/{id}", ([FromBody] WiringRequest request) => TypedResults.NoContent());
        app.MapGet("/empty-404", () => TypedResults.NotFound());
        app.MapGet("/message-404", () => TypedResults.NotFound("item was not found"));
        app.MapGet("/empty-422", () => TypedResults.UnprocessableEntity());
        app.MapGet("/unauthorized", () => TypedResults.Unauthorized());
        app.MapGet("/problem-422", () => TypedResults.Problem("reason is not allowed", statusCode: 422));

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
            var results = new List<(string Name, int Status, string ContentType, string Body)>();

            var cases = new (string Name, string Method, string Path, string? Body)[]
            {
                ("malformed JSON body", "PUT", "/items/1", "{"),
                ("missing body", "PUT", "/items/1", null),
                ("unknown route", "GET", "/does-not-exist", null),
                ("wrong method", "GET", "/items/1", null),
                ("handler NotFound()", "GET", "/empty-404", null),
                ("handler NotFound(message)", "GET", "/message-404", null),
                ("handler UnprocessableEntity()", "GET", "/empty-422", null),
                ("handler Unauthorized()", "GET", "/unauthorized", null),
                ("handler Problem(422)", "GET", "/problem-422", null),
            };

            foreach (var (name, method, requestPath, requestBody) in cases)
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), requestPath);
                if (requestBody is not null)
                {
                    request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                }

                using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                results.Add((
                    name,
                    (int)response.StatusCode,
                    response.Content.Headers.ContentType?.ToString() ?? "(none)",
                    body.Length == 0 ? "(empty)" : body));
            }

            return results;
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
            await app.DisposeAsync();
        }
    }

    private sealed record WiringRequest(
        string? Reason);
}