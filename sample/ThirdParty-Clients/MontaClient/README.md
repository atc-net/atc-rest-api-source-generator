# MontaClient

Demonstrates generating a .NET client library for an external API (Monta Partner API) using EndpointPerOperation mode with custom error handling and OAuth token management.

## Features

- **Custom error model**: `ErrorResponse` with status, message, readableMessage, errorCode, context
- **OAuth token management**: Auto-generated token refresh flow (`generateOAuthTokenManagement: true`)
- **Partial models**: Extend generated records with additional properties (`generatePartialModels: true`)
- **Named HTTP client**: `Constants.HttpClientName` resolves to `"Monta-ApiClient"`

## DI Wiring with Retry Policy

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using MontaPartner.ApiClient.Generated;

var services = new ServiceCollection();

// Register named HttpClient with resilience pipeline
services
    .AddHttpClient(Constants.HttpClientName, client =>
    {
        client.BaseAddress = new Uri("https://partner-api.monta.com");
    })
    .AddStandardResilienceHandler();

// Register all generated endpoint interfaces
services.AddChargePointsEndpoints();
services.AddChargesEndpoints();
services.AddSitesEndpoints();
services.AddTeamsEndpoints();
```

## Configuration

The `.atc-rest-api-client` marker configures generation:

```json
{
  "generationMode": "EndpointPerOperation",
  "httpClientName": "Monta-ApiClient",
  "generateOAuthTokenManagement": true,
  "generatePartialModels": true,
  "customErrorResponseModel": { ... }
}
```
