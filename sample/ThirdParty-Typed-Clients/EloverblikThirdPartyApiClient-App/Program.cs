// This sample demonstrates how to wire up and use a client generated with
//   "generationMode": "TypedClient"
//
// In this mode the generator emits a single ThirdPartyApiClient class that takes
// an HttpClient in its constructor and exposes one async method per OpenAPI
// operation. Compared to EndpointPerOperation there is no DI registration
// extension and no named-client constant - the client is registered as a typed
// client, which is why AddHttpClient<ThirdPartyApiClient>() configures both the
// transport and the client registration in one call.
//
// The other notable difference: typed-client methods return the payload directly
// and throw HttpRequestException on a non-success status code, rather than
// returning a result wrapper with IsOk / IsUnauthorized branches.
Console.WriteLine("Eloverblik ThirdParty API - TypedClient demo");
Console.WriteLine("============================================");
Console.WriteLine();

// The public Eloverblik API. Override with the ELOVERBLIK_BASE_URL environment
// variable when pointing at a test environment.
var baseAddress = Environment.GetEnvironmentVariable("ELOVERBLIK_BASE_URL")
                  ?? "https://api.eloverblik.dk";

// Eloverblik authenticates with a long-lived refresh token that is exchanged for a
// short-lived access token. Supply it via ELOVERBLIK_REFRESH_TOKEN to run live.
var refreshToken = Environment.GetEnvironmentVariable("ELOVERBLIK_REFRESH_TOKEN");

Console.WriteLine($"Base address : {baseAddress}");
Console.WriteLine($"Refresh token: {(string.IsNullOrWhiteSpace(refreshToken) ? "<not set>" : "<provided>")}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 1. Composition root
// ---------------------------------------------------------------------------
var builder = Host.CreateApplicationBuilder(args);

// Holds whichever bearer token is currently active (refresh token first, then the
// issued access token). The handler below reads it on every outgoing request.
builder.Services.AddSingleton<BearerTokenProvider>();
builder.Services.AddTransient<BearerTokenHandler>();

// Registering ThirdPartyApiClient as a typed client lets IHttpClientFactory own
// the HttpClient lifetime and hand a correctly configured instance to the
// generated constructor. This is the single place where transport concerns are
// configured: base address, timeouts, auth and (optionally) resilience handlers.
builder.Services
    .AddHttpClient<ThirdPartyApiClient>(client =>
    {
        client.BaseAddress = new Uri(baseAddress);
        client.Timeout = TimeSpan.FromSeconds(100);
    })
    .AddHttpMessageHandler<BearerTokenHandler>();

using var host = builder.Build();

// ---------------------------------------------------------------------------
// 2. Resolve the generated client
// ---------------------------------------------------------------------------
var client = host.Services.GetRequiredService<ThirdPartyApiClient>();
var tokenProvider = host.Services.GetRequiredService<BearerTokenProvider>();

try
{
    // -----------------------------------------------------------------------
    // 3. GET /thirdpartyapi/api/isalive - no auth required
    // -----------------------------------------------------------------------
    // Every method takes a generated parameters record and returns the response
    // payload directly - here simply a bool.
    Console.WriteLine("1. GET /thirdpartyapi/api/isalive");

    var isAlive = await client
        .GetThirdpartyapiApiIsaliveAsync(new GetThirdpartyapiApiIsaliveParameters())
        .ConfigureAwait(false);

    Console.WriteLine($"   API is alive: {isAlive}");
    Console.WriteLine();

    if (string.IsNullOrWhiteSpace(refreshToken))
    {
        Console.WriteLine("Set ELOVERBLIK_REFRESH_TOKEN to run the authenticated part of this demo.");
        return;
    }

    // -----------------------------------------------------------------------
    // 4. GET /thirdpartyapi/api/token - exchange refresh token for access token
    // -----------------------------------------------------------------------
    Console.WriteLine("2. GET /thirdpartyapi/api/token");

    // The token endpoint expects the refresh token as the bearer credential.
    tokenProvider.Token = refreshToken;

    var tokenResponse = await client
        .GetThirdpartyapiApiTokenAsync(new GetThirdpartyapiApiTokenParameters())
        .ConfigureAwait(false);

    Console.WriteLine("   Access token acquired.");
    Console.WriteLine();

    // Swap in the freshly issued access token so all subsequent calls are authorized.
    tokenProvider.Token = tokenResponse.Result;

    // -----------------------------------------------------------------------
    // 5. GET .../authorization/authorization/meteringpoints/{scope}/{identifier}
    // -----------------------------------------------------------------------
    Console.WriteLine("3. GET .../authorization/authorization/meteringpoints/{scope}/{identifier}");

    var scope = Environment.GetEnvironmentVariable("ELOVERBLIK_SCOPE") ?? "CVR";
    var identifier = Environment.GetEnvironmentVariable("ELOVERBLIK_IDENTIFIER") ?? "12345678";

    var meteringPointsResponse = await client
        .GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierAsync(
            new GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierParameters(
                Scope: scope,
                Identifier: identifier))
        .ConfigureAwait(false);

    var meteringPoints = meteringPointsResponse.Result ?? [];
    Console.WriteLine($"   Found {meteringPoints.Count} metering point(s).");

    foreach (var meteringPoint in meteringPoints.Take(5))
    {
        Console.WriteLine($"   - {meteringPoint.MeteringPointId} ({meteringPoint.StreetName} {meteringPoint.BuildingNumber})");
    }

    Console.WriteLine();

    // -----------------------------------------------------------------------
    // 6. POST /thirdpartyapi/api/meteringpoint/meteringpoint/getdetails
    // -----------------------------------------------------------------------
    // Operations with a request body take it as the generated parameters record's
    // Request property - the shape mirrors the OpenAPI schema exactly.
    var meteringPointIds = meteringPoints
        .Where(x => !string.IsNullOrWhiteSpace(x.MeteringPointId))
        .Select(x => x.MeteringPointId!)
        .Take(3)
        .ToList();

    if (meteringPointIds.Count == 0)
    {
        Console.WriteLine("No metering points available to query details for.");
        return;
    }

    Console.WriteLine("4. POST /thirdpartyapi/api/meteringpoint/meteringpoint/getdetails");

    var detailsResponse = await client
        .PostThirdpartyapiApiMeteringpointGetdetailsAsync(
            new PostThirdpartyapiApiMeteringpointGetdetailsParameters(
                Request: new MeteringPointsRequest(
                    new MeteringPoints(meteringPointIds))))
        .ConfigureAwait(false);

    var details = detailsResponse.Result ?? [];
    Console.WriteLine($"   Received details for {details.Count} metering point(s).");

    foreach (var detail in details)
    {
        Console.WriteLine($"   - {detail.Result?.MeteringPointId}: {detail.Result?.TypeOfMp}");
    }
}
catch (HttpRequestException ex)
{
    // In TypedClient mode both transport failures and non-success HTTP status
    // codes surface here - the generated EnsureSuccessAsync throws with the
    // status code and response body included in the message.
    Console.WriteLine($"Request failed: {ex.Message}");
}