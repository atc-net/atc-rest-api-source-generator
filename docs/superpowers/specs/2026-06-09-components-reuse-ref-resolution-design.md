# Components Reuse / `$ref` Resolution — OpenAPI 3.2 Phase 4 (increment 1)

**Date:** 2026-06-09
**Branch:** `feature/openapi32`
**Roadmap:** [`docs/roadmap-openapi32-support.md`](../../roadmap-openapi32-support.md) — Phase 4 "Components reuse" (`components.mediaTypes`, new in 3.2) **and** the pre-3.2 gap "Path Item `$ref` / `components.pathItems`" (3.1).

## Problem

The generator iterates `document.Paths` and request/response `content`, but it does not
tolerate **reference objects** in two places:

1. **Path-item `$ref`** — `paths./foo: { $ref: '#/components/pathItems/Foo' }`. This is
   valid since OpenAPI 3.1 and is the carrier for `components.pathItems` reuse. Today every
   path-enumeration site casts with the concrete pattern `is OpenApiPathItem`, which **does
   not match** the `OpenApiPathItemReference` proxy the reader produces — so a referenced
   path item is **silently dropped**: no endpoint, handler, client method, policy, or
   validation for it. No error, no warning.

2. **Media-type `$ref`** — `content.application/json: { $ref: '#/components/mediaTypes/Shared' }`.
   `components.mediaTypes` is the single new `components` key in **3.2**. Media-type access
   goes through `.Content[...]` with **no** concrete cast, so the transparent
   `OpenApiMediaTypeReference` proxy *appears* to work — but whether the schema **inside** a
   reusable media type gets a stable name and is emitted as a model is unverified (this is
   structurally the same failure mode as the Phase 2 "`itemSchema`-only model not generated"
   bug, where a schema reachable only through an indirection was not attributed to its segment).

## Verified facts (Microsoft.OpenApi 3.6.0)

Probed empirically against the installed package before designing:

- `OpenApiComponents` exposes **both** `PathItems` and `MediaTypes` dictionaries.
- The reader produces proxy types `OpenApiPathItemReference` and `OpenApiMediaTypeReference`
  (alongside `OpenApiSchemaReference`, etc.).
- **The proxies are transparent.** Parsing `paths./viaref: { $ref: '#/components/pathItems/Reusable' }`
  yields a `document.Paths["/viaref"]` whose runtime type is `OpenApiPathItemReference`, yet
  `.Operations` resolves (count 1) — the proxy delegates member access to its target. Likewise
  a media-type `$ref` parses as `OpenApiMediaTypeReference` and `.Schema` resolves to the
  target's `OpenApiSchema`.
- `OpenApiPathItem` and `OpenApiPathItemReference` **both implement `IOpenApiPathItem`**.
  The full `IOpenApiPathItem` surface (including base interfaces) is `Operations`, `Parameters`,
  `Servers`, `Description`, `Summary`, `Extensions` (+ serialize/copy). **`OpenApiPathItem` has
  zero properties absent from the interface** — so relaxing a cast from the concrete type to the
  interface cannot drop any member access.
- A 3.2.0 document with both reuse forms parses with **no diagnostic errors**.

**Consequence:** this is not a resolver rewrite. The parser already resolves internal
`#/components/...` references via transparent proxies. The work is (a) letting the proxies flow
through the path-enumeration casts, and (b) proving the media-type-reuse schema is emitted.

## Scope

In scope:

- **Path-item `$ref` (single document):** a `$ref` to `components.pathItems/X` works
  **everywhere** a path item is consumed — endpoint definition/registration, handlers, C# typed
  client, C# per-op client, TS clients (Axios + Fetch), TS hooks, *and* the cross-cutting readers
  (rate-limit / output-cache / hybrid-cache / resilience / security policies, OpenIdConnect),
  the validators, the CLI, and statistics. A referenced path item behaves **identically** to an
  inline one. (Full-uniformity coverage decision.)
- **`components.mediaTypes` reuse (single document):** a media-type `$ref` to
  `components.mediaTypes/X` whose `schema` is a **named `$ref`** to `components.schemas/Foo`
  resolves through the proxy and emits/references `Foo` via the existing schema-collection path
  (`PathSegmentHelper.GetSchemasUsedBySegment` already walks `content.Value.Schema`, and `.Schema`
  resolves through the proxy).
- **Anonymous-inline schema decision (explicit):** a `components.mediaTypes` entry whose `schema`
  is an inline anonymous object (no `components.schemas` name) is **tested explicitly**. If the
  generator does not produce a stable, single name+emission for it, the increment emits a focused
  **Warning diagnostic** and falls back to best-effort (current behavior), rather than emitting
  mis-named or duplicated types. This boundary is decided here, not discovered at test time. (The
  test result determines whether a small `GetSchemasUsedBySegment`-style attribution fix is added
  for the named-ref path; the anonymous path stays flagged regardless.)

Out of scope (deferred, gated where it could mislead):

- **Cross-file / multi-part components reuse** — a path-item or media-type `$ref` in a part file
  (`MyApi_Users.yaml`) resolving against `components` in the base file. `MergeSpecifications`
  currently merges only `Schemas`/`Parameters`/`Paths`/`Tags`; carrying `PathItems`/`MediaTypes`
  through the clone-and-merge and verifying the proxy `Target` survives is a separate increment.
- **External-file `$ref`s** (`./other.yaml#/...`) — the reader is configured without
  `LoadExternalRefs`; only internal `#/components/...` references are in scope.
- **Path-item-`$ref` sibling `summary`/`description` overrides** — relaxing the cast to the
  interface uses the proxy's resolved values; any reference-site sibling override is **not**
  separately surfaced. Accepted loss (doc-comment only).

## The approach

### Half 1 — Path-item `$ref`: relax the casts (Approach A)

Replace the concrete pattern cast `is (not) OpenApiPathItem <var>` with
`is (not) IOpenApiPathItem <var>` at every path-enumeration site (~45 across `src/`). This
mirrors the established schema-ref idiom: extraction consumes the **interface**
(`IOpenApiSchema`), and only the few sites needing reference *identity* (e.g.
`PathSegmentHelper` schema-ref check, validation) test `is OpenApiSchemaReference`. No
path-enumeration site needs path-item identity — the route comes from the `document.Paths`
**key**, which is preserved.

Rejected alternative — **Approach C (inline at parse boundary):** walk `document.Paths`
post-parse and replace each `OpenApiPathItemReference` with its concrete `.Target`, so the
existing casts succeed unchanged. Rejected because it mutates a document consumed by
server-gen / client-gen / TS-gen / CLI / validator / statistics (global blast radius for a
convenience), drops path-item-ref sibling `summary`/`description`, and introduces a
mutate-and-inline pattern that exists nowhere else in this codebase. Since the member-usage
check showed **no** site needs a concrete-only member, A is strictly the better fit.

**Site inventory** (the cast appears as `is (not) OpenApiPathItem`; relax to `IOpenApiPathItem`):

- `src/Atc.Rest.Api.Generator/Extractors/`: `EndpointDefinitionExtractor.cs`
  (lines ~153, 197, 246, 706, 731, 753, 790, 812, 860, 888), `EndpointRegistrationExtractor.cs`
  (~203), `EndpointPerOperationExtractor.cs` (~170), `HandlerExtractor.cs` (~65),
  `HttpClientExtractor.cs` (~182), `OperationParameterExtractor.cs` (~257),
  `OutputCachePoliciesExtractor.cs` (~65), `HybridCachePoliciesExtractor.cs` (~65),
  `RateLimitPoliciesExtractor.cs` (~67), `ResiliencePoliciesExtractor.cs` (~67),
  `SecurityPoliciesExtractor.cs` (~53), `ServerDependencyInjectionExtractor.cs` (~50),
  `OpenIdConnectConfigExtractor.cs` (~82).
- `src/Atc.Rest.Api.Generator/Helpers/PathSegmentHelper.cs` (~150, 627, 658).
- `src/Atc.Rest.Api.Generator/Services/`: `CodeGenerationService.cs` (~207, 347, 1908),
  `SpecificationService.cs` (~1296).
- `src/Atc.Rest.Api.Generator/Validators/OpenApiDocumentValidator.cs` (~1806, 1842, 1947).
- `src/Atc.OpenApi/Extensions/`: `OpenApiCacheExtensions.cs` (~214, 285),
  `OpenApiRateLimitExtensions.cs` (~143), `OpenApiRetryExtensions.cs` (~241),
  `OpenApiSecurityExtensions.cs` (~650).
- `src/Atc.Rest.Api.SourceGenerator/`: `ApiServerDomainGenerator.cs` (~855),
  `Extractors/EndpointInjectionExtractor.cs` (~40).
- `src/Atc.Rest.Api.Generator.Cli/`: `Services/TypeScriptClientGenerationService.cs` (~235),
  `Services/Migration/ParameterNameMigrator.cs` (~109),
  `Extractors/TypeScript/TypeScriptOperationHelper.cs` (~31, 675),
  `Extractors/TypeScript/TypeScriptRetryConfigExtractor.cs` (~118),
  `Extractors/TypeScript/TypeScriptWebhookExtractor.cs` (~33).

The exact line numbers are a snapshot — the implementer must re-grep
`is (not )?OpenApiPathItem\b` across `src/` to get the authoritative set, and confirm each
relaxed site compiles (all members used are interface members; verified none are concrete-only).

### Half 2 — `components.mediaTypes`: verification-led, with a naming boundary

No code change is expected for the **named-`$ref`** case: `.Content[...].Schema` resolves
through the transparent `OpenApiMediaTypeReference` proxy, and the inner named schema is already
collected by `PathSegmentHelper.GetSchemasUsedBySegment`. The scenario + compile test prove this
is *sufficient* (the type is actually generated and referenced), not merely that `.Schema` is
non-null.

For the **anonymous-inline** case, write the test first and let it decide:

- If the anonymous schema gets a stable name+single emission → keep it in scope, document the
  naming.
- If not (mis-named / duplicated / dropped) → emit a focused Warning diagnostic
  ("reusable media type `X` wraps an anonymous inline schema; reference a named
  `components.schemas` schema instead") and fall back to best-effort. Allocate a new
  `RuleIdentifiers` entry (Warning severity, never blocks generation), following the
  `ATC_API_OPR026` precedent.

## Per-layer plan

| Layer | Change |
|---|---|
| **Path-enumeration sites (~45)** | Relax `is (not) OpenApiPathItem` → `is (not) IOpenApiPathItem`. No member-access changes (interface covers all members). |
| **Validator** (`OpenApiDocumentValidator`) | Same cast relaxation at its 3 path-item sites, so a `$ref` path item is validated rather than silently skipped. |
| **`components.mediaTypes`** | No code unless the anonymous-inline test surfaces a `GetSchemasUsedBySegment`-style attribution gap for the named-ref path; the anonymous path gets a Warning diagnostic + best-effort fallback regardless. |
| **`RuleIdentifiers` + validator** | New Warning rule id for the anonymous-inline-reusable-media-type case (only wired if the test shows it's needed). |
| **Scenario + harness** | New `Scenarios/ComponentsReuse/` + add to the compile-verification harness theories (client + server). |

## Testing — prove generation, not snapshot-green

1. **Compile-verification (the gate).** Add `ComponentsReuse` to the client + server
   `CompilationVerificationTests` theories (the `StreamingItemSchema` pattern: generate → compile
   → `GetDiagnostics()` clean). Assert specifically:
   - the operation under the **path-item `$ref`** produces a compiling endpoint + handler + client
     method (i.e. it was not silently skipped);
   - the schema wrapped by the **reusable media type** is generated as a type and referenced by the
     operation's request/response (proxy resolution is *sufficient*).
2. **Pre/post cast behavior.** A focused test (or scenario assertion) that, before the cast
   relaxation, the ref'd path produces **no** operation, and after, it produces the expected
   operation — locking the fix against regression.
3. **Validator test.** A spec with a path-item `$ref` is validated (no false "path has no
   operations" skip) once casts are relaxed; a malformed/dangling ref still surfaces the reader's
   parse diagnostic.
4. **Anonymous-inline decision test.** A `components.mediaTypes` entry with an inline anonymous
   schema → assert either (a) a single stable named type is emitted (if in scope) or (b) the new
   Warning diagnostic is produced and generation proceeds (if deferred). The test encodes whichever
   the implementation lands on.
5. **Scenario snapshots.** `Scenarios/ComponentsReuse/` across Server / Client-Typed /
   Client-Operation / TS-Client-Axios / TS-Client-Fetch (+ TS-Hooks-ReactQuery if a GET op is
   present), exercising one path-item `$ref` op and one reusable-media-type (named-ref) op.

## Risks / open questions

- **Cast site that uses an extension member off the variable** — the probe proved
  `OpenApiPathItem` has zero concrete-only properties, so this cannot happen; the implementer
  still compiles each relaxed site to confirm.
- **Anonymous-inline naming** — the genuine unknown; resolved by writing test #4 first. The
  fallback (Warning + best-effort) keeps the build green either way.
- **Shared-instance aliasing** — two paths `$ref`-ing the same `components.pathItems` entry share
  one resolved instance. The generator treats path items / operations as read-only and routes by
  dictionary key, so no cross-contamination; noted for the implementer not to introduce mutation.
- **BOM oscillation** — the known `.verified.ts` BOM noise (`BrandedIds`, `ZodRuntimeValidate`)
  may recur on TS regeneration; strip to match BOM-free siblings (Verify is BOM-blind).
