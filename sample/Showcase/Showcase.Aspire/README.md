# Showcase.Aspire

This project is the .NET Aspire v13 AppHost that orchestrates the Showcase distributed application.

## Architecture

The AppHost manages three projects:
- **api**: The Showcase.Api web API project with CORS support
- **blazor**: The Showcase.BlazorApp Blazor WebAssembly client with MudBlazor UI
- **client**: The Showcase.ClientApp console application (optional)

## Configuration

The AppHost is configured in `Program.cs` with the following setup:

```csharp
var api = builder
    .AddProject<Projects.Showcase_Api>("api")
    .WithExternalHttpEndpoints();

builder
    .AddProject<Projects.Showcase_BlazorApp>("blazor")
    .WithReference(api)
    .WithExternalHttpEndpoints();

builder
    .AddProject<Projects.Showcase_ClientApp>("client")
    .WithReference(api);
```

## Service Discovery

When run through Aspire:

### Blazor WebAssembly Client
The BlazorApp receives the API endpoint via Aspire configuration:
- Configuration key: `services:api:https:0` or `services:api:http:0`
- Falls back to `ApiBaseAddress` from `wwwroot/appsettings.json`
- Final fallback: `https://localhost:5046`

### Console Client
The ClientApp automatically discovers the API endpoint via environment variables:
- `services__api__http__0` or `services__api__https__0`

## Running the Application

To run the application via Aspire orchestration:

```bash
dotnet run --project sample/Showcase/Showcase.Aspire
```

This will start:
1. **Showcase.Api** - REST API with Accounts, Tasks, and Files endpoints
2. **Showcase.BlazorApp** - Interactive web UI with MudBlazor components
3. **Showcase.ClientApp** - Console application for API testing

## Features

### API Features
- **Accounts**: CRUD + IAsyncEnumerable streaming + pagination
- **Tasks**: Basic CRUD operations
- **Files**: Binary file uploads (single, multiple, with metadata)
- **CORS**: Configured for cross-origin Blazor WebAssembly requests

### Blazor WebAssembly Features
- **MudBlazor v8**: Material Design component library
- **Light/Dark Mode**: Toggle with system preference detection
- **Navigation Menu**: Organized by API path segments
- **Interactive Pages**: For all API operations
- **File Upload Demos**: Single, multiple, and with metadata
- **IAsyncEnumerable**: Real-time streaming support

## Aspire v13 Features

This project uses .NET Aspire v13 features:
- **New SDK format**: `<Project Sdk="Aspire.AppHost.Sdk/13.1.0">`
- **Simplified project references**: The `Aspire.Hosting.AppHost` package is implicit in the SDK
- **Service references**: Automatic endpoint injection for service discovery
- **RunAsync**: Using the async Run method for better async/await patterns
- **External HTTP endpoints**: Both API and Blazor app exposed externally

## Dashboard

The Aspire Dashboard provides observability and monitoring for the distributed application. It requires proper OTLP endpoint configuration in `launchSettings.json`.

For development without the dashboard, you can run each project independently:
```bash
# Terminal 1: API
dotnet run --project sample/Showcase/Showcase.Api

# Terminal 2: Blazor App
dotnet run --project sample/Showcase/Showcase.BlazorApp
```
