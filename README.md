# 🚀 Atc.Rest.Api.SourceGenerator

[![NuGet Version](https://img.shields.io/nuget/v/Atc.Rest.Api.SourceGenerator.svg?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/Atc.Rest.Api.SourceGenerator)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Atc.Rest.Api.SourceGenerator.svg?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/Atc.Rest.Api.SourceGenerator)
[![Build and test](https://img.shields.io/github/actions/workflow/status/atc-net/atc-rest-api-source-generator/build.yml?branch=main&label=build&logo=github&style=for-the-badge)](https://github.com/atc-net/atc-rest-api-source-generator/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg?style=for-the-badge)](https://github.com/atc-net/atc-rest-api-source-generator/blob/main/LICENSE)

A Roslyn Source Generator that automatically generates REST API server and client code from OpenAPI specifications. **No CLI commands, no manual code generation** - just build your project and get production-ready code.

## ✨ Key Features

| Area       | Feature                          | Description                                                                      |
|------------|----------------------------------|----------------------------------------------------------------------------------|
| **Core**   | 🔧 **Zero Configuration**        | Add the NuGet package, drop your YAML file, build                                |
|            | 🏗️ **Minimal API**               | Generates modern ASP.NET Core minimal API endpoints                              |
|            | 🔒 **Contract-Enforced Results** | Handlers can **only** return responses defined in OpenAPI - compile-time safety! |
|            | ✅ **Validation**                | Automatic `[Required]`, `[Range]`, `[StringLength]` from OpenAPI constraints     |
|            | 🚨 **ProblemDetails Errors**     | Every error is RFC 9457 `application/problem+json` - server, C# and TS clients agree |
|            | 🔎 **HTTP `QUERY` (OAS 3.2)**    | `query:` operations - a read with a request body, for criteria too big for a URL |
| **Server** | 📝 **Handler Scaffolds**         | Auto-generates handler stubs for unimplemented operations                        |
|            | 🔐 **Security**                  | JWT, OAuth2 scopes, API Key, role-based auth - all from OpenAPI                  |
|            | ⏱️ **Rate Limiting**             | Server-side rate limiting from `x-ratelimit-*` extensions                        |
|            | 💾 **Caching**                   | Output caching and HybridCache support from `x-cache-*` extensions               |
| **Client** | 🔌 **Type-Safe Client**          | Strongly-typed HTTP client with full IntelliSense                                |
|            | 🔄 **Resilience**                | Per-operation retry/circuit breaker from `x-retry-*`; POST/PATCH never resent unasked |
|            | 🔗 **URL Encoding**              | Automatic RFC 3986 compliant encoding - no more broken URLs                      |
| **Models** | 🎯 **Enum Support**              | Generates C# enums from OpenAPI string enums                                     |
|            | 📊 **Pagination**                | Generic `PaginatedResult<T>` from `allOf` composition                            |
| **Data**   | 📁 **Files**                     | Upload and download any file type (PDF, Excel, images) with its media type and file name; a response can be a model **or** a file |
|            | 🧾 **XML**                       | XML bodies mapped by the `xml` keyword, negotiated by `Accept`; `application/problem+xml` on request |
|            | 🌊 **Streaming**                 | `IAsyncEnumerable<T>` support for efficient data streaming                       |
| **CLI**    | 🖥️ **Project Scaffolding**       | `generate server` / `generate client` creates complete project structure         |
|            | 🟦 **TypeScript Client**         | `generate client-typescript` — Fetch/Axios, interceptors, retry, MSW handlers    |
|            | ⚛️ **React Hooks**               | React Query (`useQuery`/`useMutation`/`useInfiniteQuery`/`useSuspenseQuery`) + SWR (`useSWR`/`useSWRMutation`/`useSWRInfinite`) — streaming hooks for `IAsyncEnumerable` |
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
- 🔧 **DI Extensions** - `AddApiHandlersFromDomain()`, `MapEndpoints()`

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
app.MapEndpoints();

app.Run();
```

> 💡 With [Atc.Rest.MinimalApi](https://www.nuget.org/packages/Atc.Rest.MinimalApi) referenced, the generated `Add{Project}Api()` / `Map{Project}Api()` also wire up RFC 9457 ProblemDetails for every error (`AddProblemDetails()` + `UseStatusCodePages()`). Without it, add those two calls yourself - see [Working with Error Responses](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Error-Responses).

## 📋 Requirements

- **.NET 10** or later
- **OpenAPI 3.0.x**, **3.1.x** or **3.2.x** specification

### Companion Packages

| Package | When | Version |
|---------|------|---------|
| [`Atc.Rest.MinimalApi`](https://www.nuget.org/packages/Atc.Rest.MinimalApi) | Server: validation filter, global error handler, endpoint definitions | **3.x** with generator 3.x (they ship together - see [Upgrading to v3](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Upgrading-to-v3)) |
| [`Atc.Rest.Client`](https://www.nuget.org/packages/Atc.Rest.Client) | C# client in `EndpointPerOperation` mode | 2.1 or later |
| [`Atc`](https://www.nuget.org/packages/Atc) | C# client in `EndpointPerOperation` mode | 3.x |

```xml
<PackageReference Include="Atc.Rest.MinimalApi" Version="3.*" />
<PackageReference Include="Atc" Version="3.*" />
<PackageReference Include="Atc.Rest.Client" Version="2.*" />
```

## 🏗️ Architecture

The project uses a **three-layer architecture**:

| Layer          | Project                        | Description                                                                 |
|----------------|--------------------------------|-----------------------------------------------------------------------------|
| **Shared**     | `Atc.Rest.Api.Generator`       | The extractors (55), services, configuration, validation (Roslyn-independent) |
| **Generators** | `Atc.Rest.Api.SourceGenerator` | Roslyn source generators (thin wrappers calling shared services)            |
| **CLI**        | `Atc.Rest.Api.Generator.Cli`    | Command-line tool for validation and generation                             |

**Benefits**: Testability, reusability across CLI and source generators, clear separation of concerns.

## ✅ Quality - how we know it works

Every release is proven, not promised. The **[test-suite report](https://html-preview.github.io/?url=https://github.com/atc-net/atc-rest-api-source-generator/blob/main/docs/test-suite-report.html)** ([`docs/test-suite-report.html`](https://github.com/atc-net/atc-rest-api-source-generator/blob/main/docs/test-suite-report.html), regenerated from a real run) is the one page to read:

- **End-to-end runtime matrices** - the generated server on Kestrel with every generated client (C# typed, C# endpoint-per-operation, TypeScript Fetch and Axios, Zod, React Query, SWR) calling it over HTTP: error bodies for every HTTP status and every way a specification can declare one, body media types (files, uploads, XML), request binding (path, query, header, cookie, JSON, multipart and URL-encoded bodies, and the requests the server must refuse), security and rate limiting, streaming, pagination, polymorphism, dates and conditional requests. Thousands of recorded cells, each with a verdict and the reason.
- **Compile gate** - every scenario specification under `test/Scenarios` through every generator, compiled as a strict consumer does (nullable, documentation, warnings as errors), plus the strict `tsc` matrix for TypeScript.
- **Diagnostics coverage** - every `ATC_API_*` build diagnostic and the test that triggers it.
- **What is not covered** - each matrix lists what it deliberately leaves out, and the cells it still expects to fail, with the reason.

The page has a start for every reader - a newcomer judging the tool, a maintainer looking for what moved, and an AI agent that wants the data and the commands - and is written by `Report_EndToEndErrorBodyMatrix` (see [Development Notes](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Development-Notes#-the-test-suite-report)).

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

| Document | Description |
|----------|-------------|
| [🚀 Getting Started](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Getting-Started-with-Basic) | Detailed setup guide with examples |
| [🖥️ Getting Started with CLI](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Getting-Started-with-CLI) | Quick start guide using CLI scaffolding |
| [⬆️ Upgrading to v3](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Upgrading-to-v3) | What changes in 3.0 and how to upgrade |
| [⬆️ Upgrading to v2](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Upgrading-to-v2) | What the v2 nullability change means for you |
| [🔄 Migration Guide](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Migration-Guide) | Migrate from old `atc-rest-api-generator` |
| [🏷️ Marker Files](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Marker-Files) | Every generator setting, per marker file |
| [⚙️ Working with Configuration](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Configuration) | `ApiGeneratorOptions.json` for the CLI |
| [🛠️ Working with the CLI](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-CLI) | Full CLI command reference |
| [📖 Working with OpenAPI](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-OpenAPI) | YAML patterns and generated output |
| [🧩 Working with Multi-Part Specs](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Multi-Part-Specs) | Split a large specification across files |
| [🕳️ Working with Nullability](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Nullability) | `nullable` vs `type: [x, "null"]`, and why |
| [🚨 Working with Error Responses](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Error-Responses) | RFC 9457 ProblemDetails, declared error models |
| [📁 Working with Files](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Files) | Uploads, downloads, model-or-file responses |
| [🧾 Working with XML](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-XML) | XML bodies, content negotiation |
| [🔐 Working with Security](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Security) | JWT, OAuth2, API Key authentication |
| [✅ Working with Validations](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Validations) | Request validation from OpenAPI constraints |
| [⏱️ Working with Rate Limiting](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Rate-Limiting) | Server-side rate limiting configuration |
| [💾 Working with Caching](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Caching) | Output caching and HybridCache configuration |
| [🔢 Working with Versioning](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Versioning) | API versioning strategies |
| [🪝 Working with Webhooks](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Webhooks) | OpenAPI webhooks on server and client |
| [🗺️ Working with Endpoint Definitions](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Endpoint-Definitions) | How endpoints are grouped and mapped |
| [🔌 Working with the C# Client](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-CSharp-Client) | Typed client and endpoint-per-operation modes |
| [🧪 Working with C# Client Testing](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-CSharp-Client-Testing) | Test doubles for generated clients |
| [🔄 Working with Resilience](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Resilience) | Client-side retry/circuit breaker patterns |
| [🟦 Working with TypeScript Client](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-TypeScript-Client) | TypeScript/React client generation |
| [☁️ Working with Aspire](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Aspire) | .NET Aspire orchestration |
| [📋 Analyzer Rules](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Analyzer-Rules) | OpenAPI validation rules reference |
| [🎪 Showcase Demo](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Showcase-Demo) | Full-featured demo with Blazor & React UI |
| [❓ FAQ & Troubleshooting](https://github.com/atc-net/atc-rest-api-source-generator/wiki/FAQ-and-Troubleshooting) | Common issues and solutions |
| [🗺️ Roadmap](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Roadmap) | Planned features and status |
| [🔧 Development Notes](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Development-Notes) | For contributors |

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
public sealed record Pet(
    [property: Required] long Id,
    [property: Required] string Name,
    string? Tag = null);

// Generated parameters
public sealed record ListPetsParameters(
    [property: FromQuery(Name = "limit"), Range(long.MinValue, 100)] int? Limit);

// Generated handler interface
public interface IListPetsHandler
{
    Task<ListPetsResult> ExecuteAsync(
        ListPetsParameters parameters,
        CancellationToken cancellationToken = default);
}

// Generated result - ONLY factory methods for responses in OpenAPI!
public sealed class ListPetsResult : IResult
{
    private ListPetsResult(IResult innerResult) { ... }  // Private!

    public static ListPetsResult Ok(List<Pet> response) => new(TypedResults.Ok(response));
    public static implicit operator ListPetsResult(List<Pet> response) => Ok(response);
    public static implicit operator ListPetsResult(Pet[] response) => Ok(response.ToList());
    // No InternalServerError(), Unauthorized(), etc. - they don't exist!
}

// Generated endpoint (in PetsEndpointDefinition)
pets
    .MapGet("/", ListPets)
    .WithName("ListPets")
    .Produces<List<Pet>>()
    .ProducesValidationProblem();
```

## 🏷️ [Marker Files](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Marker-Files)

| File                                                                                                                                        | Purpose                                   |
|---------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------|
| [`.atc-rest-api-server`](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Marker-Files#-server-contracts-atc-rest-api-server)                    | Server code (models, endpoints, handlers) |
| [`.atc-rest-api-server-handlers`](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Marker-Files#-server-handlers-atc-rest-api-server-handlers)  | Handler implementation scaffolds          |
| [`.atc-rest-api-client`](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Marker-Files#-client-contracts-atc-rest-api-client)                    | HTTP client generation                    |
