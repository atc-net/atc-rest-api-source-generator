# 🚀 Atc.Rest.Api.SourceGenerator

[![NuGet Version](https://img.shields.io/nuget/v/Atc.Rest.Api.SourceGenerator.svg?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/Atc.Rest.Api.SourceGenerator)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Atc.Rest.Api.SourceGenerator.svg?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/Atc.Rest.Api.SourceGenerator)
[![Build and test](https://img.shields.io/github/actions/workflow/status/atc-net/atc-rest-api-source-generator/build.yml?branch=main&label=build&logo=github&style=for-the-badge)](https://github.com/atc-net/atc-rest-api-source-generator/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg?style=for-the-badge)](https://github.com/atc-net/atc-rest-api-source-generator/blob/main/LICENSE)

A Roslyn Source Generator that automatically generates REST API server and client code from OpenAPI specifications. **No CLI commands, no manual code generation** - just build your project and get production-ready code.

> ⬆️ **Upgrading from 2.x?** 3.0 changes what the generated server sends for errors, several generated client types and the TypeScript result unions, and pairs with Atc.Rest.MinimalApi 3.x. Read [Upgrading to v3](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Upgrading-to-v3) before you update.

## ✨ Key Features

| Area       | Feature                          | Description                                                                      |
|------------|----------------------------------|----------------------------------------------------------------------------------|
| **Core**   | 🔧 **Zero Configuration**        | Add the NuGet package, drop your YAML file, build                                |
|            | 🏗️ **Minimal API**               | Generates modern ASP.NET Core minimal API endpoints                              |
|            | 🔒 **Contract-Enforced Results** | Handlers can **only** return responses defined in OpenAPI - compile-time safety! |
|            | ✅ **Validation**                | Automatic `[Required]`, `[Range]`, lengths, patterns (also on a `$ref` component and on array items), `email` and absolute `uri` from OpenAPI constraints; a value that breaks one - or a JSON value that does not parse - is a 400 naming it |
|            | 🚨 **ProblemDetails Errors**     | Every error is RFC 9457 `application/problem+json` - server, C# and TS clients agree |
|            | 🔎 **HTTP `QUERY` (OAS 3.2)**    | `query:` operations - a read with a request body, for criteria too big for a URL |
|            | 🩹 **OpenAPI Overlays**          | Patch a specification you do not own - names, `required`, formats, removed paths - with an [OpenAPI Overlay](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Overlays) (1.0 to 1.2, full RFC 9535 JSONPath), applied by the build and the CLI |
| **Server Push** | 📣 **Push to Clients**     | Tell a client that something changed - four ways, each declared in the spec and typed end to end. Which one to use: [Working with Server Push](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Server-Push) |
|            | 📡 **SignalR Hubs**              | `x-signalr-hubs`: a hub's events and methods once in the spec → a typed hub contract on the server, typed hub clients in C# and TypeScript (React hooks included); scale out with Azure SignalR Service, no generated code changes |
|            | 🌊 **Event Streams**             | Server-Sent Events over plain HTTP - a live feed with headers at once, a heartbeat the clients read past and a resume with `Last-Event-ID`; JSON Lines, JSON Text Sequence, JSON array and multipart too |
|            | 🪝 **Webhooks**                  | OpenAPI 3.1 `webhooks` → the receiving endpoints, handler interfaces and typed results |
|            | 🔁 **Polling, Done Cheaply**     | `ETag` + `If-None-Match` → `304 Not Modified`: a client asks often, and nothing changed costs no body |
| **Server** | 📝 **Handler Scaffolds**         | Auto-generates handler stubs for unimplemented operations                        |
|            | 🔐 **Security**                  | JWT, OAuth2 scopes, API Key, role-based auth - all from OpenAPI                  |
|            | ⏱️ **Rate Limiting**             | Server-side rate limiting from `x-ratelimit-*` extensions                        |
|            | 💾 **Caching**                   | Output caching and HybridCache support from `x-cache-*` extensions               |
| **Client** | 🔌 **Type-Safe Client**          | Strongly-typed HTTP client with full IntelliSense                                |
|            | 🔄 **Resilience**                | Per-operation retry/circuit breaker from `x-retry-*`; POST/PATCH never resent unasked |
|            | 📨 **Typed Response Headers**    | A declared response header is typed end to end - a parameter of the server's result factory, a property of the C# client's result, `headers` on the TypeScript result - enums, numbers, dates, arrays and `simple`-style objects |
|            | 🔗 **URL Encoding**              | Automatic RFC 3986 compliant encoding - no more broken URLs                      |
| **Models** | 🎯 **Enum Support**              | Strict C# enums on the server (or extensible, as an opt-in), which read a listed value **in any casing** - `OUTOFSTOCK` is `outOfStock` in the path, query, a header, a cookie or any body; **extensible enums** in the clients, so a value the server adds later is read instead of failing the response |
|            | 📊 **Pagination**                | Generic `PaginatedResult<T>` from `allOf` composition                            |
| **Data**   | 📁 **Files**                     | Upload and download any file type (PDF, Excel, images) with its media type and file name; a response can be a model **or** a file |
|            | 🧾 **XML**                       | XML bodies mapped by the `xml` keyword, negotiated by `Accept`; `application/problem+xml` on request |
|            | 🌊 **Streaming**                 | `IAsyncEnumerable<T>` in five formats: JSON array, JSON Lines, JSON Text Sequence, Server-Sent Events, multipart - read by every C# and TypeScript client; a Server-Sent Events stream can resume with `Last-Event-ID` |
| **CLI**    | 🖥️ **Project Scaffolding**       | `generate server` / `generate client` creates complete project structure         |
|            | 🟦 **TypeScript Client**         | `generate client-typescript` — Fetch/Axios (or your own fetch / axios instance), interceptors, retry, MSW handlers |
|            | ⚛️ **React Hooks**               | React Query (`useQuery`/`useMutation`/`useInfiniteQuery`/`useSuspenseQuery`) + SWR (`useSWR`/`useSWRMutation`/`useSWRInfinite`) — `queryOptions` factories for loaders and prefetching, cancellation through `AbortSignal`, streaming hooks for `IAsyncEnumerable` |
|            | 🪪 **Branded ID types**          | `--branded-ids` emits nominal types — `getUser(petId)` becomes a compile error  |
|            | 🔍 **Runtime Zod validation**    | `--zod-runtime-validate` wires Zod into response parsing — spec-drift detection  |
|            | 🔌 **SignalR Hub Hooks**         | `x-signalr-hubs` extension → typed hub connection, methods and `use<Hub>Hub()`   |
|            | 📋 **Spec Validation**           | `spec validate` validates OpenAPI specs with strict/standard modes               |
|            | 🔗 **Multi-Part Specs**          | `spec merge` / `spec split` for large API specifications                         |
|            | ⚙️ **Options Management**        | `options create` / `options validate` for configuration files                    |
|            | 🔄 **Migration**                 | `migrate validate` / `migrate execute` to migrate from old CLI generator         |

## 🤖 Built for AI Agents - Spec-Driven, Deterministic

Ask an AI agent (Claude Code, GitHub Copilot, Cursor) for the same endpoint twice and you get two different endpoints: other routes, other status codes, another error body, validation in one and not the other, a client that disagrees with the server in the corners. With this generator the agent does not make those choices:

| | AI agent on its own | AI agent + this generator |
|---|---|---|
| **What the agent writes** | Endpoints, models, validation, error handling, clients - every time | A few lines of OpenAPI YAML and the handler's business logic |
| **Same request, twice** | Two different implementations | The same code, byte for byte: same spec + same generator version = same output |
| **Wrong status code** | Found in review - or in production | Does not compile: a handler can only return the responses the spec declares |
| **Spec mistakes** | Silent | 164 `ATC_API_*` build diagnostics, each with an id the agent can look up and fix |
| **Server ↔ C# / TypeScript client** | Written separately, drifting apart | Generated from one file; every client is run against the server in the [test-suite report](https://html-preview.github.io/?url=https://github.com/atc-net/atc-rest-api-source-generator/blob/main/docs/test-suite-report.html) |
| **What you review** | Hundreds of lines of plumbing per endpoint | The YAML diff and the handler diff |

In this repository's PetStoreFull scenario, an 838-line specification with 19 operations builds 56 server files (6,104 lines) and a 46-file typed client (3,215 lines) - about 320 lines of server code per operation that no agent writes and no one reviews line by line. The price: the team designs the contract first, someone knows OpenAPI, and the generator is a dependency to upgrade with care.

**[Spec-Driven Development with AI Agents](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Spec-Driven-Development-with-AI-Agents)** has the full case - the agent loop, what it saves and costs for a Scrum Master, and the instructions to give your agent.

## 📦 Quick Setup

### 1. Install the Package

```bash
dotnet add package Atc.Rest.Api.SourceGenerator

# Server (contracts project): validation filter, global error handler, ProblemDetails wiring
dotnet add package Atc.Rest.MinimalApi
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
  "$schema": "https://raw.githubusercontent.com/atc-net/atc-rest-api-source-generator/main/schemas/atc-rest-api-server.schema.json",
  "generate": true,
  "validateSpecificationStrategy": "Standard"
}
```

**Domain project** - `.atc-rest-api-server-handlers`:

```json
{
  "$schema": "https://raw.githubusercontent.com/atc-net/atc-rest-api-source-generator/main/schemas/atc-rest-api-server-handlers.schema.json",
  "generate": true,
  "generateHandlersOutput": "Handlers"
}
```

> 💡 See [Marker Files Reference](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Marker-Files) for all configuration options. `$schema` gives completion and validation in the editor
> ([editor support](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Marker-Files#-editor-support-and-checks)); a misspelled setting is reported by the build as `ATC_API_GEN014`.

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

The generator emits `Add{Project}Api()` and `Map{Project}Api()` (here for a project named `PetStore`). `{Project}` is the API's name: the namespace's last segment that names it - a trailing `Contracts`, `Api` or `Server` is skipped, so `Contoso.PetStore.Api.Contracts` gives `AddPetStoreApi()` - or the server marker's `apiName`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddPetStoreApi();            // ProblemDetails, validation, the generated services
builder.Services.AddApiHandlersFromDomain();   // your handlers (domain project)

var app = builder.Build();

app.MapOpenApi();
app.MapPetStoreApi();                          // status-code pages, auth, global error handler, every endpoint

app.Run();
```

> 💡 Without `Atc.Rest.MinimalApi` the pair is generated too: `Map{Project}Api()` adds the framework's `UseExceptionHandler()` (outside Development) where the package's global error handler would stand. That host validates nothing and maps no exception to a status (the package's ValidationFilter and global error handler). A host that orders its own pipeline leaves its middleware to itself (`app.MapPetStoreApi(o => o.HostOwnsPipeline = true)`) - see [Working with Error Responses](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Error-Responses).

## 📋 Requirements

- **.NET 10** or later
- **A C# compiler with Roslyn 5.9 or later** for the source generator: .NET SDK **10.0.401** or later, and an IDE that uses it. With an older compiler the build reports `CS9057` and generates nothing
- **OpenAPI 3.0.x**, **3.1.x** or **3.2.x** specification

### Companion Packages

| Package | When | Version |
|---------|------|---------|
| [`Atc.Rest.MinimalApi`](https://www.nuget.org/packages/Atc.Rest.MinimalApi) | Server: validation filter, global error handler, endpoint definitions | **3.x** with generator 3.x (they ship together - see [Upgrading to v3](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Upgrading-to-v3)) |
| [`Atc.Rest.Client`](https://www.nuget.org/packages/Atc.Rest.Client) | C# client in `EndpointPerOperation` mode | 2.6.0 or later: reads streamed responses in five framings (JSON array, JSON Lines, JSON Text Sequence, Server-Sent Events, multipart) with typed errors and headers; trim- and Native AOT-ready |
| [`Atc`](https://www.nuget.org/packages/Atc) | C# client in `EndpointPerOperation` mode | 3.x |

```xml
<PackageReference Include="Atc.Rest.MinimalApi" Version="3.*" />
<PackageReference Include="Atc" Version="3.*" />
<PackageReference Include="Atc.Rest.Client" Version="2.6.0" />
```

## 🏗️ Architecture

The project uses a **three-layer architecture**:

| Layer          | Project                        | Description                                                                 |
|----------------|--------------------------------|-----------------------------------------------------------------------------|
| **Shared**     | `Atc.Rest.Api.Generator`       | The extractors (55), services, configuration, validation (Roslyn-independent) |
| **Generators** | `Atc.Rest.Api.SourceGenerator` | Roslyn source generators (thin wrappers calling shared services)            |
| **CLI**        | `Atc.Rest.Api.Generator.Cli`    | Command-line tool for validation and generation                             |

**Benefits**: Testability, reusability across CLI and source generators, clear separation of concerns.

## 🧪 Samples

Every sample under [`sample/`](sample/README.md) builds its code at compile time from the specification next to it and runs: the Swagger Petstore in a three-operation and a full version, the Showcase (every feature, with Blazor and React apps and Playwright end-to-end tests), a multi-part specification, an IoT API with role-based authorization, and clients for third-party specifications in both client modes. The [index](sample/README.md) says what each one shows, how its host is wired and how to run it.

## ✅ Quality - how we know it works

Every release is proven, not promised. The **[test-suite report](https://html-preview.github.io/?url=https://github.com/atc-net/atc-rest-api-source-generator/blob/main/docs/test-suite-report.html)** ([`docs/test-suite-report.html`](https://github.com/atc-net/atc-rest-api-source-generator/blob/main/docs/test-suite-report.html), regenerated from a real run) is the one page to read:

- **End-to-end runtime matrices** - the generated server on Kestrel with every generated client (C# typed, C# endpoint-per-operation, TypeScript Fetch and Axios, Zod, React Query, SWR) calling it over HTTP: error bodies for every HTTP status and every way a specification can declare one, body media types (files, uploads, XML), request binding (path, query, header, cookie, JSON, multipart and URL-encoded bodies, and the requests the server must refuse), security and rate limiting, streaming, pagination, polymorphism, dates, conditional requests and cancellation. Thousands of recorded cells, each with a verdict and the reason.
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
| [🧩 Working with Extensible Enums](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Extensible-Enums) | Why client enums keep a value the server adds later, and how to use them |
| [🩹 Working with Overlays](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Overlays) | Patch a third-party specification without editing it (OpenAPI Overlay) |
| [🚨 Working with Error Responses](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Error-Responses) | RFC 9457 ProblemDetails, declared error models |
| [📁 Working with Files](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Files) | Uploads, downloads, model-or-file responses |
| [🧾 Working with XML](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-XML) | XML bodies, content negotiation |
| [🔐 Working with Security](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Security) | JWT, OAuth2, API Key authentication |
| [✅ Working with Validations](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Validations) | Request validation from OpenAPI constraints |
| [⏱️ Working with Rate Limiting](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Rate-Limiting) | Server-side rate limiting configuration |
| [💾 Working with Caching](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Caching) | Output caching and HybridCache configuration |
| [🔢 Working with Versioning](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Versioning) | API versioning strategies |
| [📣 Working with Server Push](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Server-Push) | Streams, SignalR hubs, webhooks or polling with ETag - which one to use |
| [📡 Working with SignalR Hubs](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-SignalR-Hubs) | `x-signalr-hubs`: typed hub contract, C# and TypeScript hub clients |
| [🪝 Working with Webhooks](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Working-with-Webhooks) | Receive OpenAPI 3.1 webhooks: endpoints, handlers, results |
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
