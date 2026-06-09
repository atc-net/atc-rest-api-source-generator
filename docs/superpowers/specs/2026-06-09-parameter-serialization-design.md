# Parameter Serialization (style / explode / allowReserved) — Pre-3.2 gap #1

**Date:** 2026-06-09
**Branch:** `feature/openapi32`
**Roadmap:** [`docs/roadmap-openapi32-support.md`](../../roadmap-openapi32-support.md) — "Missing pre-3.2 features" → parameter `style`/`explode` serialization + `allowReserved` (the **root** gap; unblocks Phase 4 cookie-`style`/`allowReserved`).

## Problem

The generator extracts parameters but ignores their **serialization** (`style`, `explode`,
`allowReserved`). Concretely, **array (and object) query parameters do not round-trip**:

- **Typed C# client** (`HttpClientExtractor`) emits, for an array param `tags`:
  `queryParams.Add($"tags={Uri.EscapeDataString($"{parameters.Tags}")}");` —
  `$"{parameters.Tags}"` calls `.ToString()` on the collection, producing escaped garbage
  (`tags=System.Collections.Generic.List%601...`). **Broken.**
- **TypeScript client** types `query` as `Record<string, string | number | boolean | undefined>`
  (arrays excluded) and `buildUrl` uses `url.searchParams.set(key, String(value))` —
  overwrite, not repeated keys. **Broken for arrays.**
- **Per-operation C# client** is **already correct**: it routes array params through the
  external `Atc.Rest.Client` `IMessageRequestBuilder.WithQueryParameter(string, IEnumerable)`
  overload, which emits **repeated keys** (`tags=a&tags=b`) via a `&{name}=` join + a
  `#`-prefix "pre-encoded" marker.

So the three layers disagree on the wire format for arrays, and none reads the declared
`style`/`explode`/`allowReserved`. This is the same **correctness gate** as the streaming
work: the generated client and generated server must agree on the wire format, and that
format is fixed by the spec — there is no negotiation.

### What ASP.NET Core binds natively (verified)

Minimal-API query binding reads **repeated keys** (`?tags=a&tags=b`) into `T[]` / `string[]`
(and complex `T[]` when `T` has `TryParse`). This is exactly OpenAPI `style: form, explode: true`
— the **default** for query arrays. It does **not** natively bind `spaceDelimited`,
`pipeDelimited`, `deepObject`, `explode:false` comma-joined, `matrix`, or `label` — those need
a custom `TryParse`/`BindAsync`/model binder. So the default style round-trips with **zero
server work**; exotic styles would require generated server binding.

## Scope

In scope — the **default / common serialization set**, made correct and consistent across all
layers, round-tripping with native ASP.NET binding:

- Read `Style`, `Explode`, `AllowReserved` from `OpenApiParameter` (exposed by `Microsoft.OpenApi`).
- Compute the **effective** style/explode (OpenAPI defaults): location `query`/`cookie` →
  `form`; `path`/`header` → `simple`. `explode` defaults to `true` when style is `form`, else
  `false`.
- Serialize correctly for the supported set:
  - `form` + `explode:true` **array** → repeated keys (`?tags=a&tags=b`)
  - `form` **primitive** → `name=value` (as today)
  - `simple` **primitive** path/header (as today)
- **`allowReserved`**: where the client owns encoding (typed C# client, TS client), skip
  percent-encoding of RFC 3986 reserved characters (`:/?#[]@!$&'()*+,;=`) on the value.
- **New validation diagnostic (Warning severity)**: emitted when a parameter declares a combo
  this increment does not serialize correctly — `spaceDelimited`, `pipeDelimited`, `deepObject`,
  `matrix`, `label`, `form`+`explode:false` (comma-join), or `allowReserved` on the per-op
  client path. Generation **proceeds** best-effort (default-style serialization). This prevents
  silent-wrong output while keeping the build green.

Out of scope (deferred to later increments, gated by the diagnostic):

- Exotic-style **client** serialization (spaceDelimited/pipeDelimited/deepObject/explode:false
  comma-join/matrix/label).
- Custom **server** binding (`TryParse`/`BindAsync`/`deepObject` model binder) for exotic styles.
- Honoring `allowReserved` on the **per-op** client (the external builder always
  `Uri.EscapeDataString`s — see [Per-op constraint](#per-operation-client-constraint)).
- Object (non-array) query params beyond what falls out of the form-primitive path.
- 3.2 cookie `style` and `allowReserved`-on-headers/any-`in` (Phase 4 — this increment is the
  3.0 foundation they build on).

## The shared seam

A small helper (mirrors the `StreamingFraming` seam) computes the serialization decision once;
every emitter switches on it. Lives in `src/Atc.OpenApi/` (parser-independent, consumable by all
extractors):

```csharp
namespace Atc.OpenApi;

public enum ParameterStyle { Form, Simple, SpaceDelimited, PipeDelimited, DeepObject, Matrix, Label }

public enum ParameterValueKind { Primitive, Array, Object }

public readonly record struct ParameterSerialization(
    ParameterStyle Style,
    bool Explode,
    bool AllowReserved,
    ParameterValueKind ValueKind,
    bool IsSupported);   // false → emit the diagnostic + best-effort default serialization
```

Plus an extension on `OpenApiParameter` (next to the existing parameter helpers):

```csharp
// Resolves effective style/explode from the parameter + its schema kind, and decides
// whether this increment serializes it correctly.
public ParameterSerialization GetParameterSerialization();
```

`IsSupported` is true only for: `Form` (primitive any explode; array with `explode:true`) and
`Simple` (primitive). Everything else → `IsSupported = false`.

## Per-layer plan

| Layer | Change |
|---|---|
| **`Atc.OpenApi`** | `ParameterStyle`/`ParameterValueKind`/`ParameterSerialization` + `GetParameterSerialization()`; unit-tested in isolation. |
| **Typed C# client** (`HttpClientExtractor`) | For `form`+`explode` **array** params, emit a `foreach` adding repeated `name={encoded}` entries (replacing the broken `$"{parameters.X}"`); primitives unchanged; `allowReserved` → emit the value without `Uri.EscapeDataString` of reserved chars. Switch on `GetParameterSerialization()`. |
| **Per-op C# client** (`EndpointPerOperationExtractor`) | Verify array params resolve to `WithQueryParameter(string, IEnumerable)` (already correct repeated-keys). No serialization change. `allowReserved`-declared → contributes to the diagnostic only. |
| **TS client** (`TypeScriptFetch/AxiosApiClientExtractor`, `TypeScriptClientExtractor`) | Widen the `query` type to accept `string[]`/`number[]` (arrays); `buildUrl` uses `searchParams.append` per element for arrays (repeated keys), `set` for scalars. `allowReserved` → build those params without `encodeURIComponent` of reserved chars. |
| **Validator** (`src/Atc.Rest.Api.Generator/Validators/`) | New rule (new `RuleIdentifiers` entry, Warning) scanning each operation's parameters; emits when `GetParameterSerialization().IsSupported == false` (or `allowReserved` on a per-op-targeted spec), with a clear "style/explode X on parameter Y not yet supported; emitting default form serialization" message. |
| **Server** | Verify generated array query params bind from repeated keys natively. If the generated request-parameter type for an array uses `List<T>` and that doesn't bind, switch to `T[]` (or add binding). Confirmed/locked by the round-trip test; likely no change. |

### Per-operation client constraint

The per-op client serializes through the external, dictionary-backed `Atc.Rest.Client`
`IMessageRequestBuilder` (`Dictionary<string,string> queryMapper`, always `Uri.EscapeDataString`).
Array form-explode already works (the `IEnumerable` overload + `#`-prefix). But `allowReserved`
(suppressing escaping) is not expressible through that builder without a runtime change — so for
the per-op path `allowReserved` is **flagged by the diagnostic**, not honored. (An optional future
`Atc.Rest.Client` enhancement — a non-escaping query overload — would be documented separately,
as was done for streaming; not a dependency of this increment.)

## Testing — prove the wire string, not snapshot-green

1. **Extension unit tests** (`OpenApiParameterExtensionsTests` or similar): `GetParameterSerialization`
   over the matrix — location × declared style/explode × value kind (primitive/array/object) —
   asserting effective style/explode/allowReserved and `IsSupported`. Include defaults
   (no style declared) and each unsupported style → `IsSupported:false`.
2. **Emitter assertions**: typed client emits `tags=a&tags=b` (repeated keys, encoded) for a
   form-explode array — and specifically NOT `$"{parameters.Tags}"`; TS `buildUrl` uses `.append`
   for arrays; per-op uses the `IEnumerable` overload. `allowReserved` param emitted without
   reserved-char encoding.
3. **Validator tests**: a spec param with each unsupported style → the new Warning diagnostic is
   produced; a supported param → no diagnostic.
4. **Round-trip** (the gate): extend the compile/round-trip harness — a scenario with an array
   query param, generated client serializes the query, generated server (hosted or its bound
   parameter type) reads back the same array. Reuse the `CompilationVerificationHarness` /
   `StreamingWireFramingTests` patterns (compile → load → assert), or a focused
   `WebApplicationFactory`/`DefaultHttpContext`-level binding test.
5. **Scenario + snapshots**: `PetStoreFull` `findPetsByTags`/`findPetsByStatus` already declare
   array query params — their typed-client + TS snapshots flip from broken to correct. Add a
   focused scenario if a cleaner minimal repro is wanted (e.g. `ParameterSerialization` with one
   array-query op + one `allowReserved` op + one unsupported-style op for the diagnostic).

## Risks / open questions

- **Server array binding type** — confirm whether the generated request-parameter type for an
  array query param is `T[]` (native bind) or `List<T>` (verify it binds from repeated keys);
  the round-trip test is the arbiter. May require switching the emitted type to `T[]`.
- **`allowReserved` reserved-char set** — implement as "do not `EscapeDataString`" (emit raw) vs
  a precise RFC 3986 reserved-only passthrough. Start with the simpler "skip escaping for
  allowReserved params" and document it; the round-trip/string test pins behavior.
- **TS `query` type widening** churns the TS client base (`ApiClient.ts`) across all TS scenarios
  (additive — arrays added to the union). Expect broad, additive `.verified.ts` churn (same shape
  as the streaming `StreamFraming` addition); keep encoding consistent (no stray BOM).
- **Diagnostic id** — allocate a new `ATC_API_*` rule id in `RuleIdentifiers`; Warning severity so
  it never blocks generation.
