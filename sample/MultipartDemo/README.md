# MultipartDemo

Demonstrates multi-part OpenAPI specification splitting, where a large API spec is organized into domain-specific files that the source generator automatically merges at build time.

## Spec Organization

```
MultipartDemo.yaml              <-- Base: info, servers, security, rate limit / retry settings, webhooks, all components (paths: {})
MultipartDemo_Accounts.yaml     <-- /accounts endpoints
MultipartDemo_Files.yaml        <-- /files endpoints (upload / download)
MultipartDemo_Notifications.yaml <-- /notifications endpoints
MultipartDemo_Tasks.yaml        <-- /tasks endpoints
MultipartDemo_Testing.yaml      <-- /testing endpoints
MultipartDemo_Users.yaml        <-- /users endpoints
MultipartDemo.Merged.yaml       <-- The merged document (generated with `spec merge`, for reference)
```

## How It Works

1. The base spec (`MultipartDemo.yaml`) holds everything shared - the document settings, the webhooks and every component - and no paths.
2. Each part file (`MultipartDemo_{Domain}.yaml`) holds the paths of one domain; its `$ref`s point at the base's components. A part has no `info` section (ATC_API_MPT007).
3. Every project adds the base and the parts as `AdditionalFiles` (`..\MultipartDemo.yaml` and `..\MultipartDemo_*.yaml`). At build time the source generator finds the parts by the naming convention (`{BaseName}_{PartName}.yaml`), merges them, and generates code from the merged document.
4. The spec deliberately keeps the OpenAPI 3.0 `nullable: true` spelling in a 3.1 document (see the note at the top of `MultipartDemo.yaml`).
5. The merged spec can also be produced via the CLI: `atc-rest-api-gen spec merge -s MultipartDemo.yaml -o MultipartDemo.Merged.yaml`

## Projects

| Project | Purpose |
|---------|---------|
| `MultipartDemo.Api` | ASP.NET Core server with generated endpoints |
| `MultipartDemo.Api.Contracts` | Generated server contracts (models, handlers, endpoints) |
| `MultipartDemo.Api.Domain` | Handler implementations |
| `MultipartDemo.ClientApp` | Generated typed HTTP client |

## CLI Commands

```bash
# Merge parts into a single spec
atc-rest-api-gen spec merge -s MultipartDemo.yaml -o MultipartDemo.Merged.yaml

# Split a single spec into parts (reverse operation)
atc-rest-api-gen spec split -s MultipartDemo.Merged.yaml -o . --prefix MultipartDemo

# Validate the merged spec
atc-rest-api-gen validate -s MultipartDemo.yaml
```

## Running

```bash
dotnet build
dotnet run --project MultipartDemo.Api
```
