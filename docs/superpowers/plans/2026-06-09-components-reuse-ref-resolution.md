# Components Reuse / `$ref` Resolution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make path-item `$ref` (`components.pathItems`) and media-type `$ref` (`components.mediaTypes`) work end-to-end — so a referenced path item generates identical output to an inline one, and a reusable media type's schema resolves and emits correctly.

**Architecture:** Microsoft.OpenApi 3.6.0 already produces transparent reference proxies (`OpenApiPathItemReference`, `OpenApiMediaTypeReference`) that delegate member access to their resolved targets. The entire fix for path-item `$ref` is relaxing the concrete `is OpenApiPathItem` pattern casts to `is IOpenApiPathItem` across ~50 sites; every method signature that takes `OpenApiPathItem pathItem` (non-`this`) must also be updated to `IOpenApiPathItem pathItem`. Media-type reuse is verification-led: the proxy already resolves `.Schema`, so the named-ref case needs only a compile-verified test; anonymous inline schemas are handled with a new `ATC_API_SCH019` Warning diagnostic.

**Tech Stack:** C# 14 / .NET 10 · `Microsoft.OpenApi` 3.6.0 (`IOpenApiPathItem` interface, `OpenApiPathItemReference`/`OpenApiMediaTypeReference` proxies) · Roslyn source generators · xUnit v3 · `CompilationVerificationHarness` (existing)

---

## File map

### New files
- `test/Scenarios/ComponentsReuse/ComponentsReuse.yaml` — scenario spec (3.2.0, one inline path item, one path-item `$ref`, one response using reusable media type)
- `test/Scenarios/ComponentsReuse/Server/.atc-rest-api-server` — server marker (`{}`)
- `test/Scenarios/ComponentsReuse/Client-Typed/.atc-rest-api-client` — typed-client marker (`{}`)
- `test/Scenarios/ComponentsReuse/Client-Operation/.atc-rest-api-client` — per-op marker (`{}`)
- `test/Scenarios/ComponentsReuse/TS-Client-Axios/.atc-rest-api-ts-client` — TS Axios marker (`{}`)
- `test/Scenarios/ComponentsReuse/TS-Client-Fetch/.atc-rest-api-ts-client` — TS Fetch marker (`{}`)
- `test/Scenarios/ComponentsReuse/TS-Hooks-ReactQuery/.atc-rest-api-ts-client` — TS hooks marker (`{}`)
- `test/Atc.Rest.Api.SourceGenerator.Tests/Generators/ComponentsReuseTests.cs` — compile-verify + regression tests

### Modified files (cast relaxation — Half 1)
The authoritative list of files is the result of:
```
grep -rn "is (not )?OpenApiPathItem\b" src/ --include="*.cs"
```
As of authoring (re-grep to confirm exact lines before editing):

**Pattern casts** (`is (not) OpenApiPathItem <var>` or `is OpenApiPathItem { ... }`) — change to `IOpenApiPathItem`:
- `src/Atc.Rest.Api.Generator/Extractors/EndpointDefinitionExtractor.cs` (~lines 153, 197, 246, 706, 731, 753, 790, 812, 860, 888)
- `src/Atc.Rest.Api.Generator/Extractors/EndpointRegistrationExtractor.cs` (~203)
- `src/Atc.Rest.Api.Generator/Extractors/EndpointPerOperationExtractor.cs` (~170)
- `src/Atc.Rest.Api.Generator/Extractors/HandlerExtractor.cs` (~65)
- `src/Atc.Rest.Api.Generator/Extractors/HttpClientExtractor.cs` (~182)
- `src/Atc.Rest.Api.Generator/Extractors/OperationParameterExtractor.cs` (~257)
- `src/Atc.Rest.Api.Generator/Extractors/OutputCachePoliciesExtractor.cs` (~65)
- `src/Atc.Rest.Api.Generator/Extractors/HybridCachePoliciesExtractor.cs` (~65)
- `src/Atc.Rest.Api.Generator/Extractors/RateLimitPoliciesExtractor.cs` (~67)
- `src/Atc.Rest.Api.Generator/Extractors/ResiliencePoliciesExtractor.cs` (~67)
- `src/Atc.Rest.Api.Generator/Extractors/SecurityPoliciesExtractor.cs` (~53)
- `src/Atc.Rest.Api.Generator/Extractors/ServerDependencyInjectionExtractor.cs` (~50)
- `src/Atc.Rest.Api.Generator/Extractors/OpenIdConnectConfigExtractor.cs` (~82)
- `src/Atc.Rest.Api.Generator/Helpers/PathSegmentHelper.cs` (~150, 627, 658)
- `src/Atc.Rest.Api.Generator/Services/CodeGenerationService.cs` (~207, 347, 1908)
- `src/Atc.Rest.Api.Generator/Services/SpecificationService.cs` — `SerializePathItem` body (~1296; see Task 3 note)
- `src/Atc.Rest.Api.Generator/Validators/OpenApiDocumentValidator.cs` (~1806, 1842, 1947)
- `src/Atc.OpenApi/Extensions/OpenApiCacheExtensions.cs` (~214, 285)
- `src/Atc.OpenApi/Extensions/OpenApiRateLimitExtensions.cs` (~143)
- `src/Atc.OpenApi/Extensions/OpenApiRetryExtensions.cs` (~241)
- `src/Atc.OpenApi/Extensions/OpenApiSecurityExtensions.cs` (~650)
- `src/Atc.Rest.Api.SourceGenerator/ApiServerDomainGenerator.cs` (~855)
- `src/Atc.Rest.Api.SourceGenerator/Extractors/EndpointInjectionExtractor.cs` (~40)
- `src/Atc.Rest.Api.Generator.Cli/Services/TypeScriptClientGenerationService.cs` (~235)
- `src/Atc.Rest.Api.Generator.Cli/Services/Migration/ParameterNameMigrator.cs` (~109)
- `src/Atc.Rest.Api.Generator.Cli/Extractors/TypeScript/TypeScriptOperationHelper.cs` (~31, 675)
- `src/Atc.Rest.Api.Generator.Cli/Extractors/TypeScript/TypeScriptRetryConfigExtractor.cs` (~118)
- `src/Atc.Rest.Api.Generator.Cli/Extractors/TypeScript/TypeScriptWebhookExtractor.cs` (~33)

**Method signatures** (non-`this` param `OpenApiPathItem pathItem` → `IOpenApiPathItem pathItem`; callers must pass the `IOpenApiPathItem` variable):
- `src/Atc.OpenApi/Extensions/OpenApiSecurityExtensions.cs` — `ExtractSecurityConfiguration` (~83) and `ExtractUnifiedSecurityConfiguration` (~269)
- `src/Atc.OpenApi/Extensions/OpenApiRetryExtensions.cs` — `ExtractRetryConfiguration` (~116)
- `src/Atc.OpenApi/Extensions/OpenApiRateLimitExtensions.cs` — `ExtractRateLimitConfiguration` (~68)
- `src/Atc.OpenApi/Extensions/OpenApiCacheExtensions.cs` — `ExtractCacheConfiguration` (~103)
- `src/Atc.Rest.Api.Generator/Extractors/EndpointDefinitionExtractor.cs` — `GenerateSecurityMetadata` (~1007), `GenerateRateLimitingMetadata` (~1058), `GenerateOutputCachingMetadata` (~1098), `GenerateProducesMetadata` (~1239)
- `src/Atc.Rest.Api.Generator/Extractors/EndpointRegistrationExtractor.cs` — `GenerateEndpointMapping` (~231)
- `src/Atc.Rest.Api.Generator/Extractors/EndpointPerOperationExtractor.cs` — private helper (~233)
- `src/Atc.Rest.Api.Generator/Extractors/ResultClassExtractor.cs` — `ExtractResultClass` (~146)
- `src/Atc.Rest.Api.Generator/Helpers/OperationFeaturesHelper.cs` — `DetectOperationFeatures` (~18)

### Modified files (Half 2 — media-type / diagnostics)
- `src/Atc.Rest.Api.Generator/RuleIdentifiers.cs` — add `ATC_API_SCH019`
- `src/Atc.Rest.Api.Generator/Validators/OpenApiDocumentValidator.cs` — wire SCH019 for anonymous inline schemas in `components.mediaTypes`
- `test/Atc.Rest.Api.SourceGenerator.Tests/Generators/CompilationVerificationTests.cs` — add `ComponentsReuse` theory rows

---

## Task 1: Scenario YAML and marker files

**Files:**
- Create: `test/Scenarios/ComponentsReuse/ComponentsReuse.yaml`
- Create: `test/Scenarios/ComponentsReuse/Server/.atc-rest-api-server`
- Create: `test/Scenarios/ComponentsReuse/Client-Typed/.atc-rest-api-client`
- Create: `test/Scenarios/ComponentsReuse/Client-Operation/.atc-rest-api-client`
- Create: `test/Scenarios/ComponentsReuse/TS-Client-Axios/.atc-rest-api-ts-client`
- Create: `test/Scenarios/ComponentsReuse/TS-Client-Fetch/.atc-rest-api-ts-client`
- Create: `test/Scenarios/ComponentsReuse/TS-Hooks-ReactQuery/.atc-rest-api-ts-client`

- [ ] **Step 1: Create the scenario YAML**

Create `test/Scenarios/ComponentsReuse/ComponentsReuse.yaml`:

```yaml
openapi: "3.2.0"
info:
  title: Components Reuse Test
  version: 1.0.0
  description: >
    Exercises components.pathItems ($ref path items, pre-3.2 gap) and
    components.mediaTypes (new in 3.2). /items uses an inline path item
    whose response content refs a components.mediaTypes entry.
    /items/{id} is itself a $ref to components.pathItems/ItemById.

paths:
  /items:
    get:
      operationId: listItems
      summary: List items
      tags:
        - items
      responses:
        "200":
          description: OK
          content:
            application/json:
              $ref: '#/components/mediaTypes/ItemListContent'
  /items/{id}:
    $ref: '#/components/pathItems/ItemById'

components:
  pathItems:
    ItemById:
      get:
        operationId: getItemById
        summary: Get item by ID
        tags:
          - items
        parameters:
          - name: id
            in: path
            required: true
            schema:
              type: string
        responses:
          "200":
            description: OK
            content:
              application/json:
                schema:
                  $ref: '#/components/schemas/Item'
          "404":
            description: Not found
  mediaTypes:
    ItemListContent:
      schema:
        type: array
        items:
          $ref: '#/components/schemas/Item'
  schemas:
    Item:
      type: object
      title: Item
      properties:
        id:
          type: string
        name:
          type: string
```

- [ ] **Step 2: Create marker files**

Each file contains exactly `{}`.

```
test/Scenarios/ComponentsReuse/Server/.atc-rest-api-server          → {}
test/Scenarios/ComponentsReuse/Client-Typed/.atc-rest-api-client    → {}
test/Scenarios/ComponentsReuse/Client-Operation/.atc-rest-api-client → {}
test/Scenarios/ComponentsReuse/TS-Client-Axios/.atc-rest-api-ts-client → {}
test/Scenarios/ComponentsReuse/TS-Client-Fetch/.atc-rest-api-ts-client → {}
test/Scenarios/ComponentsReuse/TS-Hooks-ReactQuery/.atc-rest-api-ts-client → {}
```

- [ ] **Step 3: Commit the scenario skeleton**

```bash
git add test/Scenarios/ComponentsReuse/
git commit -m "test(openapi32): add ComponentsReuse scenario YAML + marker files"
```

---

## Task 2: Write failing regression test (prove the bug exists)

**Files:**
- Create: `test/Atc.Rest.Api.SourceGenerator.Tests/Generators/ComponentsReuseTests.cs`

This test will FAIL before Task 3 (because the `$ref` path item is silently dropped) and PASS after. Writing it first proves the test actually catches the bug.

- [ ] **Step 1: Create the test file**

Create `test/Atc.Rest.Api.SourceGenerator.Tests/Generators/ComponentsReuseTests.cs`:

```csharp
namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Compile-verification tests for the ComponentsReuse scenario.
/// Covers two things:
///   1. Path-item $ref (components.pathItems) — getItemById must be generated.
///   2. components.mediaTypes reuse — Item schema must be emitted.
/// </summary>
public class ComponentsReuseTests
{
    [Fact]
    public void RefPathItem_ServerGeneratesOperationAndCompiles()
    {
        // Arrange + Act — run server generator for the scenario.
        var sources = CompilationVerificationHarness.RunServer(
            "ComponentsReuse",
            "ComponentsReuse.yaml");

        // The $ref path item (/items/{id} → components/pathItems/ItemById) must appear.
        // Before the cast fix, is OpenApiPathItem silently drops the ref — this assertion
        // catches that regression.
        Assert.Contains(
            sources,
            s => s.Source.Contains("getItemById", StringComparison.OrdinalIgnoreCase),
            "Expected getItemById operation from the $ref path item to be generated.");

        // Generated server must compile without errors.
        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources);
        Assert.True(
            errors.Count == 0,
            "Server for ComponentsReuse did not compile:\n" + string.Join("\n", errors));
    }

    [Fact]
    public void RefPathItem_ClientGeneratesOperationAndCompiles()
    {
        // Arrange + Act — run typed-client generator for the scenario.
        var sources = CompilationVerificationHarness.RunClient(
            "ComponentsReuse",
            "ComponentsReuse.yaml");

        // Same assertion: typed client must include getItemById.
        Assert.Contains(
            sources,
            s => s.Source.Contains("getItemById", StringComparison.OrdinalIgnoreCase),
            "Expected getItemById operation from the $ref path item in typed client.");

        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources);
        Assert.True(
            errors.Count == 0,
            "Typed client for ComponentsReuse did not compile:\n" + string.Join("\n", errors));
    }

    [Fact]
    public void ReusableMediaType_ItemSchemaIsEmittedAndCompiles()
    {
        // Arrange + Act — server output must contain the Item model (used via the
        // components.mediaTypes $ref proxy; verifies proxy resolution is *sufficient*,
        // not merely that .Schema is non-null).
        var sources = CompilationVerificationHarness.RunServer(
            "ComponentsReuse",
            "ComponentsReuse.yaml");

        Assert.Contains(
            sources,
            s => s.Source.Contains("class Item", StringComparison.Ordinal),
            "Expected Item model to be emitted (reached via components.mediaTypes proxy).");

        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources);
        Assert.True(
            errors.Count == 0,
            "Server for ComponentsReuse did not compile:\n" + string.Join("\n", errors));
    }
}
```

- [ ] **Step 2: Run the test to confirm it fails (proves the bug)**

Run from the repo root:
```
dotnet run --project test/Atc.Rest.Api.SourceGenerator.Tests/Atc.Rest.Api.SourceGenerator.Tests.csproj -- -class "*ComponentsReuseTests"
```

Expected: `RefPathItem_ServerGeneratesOperationAndCompiles` and `RefPathItem_ClientGeneratesOperationAndCompiles` FAIL with "Expected getItemById operation... to be generated." (The `$ref` path item is silently dropped today.) `ReusableMediaType_ItemSchemaIsEmittedAndCompiles` may also fail if the schema isn't attributed.

- [ ] **Step 3: Commit the failing test**

```bash
git add test/Atc.Rest.Api.SourceGenerator.Tests/Generators/ComponentsReuseTests.cs
git commit -m "test(openapi32): add ComponentsReuseTests — failing before cast-relaxation fix"
```

---

## Task 3: Relax all concrete path-item casts (Half 1 — the bulk fix)

**Files:** See file map above (~28 files across `src/`). Do NOT edit test files in this task.

**Critical understanding before starting:**
- A **pattern cast** like `if (x is not OpenApiPathItem item)` must become `if (x is not IOpenApiPathItem item)`. The variable `item` becomes `IOpenApiPathItem` — that's fine because `IOpenApiPathItem` exposes all members used (verified: `OpenApiPathItem` has zero concrete-only properties).
- A **method signature** like `void Foo(OpenApiPathItem pathItem)` must become `void Foo(IOpenApiPathItem pathItem)` — because the callers pass `IOpenApiPathItem` variables after their casts are relaxed.
- **`SpecificationService.SerializePathItem`** already takes `IOpenApiPathItem pathItem` but then casts to concrete inside the body. Fix it differently (see Step 2 below).
- **Property pattern** `is OpenApiPathItem { Operations.Count: > 0 }` → `is IOpenApiPathItem { Operations.Count: > 0 }` (same syntax; `Operations` is on the interface).

- [ ] **Step 1: Re-grep to get the authoritative site list**

Run this from the repo root to get every file and line needing a change:
```
grep -rn "is \(not \)\?OpenApiPathItem\b\|OpenApiPathItem pathItem\b" src/ --include="*.cs"
```

Collect the output. Every `is (not) OpenApiPathItem` is a pattern-cast site; every `OpenApiPathItem pathItem` where `pathItem` is a regular (non-`this`) parameter is a method-signature site.

- [ ] **Step 2: Fix `SpecificationService.SerializePathItem` (the special case)**

File: `src/Atc.Rest.Api.Generator/Services/SpecificationService.cs`

This method already takes `IOpenApiPathItem` as the parameter but immediately casts to concrete:

```csharp
// BEFORE (around line 1296):
private static void SerializePathItem(
    StringBuilder sb,
    IOpenApiPathItem pathItem,
    string indent)
{
    if (pathItem is not OpenApiPathItem item)
    {
        return;
    }

    if (item.Operations == null)
    {
        return;
    }

    foreach (var operation in item.Operations)
    {
        var method = operation
            .Key
            .ToString()
            .ToLowerInvariant();
        sb.AppendLine($"{indent}{method}:");
        SerializeOperation(sb, operation.Value, indent + "  ");
    }
}
```

Change to (remove the concrete cast entirely; use `pathItem` directly):

```csharp
// AFTER:
private static void SerializePathItem(
    StringBuilder sb,
    IOpenApiPathItem pathItem,
    string indent)
{
    if (pathItem.Operations == null)
    {
        return;
    }

    foreach (var operation in pathItem.Operations)
    {
        var method = operation
            .Key
            .ToString()
            .ToLowerInvariant();
        sb.AppendLine($"{indent}{method}:");
        SerializeOperation(sb, operation.Value, indent + "  ");
    }
}
```

- [ ] **Step 3: Fix pattern casts in all other files**

For each **pattern cast site** (every `is (not) OpenApiPathItem <var>` match from Step 1, except `SpecificationService.SerializePathItem` already done):

Change `OpenApiPathItem` to `IOpenApiPathItem`. The variable type changes implicitly. Examples:

```csharp
// BEFORE:
if (pathItemInterface is not OpenApiPathItem pathItem)
    return;
// AFTER:
if (pathItemInterface is not IOpenApiPathItem pathItem)
    return;

// BEFORE:
if (path.Value is not OpenApiPathItem pathItem || pathItem.Operations == null)
    continue;
// AFTER:
if (path.Value is not IOpenApiPathItem pathItem || pathItem.Operations == null)
    continue;

// BEFORE (property pattern, PathSegmentHelper.cs ~627):
if (path.Value is OpenApiPathItem { Operations.Count: > 0 })
// AFTER:
if (path.Value is IOpenApiPathItem { Operations.Count: > 0 })

// BEFORE (and-pattern, ApiServerDomainGenerator.cs ~855):
pathItemInterface is OpenApiPathItem pathItem)
// AFTER:
pathItemInterface is IOpenApiPathItem pathItem)
```

- [ ] **Step 4: Fix method signatures in `Atc.OpenApi` extension methods**

These are **public API methods** in `src/Atc.OpenApi/Extensions/` that take `OpenApiPathItem pathItem` as a non-`this` parameter. Change the parameter type to `IOpenApiPathItem pathItem`. The method bodies only access `.Extensions`, `.Parameters`, `.Servers` — all on the interface.

Files and signatures to update:

`src/Atc.OpenApi/Extensions/OpenApiSecurityExtensions.cs`:
```csharp
// BEFORE (~line 83):
public static (bool AuthRequired, ...) ExtractSecurityConfiguration(
    this OpenApiOperation operation,
    OpenApiPathItem pathItem,      // ← change this
    OpenApiDocument document)

// AFTER:
public static (bool AuthRequired, ...) ExtractSecurityConfiguration(
    this OpenApiOperation operation,
    IOpenApiPathItem pathItem,     // ← changed
    OpenApiDocument document)

// BEFORE (~line 269):
public static UnifiedSecurityConfig ExtractUnifiedSecurityConfiguration(
    this OpenApiOperation operation,
    OpenApiPathItem pathItem,      // ← change this
    OpenApiDocument document)

// AFTER:
public static UnifiedSecurityConfig ExtractUnifiedSecurityConfiguration(
    this OpenApiOperation operation,
    IOpenApiPathItem pathItem,     // ← changed
    OpenApiDocument document)
```

`src/Atc.OpenApi/Extensions/OpenApiRetryExtensions.cs`:
```csharp
// BEFORE (~line 116):
public static RetryConfiguration? ExtractRetryConfiguration(
    this OpenApiOperation operation,
    OpenApiPathItem pathItem,      // ← change this
    OpenApiDocument document)

// AFTER:
public static RetryConfiguration? ExtractRetryConfiguration(
    this OpenApiOperation operation,
    IOpenApiPathItem pathItem,     // ← changed
    OpenApiDocument document)
```

`src/Atc.OpenApi/Extensions/OpenApiRateLimitExtensions.cs`:
```csharp
// BEFORE (~line 68):
public static RateLimitConfiguration? ExtractRateLimitConfiguration(
    this OpenApiOperation operation,
    OpenApiPathItem pathItem,      // ← change this
    OpenApiDocument document)

// AFTER:
public static RateLimitConfiguration? ExtractRateLimitConfiguration(
    this OpenApiOperation operation,
    IOpenApiPathItem pathItem,     // ← changed
    OpenApiDocument document)
```

`src/Atc.OpenApi/Extensions/OpenApiCacheExtensions.cs`:
```csharp
// BEFORE (~line 103):
public static CacheConfiguration? ExtractCacheConfiguration(
    this OpenApiOperation operation,
    OpenApiPathItem pathItem,      // ← change this
    OpenApiDocument document)

// AFTER:
public static CacheConfiguration? ExtractCacheConfiguration(
    this OpenApiOperation operation,
    IOpenApiPathItem pathItem,     // ← changed
    OpenApiDocument document)
```

- [ ] **Step 5: Fix method signatures in extractors and helpers**

These are **private methods** within extractors; change `OpenApiPathItem pathItem` → `IOpenApiPathItem pathItem` in each signature. The method bodies use only `pathItem.Parameters`, `pathItem.Extensions`, `pathItem.Servers`, `pathItem.Operations` — all interface members.

`src/Atc.Rest.Api.Generator/Extractors/EndpointDefinitionExtractor.cs` — four private methods:
```csharp
// Change OpenApiPathItem to IOpenApiPathItem in each:
private static void GenerateSecurityMetadata(
    StringBuilder builder, OpenApiOperation operation,
    IOpenApiPathItem pathItem,  // was OpenApiPathItem
    OpenApiDocument openApiDoc, UnifiedSecurityConfig? groupSecurity)

private static void GenerateRateLimitingMetadata(
    StringBuilder builder, OpenApiOperation operation,
    IOpenApiPathItem pathItem,  // was OpenApiPathItem
    OpenApiDocument openApiDoc, RateLimitConfiguration? groupRateLimit)

private static void GenerateOutputCachingMetadata(
    StringBuilder builder, string httpMethod, OpenApiOperation operation,
    IOpenApiPathItem pathItem,  // was OpenApiPathItem
    OpenApiDocument openApiDoc, CacheConfiguration? groupOutputCache)

private static void GenerateProducesMetadata(
    StringBuilder builder, OpenApiDocument openApiDoc, OpenApiOperation operation,
    IOpenApiPathItem pathItem,  // was OpenApiPathItem
    string httpMethod, string projectName, string segment,
    SystemTypeConflictResolver systemTypeResolver,
    TypeConflictRegistry? registry = null)
```

`src/Atc.Rest.Api.Generator/Extractors/EndpointRegistrationExtractor.cs`:
```csharp
private static void GenerateEndpointMapping(
    StringBuilder builder, OpenApiDocument openApiDoc,
    IOpenApiPathItem pathItem,  // was OpenApiPathItem
    string path, string httpMethod, OpenApiOperation? operation, bool isFirst)
```

`src/Atc.Rest.Api.Generator/Extractors/EndpointPerOperationExtractor.cs` (private helper, ~line 233 — find it via grep for `OpenApiPathItem pathItem` in that file):
```csharp
// Same pattern: OpenApiPathItem pathItem → IOpenApiPathItem pathItem
```

`src/Atc.Rest.Api.Generator/Extractors/ResultClassExtractor.cs`:
```csharp
private static ClassParameters ExtractResultClass(
    OpenApiDocument openApiDoc, string projectName, string operationId,
    OpenApiOperation operationValue,
    IOpenApiPathItem pathItem,  // was OpenApiPathItem
    string httpMethod, string namespaceValue, string modelsNamespace, ...)
```

`src/Atc.Rest.Api.Generator/Helpers/OperationFeaturesHelper.cs`:
```csharp
public static Models.OperationFeatures DetectOperationFeatures(
    OpenApiOperation operation,
    IOpenApiPathItem pathItem,  // was OpenApiPathItem
    OpenApiDocument document, string httpMethod)
```

- [ ] **Step 6: Build — verify all changes compile**

```
dotnet build Atc.Rest.Api.SourceGenerator.slnx -c Release
```

Expected: 0 errors, 0 warnings. If any compile errors appear, they are due to a missed cast or method signature — fix them before proceeding. The error message will point directly to the site.

- [ ] **Step 7: Run the previously failing test — verify it now passes**

```
dotnet run --project test/Atc.Rest.Api.SourceGenerator.Tests/Atc.Rest.Api.SourceGenerator.Tests.csproj -- -class "*ComponentsReuseTests"
```

Expected: All 3 tests PASS.

- [ ] **Step 8: Commit**

```bash
git add src/
git commit -m "fix(openapi32): relax OpenApiPathItem casts to IOpenApiPathItem — components.pathItems \$ref now generates"
```

---

## Task 4: Anonymous-inline media type decision + ATC_API_SCH019 Warning

**Files:**
- Modify: `src/Atc.Rest.Api.Generator/RuleIdentifiers.cs`
- Modify: `src/Atc.Rest.Api.Generator/Validators/OpenApiDocumentValidator.cs`
- Modify: `test/Atc.Rest.Api.SourceGenerator.Tests/Generators/ComponentsReuseTests.cs`

The spec says: test the anonymous-inline case, then either keep it in scope (if a stable name is emitted) or wire a Warning diagnostic (if not). This task does BOTH: write the test first, observe the result, then wire the diagnostic.

- [ ] **Step 1: Write the anonymous-inline behavior test**

Add to `test/Atc.Rest.Api.SourceGenerator.Tests/Generators/ComponentsReuseTests.cs` (after the existing methods):

```csharp
[Fact]
public void AnonymousInlineMediaType_ProducesWarningDiagnostic()
{
    // A components.mediaTypes entry whose schema is an anonymous inline object
    // (no $ref to components.schemas, no title) is a best-effort case.
    // The generator should warn (ATC_API_SCH019) rather than silently emit
    // a mis-named or duplicated type.
    const string yaml = """
        openapi: "3.2.0"
        info:
          title: Anon Test
          version: 1.0.0
        paths:
          /things:
            get:
              operationId: listThings
              tags:
                - things
              responses:
                "200":
                  description: OK
                  content:
                    application/json:
                      $ref: '#/components/mediaTypes/ThingList'
        components:
          mediaTypes:
            ThingList:
              schema:
                type: array
                items:
                  type: object
                  properties:
                    id:
                      type: string
        """;

    var doc = Atc.Rest.Api.Generator.Helpers.OpenApiDocumentHelper.ParseYaml(yaml);

    var diagnostics = OpenApiDocumentValidator.Validate(
        ValidateSpecificationStrategy.Standard,
        doc,
        [],
        "anon-test.yaml");

    // ATC_API_SCH019: reusable media type wraps an anonymous inline schema.
    Assert.Contains(
        diagnostics,
        d => d.RuleId == RuleIdentifiers.AnonymousInlineMediaTypeSchema,
        "Expected ATC_API_SCH019 warning for anonymous inline schema in components.mediaTypes.");
}
```

- [ ] **Step 2: Run the test — it will FAIL (rule doesn't exist yet)**

```
dotnet run --project test/Atc.Rest.Api.SourceGenerator.Tests/Atc.Rest.Api.SourceGenerator.Tests.csproj -- -class "*ComponentsReuseTests" -method "*AnonymousInlineMediaType*"
```

Expected: FAIL with CS0117 (member not found) or test failure.

- [ ] **Step 3: Add ATC_API_SCH019 to RuleIdentifiers**

In `src/Atc.Rest.Api.Generator/RuleIdentifiers.cs`, after line ~339 (`SchemaNameCollision = "ATC_API_SCH018"`):

```csharp
    /// <summary>
    /// ATC_API_SCH019: A reusable media type (in components.mediaTypes) wraps an anonymous
    /// inline schema with no $ref to components.schemas. Reference a named schema instead to
    /// ensure a stable, single type emission.
    /// </summary>
    public const string AnonymousInlineMediaTypeSchema = "ATC_API_SCH019";
```

- [ ] **Step 4: Wire the diagnostic in OpenApiDocumentValidator**

In `src/Atc.Rest.Api.Generator/Validators/OpenApiDocumentValidator.cs`, find the main path-validation loop (the method that iterates `document.Paths` and calls sub-validators). After the existing path-item loop, add a new method call and implement the `components.mediaTypes` validation.

Find the method that calls the per-operation validators (search for `ValidateUnauthorizedResponse` or `ValidateBadRequestHasParameters` — these are called in a per-operation loop). Near the top of the public `Validate` method, add a call to the new helper:

```csharp
// After existing component validations (near where schemas/parameters are validated),
// add a call to ValidateComponentsMediaTypes:
ValidateComponentsMediaTypes(diagnostics, sourceFilePath, document);
```

Implement the new private method:

```csharp
/// <summary>
/// Warns when a components.mediaTypes entry wraps an anonymous inline schema
/// (no $ref to components.schemas). Such schemas cannot be given a stable name.
/// </summary>
private static void ValidateComponentsMediaTypes(
    List<DiagnosticMessage> diagnostics,
    string sourceFilePath,
    OpenApiDocument document)
{
    if (document.Components?.MediaTypes == null)
    {
        return;
    }

    foreach (var mediaType in document.Components.MediaTypes)
    {
        var schema = mediaType.Value?.Schema;
        if (schema == null)
        {
            continue;
        }

        // A $ref schema (OpenApiSchemaReference) resolves to a named components.schemas entry — OK.
        // An inline schema with no reference and no title/id is anonymous — warn.
        if (schema is not OpenApiSchemaReference && string.IsNullOrEmpty(schema.Title))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleId: RuleIdentifiers.AnonymousInlineMediaTypeSchema,
                Message: $"Reusable media type '{mediaType.Key}' in components.mediaTypes wraps an anonymous inline schema. " +
                         "Reference a named components.schemas entry instead to ensure a stable type name in generated code.",
                Severity: DiagnosticSeverity.Warning,
                FilePath: sourceFilePath));
        }
    }
}
```

**Note on array schemas:** A media type whose `schema.Type == "array"` with an inline `items` that is anonymous is also problematic. The check above (`schema is not OpenApiSchemaReference`) will catch the top-level array schema only if it lacks a title. The array case in the scenario (`ItemListContent` wraps `type: array, items: {$ref: Item}`) has a named-ref `items` — that's fine and will NOT trigger the warning. The anonymous case has `items: { type: object, properties: {...} }` with no title — the top-level array schema has no title → triggers the warning.

- [ ] **Step 5: Build and run the test — it should now pass**

```
dotnet build Atc.Rest.Api.SourceGenerator.slnx
dotnet run --project test/Atc.Rest.Api.SourceGenerator.Tests/Atc.Rest.Api.SourceGenerator.Tests.csproj -- -class "*ComponentsReuseTests"
```

Expected: All 4 tests PASS.

Also run the validator unit tests:
```
dotnet run --project test/Atc.Rest.Api.Generator.Tests/Atc.Rest.Api.Generator.Tests.csproj -- -class "*RuleIdentifiers*"
```

Confirm `ATC_API_SCH019` appears in rule-coverage tests if there are any (grep for `RuleCoverage` in the test project). If a coverage test checks that all rule IDs have associated tests, add a test named `*ATC_API_SCH019*` to satisfy it.

- [ ] **Step 6: Verify the ComponentsReuse scenario YAML does NOT trigger SCH019**

The `ItemListContent` entry wraps `type: array, items: { $ref: '#/components/schemas/Item' }`. The top-level array schema has no title → this WILL trigger SCH019 unless we exclude array schemas whose items are named refs.

Check: does the YAML validation test for the scenario (`YamlValidation_HasNoErrors`) pass?

Run:
```
dotnet run --project test/Atc.Rest.Api.Generator.IntegrationTests/Atc.Rest.Api.Generator.IntegrationTests.csproj -- -method "*YamlValidation_HasNoErrors*ComponentsReuse*"
```

If SCH019 fires for `ItemListContent`, adjust the YAML to give it a title OR adjust the SCH019 check to exclude array schemas where `.Items` is a named `$ref`:

```csharp
// Extended check in ValidateComponentsMediaTypes:
// For array schemas, look at whether items is a named ref (acceptable).
var effectiveSchema = (schema.Type == "array" && schema.Items is OpenApiSchemaReference) 
    ? null  // array with named-ref items — OK, skip
    : schema;

if (effectiveSchema == null)
    continue;

if (effectiveSchema is not OpenApiSchemaReference && string.IsNullOrEmpty(effectiveSchema.Title))
{
    // emit SCH019
}
```

Apply whichever approach keeps the scenario YAML clean and the SCH019 logic correct.

- [ ] **Step 7: Commit**

```bash
git add src/ test/Atc.Rest.Api.SourceGenerator.Tests/Generators/ComponentsReuseTests.cs
git commit -m "feat(openapi32): ATC_API_SCH019 — warn on anonymous inline schema in components.mediaTypes"
```

---

## Task 5: Add ComponentsReuse to compile-verification theories

**Files:**
- Modify: `test/Atc.Rest.Api.SourceGenerator.Tests/Generators/CompilationVerificationTests.cs`

- [ ] **Step 1: Add InlineData rows**

In `CompilationVerificationTests.cs`, add `"ComponentsReuse", "ComponentsReuse.yaml"` to three existing `[Theory]` blocks:

1. `ClientGenerator_GeneratedCode_CompilesWithoutErrors` (already has `[InlineData("StreamingItemSchema", ...)]` and `[InlineData("ParameterSerialization", ...)]`)

```csharp
// Add:
[InlineData("ComponentsReuse", "ComponentsReuse.yaml")]
```

2. `ClientGenerator_PerOperation_GeneratedCode_CompilesWithoutErrors`

```csharp
// Add:
[InlineData("ComponentsReuse", "ComponentsReuse.yaml")]
```

3. `ServerGenerator_GeneratedCode_CompilesWithoutErrors`

```csharp
// Add:
[InlineData("ComponentsReuse", "ComponentsReuse.yaml")]
```

- [ ] **Step 2: Run the theories to confirm they pass**

```
dotnet run --project test/Atc.Rest.Api.SourceGenerator.Tests/Atc.Rest.Api.SourceGenerator.Tests.csproj -- -class "*CompilationVerificationTests*"
```

Expected: All rows including the new `ComponentsReuse` rows PASS.

- [ ] **Step 3: Commit**

```bash
git add test/Atc.Rest.Api.SourceGenerator.Tests/Generators/CompilationVerificationTests.cs
git commit -m "test(openapi32): add ComponentsReuse to compile-verification theories"
```

---

## Task 6: Bootstrap integration test snapshots

**Files:**
- Create: many `test/Scenarios/ComponentsReuse/**/*.verified.cs` and `*.verified.ts` files

The integration test runner (`ScenarioTests`) auto-discovers scenarios and compares generated output against `.verified.*` snapshot files. Since this is a new scenario, there are no `.verified.*` files yet — the first run will generate `.received.*` files. Copy those to create the initial snapshots.

- [ ] **Step 1: Run integration tests for the new scenario**

```
dotnet run --project test/Atc.Rest.Api.Generator.IntegrationTests/Atc.Rest.Api.Generator.IntegrationTests.csproj -- -method "*ComponentsReuse*"
```

Expected: FAIL (no `.verified.*` files exist yet). This generates `.received.*` files in the scenario directory.

- [ ] **Step 2: Inspect the received files**

Look at the generated output in `test/Scenarios/ComponentsReuse/`. Verify:
- Server: `IGetItemByIdHandler.verified.cs` (or similar) exists and looks correct
- Client-Typed: has methods for both `listItems` and `getItemById`
- `Item.verified.cs` exists in the models folder (proves the schema is emitted)
- No files reference `OpenApiPathItemReference` or other internal type names

If anything looks wrong (e.g., `getItemById` is missing, or `Item` is missing), stop and debug before accepting snapshots. The compile-verification tests from Task 5 should already confirm correctness — if they pass, the output is structurally sound.

- [ ] **Step 3: Copy received → verified**

PowerShell (run from repo root):
```powershell
Get-ChildItem -Path "test\Scenarios\ComponentsReuse" -Filter "*.received.*" -Recurse |
  ForEach-Object {
    $verified = $_.FullName -replace '\.received\.', '.verified.'
    Copy-Item $_.FullName $verified
    Remove-Item $_.FullName
  }
```

- [ ] **Step 4: Strip any stray BOM from `.verified.ts` files**

The known BOM oscillation issue (see project memory) may affect newly created `.verified.ts` files. Strip BOMs to match the BOM-free convention:

```powershell
Get-ChildItem -Path "test\Scenarios\ComponentsReuse" -Filter "*.verified.ts" -Recurse |
  ForEach-Object {
    $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
      [System.IO.File]::WriteAllBytes($_.FullName, $bytes[3..($bytes.Length - 1)])
      Write-Host "Stripped BOM: $($_.FullName)"
    }
  }
```

- [ ] **Step 5: Run integration tests again — confirm they pass**

```
dotnet run --project test/Atc.Rest.Api.Generator.IntegrationTests/Atc.Rest.Api.Generator.IntegrationTests.csproj -- -method "*ComponentsReuse*"
```

Expected: All ComponentsReuse scenario tests PASS with no diff.

- [ ] **Step 6: Commit snapshots**

```bash
git add test/Scenarios/ComponentsReuse/
git commit -m "test(openapi32): bootstrap ComponentsReuse scenario snapshots"
```

---

## Task 7: Full build + run all tests

**Goal:** Confirm the entire suite is green in Release mode and no existing scenario regressions were introduced.

- [ ] **Step 1: Release build**

```
dotnet build Atc.Rest.Api.SourceGenerator.slnx -c Release
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 2: Run all tests**

```
dotnet run --project test/Atc.Rest.Api.Generator.Tests/Atc.Rest.Api.Generator.Tests.csproj
dotnet run --project test/Atc.Rest.Api.SourceGenerator.Tests/Atc.Rest.Api.SourceGenerator.Tests.csproj
dotnet run --project test/Atc.Rest.Api.Generator.IntegrationTests/Atc.Rest.Api.Generator.IntegrationTests.csproj
```

Expected: All pass. If integration tests report `.received.*` diffs for any existing scenario (other than `ComponentsReuse`), those are regressions introduced by the cast-relaxation — investigate and fix before proceeding.

**Common regression to watch for:** the cast relaxation changes behaviour for path items that are `$ref`s; existing scenarios only use inline path items, so `.Operations` resolves identically. The only change visible in existing snapshots would be if a validator or policy extractor that previously bailed out early on a ref now processes something new. Since existing scenarios have no path-item refs, no diff is expected.

- [ ] **Step 3: Final commit (if any cleanup needed from test run)**

If the test run reveals any stray `.received.*` files needing acceptance, accept them, commit, and re-run until clean.

```bash
git add -A
git commit -m "fix(openapi32): clean up any snapshot diffs after cast relaxation"
```
