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

## Overlay: fixes to the vendor's specification

`Monta.yaml` is used exactly as Monta publishes it. `Monta.overlay.yaml` - an
[OpenAPI Overlay](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Overlays) the marker
lists in `"overlays"` - adds `id` to the `required` lists of `Charge` and `Site`, so their generated `Id` is a `long`
instead of a `long?`. Replace `Monta.yaml` with a new version and the fix is applied again.

```bash
# See the specification the build generates from
atc-rest-api-gen spec overlay apply -s Monta.yaml --overlay Monta.overlay.yaml -o effective.yaml
```
