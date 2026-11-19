# PetStoreFull.Aspire

This project is the Aspire 17 AppHost that orchestrates the PetStoreFull distributed application.

## Architecture

The AppHost manages two projects:
- **api**: The PetStoreFull.Api web API project
- **client**: The PetStoreFull.ClientApp console application

## Configuration

The AppHost is configured in `Program.cs` with the following setup:

```csharp
var api = builder.AddDotnetProject("api", "../PetStoreFull.Api/PetStoreFull.Api.csproj")
    .WithExternalHttpEndpoints();

var client = builder.AddDotnetProject("client", "../PetStoreFull.ClientApp/PetStoreFull.ClientApp.csproj")
    .WithReference(api);
```

## Service Discovery

When run through Aspire, the client application automatically discovers the API endpoint via environment variables injected by Aspire:
- `services__api__http__0` or `services__api__https__0`

The client's `Program.cs` reads this environment variable to configure the HttpClient base address.

## Running the Application

To run the application via Aspire orchestration:

Running a C# AppHost on Aspire 17 needs the [Aspire CLI](https://aspire.dev/get-started/install-cli/)
installed: the dashboard and the developer control plane come from its bundle.

```bash
dotnet run --project sample/PetStoreFull/PetStoreFull.Aspire
```

This will start both the API and the client with proper service discovery configured.

## Aspire 17

This AppHost uses the Aspire 17 project layout:
- **Plain SDK**: `<Project Sdk="Microsoft.NET.Sdk">` with explicit `Aspire.Hosting.AppHost` and `Aspire.Hosting.Dotnet`
  package references (the `Aspire.AppHost.Sdk` is obsolete in Aspire 17)
- **Projects by path**: each service is added with `AddDotnetProject(name, path)`, so the AppHost has no
  `ProjectReference` to the services and no generated `Projects.*` types
- **Service references**: Automatic endpoint injection for service discovery
- **RunAsync**: Using the async Run method for better async/await patterns

## Dashboard

The Aspire Dashboard provides observability and monitoring for the distributed application. It requires proper OTLP endpoint configuration in `launchSettings.json`.

For development without the dashboard, you can run each project independently.
