# OpenAPI 3.2 Support Roadmap

This roadmap tracks the work required to support
[OpenAPI Specification v3.2.0](https://spec.openapis.org/oas/v3.2.0.html)
(announced [2025-09-23](https://www.openapis.org/blog/2025/09/23/announcing-openapi-v3-2),
[release notes](https://github.com/OAI/OpenAPI-Specification/releases/tag/3.2.0)) in
`Atc.Rest.Api.SourceGenerator`.

It maps every 3.2 feature against the **current** state of the generator (which
supports OpenAPI 3.0.x and 3.1.x today) and classifies the implementation status.

## Status legend

| Emoji | Meaning |
|-------|---------|
| ✅ | Done — fully implemented and exercised by a scenario/test |
| 🟡 | Partial — a related capability exists, but the 3.2 specifics are not wired in |
| ❌ | Not started — recognized by the parser, but not consumed or generated |
| ⬜ | Out of scope / low priority — documentation-only or not relevant to code generation |

Two dimensions matter for every feature:

- **Parser** — does the underlying object model (`Microsoft.OpenApi`) expose it?
- **Generator** — does *our* validation/extraction/generation layer consume it?

## Foundation: the parser is not a blocker

The single most important finding: the project already references
**`Microsoft.OpenApi` / `Microsoft.OpenApi.YamlReader` 3.6.0** (the latest, published
2026-01-06), and that version **already ships the complete 3.2.0 object model**.

Verified against the installed package the following 3.2 members exist:
`OpenApiSpecVersion.OpenApi3_2`, `AdditionalOperations`, `ItemSchema`, `QueryString`,
`PrefixEncoding`, `DeviceAuthorization`, `OAuth2MetadataUrl`, `DefaultMapping`,
`NodeType`, and `$self`.

**Consequence:** there is no upstream dependency upgrade or parser-rewrite blocker.
Every item below is work inside this repository — in the validators
(`src/Atc.Rest.Api.Generator/Validators/`), the extractors
(`src/Atc.Rest.Api.Generator/Extractors/`), the OpenAPI helpers
(`src/Atc.OpenApi/`), and the code generators.

| Item | Parser | Generator | Notes |
|-------|:------:|:---------:|-------|
| `Microsoft.OpenApi` exposes 3.2 object model | ✅ | — | Already on 3.6.0; no upgrade needed |
| Accept `openapi: 3.2.0` as a known version | ✅ | ✅ | **Phase 0 done.** Parse-time stash of `OpenApiDiagnostic.SpecificationVersion` into the document's (non-serialized) `Metadata` bag, exposed via `OpenApiDocument.GetOpenApiSpecVersion()`. `OpenApiDocumentValidator` now rejects only the real OpenAPI 2.0 spec version (was incorrectly keyed off `info.version`) and recognizes 3.0/3.1/3.2 |
| Version-aware diagnostics (3.2 vs 3.1 vs 3.0) | ✅ | 🟡 | **Phase 0 foundation done.** The spec version is now reachable from code holding only the document (`GetOpenApiSpecVersion()`), so later phases can gate behavior on `OpenApi3_2`. No 3.2-specific extraction branch exists yet (intentional — Phases 1+) |

## Current baseline (3.0 / 3.1)

For context, these already-supported capabilities are the foundation the 3.2 work
builds on:

- OpenAPI 3.0.x and 3.1.x parsing and validation (standard + strict modes)
- Schemas/models, enums, polymorphism (`oneOf`/`anyOf`/`allOf` + `discriminator`)
- 3.1 JSON Schema features: type arrays, `$ref` siblings, `const`,
  `unevaluatedProperties`, `prefixItems` (tuples), `contentEncoding`/`contentMediaType`
- Webhooks (3.1)
- Parameters (path, query, header, cookie), request bodies, multipart
- Security schemes: `http`, `apiKey`, `oauth2`, `openIdConnect`
- Servers and server variable resolution
- Server + C# client + TypeScript client (with React Query hooks) generation

## Feature matrix

### Tags — multipurpose, with nesting

| Feature | Parser | Generator | Notes |
|---------|:------:|:---------:|-------|
| `Tag.summary` | ✅ | ❌ | New field; emit into doc comments |
| `Tag.parent` (nesting) | ✅ | ❌ | Could drive hierarchical namespace / folder grouping |
| `Tag.kind` (e.g. `nav`) | ✅ | ❌ | Free-form classification; could filter which tags affect grouping |

Today tags are used implicitly via the first path segment for namespace grouping.
Adopting 3.2 tags means optionally honoring `parent`/`kind` to drive grouping and
emitting `summary` into doc comments.
### HTTP methods

| Feature | Parser | Generator | Notes |
|---------|:------:|:---------:|-------|
| `query` method | ✅ | ❌ | New safe-with-body method. Needs mapping to a minimal-API route + client call. ASP.NET Core has no `MapQuery`; requires `MapMethods("QUERY", ...)` |
| `additionalOperations` (custom methods, e.g. `LINK`) | ✅ | ❌ | Map of non-standard methods as full Operation Objects. Endpoint/handler/client extractors currently iterate the fixed operation set only |

This is the largest structural change: extractors that enumerate
`pathItem.Operations` must also enumerate `query` and `additionalOperations`.
### Document identity & URL resolution

| Feature | Parser | Generator | Notes |
|---------|:------:|:---------:|-------|
| `$self` top-level field | ✅ | ❌ | Base URI for relative-reference resolution. Mostly a parsing/merge concern; low impact on generated code but relevant to multi-part spec merging |

### Streaming & sequential media types

| Feature | Parser | Generator | Notes |
|---------|:------:|:---------:|-------|
| `itemSchema` on a media type | ✅ | ✅ | Drives streaming detection (`GetStreamingResponse`) and the per-element type across all layers |
| `text/event-stream` (SSE) | ✅ | ✅ | First-party `TypedResults.ServerSentEvents` (server) + `SseParser` (C# client) + `data:` parse (TS); wire-byte tested |
| `application/jsonl`, `application/json-seq`, `multipart/mixed` | ✅ | ✅ | All emit/read real per-media-type framing; `IAsyncEnumerable<T>` (server) + async iterator (clients); round-trip wire-byte tested |

Recommended target shape: server handlers return `IAsyncEnumerable<TItem>`; C# client
exposes streaming reads; TypeScript client exposes an async iterator. This generalizes
the existing TS streaming heuristic onto a real spec signal.
### Parameters & headers

| Feature | Parser | Generator | Notes |
|---------|:------:|:---------:|-------|
| `in: querystring` location | ✅ | ❌ | Entire query string parsed as one value via `content`. New parameter location alongside path/query/header/cookie |
| `Parameter.summary` | ✅ | ❌ | New short-description field for path/query/header/cookie parameters |
| `Header.summary` | ✅ | ❌ | New short-description field for response headers |
| `allowReserved` on headers / any `in` | ✅ | ❌ | Affects percent-encoding behavior; relevant to client serialization |
| `style: cookie` for cookie content | ✅ | ❌ | No `style`/`explode` serialization handling exists today at all |

Note: the generator does **not** currently implement parameter `style`/`explode`
serialization, so 3.2 additions here land on top of a pre-existing gap.
### Multipart media types

| Feature | Parser | Generator | Notes |
|---------|:------:|:---------:|-------|
| `itemSchema` for multipart items | ✅ | ✅ | `multipart/mixed` streaming shipped (Phase 2) — emitted `MultipartMixedResult<T>` + boundary-delimited reader. See streaming row |
| `prefixEncoding` / `itemEncoding` | ✅ | ❌ | Replace `encoding` for multipart. Multipart is supported today via schema flattening, but not these new encoding fields |

### XML representation

| Feature | Parser | Generator | Notes |
|---------|:------:|:---------:|-------|
| `xml.nodeType` (`element`/`attribute`/`text`/`cdata`/`none`) | ✅ | ⬜ | Generator targets JSON APIs; XML modeling is low priority unless XML content support is on the roadmap |
| `attribute`/`wrapped` deprecation in favor of `nodeType` | ✅ | ⬜ | Same scope note |
| `xml` allowed on any Schema Object; IRI namespaces | ✅ | ⬜ | Low priority |

### Examples

| Feature | Parser | Generator | Notes |
|---------|:------:|:---------:|-------|
| `Example.dataValue` (structured) | ✅ | ❌ | Example handling is limited today (mostly used for x-* config). Useful for generated docs/tests/mock data |
| `Example.serializedValue` | ✅ | ❌ | Wire-format example |
| `externalValue` clarified as serialized | ✅ | ⬜ | Documentation clarification |

### Security schemes

| Feature | Parser | Generator | Notes |
|---------|:------:|:---------:|-------|
| OAuth2 Device Authorization flow (`deviceAuthorization`, `deviceAuthorizationUrl`) | ✅ | ❌ | New flow type. `OAuthFlowsInfo`/`OAuthFlowInfo` models would need a device-flow arm |
| `oauth2MetadataUrl` | ✅ | ❌ | Auth-server metadata URL (RFC 8414) |
| `SecurityScheme.summary` | ✅ | ❌ | New short-description field for security schemes |
| `deprecated` on a security scheme | ✅ | ❌ | Emit `[Obsolete]`/deprecation notes where schemes flow into generated code |
| Reference a security scheme by URI | ✅ | ❌ | Resolution concern in security extractors |

The four existing scheme types (`http`, `apiKey`, `oauth2`, `openIdConnect`) are
supported; 3.2 extends `oauth2` and adds scheme-level metadata.

### Server Object

| Feature | Parser | Generator | Notes |
|---------|:------:|:---------:|-------|
| `Server.name` | ✅ | ❌ | New field alongside `description`/`url`/`variables` |
| URL must not include fragment/query | ✅ | 🟡 | `ServerUrlHelper` resolves variables and base paths; should validate the no-fragment/query rule |
| ABNF for variable substitution / single-use variables | ✅ | 🟡 | Variable resolution exists; ABNF conformance and single-use validation are not enforced |

### Polymorphism & Data Modeling

| Feature | Parser | Generator | Notes |
|---------|:------:|:---------:|-------|
| Optional `discriminator.propertyName` | ✅ | 🟡 | `PolymorphicTypeExtractor` currently assumes a discriminator property; needs to tolerate absence if the property is defined in the schema |
| `discriminator.defaultMapping` | ✅ | ❌ | Fallback schema when the value is missing/unrecognized — maps to a default concrete type in deserialization |
| **Annotated Enumerations** (`oneOf`/`anyOf` + `const`) | ✅ | ❌ | Pattern for associating metadata (description, deprecated) with individual enum members. Generator should map this to a real C# enum with attributes or a TS union/enum with JSDoc |
| **Generic Data Structures** (`$dynamicAnchor`/`$dynamicRef`) | ✅ | ❌ | Formal support for "template" schemas (e.g. `PaginatedResponse<T>`). Generator should map these to C#/TS Generics instead of duplicating types |
| `Schema.summary` | ✅ | ❌ | New short-description field for models; emit into C# `<summary>` or TS JSDoc |

### Response Object

| Feature | Parser | Generator | Notes |
|---------|:------:|:---------:|-------|
| Optional `description` | ✅ | 🟡 | Generators may assume a description is present; verify nullable handling |
| `Response.summary` | ✅ | ❌ | New short-description field; emit into doc comments |

### Components reuse

| Feature | Parser | Generator | Notes |
|---------|:------:|:---------:|-------|
| `components.mediaTypes` (reusable Media Type Objects) | ✅ | ✅ | **Phase 4-1 done.** `$ref` to a `components.mediaTypes` entry resolves transparently via `OpenApiMediaTypeReference` proxy — named-schema items are collected and emitted correctly. Anonymous inline schemas (no `$ref` to `components.schemas`, no title) emit Warning `ATC_API_SCH019` with best-effort fallback; array schemas whose `.Items` is a named `$ref` are excluded from the warning. Reference scenario: `test/Scenarios/ComponentsReuse/` |

> Note: `components.pathItems` (reusable Path Item Objects) was introduced in
> **OpenAPI 3.1** and is now **supported** — see the
> [Missing pre-3.2 features](#missing-pre-32-features-30--31-gaps) section.

### Link Object & runtime expressions

| Feature | Parser | Generator | Notes |
|---------|:------:|:---------:|-------|
| Formal ABNF for path templating / server variables / Link runtime expressions | ✅ | ⬜ | Links are not generated today; documentation/validation only |

## Missing pre-3.2 features (3.0 / 3.1 gaps)

Several capabilities from **earlier** OpenAPI versions are still not supported and
are worth closing before — or alongside — the 3.2 work, because some 3.2 features
build directly on them (e.g. 3.2's `allowReserved`-on-any-`in` and cookie `style`
extend serialization machinery that does not exist yet). The engineering roadmap
([`docs/roadmap.md`](roadmap.md)) already flags "Full OpenAPI 3.1 + JSON Schema
2020-12 — currently ~80%" as the highest-leverage near-term item; this section
itemizes that.

| Feature | Spec ver | Generator | Notes |
|---------|:--------:|:---------:|-------|
| Parameter `style` / `explode` serialization | 3.0 | 🟡 | Form-`explode` arrays on query params serialize as repeated keys (`?tags=a&tags=b`) — typed C# client (foreach), per-op client (`WithQueryParameter(IEnumerable)`), and TS clients (Fetch `append` / Axios `indexes: null`); the server binds them via `ParsableList<T>` and the client↔server round-trip is verified against a real ASP.NET Core .NET 10 host. `$ref`-to-array query params resolve to `List<T>`/`ParsableList<T>` and get the same treatment. Exotic styles (`spaceDelimited`, `pipeDelimited`, `deepObject`, form `explode:false`, `matrix`, `label`) and object query params are **deferred** behind warning `ATC_API_OPR026` and fall back to default form/collection-`.ToString()` serialization. Caveat (pre-existing): array query values are form-explode repeated keys end-to-end, but the server's `ParsableList<T>` relies on ASP.NET joining repeated keys with commas then comma-**splitting**, so an array element value that contains a literal comma round-trips lossily (over-split). Reference scenario: `test/Scenarios/ParameterSerialization` |
| `allowReserved` on query parameters | 3.0 | 🟡 | The **typed C# client** emits the value un-encoded (`Uri.EscapeDataString` skipped) for `allowReserved` primitive query params. Documented limitations: the per-operation client (external `WithQueryParameter` builder) does not honor `allowReserved`, and the **TS clients do not honor `allowReserved` at all** (values are encoded normally by fetch/axios). 3.2 broadens `allowReserved` to headers and any `in`, which remains unsupported |
| Links Object | 3.0 | ❌ | Not generated at all (no `OpenApiLink` handling) |
| Callbacks code generation | 3.0 | 🟡 | A `Scenarios/Callbacks30` spec exists and parses, but no extractor emits callback code |
| Path Item `$ref` / `components.pathItems` | 3.1 | ✅ | **Phase 4-1 done.** All ~45 `is OpenApiPathItem` pattern casts relaxed to `is IOpenApiPathItem` across ~28 files. `OpenApiPathItemReference` (Microsoft.OpenApi's transparent proxy) now flows through every extractor, validator, policy extractor, TS client, and CLI. A path-item `$ref` generates identical output to an inline path item. Reference scenario: `test/Scenarios/ComponentsReuse/` |
| `mutualTLS` security scheme | 3.1 | ❌ | Not present in `SecuritySchemeType` (only `http`, `apiKey`, `oauth2`, `openIdConnect`) |
| Response headers extraction | 3.0 | 🟡 | Only the `Location` header is handled (redirects); general response-header objects are not surfaced |
| Examples (`example` / `examples` objects) | 3.0 | 🟡 | Example handling is limited (mostly used to read x-* config); not systematically extracted into docs/tests/mocks |
| Full JSON Schema 2020-12 coverage | 3.1 | 🟡 | ~80% per the engineering roadmap; strong on type arrays, `$ref` siblings, `const`, `prefixItems`, `contentEncoding`, but not complete |

These should feed Phase 0/4 (or a dedicated "3.1 completion" track) since they share
extractor surface with the 3.2 items.

## Phased implementation plan

The phases are ordered by value-to-effort and structural dependency. Each phase
should ship with new `.yaml` scenarios under `test/Scenarios/` plus `.verified`
snapshots for server, C# client, and TypeScript client output.

### Phase 0 — Recognize 3.2 (foundation) ✅

- ✅ The parsed OpenAPI spec version (3.0/3.1/3.2) is now captured at parse time
  (`OpenApiDocumentHelper`) into the document's non-serialized `Metadata` bag and
  exposed via `OpenApiDocument.GetOpenApiSpecVersion()` — making it reachable from
  code that only holds the document (validators, extractors), so later phases can
  gate behavior on `OpenApi3_2`. (The version itself only lives on the parser
  diagnostic, not the document.)
- ✅ `OpenApiDocumentValidator` recognizes 3.0/3.1/3.2 and rejects only the real
  OpenAPI 2.0 spec version. (Previously it keyed the 2.0 check off `info.version`,
  which is the API's own semantic version — so `info.version: 2.0.0` on a 3.x spec
  was falsely rejected; now fixed.)
- ✅ `Scenarios/OpenApi32Features/` parses and generates cleanly end-to-end for
  server, C# client, and TypeScript (Axios + Fetch) clients. It uses only
  currently-supported 3.0/3.1 features under `openapi: 3.2.0`, proving the pipeline
  accepts 3.2 documents with no generated-code change.
- Tests: `OpenApiDocumentSpecVersionTests` (extension), `OpenApiVersionValidationTests`
  (validator), plus the auto-discovered `OpenApi32Features` integration scenario.
- Not done (intentional, deferred to later phases): the CLI
  `SpecificationValidator.ExtractOpenApiSpecVersion` still reads the raw YAML string
  for display, and `StatisticsCollector` still reports `info.version` as the
  "specification version"; neither blocks recognition.

### Phase 1 — HTTP methods (highest structural impact) ✅

- ✅ **Key finding:** the Microsoft.OpenApi 3.6.0 reader flattens both the `query`
  method and `additionalOperations` (custom verbs like `LINK`) into
  `pathItem.Operations`, keyed by `System.Net.Http.HttpMethod` (`"QUERY"`, `"LINK"`).
  So enumeration already yields them — no operation-enumeration change was needed.
  Handlers, results, parameters all generate already; only route registration and
  client HTTP dispatch needed fixing.
- ✅ Server: `EndpointMapHelper` routes standard verbs via `Map{Verb}` and
  non-standard verbs via `MapMethods(route, new[] { "VERB" }, handler)`. Wired into
  `EndpointDefinitionExtractor` (the active server path).
- ✅ C# typed client (`HttpClientExtractor`): generic default arm builds
  `new HttpRequestMessage(new HttpMethod("VERB"), url)` + `SendAsync`, dispatching by
  request-body / return-type presence (also fixes the pre-existing missing-PATCH gap).
- ✅ C# per-operation client (`EndpointPerOperationExtractor`):
  `EndpointMapHelper.BuildHttpMethodExpression` — RFC-standard verbs keep
  `HttpMethod.{Pascal}`, custom verbs use `new HttpMethod("VERB")`.
- ✅ TypeScript client: already method-string-driven (`request('QUERY', …)`), no
  code change needed; covered by scenario snapshots.
- ✅ Scenario: `Scenarios/HttpMethods/` (Server, Client-Typed, Client-Operation,
  TS-Client-Axios, TS-Client-Fetch) exercises `query` + a custom `LINK` operation
  end-to-end.
- Known follow-up (not blocking): snapshot tests assert generated *text*, not that
  it compiles/runs. A sample project or compile-check exercising the 3.2 `query`
  and custom-verb output would close that gap (no existing scenario compile-checks
  generated code, so this matches the current bar).

### Phase 2 — Streaming / sequential media types ✅

- ✅ **Foundation laid:** `OpenApiMediaType.ItemSchema` (3.2) is exposed by the
  parser. Added `OpenApiOperation.GetStreamingItemSchema()` (returns the per-element
  `itemSchema` from a 2xx response media type) and `IsStreamingResponse()`
  (`x-return-async-enumerable` OR an `itemSchema` is present). These are the shared
  signals all layers will consume. Zero churn (no existing scenario uses `itemSchema`).
- ✅ **C# server + typed client wired:** `ResultClassExtractor` (server
  `Ok(IAsyncEnumerable<TItem>)`) and `HttpClientExtractor` (typed client
  `IAsyncEnumerable<TItem>` + `DeserializeAsyncEnumerable<TItem>`) now use
  `IsStreamingResponse()` and derive the element type directly from `itemSchema`.
  `Scenarios/StreamingItemSchema/` (Server + Client-Typed) drives streaming purely off
  `itemSchema` (no x-* annotation). Zero churn on existing scenarios.
- ✅ **TS client wired:** `TypeScriptClientExtractor` (all 4 streaming checks) +
  `TypeScriptOperationHelper.GetReturnType` now use `IsStreamingResponse()` and map
  `itemSchema` directly to the TS item type (not via `GetStreamingItemType`). The
  `StreamingItemSchema` scenario's `TS-Client-Axios` folder emits
  `async *streamEvents(): AsyncGenerator<Event>` / `requestStream<Event>` from
  `itemSchema`. Zero churn.
- ✅ **All remaining layers wired:** the per-operation C# client
  (`EndpointPerOperationExtractor` → `Task<StreamingEndpointResponse<TItem>>`) and the
  TS hooks (`TypeScriptReactQueryHookExtractor` → `useState<readonly TItem[]>` + a
  `for await` stream loop; `TypeScriptSwrHookExtractor`) now use `IsStreamingResponse()`
  with `itemSchema`-direct typing. Every "should this stream" call site across the
  codebase is now driven by `IsStreamingResponse()`; only `IsPaginatedStreamingOperation`'s
  internal gate remains on the x-* signal (by design). The `StreamingItemSchema` scenario
  covers Server + Client-Typed + Client-Operation + TS-Client-Axios + TS-Hooks-ReactQuery.
  **Detection/typing is complete; zero churn throughout.**
- ✅ **Substantive piece DONE — true per-media-type wire framing.** Every sequential
  media type now emits/reads its real wire format, keyed strictly on the declared
  response media type via `OpenApiOperation.GetStreamingFraming()` →
  `StreamingFraming { JsonArray, ServerSentEvents, JsonLines, JsonSequence, MultipartMixed }`
  (`Atc.OpenApi`). The legacy `application/json` + `x-return-async-enumerable` path stays
  JSON-array (zero churn). Framing matrix shipped:

  | Declared media type | Framing | Server emit | C# client read |
  |---|---|---|---|
  | `text/event-stream` | Server-Sent Events | first-party `TypedResults.ServerSentEvents` | first-party `SseParser` |
  | `application/jsonl` (+ `x-ndjson`, `x-jsonlines`) | JSON Lines | emitted `JsonLinesResult<T>` | `DeserializeAsyncEnumerable(topLevelValues:true)` (typed) / line-read via `IContractSerializer` (per-op) |
  | `application/json-seq` | JSON Text Sequence (RFC 7464) | emitted `JsonSequenceResult<T>` (`RS json LF`) | async **RS-delimited** byte reader (handles embedded LFs / pretty JSON) |
  | `multipart/mixed` | Multipart | emitted `MultipartMixedResult<T>` (boundary parts) | boundary-delimited reader (handles multi-line bodies) |
  | `application/json` + `x-return-async-enumerable` | JSON array (legacy) | `TypedResults.Ok` | `DeserializeAsyncEnumerable` |

  Emitted helpers: server `Streaming/SequentialResults.cs` (`SequentialStreamWriter` +
  per-framing `IResult`s, async writes, serializer via
  `GetRequiredService<IOptions<JsonOptions>>`); client `Streaming/StreamReaders.cs`
  (mode-aware — typed uses `JsonSerializerOptions`, per-op uses `IContractSerializer`).
  The per-op C# client builds `StreamingEndpointResponse<T>` inline (its ctor is public),
  so **no `Atc.Rest.Client` change was required.** TypeScript clients gained a
  framing-aware `requestStream` (`'sse'` / `'multipart'` branches; jsonl/json-seq reuse
  the brace-scan). All five framings exercised end-to-end by `Scenarios/StreamingItemSchema`
  (`/events` jsonl, `/events-sse`, `/events-seq`, `/events-multipart`) across Server +
  Client-Typed + Client-Operation + TS-Client-Axios/Fetch + TS-Hooks-ReactQuery.
- ✅ **Bug fixed (itemSchema-only model not generated):** `PathSegmentHelper.GetSchemasUsedBySegment`
  now also collects `content.ItemSchema` (request body + responses), so a type referenced
  only via `itemSchema` (e.g. a streamed `Event`) is attributed to its segment and emitted.
  **Compile-validated**: `StreamingItemSchema` is now in the client + server compile-verification
  theories and passes. (The earlier "naive fix breaks namespacing" observation was an artifact
  of the integration test *helper* using a flat model namespace, which diverges from the real
  `ApiServerGenerator`'s segment-partitioned placement; the real generator — what the compile
  harness validates — is consistent and green.)
- ✅ **Prerequisite infra — BUILT:** a real compile-verification harness.
  `CompilationVerificationTests` now compiles generated **client and server** output for
  PetStoreSimple / Demo / HttpMethods (parse trees + synthetic ImplicitUsings + full TPA
  references including ASP.NET Core + `Atc.Rest.Client`, then `GetDiagnostics()`). The test
  project gained `FrameworkReference Microsoft.AspNetCore.App` + `Atc.Rest.Client`; the
  server generator needs ASP.NET refs in its input compilation to emit, so `RunGenerator`
  has a `useFullReferences` flag. This unblocks the itemSchema-model fix and the
  wire-framing work — both can now be validated by real compilation, not just snapshots.
- ✅ **Proven by wire-byte + round-trip tests (not snapshot-green alone).**
  `StreamingWireFramingTests` compiles the emitted helpers, loads them, and exercises
  **write→read over a `MemoryStream`** per framing: SSE parses real `data: …\n\n`; jsonl
  is newline-delimited (asserts not a JSON array); json-seq asserts the `RS` (0x1E) byte
  and reads back through the RS-delimited reader (incl. an **embedded-newline / pretty-JSON**
  discriminator test); multipart asserts the boundary delimiters and a **multi-line-body**
  test. Plus `*Result_ExecuteAsync_*` tests drive each server `IResult` against a
  sync-IO-disallowed response body (proving async writes — no Kestrel
  `AllowSynchronousIO` throw) and a `ConfigureHttpJsonOptions`-camelCase context (proving
  the configured serializer is honored, not bare defaults). The earlier
  "must not be marked ✅ off snapshot-green alone" caveat is satisfied.
- ⏭ **Tracked follow-ups (non-blocking, do not gate Phase 2):**
  TS `multipart` client buffers the full response before splitting (the C# reader is
  incremental); per-op C# readers are compile-verified but not directly runtime-exercised
  (their parse loops are byte-identical to the runtime-proven typed readers); repo-wide
  `.verified.ts` BOM normalization + dropping the BOM step from the TS-snapshot-bootstrap
  recipe (a few `ApiClient.verified.ts` oscillate BOM because the bootstrap re-encode adds
  one while Verify is BOM-blind — functionally invisible, diff noise only). Pre-existing,
  unrelated to framing: TS model named `Event` collides with the DOM `Event` (no import
  emitted); the hand-rolled TS SSE parser is LF-only and duplicated across Fetch/Axios.

### Phase 3 — Security & polymorphism enhancements ❌

- OAuth2 device flow, `oauth2MetadataUrl`, scheme `deprecated`, scheme `$ref`-by-URI.
- Optional `discriminator.propertyName` and `defaultMapping` in
  `PolymorphicTypeExtractor`.

### Phase 4 — Parameters, components reuse, metadata 🟡

**Phase 4-1 done:** `components.pathItems` $ref + `components.mediaTypes` reuse — see above.

Remaining:
- `in: querystring`, `allowReserved`, `style: cookie`.
- `Tag.summary`/`parent`/`kind`, `Server.name`, `Response.summary`, optional
  `Response.description`, `$self`, `Example.dataValue`/`serializedValue`.
- Support for `summary` across all supported objects (Schema, Parameter, Header, Security Scheme).
- Support for Annotated Enumerations and Generic Data Structures.

### Phase 5 — XML & documentation-only ⬜

- `xml.nodeType` and related XML modeling — only if XML content support is pursued.
- ABNF/Link clarifications are documentation-level and need no generator change.

## Summary

| Area | Status | Phase |
|------|:------:|:-----:|
| Parser / object model (`Microsoft.OpenApi` 3.6.0) | ✅ | — |
| Recognize `3.2.0` version | ✅ | 0 |
| `query` method + `additionalOperations` | ✅ | 1 |
| Streaming / sequential media types (`itemSchema`, SSE/jsonl/json-seq/multipart framing) | ✅ | 2 |
| OAuth2 device flow + scheme metadata | ❌ | 3 |
| Discriminator `defaultMapping` + optional `propertyName` | 🟡 | 3 |
| `querystring` / `allowReserved` / cookie `style` | ❌ | 4 |
| `components.mediaTypes` reuse + `components.pathItems` $ref | ✅ | 4 |
| Tag nesting, `Server.name`, `Response.summary`, `$self`, examples | ❌ | 4 |
| Annotated Enumerations & Generic Data Structures | ❌ | 4 |
| `summary` on Schema, Parameter, Header, etc. | ❌ | 4 |
| XML `nodeType`, ABNF, Links | ⬜ | 5 |
| [Pre-3.2 gaps](#missing-pre-32-features-30--31-gaps) (style/explode, Links, callbacks, mutualTLS, …) | 🟡 | 0/4 |

**Bottom line:** OpenAPI 3.2 support is entirely a downstream effort — the parser
(`Microsoft.OpenApi` 3.6.0) already understands 3.2. The work is recognizing the
version, then teaching the validators, extractors, and generators to consume the new
fields, with HTTP methods and streaming being the most structurally significant
phases. Some 3.2 features (parameter serialization, components reuse) sit on top of
**pre-existing 3.0/3.1 gaps**, so closing those first removes rework.
