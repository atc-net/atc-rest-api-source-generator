# Parameter Serialization (style / explode / allowReserved) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Serialize array query parameters correctly (form/explode repeated keys) and consistently across the typed C# client, per-op C# client, and TypeScript client so they round-trip with ASP.NET Core's native binding, honor `allowReserved` where the client owns encoding, and emit a Warning diagnostic for declared-but-unsupported styles instead of silently producing wrong wire output.

**Architecture:** A parser-independent `ParameterSerialization` seam in `Atc.OpenApi` computes effective `style`/`explode`/`allowReserved`/value-kind + an `IsSupported` flag from an `OpenApiParameter`. Each emitter switches on it: the typed client emits a repeated-key `foreach` for form-explode arrays (was: `.ToString()` garbage); the TS client widens its query type and uses `searchParams.append`; the per-op client is already correct via the external builder. A new validator rule warns on unsupported style/explode combos; generation proceeds best-effort. Exotic styles and custom server binding are explicitly deferred.

**Tech Stack:** .NET 10 / C# 14, Roslyn source generators (netstandard2.0 libs), `Microsoft.OpenApi` 3.6.0, TypeScript emitters, xUnit v3 native runner.

**Spec:** [`docs/superpowers/specs/2026-06-09-parameter-serialization-design.md`](../specs/2026-06-09-parameter-serialization-design.md)

---

## Conventions (this repo)

- Build: `dotnet build Atc.Rest.Api.SourceGenerator.slnx` (add `-c Release` for warnings-as-errors).
- **Tests run on the xUnit v3 native in-process runner.** Filter with `-class "*Name"` / `-method "*Name"` (single dash) — NOT `--filter-method`. Invoke: `dotnet run --project <testproj.csproj> -- -class "*Foo"`.
- Integration snapshots (`test/Atc.Rest.Api.Generator.IntegrationTests`) compare generated output to `.verified.cs`/`.verified.ts` under `test/Scenarios/`; on mismatch a `.received.*` is written. Accept by copying `.received`→`.verified` (Verify writes BOM-free; do not hand-add a BOM). One mismatch per scenario per run — iterate.
- PowerShell: no `Get-Content -Raw` (use `[System.IO.File]::ReadAllText()`); write non-trivial PS to a `.ps1`, run `powershell -ExecutionPolicy Bypass -File`.
- Coding standards: file-scoped namespaces, `var`, camelCase private fields (no underscore), XML docs on public members, Release 0/0. `Atc.OpenApi` is netstandard2.0.
- **"Target generated output"** blocks are the contract for an emit change — write extractor code until the snapshot equals that text.

## Plan-level decisions (refinements over the spec, baked in here)

1. **Reuse `Microsoft.OpenApi.ParameterStyle`** for the style enum rather than duplicating it — the extractors already reference `Microsoft.OpenApi`, and `OpenApiParameter.Style` is a `ParameterStyle?`. (The spec sketched an own-enum; reusing the parser enum is cleaner and avoids a redundant mapping. We still define our own `ParameterValueKind` + `ParameterSerialization` record.)
2. **The validator warning fires on `!IsSupported` (style/explode/value-kind) only.** `allowReserved` is honored by the typed + TS clients, so a doc-wide `allowReserved` warning would falsely warn those users. The per-op client's inability to honor `allowReserved` (external builder always escapes) is a **documented known limitation**, not a per-spec diagnostic.

## File structure

**Create:**
- `src/Atc.OpenApi/ParameterSerialization.cs` — `ParameterValueKind` enum + `ParameterSerialization` readonly record struct.
- `src/Atc.OpenApi/Extensions/OpenApiParameterExtensions.cs` — `GetParameterSerialization()` extension (if a parameter-extensions file already exists, add to it instead).
- `test/Scenarios/ParameterSerialization/ParameterSerialization.yaml` — focused scenario (one form-explode array-query op, one `allowReserved` op, one unsupported-style op).

**Modify:**
- `src/Atc.Rest.Api.Generator/RuleIdentifiers.cs` — add `ParameterSerializationNotSupported = "ATC_API_OPR026"`.
- `src/Atc.Rest.Api.Generator/Validators/DiagnosticBuilder.cs` — add `ParameterSerializationNotSupportedWarning(...)`.
- `src/Atc.Rest.Api.Generator/Validators/OpenApiDocumentValidator.cs` — emit it in the `ValidateOperations` parameter loop.
- `src/Atc.Rest.Api.Generator/Extractors/HttpClientExtractor.cs` — typed client: array form-explode repeated keys + `allowReserved`.
- `src/Atc.Rest.Api.Generator.Cli/Extractors/TypeScript/TypeScriptFetchApiClientExtractor.cs` and `TypeScriptAxiosApiClientExtractor.cs` — query type widening + `buildUrl` append.
- `src/Atc.Rest.Api.Generator.Cli/Extractors/TypeScript/TypeScriptClientExtractor.cs` — ensure array params flow into the query object (verify; adjust if it stringifies).
- `test/Atc.Rest.Api.SourceGenerator.Tests/Generators/...` — round-trip URI-capture test + server array-param-type assertion.
- `docs/roadmap-openapi32-support.md` — flip the two pre-3.2 param rows.

---

## Task 1: `ParameterSerialization` seam in `Atc.OpenApi`

**Files:**
- Create: `src/Atc.OpenApi/ParameterSerialization.cs`
- Create (or extend): `src/Atc.OpenApi/Extensions/OpenApiParameterExtensions.cs`
- Test: `test/Atc.Rest.Api.Generator.Tests/Extensions/OpenApiParameterExtensionsTests.cs`

- [ ] **Step 1: Write failing tests**

First READ an existing extensions-test in `test/Atc.Rest.Api.Generator.Tests/Extensions/` (e.g. `OpenApiOperationExtensionsTests.cs`) and reuse its `OpenApiParameter` construction idiom. Add `OpenApiParameterExtensionsTests.cs`:

```csharp
namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class OpenApiParameterExtensionsTests
{
    [Fact]
    public void GetParameterSerialization_QueryArray_DefaultsToFormExplodeSupported()
    {
        var param = new OpenApiParameter
        {
            Name = "tags",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
        };

        var s = param.GetParameterSerialization();

        Assert.Equal(ParameterStyle.Form, s.Style);
        Assert.True(s.Explode);                 // form default
        Assert.Equal(ParameterValueKind.Array, s.ValueKind);
        Assert.True(s.IsSupported);
        Assert.False(s.AllowReserved);
    }

    [Fact]
    public void GetParameterSerialization_QueryPrimitive_DefaultsToFormSupported()
    {
        var param = new OpenApiParameter { Name = "q", In = ParameterLocation.Query, Schema = new OpenApiSchema { Type = JsonSchemaType.String } };

        var s = param.GetParameterSerialization();

        Assert.Equal(ParameterStyle.Form, s.Style);
        Assert.Equal(ParameterValueKind.Primitive, s.ValueKind);
        Assert.True(s.IsSupported);
    }

    [Fact]
    public void GetParameterSerialization_PathPrimitive_DefaultsToSimpleSupported()
    {
        var param = new OpenApiParameter { Name = "id", In = ParameterLocation.Path, Schema = new OpenApiSchema { Type = JsonSchemaType.String } };

        var s = param.GetParameterSerialization();

        Assert.Equal(ParameterStyle.Simple, s.Style);
        Assert.True(s.IsSupported);
    }

    [Theory]
    [InlineData(ParameterStyle.SpaceDelimited)]
    [InlineData(ParameterStyle.PipeDelimited)]
    [InlineData(ParameterStyle.DeepObject)]
    public void GetParameterSerialization_ExoticArrayStyle_NotSupported(ParameterStyle style)
    {
        var param = new OpenApiParameter
        {
            Name = "tags",
            In = ParameterLocation.Query,
            Style = style,
            Schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
        };

        Assert.False(param.GetParameterSerialization().IsSupported);
    }

    [Fact]
    public void GetParameterSerialization_FormArrayExplodeFalse_NotSupported()
    {
        var param = new OpenApiParameter
        {
            Name = "tags",
            In = ParameterLocation.Query,
            Style = ParameterStyle.Form,
            Explode = false,
            Schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
        };

        var s = param.GetParameterSerialization();
        Assert.False(s.Explode);
        Assert.False(s.IsSupported);  // comma-join deferred
    }

    [Fact]
    public void GetParameterSerialization_AllowReserved_IsCaptured()
    {
        var param = new OpenApiParameter { Name = "q", In = ParameterLocation.Query, AllowReserved = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String } };

        Assert.True(param.GetParameterSerialization().AllowReserved);
    }
}
```

(Confirm the exact `OpenApiSchema.Type`/`JsonSchemaType` and `ParameterStyle`/`AllowReserved` member names against the installed `Microsoft.OpenApi` 3.6.0 — adjust the construction to match the package, the tests are the contract for behavior.)

- [ ] **Step 2: Run — expect FAIL** (`GetParameterSerialization`/`ParameterValueKind` undefined).

Run: `dotnet run --project test/Atc.Rest.Api.Generator.Tests/Atc.Rest.Api.Generator.Tests.csproj -- -class "*OpenApiParameterExtensionsTests"`

- [ ] **Step 3: Create `src/Atc.OpenApi/ParameterSerialization.cs`**

```csharp
namespace Atc.OpenApi;

/// <summary>The shape of a parameter's value, which (with style/explode) determines serialization.</summary>
public enum ParameterValueKind
{
    /// <summary>A scalar (string, number, boolean, enum).</summary>
    Primitive,

    /// <summary>An array of items.</summary>
    Array,

    /// <summary>An object with properties.</summary>
    Object,
}

/// <summary>
/// The effective serialization of an OpenAPI parameter (RFC 6570 / OpenAPI style+explode),
/// plus whether this generator serializes it correctly today. <see cref="Style"/> reuses
/// <see cref="Microsoft.OpenApi.ParameterStyle"/>.
/// </summary>
public readonly record struct ParameterSerialization(
    ParameterStyle Style,
    bool Explode,
    bool AllowReserved,
    ParameterValueKind ValueKind,
    bool IsSupported);
```

- [ ] **Step 4: Add `GetParameterSerialization()`**

In `src/Atc.OpenApi/Extensions/OpenApiParameterExtensions.cs` (create the file with the same `extension(...)`/usings style as the sibling extension files; if a parameter-extensions file already exists, add the method there):

```csharp
namespace Atc.OpenApi.Extensions;

public static class OpenApiParameterExtensions
{
    extension(OpenApiParameter parameter)
    {
        /// <summary>
        /// Computes the effective style/explode/allowReserved and value-kind for the parameter,
        /// and whether this generator serializes that combination correctly (form primitive any
        /// explode; form array with explode:true; simple primitive). Unsupported combinations
        /// return <c>IsSupported = false</c> so the caller can emit a diagnostic and fall back.
        /// </summary>
        public ParameterSerialization GetParameterSerialization()
        {
            var valueKind = GetValueKind(parameter.Schema);

            var style = parameter.Style ?? DefaultStyleFor(parameter.In);
            var explode = parameter.Explode ?? (style == ParameterStyle.Form);
            var allowReserved = parameter.AllowReserved ?? false;

            var isSupported = style switch
            {
                ParameterStyle.Form => valueKind == ParameterValueKind.Primitive
                    || (valueKind == ParameterValueKind.Array && explode),
                ParameterStyle.Simple => valueKind == ParameterValueKind.Primitive,
                _ => false,
            };

            return new ParameterSerialization(style, explode, allowReserved, valueKind, isSupported);
        }
    }

    private static ParameterStyle DefaultStyleFor(ParameterLocation? location)
        => location is ParameterLocation.Query or ParameterLocation.Cookie
            ? ParameterStyle.Form
            : ParameterStyle.Simple;

    private static ParameterValueKind GetValueKind(IOpenApiSchema? schema)
    {
        if (schema is null)
        {
            return ParameterValueKind.Primitive;
        }

        if (schema.Type?.HasFlag(JsonSchemaType.Array) == true)
        {
            return ParameterValueKind.Array;
        }

        if (schema.Type?.HasFlag(JsonSchemaType.Object) == true || (schema.Properties is { Count: > 0 }))
        {
            return ParameterValueKind.Object;
        }

        return ParameterValueKind.Primitive;
    }
}
```

(Match the exact nullable shapes of `OpenApiParameter.Style`/`Explode`/`AllowReserved` and `IOpenApiSchema.Type` in 3.6.0. If `In` is non-nullable, drop the `?`.)

- [ ] **Step 5: Run — expect PASS.** Then `dotnet build Atc.Rest.Api.SourceGenerator.slnx` (0 errors; no emit changed, no snapshot churn).

- [ ] **Step 6: Commit**

```bash
git add src/Atc.OpenApi/ParameterSerialization.cs src/Atc.OpenApi/Extensions/OpenApiParameterExtensions.cs test/Atc.Rest.Api.Generator.Tests/Extensions/OpenApiParameterExtensionsTests.cs
git commit -m "feat(params): add ParameterSerialization classifier seam"
```

---

## Task 2: Validation diagnostic for unsupported styles

**Files:**
- Modify: `src/Atc.Rest.Api.Generator/RuleIdentifiers.cs`, `Validators/DiagnosticBuilder.cs`, `Validators/OpenApiDocumentValidator.cs`
- Test: `test/Atc.Rest.Api.SourceGenerator.Tests/Validators/OperationValidationTests.cs` (or the validator-tests file that already exists — confirm by reading it)

- [ ] **Step 1: Write failing validator tests**

Read the existing validator test file (e.g. `test/Atc.Rest.Api.SourceGenerator.Tests/Validators/OperationValidationTests.cs`) and mirror how it builds a doc + calls `OpenApiDocumentValidator.Validate` + asserts a diagnostic id. Add:

```csharp
[Fact]
public void Validate_QueryArray_SpaceDelimited_EmitsParameterSerializationWarning()
{
    var document = /* build a doc with GET /items?tags (array, style: spaceDelimited) — reuse the file's builder */;

    var diagnostics = OpenApiDocumentValidator.Validate(document, "spec.yaml");

    Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.ParameterSerializationNotSupported && d.Severity == DiagnosticSeverity.Warning);
}

[Fact]
public void Validate_QueryArray_DefaultFormExplode_NoSerializationWarning()
{
    var document = /* GET /items?tags (array, no style) */;

    var diagnostics = OpenApiDocumentValidator.Validate(document, "spec.yaml");

    Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.ParameterSerializationNotSupported);
}
```

- [ ] **Step 2: Run — expect FAIL** (`ParameterSerializationNotSupported` undefined).

- [ ] **Step 3: Add the rule id** — in `RuleIdentifiers.cs`, in the `OPR` section:

```csharp
/// <summary>
/// ATC_API_OPR026: Parameter declares a style/explode serialization this generator does not
/// yet support; default form serialization is emitted instead.
/// </summary>
public const string ParameterSerializationNotSupported = "ATC_API_OPR026";
```

- [ ] **Step 4: Add the DiagnosticBuilder method** — in `DiagnosticBuilder.cs` (mirror `NamingConventionWarning`'s shape — confirm the exact `DiagnosticMessage` ctor/params by reading it):

```csharp
public static DiagnosticMessage ParameterSerializationNotSupportedWarning(
    string parameterName,
    string styleDescription,
    string sourceFilePath)
    => new DiagnosticMessage(
        RuleIdentifiers.ParameterSerializationNotSupported,
        $"Parameter '{parameterName}' declares {styleDescription}, which is not yet supported; emitting default form serialization.",
        DiagnosticSeverity.Warning,
        sourceFilePath,
        GetDocUrl(RuleIdentifiers.ParameterSerializationNotSupported));
```

(Match the actual `DiagnosticMessage` record shape and the `GetDocUrl` usage in that file.)

- [ ] **Step 5: Emit it in `OpenApiDocumentValidator.ValidateOperations`** — inside the existing `foreach (var parameter in operation.Parameters)` loop (after the name-casing check, ~line 206), resolving the parameter first if needed:

```csharp
if (parameter is OpenApiParameter p)
{
    var serialization = p.GetParameterSerialization();
    if (!serialization.IsSupported)
    {
        diagnostics.Add(DiagnosticBuilder.ParameterSerializationNotSupportedWarning(
            p.Name ?? "(unnamed)",
            $"style '{serialization.Style}' explode={serialization.Explode} on {serialization.ValueKind}",
            sourceFilePath));
    }
}
```

(Use whatever resolve/cast the surrounding code uses for `parameter`. Add `using Atc.OpenApi;` / `using Atc.OpenApi.Extensions;` if needed.)

- [ ] **Step 6: Run — expect PASS.** Build `-c Release` 0/0.

- [ ] **Step 7: Commit** `feat(params): warn (ATC_API_OPR026) on unsupported parameter style/explode`.

---

## Task 3: Typed C# client — form-explode array serialization + allowReserved

**Files:**
- Modify: `src/Atc.Rest.Api.Generator/Extractors/HttpClientExtractor.cs` (query-param emit ~lines 637–684; `BuildEncodedExpression`/`NeedsUrlEncoding` ~1341–1410)
- Test: `test/Atc.Rest.Api.Generator.Tests/Extractors/HttpClientExtractorTests.cs`

**Background:** today an array query param emits `queryParams.Add($"tags={Uri.EscapeDataString($"{parameters.Tags}")}")` — `$"{parameters.Tags}"` stringifies the collection (garbage), because `NeedsUrlEncoding` returns `false` for `[]`. Fix: for a form-explode array, emit a `foreach` adding one `name={encoded-element}` per element (repeated keys). The optional-array null guard (`!= null && .Length > 0`) already exists.

- [ ] **Step 1: Write failing test**

In `HttpClientExtractorTests.cs` (mirror existing tests that assert generated client text for an operation), add a test for an operation with an array query param (`tags: array of string`) asserting the generated client body:
- contains `foreach (var item in parameters.Tags)` and `queryParams.Add($"tags={Uri.EscapeDataString(item)}");`
- does NOT contain `$"{parameters.Tags}"` (the broken form).

- [ ] **Step 2: Run — expect FAIL.**

Run: `dotnet run --project test/Atc.Rest.Api.Generator.Tests/Atc.Rest.Api.Generator.Tests.csproj -- -class "*HttpClientExtractorTests"`

- [ ] **Step 3: Implement the array branch**

In the query-param loop, switch on `param.GetParameterSerialization()`. For `ValueKind == Array && Style == Form && Explode` (the supported array case), emit the repeated-key foreach instead of the single `queryParams.Add`. Target generated output for a required array `tags` of `string`:

```csharp
foreach (var item in parameters.Tags)
{
    queryParams.Add($"tags={Uri.EscapeDataString(item)}");
}
```

For an optional array, keep the existing `if (parameters.Tags != null && parameters.Tags.Length > 0) { <foreach> }` guard around the foreach. For a non-string element type (e.g. `int`), encode via interpolation like `BuildEncodedExpression` does: `Uri.EscapeDataString($"{item}")` (or skip encoding for URL-safe element types per `NeedsUrlEncoding`). For `allowReserved == true`, emit the element WITHOUT `Uri.EscapeDataString` (raw `{item}`). Primitive params keep the existing path; unsupported combos (per Task 2) fall through to the existing single-value path (best-effort) — the Task-2 warning already fired.

Add a helper in `HttpClientExtractor` (e.g. `BuildQueryArrayForeach(paramName, accessExpr, elementType, allowReserved)`) returning the emitted lines, to keep the loop readable.

- [ ] **Step 4: Run — expect PASS.** Build 0/0.

- [ ] **Step 5: Accept snapshots** — run the integration suite; `PetStoreFull` `findPetsByTags`/`findPetsByStatus` typed-client snapshots flip from the broken `$"{parameters.Tags}"` to the foreach. Inspect each `.received.cs`, confirm it matches the target, promote `.received`→`.verified`. Confirm no non-array query params changed.

- [ ] **Step 6: Commit** `fix(params): typed client emits form-explode repeated keys for array query params`.

---

## Task 4: TypeScript client — array query params + allowReserved

**Files:**
- Modify: `TypeScriptFetchApiClientExtractor.cs` (`buildUrl` ~433–447; query type decl ~97 / ~435), `TypeScriptAxiosApiClientExtractor.cs` (same methods), `TypeScriptClientExtractor.cs` (query-object assembly)
- Test: `test/Atc.Rest.Api.Generator.Cli.Tests/Extractors/TypeScript/TypeScriptClientExtractorTests.cs`

- [ ] **Step 1: Write failing test** asserting the generated TS `buildUrl` uses `searchParams.append` for array values and the query type accepts arrays (e.g. `(string | number | boolean)[]`), and that an array-query op passes the array into the query object.

- [ ] **Step 2: Run — expect FAIL.**

Run: `dotnet run --project test/Atc.Rest.Api.Generator.Cli.Tests/Atc.Rest.Api.Generator.Cli.Tests.csproj -- -class "*TypeScriptClientExtractorTests"`

- [ ] **Step 3: Widen the query type + append in `buildUrl`** (both Fetch and Axios extractors). Target generated output:

```typescript
buildUrl(path: string, query?: Record<string, string | number | boolean | (string | number | boolean)[] | undefined>): string {
  const url = new URL(`${this.baseUrl}${path}`);
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value === undefined) {
        continue;
      }
      if (Array.isArray(value)) {
        for (const item of value) {
          url.searchParams.append(key, String(item));
        }
      } else {
        url.searchParams.set(key, String(value));
      }
    }
  }
  return url.toString();
}
```

Also widen the `query?: Record<...>` field on the `RequestOptions` type (~line 97) to the same union.

- [ ] **Step 4: Verify array params flow into the query object** — in `TypeScriptClientExtractor.cs`, confirm array query params are passed into the `query` object as the array (not `String(...)`/joined). If it stringifies, fix it to pass the array through. (`allowReserved`: `URL.searchParams` always percent-encodes; honoring `allowReserved` in TS requires building the query string manually. Scope: emit array/scalar correctly now; if an `allowReserved` param is present, note it as a TS limitation in a comment — the C# typed client honors it, TS via `searchParams` does not. Keep this minimal; do not block.)

- [ ] **Step 5: Run — expect PASS.** Accept snapshots: `buildUrl` churns across all TS scenarios' `ApiClient.verified.ts` (additive — array branch + widened type). Confirm additive-only; no stray BOM. Array-query ops (PetStoreFull) gain array-passing.

- [ ] **Step 6: Commit** `fix(params): TS client serializes array query params as repeated keys`.

---

## Task 5: Round-trip proof + scenario + per-op verification + finalize

**Files:**
- Create: `test/Scenarios/ParameterSerialization/ParameterSerialization.yaml` (+ accept all generated snapshots)
- Modify: `test/Atc.Rest.Api.SourceGenerator.Tests/Generators/` (round-trip test), `docs/roadmap-openapi32-support.md`

- [ ] **Step 1: Add the focused scenario** `test/Scenarios/ParameterSerialization/ParameterSerialization.yaml`:

```yaml
openapi: "3.0.3"
info:
  version: 1.0.0
  title: Parameter Serialization Test
paths:
  /items:
    get:
      operationId: listItems
      tags: [items]
      parameters:
        - name: tags
          in: query
          required: false
          schema:
            type: array
            items:
              type: string
        - name: q
          in: query
          required: false
          allowReserved: true
          schema:
            type: string
        - name: legacy
          in: query
          required: false
          style: spaceDelimited
          schema:
            type: array
            items:
              type: string
      responses:
        "200":
          description: OK
          content:
            application/json:
              schema:
                type: array
                items:
                  $ref: "#/components/schemas/Item"
components:
  schemas:
    Item:
      type: object
      title: Item
      properties:
        id:
          type: string
```

(This exercises: a supported form-explode array `tags`, an `allowReserved` primitive `q`, and an unsupported `spaceDelimited` array `legacy` → Task-2 warning. The scenario is auto-discovered by the integration tests.)

- [ ] **Step 2: Write the round-trip URI-capture test**

In `test/Atc.Rest.Api.SourceGenerator.Tests/Generators/`, add a test that compiles+loads the `ParameterSerialization` typed client (reuse `CompilationVerificationHarness.RunClient` + `EmitAndLoad`), constructs it with an `HttpClient` backed by a capturing handler, calls `ListItemsAsync(new ... { Tags = new[] {"a","b"} })`, and asserts the captured request URI query is `tags=a&tags=b` (repeated keys). This proves the client emits the form-explode wire format that ASP.NET binds natively.

```csharp
[Fact]
public async Task TypedClient_ArrayQueryParam_SerializesAsRepeatedKeys()
{
    var clientType = LoadGeneratedType("...Client");          // the generated client class
    var capturing = new CapturingHandler();                    // records request.RequestUri
    var httpClient = new HttpClient(capturing) { BaseAddress = new Uri("https://x") };
    var client = Activator.CreateInstance(clientType, httpClient)!;
    // build the parameters object via reflection, set Tags = new[]{"a","b"}, invoke ListItemsAsync
    // ...
    Assert.Contains("tags=a&tags=b", capturing.LastUri!.Query);
}
```

(Add a tiny `CapturingHandler : HttpMessageHandler` returning `200 []`. Adapt the reflection to the generated method/parameters signature — read the generated `ParameterSerialization` Client-Typed snapshot to get exact names.)

- [ ] **Step 3: Assert the generated server binds repeated keys** — add a test asserting the generated server's `listItems` request-parameter type for `tags` is a natively-bindable array (`string[]` or `List<string>`), and a minimal hosted sanity check that ASP.NET .NET 10 binds `?tags=a&tags=b` to `string[]` (a hand-written `app.MapGet("/x", (string[] tags) => tags.Length)` via `WebApplicationFactory`/`TestServer`, asserting 2). If the generated server array type does NOT bind (e.g. `List<T>` fails), switch the emitted array query-param type to `T[]` in the server generator and re-accept the server snapshot. (Document the outcome.)

- [ ] **Step 4: Verify per-op client unchanged + correct** — confirm the `ParameterSerialization` Client-Operation snapshot emits `requestBuilder.WithQueryParameter("tags", parameters.Tags)` resolving to the `IEnumerable` overload (repeated keys). No code change expected; this is a snapshot/verification step.

- [ ] **Step 5: Accept all `ParameterSerialization` snapshots** (Server, Client-Typed, Client-Operation, TS-Client-Axios, TS-Client-Fetch, TS-Hooks-ReactQuery) and confirm the Task-2 warning is produced for `legacy` (spaceDelimited).

- [ ] **Step 6: Full verification** — `dotnet build -c Release` 0/0; full integration suite green; the new round-trip + extension + validator tests green.

- [ ] **Step 7: Update `docs/roadmap-openapi32-support.md`** — in the pre-3.2 table, flip `Parameter style / explode serialization` and `allowReserved on query parameters` from ❌ toward ✅/🟡: form-explode arrays + allowReserved (typed/TS) done; exotic styles deferred behind ATC_API_OPR026; note the per-op allowReserved limitation. Be precise (don't overclaim — exotic styles + custom server binding remain).

- [ ] **Step 8: Commit** `feat(params): round-trip-verified array query serialization + ParameterSerialization scenario`.

---

## Self-review notes (carried)

- Per-op `allowReserved` is a documented limitation (external builder always escapes), not a diagnostic — avoids false warnings for typed/TS users who DO honor it.
- The round-trip proof is client-URI-capture + server-bind-sanity, not a full generated-server host (tractable; the heavier hosted round-trip is a future option).
- TS `allowReserved` via `URL.searchParams` always encodes — noted as a TS limitation; C# typed client honors it.
- Exotic styles + custom server binding are explicitly out of scope, gated by ATC_API_OPR026.
