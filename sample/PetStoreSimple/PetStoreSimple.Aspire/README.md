# PetStoreSimple.Aspire

This project is the .NET Aspire v13 AppHost that orchestrates the PetStoreSimple distributed application.

## Architecture

The AppHost manages two projects:
- **api**: The PetStoreSimple.Api web API project (Native AOT-enabled with `PublishAot=true`)
- **client**: The PetStoreSimple.ClientApp console application

## Configuration

The AppHost is configured in `Program.cs` with the following setup:

```csharp
var api = builder.AddProject<Projects.PetStoreSimple_Api>("api")
    .WithExternalHttpEndpoints();

var client = builder.AddProject<Projects.PetStoreSimple_ClientApp>("client")
    .WithReference(api);
```

## Service Discovery

When run through Aspire, the client application automatically discovers the API endpoint via environment variables injected by Aspire:
- `services__api__http__0` or `services__api__https__0`

The client's `Program.cs` reads this environment variable to configure the HttpClient base address.

## Running the Application

To run the application via Aspire orchestration:

```bash
dotnet run --project sample/PetStoreSimple/PetStoreSimple.Aspire
```

This will start both the API and the client with proper service discovery configured.

## Aspire v13 Features

This project uses .NET Aspire v13 features:
- **New SDK format**: `<Project Sdk="Aspire.AppHost.Sdk/13.1.0">`
- **Simplified project references**: The `Aspire.Hosting.AppHost` package is implicit in the SDK
- **Service references**: Automatic endpoint injection for service discovery
- **RunAsync**: Using the async Run method for better async/await patterns

## Dashboard

The Aspire Dashboard provides observability and monitoring for the distributed application. It requires proper OTLP endpoint configuration in `launchSettings.json`.

For development without the dashboard, you can run each project independently.
