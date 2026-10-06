namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The host environment and a 5xx from an exception: in Production the body is a ProblemDetails that carries nothing
/// of the exception - not its message, its type, a stack frame or the handler's type name - on both the package's
/// host and the plain <c>MapEndpoints()</c> host; in Development the message is there to read.
/// </summary>
public partial class ErrorResponseBodyRuntimeTests
{
    private static readonly (string Mode, string Message, string TypeName)[] ExceptionProbes =
    [
        ("throw", "boom from handler", "InvalidDataException"),
        ("throw-not-implemented", "not implemented in handler", "NotImplementedException"),
    ];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Production_A5xxFromAnException_CarriesNothingOfIt_AndDevelopmentNamesIt(
        bool plainHost)
    {
        var hostName = plainHost ? "the MapEndpoints host" : "the Atc.Rest.MinimalApi host";
        foreach (var environment in (string[])["Production", "Development"])
        {
            await using var host = await ErrorBodyHost.StartAsync(plainHost: plainHost, environment: environment);
            using var http = new HttpClient { BaseAddress = host.BaseAddress };
            foreach (var (mode, message, typeName) in ExceptionProbes)
            {
                using var response = await http.GetAsync(new Uri($"faults/{mode}", UriKind.Relative), TestContext.Current.CancellationToken);
                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var label = $"{hostName}, {environment}, {mode}: {(int)response.StatusCode} {response.Content.Headers.ContentType}\n{body}";
                Assert.True((int)response.StatusCode >= 500, label);

                if (environment == "Production")
                {
                    // What a stranger must never read: the message, the exception type, a stack frame, the handler
                    string[] secrets = [message, typeName, "   at ", "RuntimeTestHandlers", "Exception"];
                    var leaked = secrets.Where(s => body.Contains(s, StringComparison.Ordinal)).ToList();
                    Assert.True(leaked.Count == 0, $"leaks {string.Join(", ", leaked.Select(l => $"'{l.Trim()}'"))} - {label}");
                    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
                    Assert.True(TryReadStatus(body, out var problemStatus) && problemStatus == (int)response.StatusCode, label);
                }
                else
                {
                    Assert.True(body.Contains(message, StringComparison.Ordinal), $"Development does not say what went wrong - {label}");
                }
            }
        }
    }
}