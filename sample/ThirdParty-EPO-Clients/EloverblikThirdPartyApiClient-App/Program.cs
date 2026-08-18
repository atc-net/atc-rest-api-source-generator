// This sample demonstrates how to wire up and use a client generated with
//   "generationMode": "EndpointPerOperation"
//
// In this mode the generator emits one endpoint class per OpenAPI operation
// (for example GetThirdpartyapiApiIsaliveEndpoint) behind a matching interface.
// Every endpoint is resolved from dependency injection and talks to a *named*
// HttpClient created by IHttpClientFactory. The generated Constants.HttpClientName
// carries the name configured via "httpClientName" in the .atc-rest-api-client
// marker file, so registration and consumption can never drift apart.
Console.WriteLine("Eloverblik ThirdParty API - EndpointPerOperation demo");
Console.WriteLine("=====================================================");
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

// AddEloverblikApiThirdPartyApiEndpoints() is generated. It registers every
// I<Operation>Endpoint -> <Operation>Endpoint pair and calls AddAtcRestClientCore()
// so IHttpMessageFactory and IContractSerializer are available.
builder.Services.AddEloverblikApiThirdPartyApiEndpoints();

// Holds whichever bearer token is currently active (refresh token first, then the
// issued access token). The handler below reads it on every outgoing request.
builder.Services.AddSingleton<BearerTokenProvider>();
builder.Services.AddTransient<BearerTokenHandler>();

// The endpoints ask IHttpClientFactory for a client named Constants.HttpClientName.
// Registering it here is the single place where transport concerns are configured:
// base address, timeouts, auth and (optionally) resilience handlers.
builder.Services
    .AddHttpClient(
        Constants.HttpClientName,
        client =>
        {
            client.BaseAddress = new Uri(baseAddress);
            client.Timeout = TimeSpan.FromSeconds(100);
        })
    .AddHttpMessageHandler<BearerTokenHandler>();

using var host = builder.Build();

// ---------------------------------------------------------------------------
// 2. Resolve the generated endpoints
// ---------------------------------------------------------------------------
var isAliveEndpoint = host.Services.GetRequiredService<IGetThirdpartyapiApiIsaliveEndpoint>();
var tokenEndpoint = host.Services.GetRequiredService<IGetThirdpartyapiApiTokenEndpoint>();
var meteringPointsEndpoint = host.Services.GetRequiredService<IGetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierEndpoint>();
var detailsEndpoint = host.Services.GetRequiredService<IPostThirdpartyapiApiMeteringpointGetdetailsEndpoint>();
var tokenProvider = host.Services.GetRequiredService<BearerTokenProvider>();

try
{
    // -----------------------------------------------------------------------
    // 3. GET /thirdpartyapi/api/isalive - no auth required
    // -----------------------------------------------------------------------
    // Each endpoint exposes a single ExecuteAsync taking a generated parameters
    // record. The result is a status wrapper: IsOk / IsUnauthorized / ... with a
    // matching <Status>Content property exposing the typed payload.
    Console.WriteLine("1. GET /thirdpartyapi/api/isalive");

    var isAliveResult = await isAliveEndpoint
        .ExecuteAsync(new GetThirdpartyapiApiIsaliveParameters())
        .ConfigureAwait(false);

    if (isAliveResult.IsOk)
    {
        Console.WriteLine($"   API is alive: {isAliveResult.OkContent}");
    }
    else
    {
        // Never assume success - inspect the failure branch before reading content.
        Console.WriteLine($"   Unexpected status: {isAliveResult.StatusCode}");
    }

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

    var tokenResult = await tokenEndpoint
        .ExecuteAsync(new GetThirdpartyapiApiTokenParameters())
        .ConfigureAwait(false);

    if (!tokenResult.IsOk)
    {
        Console.WriteLine($"   Could not acquire access token: {tokenResult.StatusCode}");
        return;
    }

    Console.WriteLine("   Access token acquired.");
    Console.WriteLine();

    // Swap in the freshly issued access token so all subsequent calls are authorized.
    tokenProvider.Token = tokenResult.OkContent.Result;

    // -----------------------------------------------------------------------
    // 5. GET .../authorization/authorization/meteringpoints/{scope}/{identifier}
    // -----------------------------------------------------------------------
    Console.WriteLine("3. GET .../authorization/authorization/meteringpoints/{scope}/{identifier}");

    var scope = Environment.GetEnvironmentVariable("ELOVERBLIK_SCOPE") ?? "CVR";
    var identifier = Environment.GetEnvironmentVariable("ELOVERBLIK_IDENTIFIER") ?? "12345678";

    var meteringPointsResult = await meteringPointsEndpoint
        .ExecuteAsync(new GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierParameters(
            Scope: scope,
            Identifier: identifier))
        .ConfigureAwait(false);

    if (!meteringPointsResult.IsOk)
    {
        Console.WriteLine($"   Request failed: {meteringPointsResult.StatusCode}");
        return;
    }

    var meteringPoints = meteringPointsResult.OkContent.Result ?? [];
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

    var detailsResult = await detailsEndpoint
        .ExecuteAsync(new PostThirdpartyapiApiMeteringpointGetdetailsParameters(
            Request: new MeteringPointsRequest(
                new MeteringPoints(meteringPointIds))))
        .ConfigureAwait(false);

    if (!detailsResult.IsOk)
    {
        Console.WriteLine($"   Request failed: {detailsResult.StatusCode}");
        return;
    }

    var details = detailsResult.OkContent.Result ?? [];
    Console.WriteLine($"   Received details for {details.Count} metering point(s).");

    foreach (var detail in details)
    {
        Console.WriteLine($"   - {detail.Result?.MeteringPointId}: {detail.Result?.TypeOfMp}");
    }
}
catch (HttpRequestException ex)
{
    // Transport-level failures such as DNS, TLS or connection refused surface as
    // exceptions, while HTTP status codes are modelled on the result object instead.
    Console.WriteLine($"Network error: {ex.Message}");
}