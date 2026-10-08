namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The OAuth token provider of a client reads the token endpoint's response as the serializer reads it with Web
/// defaults - member names in any case, a number in a string, unknown members skipped, a member of the wrong type an
/// error - so reading it without reflection (trim- and AOT-safe) changes nothing.
/// </summary>
public sealed class OAuthTokenResponseReadingTests
{
    private static readonly Lazy<Assembly> Client = new(
        () => CompilationVerificationHarness.EmitAndLoad(GeneratorSnapshotHarness.RunRawForCompilation("SecurityStandard", "Client-Typed")));

    [Theory]
    [InlineData("""{"access_token":"a","token_type":"Bearer","expires_in":3600,"refresh_token":"r","scope":"s"}""", "a|Bearer|3600|r|s")]
    [InlineData("""{"ACCESS_TOKEN":"a","Expires_In":"60","Token_Type":"mac"}""", "a|mac|60||")]
    [InlineData("""{"access_token":"a","token_type":null,"refresh_token":null}""", "a||0||")]
    [InlineData("""{"access_token":"a","extra":{"x":[1]},"expires_in":-5}""", "a|Bearer|-5||")]
    [InlineData("""{"access_token":"a","access_token":"b"}""", "b|Bearer|0||")]
    [InlineData("""null""", "(none)")]
    [InlineData("""{"access_token":"a","expires_in":1.5}""", "JsonException")]
    [InlineData("""{"access_token":"a","expires_in":null}""", "JsonException")]
    [InlineData("""{"access_token":5}""", "JsonException")]
    [InlineData("""[1]""", "JsonException")]
    [InlineData("", "JsonException")]
    [InlineData("""{"access_token":"a"} x""", "JsonException")]
    public async Task TheTokenResponse_IsReadAsTheSerializerReadsIt(
        string body,
        string expected)
        => Assert.Equal(expected, await RequestTokenAsync(body));

    private static async Task<string> RequestTokenAsync(string body)
    {
        var assembly = Client.Value;
        var optionsType = assembly.GetType("SecurityStandard.Generated.OAuth.OAuthClientOptions", throwOnError: true)!;
        var providerType = assembly.GetType("SecurityStandard.Generated.OAuth.OAuthTokenProvider", throwOnError: true)!;

        var options = Activator.CreateInstance(optionsType)!;
        var wrapped = typeof(Microsoft.Extensions.Options.Options)
            .GetMethod(nameof(Microsoft.Extensions.Options.Options.Create))!
            .MakeGenericMethod(optionsType)
            .Invoke(null, [options])!;

        using var endpoint = new TokenEndpoint(body);
        using var httpClient = new HttpClient(endpoint, disposeHandler: false);
        var provider = Activator.CreateInstance(providerType, httpClient, wrapped)!;
        var request = providerType.GetMethod("RequestTokenWithClientCredentialsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        try
        {
            var task = (Task)request.Invoke(provider, [options, CancellationToken.None])!;
            await task;
            var token = task.GetType().GetProperty("Result")!.GetValue(task);
            if (token is null)
            {
                return "(none)";
            }

            string? Get(string name)
                => token.GetType().GetProperty(name)!.GetValue(token)?.ToString();

            return $"{Get("AccessToken")}|{Get("TokenType")}|{Get("ExpiresIn")}|{Get("RefreshToken")}|{Get("Scope")}";
        }
        catch (JsonException)
        {
            return nameof(JsonException);
        }
    }

    private sealed class TokenEndpoint(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}