# MultipartDemo

Demonstrates multi-part OpenAPI specification splitting, where a large API spec is organized into domain-specific files that the source generator automatically merges at build time.

## Spec Organization

```
MultipartDemo.yaml              <-- Main spec (base document with shared definitions)
MultipartDemo_Accounts.yaml     <-- Account management endpoints
MultipartDemo_Common.yaml       <-- Shared schemas referenced across domains
MultipartDemo_Files.yaml        <-- File upload/download endpoints
MultipartDemo_Notifications.yaml <-- Notification endpoints
MultipartDemo_Tasks.yaml        <-- Task management endpoints
MultipartDemo_Users.yaml        <-- User management endpoints
MultipartDemo.Merged.yaml       <-- Pre-merged output (for reference/debugging)
```

## How It Works

1. The base spec (`MultipartDemo.yaml`) defines shared components and uses the `x-multi-part` extension to list all part files.
2. Each part file (`MultipartDemo_{Domain}.yaml`) contains paths and schemas for a specific domain.
3. At build time, the source generator detects all YAML files, merges them using the naming convention (`{BaseName}_{PartName}.yaml`), and generates code from the unified document.
4. The merged spec can also be produced via the CLI: `atc-rest-api-gen spec merge -s MultipartDemo.yaml -o MultipartDemo.Merged.yaml`

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
