namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The generated scope and role checks against real signed JWTs read by <c>AddJwtBearer</c>, as an identity provider
/// issues them: scopes in a space-separated <c>scp</c> or <c>scope</c> claim, or as an array (one claim each); roles as
/// a <c>roles</c> array. With <c>MapInboundClaims</c> on, the handler renames <c>scp</c> and <c>roles</c> to the long
/// claim types; off, they keep their names - and then a role only counts when the host sets <c>RoleClaimType</c>
/// to <c>roles</c>, which is where consumers get a 403 in production. The guard matrix uses a plain-text token instead.
/// </summary>
public partial class ErrorResponseBodyRuntimeTests
{
    private static readonly SymmetricSecurityKey JwtKey = new(Encoding.UTF8.GetBytes("contoso-test-signing-key-of-at-least-256-bits!"));

    public static TheoryData<bool, string?, string, string, int> JwtCases
        => new()
        {
            // MapInboundClaims, RoleClaimType, the token's claims (JSON), the path, the expected status
            { true, null, """{"scp":"things:read"}""", "/guard/scope", 200 },
            { false, null, """{"scp":"things:read"}""", "/guard/scope", 200 },
            { true, null, """{"scope":"things:read things:write"}""", "/guard/scope", 200 },
            { false, null, """{"scope":"things:read things:write"}""", "/guard/scope", 200 },
            { true, null, """{"scp":["things:write","admin"]}""", "/guard/scopes", 200 },
            { false, null, """{"scp":["things:write","admin"]}""", "/guard/scopes", 200 },
            { true, null, """{"scp":"things:write"}""", "/guard/scopes", 403 },
            { false, null, """{"scp":"things:write"}""", "/guard/scope", 403 },
            { true, null, """{"roles":["admin"]}""", "/guard/role", 200 },
            { false, null, """{"roles":["admin"]}""", "/guard/role", 403 },
            { false, "roles", """{"roles":["admin"]}""", "/guard/role", 200 },
            { true, null, """{"roles":["reader"]}""", "/guard/role", 403 },
            { true, null, "{}", "/guard/bearer", 200 },
        };

    [Theory]
    [MemberData(nameof(JwtCases))]
    public async Task GeneratedPolicies_ReadScopesAndRolesFromARealJwt(
        bool mapInboundClaims,
        string? roleClaimType,
        string claims,
        string path,
        int expectedStatus)
    {
        await using var host = await XmlTestHost.StartAsync(
            GuardYaml,
            GuardHandlerSource,
            "ErrorBodies.RuntimeTest.GuardRegistration",
            configureServices: services => services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = mapInboundClaims;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = "https://login.contoso.example",
                        ValidAudience = "api://things",
                        IssuerSigningKey = JwtKey,
                        RoleClaimType = roleClaimType ?? ClaimTypes.Role,
                    };
                })
                .AddScheme<AuthenticationSchemeOptions, GuardApiKeyHandler>("api_key", _ => { }));

        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path.TrimStart('/'), UriKind.Relative));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", IssueToken(claims));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, (int)response.StatusCode);
    }

    private static string IssueToken(string claimsJson)
    {
        var claims = JsonNode.Parse(claimsJson)!.AsObject().ToDictionary(
            c => c.Key,
            c => c.Value is JsonArray array ? (object)array.Select(v => (string)v!).ToList() : (string)c.Value!,
            StringComparer.Ordinal);
        claims["sub"] = "contoso-user";

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "https://login.contoso.example",
            Audience = "api://things",
            Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = claims,
            SigningCredentials = new SigningCredentials(JwtKey, SecurityAlgorithms.HmacSha256),
        });
    }
}