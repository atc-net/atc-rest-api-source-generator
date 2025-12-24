# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Atc.Rest.Api.SourceGenerator is a Roslyn Source Generator that automatically generates REST API server and client code from OpenAPI specifications. The key differentiator is **zero CLI commands** - just build your project and get production-ready code.

**Target Framework**: .NET 10 (C# 14)

## Build Commands

```bash
# Build entire solution
dotnet build Atc.Rest.Api.SourceGenerator.slnx

# Build in Release mode (enables TreatWarningsAsErrors)
dotnet build Atc.Rest.Api.SourceGenerator.slnx -c Release

# Build the CLI tool only
dotnet build src/Atc.Rest.Api.CliGenerator/Atc.Rest.Api.CliGenerator.csproj
```

## Running Tests

Tests use the built-in Microsoft Testing Platform with xUnit-style assertions:

```bash
# Run all tests
dotnet test Atc.Rest.Api.SourceGenerator.slnx

# Run a specific test project
dotnet test test/Atc.Rest.Api.Generator.Tests/Atc.Rest.Api.Generator.Tests.csproj

# Run a single test class
dotnet test --filter "FullyQualifiedName~CasingHelperTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName~CasingHelperTests.IsCamelCase_ReturnsExpectedResult"
```

Test projects:
- `Atc.Rest.Api.Generator.Tests` - Unit tests for the shared generator library
- `Atc.Rest.Api.SourceGenerator.Tests` - Unit tests for Roslyn source generators
- `Atc.Rest.Api.CliGenerator.Tests` - Tests for CLI commands
- `Atc.Rest.Api.Generator.IntegrationTests` - End-to-end scenario tests

## Architecture

The project uses a **three-layer architecture**:

```
┌─────────────────────────────────────────────────────────────────┐
│ Atc.Rest.Api.SourceGenerator (netstandard2.0)                   │
│ Roslyn IIncrementalGenerator implementations                    │
│ - ApiServerGenerator, ApiClientGenerator, ApiServerDomainGenerator
└───────────────────────────┬─────────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────────┐
│ Atc.Rest.Api.Generator (netstandard2.0)                         │
│ All extractors, services, configuration, validation             │
│ - Extractors: Schema, Handler, Endpoint, Security, etc.         │
│ - Completely Roslyn-independent                                 │
└───────────────────────────┬─────────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────────┐
│ Atc.Rest.Api.CliGenerator (net10.0)                             │
│ CLI tool (atc-rest-api-gen) for project scaffolding/validation  │
└─────────────────────────────────────────────────────────────────┘

Supporting Libraries:
- Atc.CodeGeneration.CSharp - C# code generation primitives
- Atc.OpenApi - OpenAPI document extensions and helpers
```

**Why netstandard2.0?** Source generators must target netstandard2.0 to work with all .NET SDK versions.

## Key Source Generator Concepts

### Marker Files (Triggers)

Source generation is triggered by marker files in the project:
- `.atc-rest-api-server-contracts` - Generates server code (models, endpoints, handlers)
- `.atc-rest-api-server-handlers` - Generates handler implementation scaffolds
- `.atc-rest-api-client-contracts` - Generates HTTP client code

### Generation Flow

1. Generator detects marker file via `AdditionalTextsProvider`
2. Loads configuration from marker file JSON
3. Finds and parses all `.yaml`/`.yml` files
4. Validates OpenAPI spec against configured strategy
5. Extracts types using `*Extractor` classes in `Atc.Rest.Api.Generator`
6. Generates C# code using `Atc.CodeGeneration.CSharp` primitives

### Multi-Part Specifications

Specs can be split across files: `MyApi.yaml` (base) + `MyApi_Users.yaml`, `MyApi_Products.yaml` (parts).
The generator automatically merges them during build.

## Code Organization Patterns

### Extractors

Located in `src/Atc.Rest.Api.Generator/Extractors/`. Each extracts specific code artifacts:
- `SchemaExtractor` - Models/records from OpenAPI schemas
- `EnumExtractor` - Enums from string schemas with enum values
- `HandlerExtractor` - Handler interfaces from operations
- `EndpointRegistrationExtractor` - Minimal API endpoint mappings
- Security, RateLimit, Caching extractors for cross-cutting concerns

### Type Conflict Resolution

`TypeConflictRegistry` handles naming conflicts between OpenAPI schemas and C# built-in types (e.g., a schema named "Task" conflicts with `System.Threading.Tasks.Task`).

### Path Segment Grouping

Operations are grouped by first path segment for namespace organization:
`/pets/{id}` → `Pets` namespace, `/users/{id}` → `Users` namespace

## Coding Standards

- Uses ATC coding rules with strict analyzer settings
- All warnings treated as errors in Release builds
- File-scoped namespaces required
- Private fields use camelCase (no underscore prefix)
- `var` preferred for all variable declarations

## Documentation

All documentation is maintained in the wiki repository:
`D:\Code\atc-net\atc-rest-api-source-generator.wiki`

This corresponds to the GitHub wiki at https://github.com/atc-net/atc-rest-api-source-generator/wiki

## Sample Projects

The `sample/` directory contains working examples:
- `PetStoreSimple` - Basic API generation example
- `PetStoreFull` - Full-featured example with Aspire
- `Showcase` - Complete demo with Blazor UI
- `MultipartDemo` - Multi-file specification example
