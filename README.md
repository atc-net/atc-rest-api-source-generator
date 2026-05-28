# 🚀 Atc.Rest.Api.SourceGenerator

A Roslyn Source Generator that automatically generates REST API server and client code from OpenAPI specifications. **No CLI commands, no manual code generation** - just build your project and get production-ready code.

## ✨ Key Features

| Area       | Feature                          | Description                                                                      |
|------------|----------------------------------|----------------------------------------------------------------------------------|
| **Core**   | 🔧 **Zero Configuration**        | Add the NuGet package, drop your YAML file, build                                |
|            | 🏗️ **Minimal API**               | Generates modern ASP.NET Core minimal API endpoints                              |
|            | 🔒 **Contract-Enforced Results** | Handlers can **only** return responses defined in OpenAPI - compile-time safety! |
|            | ✅ **Validation**                | Automatic `[Required]`, `[Range]`, `[StringLength]` from OpenAPI constraints     |
| **Server** | 📝 **Handler Scaffolds**         | Auto-generates handler stubs for unimplemented operations                        |
|            | 🔐 **Security**                  | JWT, OAuth2 scopes, API Key, role-based auth - all from OpenAPI                  |
|            | ⏱️ **Rate Limiting**             | Server-side rate limiting from `x-ratelimit-*` extensions                        |
|            | 💾 **Caching**                   | Output caching and HybridCache support from `x-cache-*` extensions               |
| **Client** | 🔌 **Type-Safe Client**          | Strongly-typed HTTP client with full IntelliSense                                |
|            | 🔄 **Resilience**                | Client-side retry/circuit breaker from `x-retry-*` extensions                    |
|            | 🔗 **URL Encoding**              | Automatic RFC 3986 compliant encoding - no more broken URLs                      |
| **Models** | 🎯 **Enum Support**              | Generates C# enums from OpenAPI string enums                                     |
|            | 📊 **Pagination**                | Generic `PaginatedResult<T>` from `allOf` composition                            |
| **Data**   | 📁 **File Uploads**              | Full support for binary uploads (single, multiple, with metadata)                |
|            | 🌊 **Streaming**                 | `IAsyncEnumerable<T>` support for efficient data streaming                       |
| **CLI**    | 🖥️ **Project Scaffolding**       | `generate server` / `generate client` creates complete project structure         |
|            | 🟦 **TypeScript Client**         | `generate client-typescript` — Fetch/Axios, interceptors, retry, MSW handlers    |
|            | ⚛️ **React Hooks**               | React Query (`useQuery`/`useMutation`/`useInfiniteQuery`/`useSuspenseQuery`) + SWR — streaming hooks for `IAsyncEnumerable` |
|            | 🪪 **Branded ID types**          | `--branded-ids` emits nominal types — `getUser(petId)` becomes a compile error  |
|            | 🔍 **Runtime Zod validation**    | `--zod-runtime-validate` wires Zod into response parsing — spec-drift detection  |
|            | 🔌 **SignalR Hub Hooks**         | `x-signalr-hubs` extension → typed `use<Hub>Hub()` with auto-reconnect          |
|            | 📋 **Spec Validation**           | `spec validate` validates OpenAPI specs with strict/standard modes               |
|            | 🔗 **Multi-Part Specs**          | `spec merge` / `spec split` for large API specifications                         |
|            | ⚙️ **Options Management**        | `options create` / `options validate` for configuration files                    |
|            | 🔄 **Migration**                 | `migrate validate` / `migrate execute` to migrate from old CLI generator         |

## 📦 Quick Setup

### 1. Install the Package

```bash
dotnet add package Atc.Rest.Api.SourceGenerator
```

### 2. Add Your OpenAPI Spec

Add your `*.yaml` file and marker file to `.csproj`:

```xml
<ItemGroup>
  <AdditionalFiles Include="MyApi.yaml" />
  <AdditionalFiles Include=".atc-rest-api-server" />
</ItemGroup>
```

### 3. Create Marker Files

For a typical three-layer architecture, use different marker files per project:

| Project      | Marker File                     | Purpose                                          |
|--------------|---------------------------------|--------------------------------------------------|
| **Contracts** | `.atc-rest-api-server`          | Models, handler interfaces, endpoints, DI        |
| **Domain**    | `.atc-rest-api-server-handlers` | Handler implementation scaffolds                 |
| **Client**    | `.atc-rest-api-client`          | Type-safe HTTP client                            |

**Contracts project** - `.atc-rest-api-server`:

```json
{
  "generate": true,
  "validateSpecificationStrategy": "Standard"
}
```

**Domain project** - `.atc-rest-api-server-handlers`:

```json
{
  "generate": true,
  "generateHandlersOutput": "Handlers"
}
```

> 💡 See [Marker Files Reference](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Marker-Files) for all configuration options.

### 4. Build and Use

```bash
dotnet build
```

Generated code includes:

- 📦 **Models** - C# records with validation attributes
- 🔌 **Handler Interfaces** - `IListPetsHandler`, `ICreatePetHandler`, etc.
- 🛣️ **Endpoints** - Minimal API `MapGet`, `MapPost`, etc.
- 🔧 **DI Extensions** - `AddApiHandlersFromDomain()`, `MapApiEndpoints()`

### 5. Implement Handlers

```csharp
public class GetPetByIdHandler : IGetPetByIdHandler
{
    public async Task<GetPetByIdResult> ExecuteAsync(
        GetPetByIdParameters parameters,
        CancellationToken ct)
    {
        var pet = await repository.GetByIdAsync(parameters.PetId, ct);

        // ✅ Clean syntax with implicit operator
        if (pet is not null)
            return pet;  // Implicitly converts to GetPetByIdResult.Ok(pet)

        // ✅ Factory methods for error responses
        return GetPetByIdResult.NotFound();

        // ❌ COMPILE ERROR - Method doesn't exist!
        // return GetPetByIdResult.InternalServerError();
    }
}
```

> 💡 **Type-Safe Results**: Result classes have private constructors and factory methods matching your OpenAPI responses. You literally cannot return a status code that isn't in your spec!

### 6. Wire Up Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddApiHandlersFromDomain();

var app = builder.Build();

app.MapOpenApi();
app.MapApiEndpoints();

app.Run();
```

## 📋 Requirements

- **.NET 10** or later
- **OpenAPI 3.0.x** or **3.1.x** specification

### Optional Dependencies

For `EndpointPerOperation` client generation mode:

```xml
<PackageReference Include="Atc" Version="3.*" />
<PackageReference Include="Atc.Rest.Client" Version="2.*" />
```

## 🏗️ Architecture

The project uses a **three-layer architecture**:

| Layer          | Project                        | Description                                                                 |
|----------------|--------------------------------|-----------------------------------------------------------------------------|
| **Shared**     | `Atc.Rest.Api.Generator`       | All 12 extractors, services, configuration, validation (Roslyn-independent) |
| **Generators** | `Atc.Rest.Api.SourceGenerator` | Roslyn source generators (thin wrappers calling shared services)            |
| **CLI**        | `Atc.Rest.Api.Generator.Cli`    | Command-line tool for validation and generation                             |

**Benefits**: Testability, reusability across CLI and source generators, clear separation of concerns.

## 🖥️ CLI Tool

> **[Installation Guide](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-CLI#-installation)**

The `atc-rest-api-gen` CLI provides a guided experience for project setup and validation:

```bash
# Generate server project (contracts + domain)
atc-rest-api-gen generate server -s api.yaml -o output/MyApp.Api.Contracts -n MyApp.Api.Contracts

# Generate client project
atc-rest-api-gen generate client -s api.yaml -o output/MyApp.Client -n MyApp.Client

# Generate TypeScript client (fetch or Axios, with optional hooks/Zod/scaffold)
atc-rest-api-gen generate client-typescript -s api.yaml -o ./src/api --hooks ReactQuery

# Validate OpenAPI specification
atc-rest-api-gen spec validate -s api.yaml

# Create default configuration file
atc-rest-api-gen options create -o ./
```

See [Working with the CLI](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-CLI) for full documentation.

## 📚 WIKI - Documentation

Read the full documentation on the [WIKI](https://github.com/atc-net/atc-rest-api-source-generator/wiki)

| Document                                                                                                                          | Description                                  |
|-----------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------|
| [🚀 Getting Started](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Getting-Started-with-Basic)                    | Detailed setup guide with examples           |
| [🖥️ Getting Started with CLI](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Getting-Started-with-CLI)            | Quick start guide using CLI scaffolding      |
| [🔄 Migration Guide](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Migration-Guide)                               | Migrate from old `atc-rest-api-generator`    |
| [⚙️ Working with the CLI](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-CLI)                         | Full CLI command reference                   |
| [📖 Working with OpenAPI](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-OpenAPI)                     | YAML patterns and generated output           |
| [🔐 Working with Security](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Security)                   | JWT, OAuth2, API Key authentication          |
| [✅ Working with Validations](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Validations)             | Request validation from OpenAPI constraints  |
| [⏱️ Working with Rate Limiting](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Rate-Limiting)         | Server-side rate limiting configuration      |
| [🔄 Working with Resilience](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Resilience)               | Client-side retry/circuit breaker patterns   |
| [💾 Working with Caching](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Caching)                     | Output caching and HybridCache configuration |
| [🔢 Working with Versioning](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Versioning)               | API versioning strategies                    |
| [📋 Analyzer Rules](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Analyzer-Rules)                                 | OpenAPI validation rules reference           |
| [🟦 Working with TypeScript Client](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-TypeScript-Client) | TypeScript/React client generation           |
| [🎪 Showcase Demo](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Showcase-Demo)                                   | Full-featured demo with Blazor & React UI    |
| [❓ FAQ & Troubleshooting](https://github.com/atc-net/atc-rest-api-source-generator/wiki/FAQ-and-Troubleshooting)                  | Common issues and solutions                  |
| [🗺️ Roadmap](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Roadmap)                                              | Planned features and status                  |
| [🔧 Development Notes](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Development-Notes)                           | For contributors                             |

## 🎯 Generated from This YAML

```yaml
paths:
  /pets:
    get:
      operationId: listPets
      parameters:
        - name: limit
          in: query
          schema:
            type: integer
            maximum: 100
      responses:
        '200':
          content:
            application/json:
              schema:
                type: array
                items:
                  $ref: '#/components/schemas/Pet'
```

## ⬇️ To This C# Code

```csharp
// Generated model
public record Pet(
    [property: Required] long Id,
    [property: Required] string Name,
    string? Tag);

// Generated parameters
public class ListPetsParameters
{
    [FromQuery(Name = "limit")]
    [Range(long.MinValue, 100)]
    public int? Limit { get; set; }
}

// Generated handler interface
public interface IListPetsHandler
{
    Task<ListPetsResult> ExecuteAsync(
        ListPetsParameters parameters,
        CancellationToken ct = default);
}

// Generated result - ONLY factory methods for responses in OpenAPI!
public class ListPetsResult : IResult
{
    private ListPetsResult(IResult inner) { ... }  // Private!

    public static ListPetsResult Ok(Pet[] response) => ...
    public static implicit operator ListPetsResult(Pet[] r) => Ok(r);
    // No InternalServerError(), Unauthorized(), etc. - they don't exist!
}

// Generated endpoint
endpoints.MapGet("/pets", async (
    [FromServices] IListPetsHandler handler,
    [AsParameters] ListPetsParameters parameters,
    CancellationToken ct)
    => await handler.ExecuteAsync(parameters, ct));
```

## 🏷️ [Marker Files](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Marker-Files)

| File                                                                                                                                        | Purpose                                   |
|---------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------|
| [`.atc-rest-api-server`](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Marker-Files#-server-contracts-atc-rest-api-server)                    | Server code (models, endpoints, handlers) |
| [`.atc-rest-api-server-handlers`](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Marker-Files#-server-handlers-atc-rest-api-server-handlers)  | Handler implementation scaffolds          |
| [`.atc-rest-api-client`](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Marker-Files#-client-contracts-atc-rest-api-client)                    | HTTP client generation                    |
