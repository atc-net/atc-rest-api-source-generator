# OpenAPI 3.2 — Streaming Wire-Framing (Phase 2 substantive work)

**Date:** 2026-06-08
**Branch:** `feature/openapi32`
**Roadmap:** [`docs/roadmap-openapi32-support.md`](../../roadmap-openapi32-support.md) — Phase 2, the remaining "substantive piece"

## Problem

Phase 2 wired **streaming detection and typing** across every layer (server, C# typed
client, C# per-operation client, TypeScript client, TS hooks): an OpenAPI 3.2
`itemSchema` on a 2xx response media type now produces `IAsyncEnumerable<T>` (server /
C# client) and an async iterator (TS). But the **wire format is wrong**. Today the
system is *internally consistent but format-incorrect*:

- Server emits a JSON **array** (`TypedResults.Ok(IAsyncEnumerable<T>)` → `[{…},{…}]`).
- Every C# client reads a JSON array (`JsonSerializer.DeserializeAsyncEnumerable<T>`
  with default options).
- The TypeScript client uses a brace-matched object scan that happens to tolerate both
  JSON-array and NDJSON.
- The **declared media type is ignored**. A spec saying `application/jsonl` or
  `text/event-stream` still gets a JSON array on the wire.

The value of this phase is **interop with non-generated peers** — a browser
`EventSource`, a `curl | jq` reading JSON Lines, any consumer that honors the declared
`Content-Type`. That only works if the bytes match the declared media type.

### This is a correctness gate, not a gap-fill

Framing is fixed by the declared media type — there is **no content negotiation**. The
moment the server emits real `application/jsonl` (`{}\n{}\n`) while a client still
expects `[{},{}]`, **that generated server↔client pair stops interoperating**. So the
work is not "add framing where convenient"; it is "move every layer to the same
media-type-keyed framing together, or knowingly ship a broken pair." We choose to move
every C# layer together (see [Per-operation client](#per-operation-c-client)).

## Verified facts (these shaped the design)

1. **`Microsoft.OpenApi` 3.6.0** already exposes `OpenApiMediaType.ItemSchema`. No parser
   change needed.
2. **.NET 10 ships first-party SSE both directions:**
   - Write: `TypedResults.ServerSentEvents<T>(IAsyncEnumerable<T>, string? eventType)` and
     `…(IAsyncEnumerable<SseItem<T>>)` (`Microsoft.AspNetCore.Http.Results`). Strings are
     written raw; other `T` via the configured JSON options.
   - Read: `System.Net.ServerSentEvents.SseParser` (`SseParser.Create<T>(stream, parser)
     .EnumerateAsync()`).
3. **`System.Text.Json` reads JSON Lines** via
   `JsonSerializer.DeserializeAsyncEnumerable<T>(stream, topLevelValues: true, options, ct)`
   (whitespace-separated top-level values; `\n` is whitespace).
4. **The external `Atc.Rest.Client` 2.0.36 is hard-locked to JSON-array framing.** Its
   `MessageResponseBuilder.BuildStreamingEndpointResponseAsync<T>` calls
   `serializer.DeserializeAsyncEnumerable<T>(stream)`, which is
   `JsonSerializer.DeserializeAsyncEnumerable<T>(stream, options)` with default options
   (JSON array). It cannot be changed from this repository.
5. **No first-party client-side multipart *response* reader** exists in modern .NET
   without pulling in `Microsoft.AspNetCore.WebUtilities` (an ASP.NET dependency we will
   not add to a client library). The multipart/mixed reader is hand-rolled into emitted
   helper code.

## Scope

In scope — **all four** sequential framings, keyed on the declared response media type:

| Declared media type | Framing | Wire shape (per item) |
|---|---|---|
| `text/event-stream` | Server-Sent Events | `data: <json>\n\n` (event/id honored where present) |
| `application/jsonl` (also `application/x-ndjson`, `application/x-jsonlines`) | JSON Lines | `<json>\n` |
| `application/json-seq` | JSON Text Sequence (RFC 7464) | `\x1e<json>\n` |
| `multipart/mixed` | Multipart | `--<boundary>\r\nContent-Type: application/json\r\n\r\n<json>\r\n` … `--<boundary>--\r\n` |
| `application/json` + `x-return-async-enumerable` | **JSON array (unchanged)** | `[{…},{…}]` |

Out of scope:

- Changing the existing `application/json` + `x-return-async-enumerable` path. It stays
  JSON-array so the existing `Demo` async-enumerable scenario and the external
  `Atc.Rest.Client` per-op path keep working with **zero churn**.
- Request-body streaming (`itemSchema` on a request media type). Responses only.
- Bidirectional / duplex streaming.

## The core seam

`OpenApiOperation.GetStreamingItemSchema()` (`OpenApiOperationExtensions.cs:254`) returns
the item schema but **discards the media-type key** that determines framing. Foundation
change, in `src/Atc.OpenApi/Extensions/OpenApiOperationExtensions.cs`:

```csharp
// Returns the 2xx streaming media type + its per-element itemSchema, or null.
public (string MediaType, IOpenApiSchema ItemSchema)? GetStreamingResponse();

public enum StreamingFraming
{
    JsonArray,          // application/json + x-return-async-enumerable (legacy, unchanged)
    ServerSentEvents,   // text/event-stream
    JsonLines,          // application/jsonl, application/x-ndjson, application/x-jsonlines
    JsonSequence,       // application/json-seq (RFC 7464)
    MultipartMixed,     // multipart/mixed
}

// Classifies the operation's streaming response. Returns JsonArray when the only
// streaming signal is the x-return-async-enumerable annotation on application/json.
public StreamingFraming GetStreamingFraming();
```

`IsStreamingResponse()` keeps its current contract (true for any framing incl. the
legacy annotation). `GetStreamingItemSchema()` is retained (now a thin wrapper over
`GetStreamingResponse()`) so existing callers are undisturbed; new emit code branches on
`GetStreamingFraming()`.

## Per-layer emit strategy

Every emitter switches on `GetStreamingFraming()`. `JsonArray` always means "exactly
what we emit today."

| Layer | SSE | jsonl / json-seq | multipart/mixed |
|---|---|---|---|
| **Server** (`ResultClassExtractor`) | `TypedResults.ServerSentEvents(IAsyncEnumerable<T>)` | emitted `IResult` writer (helper) | emitted `IResult` writer (helper) |
| **C# typed client** (`HttpClientExtractor`) | `SseParser` | `DeserializeAsyncEnumerable(topLevelValues:true)` / RS-split helper | emitted boundary reader (helper) |
| **C# per-op client** (`EndpointPerOperationExtractor`) | inline (bypasses external helper) | inline | inline |
| **TS client** (`TypeScriptFetch/AxiosApiClientExtractor`, `TypeScriptClientExtractor`) | `data:`-aware parse | line / RS parse | boundary parse |
| **TS hooks** (`TypeScriptReactQueryHookExtractor`, `TypeScriptSwrHookExtractor`) | unchanged — consume the async iterator | unchanged | unchanged |

### Per-operation C# client

The per-op client currently routes streaming through the **external, JSON-array-locked**
`Atc.Rest.Client.BuildStreamingEndpointResponseAsync<T>`. For the new framings we
**bypass the external helper** and instead call the **emitted in-repo `StreamReaders.cs`
helper** (shared with the typed client — see the chosen approach below) from the
generated per-op endpoint code, so that server, typed client, and per-op client all
round-trip the same wire bytes. "Inline" here means "in-repo emitted code rather than the
external NuGet helper," not "duplicated per method." The legacy `JsonArray` framing is
unchanged — it keeps using the external `BuildStreamingEndpointResponseAsync<T>`.

## Approach for shared framing code — "Emitted shared helper file"

Chosen over (A) fully-inline-per-method and (C) push-into-external-runtime.

- **Server:** emit one helper file, e.g. `Streaming/SequentialResults.cs`, containing the
  `IResult` writers needed by the spec (`JsonLinesResult<T>`, `JsonSequenceResult<T>`,
  `MultipartMixedResult<T>`). SSE uses first-party `TypedResults.ServerSentEvents`
  directly (no helper). Emitted only when at least one operation needs that framing.
- **C# clients (typed + per-op):** emit one helper file, e.g. `Streaming/StreamReaders.cs`,
  with the readers that are not one-liners (`ReadJsonSequenceAsync<T>`,
  `ReadMultipartMixedAsync<T>`; SSE via `SseParser`, jsonl via the
  `topLevelValues:true` overload are emitted inline as they are one-liners).
- **TS clients:** extend the generated `ApiClient` base with framing-aware stream parsing.
  Replace the single brace-scan `requestStream<T>` with a media-type-aware
  `requestStream<T>(method, path, options, framing)` (or a small set of
  `streamSse`/`streamJsonLines`/`streamJsonSeq`/`streamMultipart` private methods), and
  have `TypeScriptClientExtractor` pass the operation's framing.

No new NuGet/runtime dependency on either side; all framing code is emitted in-repo.

### `multipart/mixed` details

- **Boundary:** generated server picks a stable boundary constant per response writer
  (e.g. a fixed GUID-free token); `Content-Type` becomes
  `multipart/mixed; boundary=<token>`.
- **Each part:** `Content-Type: application/json` header, blank line, the item's JSON,
  CRLF. Terminates with the closing `--<token>--` delimiter.
- **Client read:** hand-rolled boundary scanner over the response stream that yields each
  part body and deserializes it as `T`. Lives in the emitted `StreamReaders.cs` helper so
  the ~40-line parser is written once.

## Testing — wire bytes, not just text

Snapshot tests assert generated **text**; the compile harness asserts it **compiles**.
Neither proves the **bytes on the wire**. Success bar for this phase:

1. **Snapshot coverage** — `.verified.cs` / `.verified.ts` for server, C# typed client,
   C# per-op client, TS client (Axios + Fetch), TS hooks across the framings.
2. **Round-trip wire test** — extend `CompilationVerificationTests` (which already
   compiles generated server + client): host the generated server with
   `Microsoft.AspNetCore.TestHost`, then for each framing assert
   - (a) the raw response body **bytes** match the expected framing
     (`data: …\n\n`, `{}\n{}\n`, `\x1e{}\n`, multipart boundaries), and
   - (b) the generated C# client reads them back into the **same item sequence**.

   This is the only test that actually proves the phase; the roadmap explicitly warns it
   "must not be marked ✅ off snapshot-green alone."
3. **Extension unit tests** — `OpenApiOperationExtensionsTests` for `GetStreamingResponse`
   and `GetStreamingFraming` across each media type (incl. the legacy annotation →
   `JsonArray`, and the alias media types).

## Scenarios

The existing `test/Scenarios/StreamingItemSchema/` uses `application/jsonl`. Extend
scenario coverage so each framing is exercised end-to-end (server + Client-Typed +
Client-Operation + TS-Client-Axios + TS-Client-Fetch + TS-Hooks-ReactQuery):

- Reuse / keep `StreamingItemSchema` (jsonl).
- Add operations / a scenario covering `text/event-stream`, `application/json-seq`, and
  `multipart/mixed`.

Exact scenario layout (one multi-op scenario vs. several) is an implementation-plan
decision.

## Implementation sequencing

All four framings are in scope. Order, each shipping its scenario coverage + round-trip
test before the next:

1. **Core seam** — `GetStreamingResponse`, `StreamingFraming`, `GetStreamingFraming` +
   extension unit tests. No emit change yet (legacy path still `JsonArray`).
2. **SSE** — server `TypedResults.ServerSentEvents`; C# clients via `SseParser`; TS
   `data:`-aware parse. Highest value, mostly first-party.
3. **jsonl + json-seq** — shared line/RS logic; server writers; client readers; TS parse.
4. **multipart/mixed** — server writer + hand-rolled client boundary reader + TS boundary
   parse. The heavy one.

## Risks / open questions

- **`SseParser` item parser signature** — confirm the exact delegate shape
  (`(string eventType, ReadOnlySpan<byte> data) => T`) against the .NET 10 SDK when
  implementing step 2; the JSON deserialize inside it must use the client's configured
  options.
- **Round-trip test host** — `Microsoft.AspNetCore.TestHost` must be added to the
  `Atc.Rest.Api.SourceGenerator.Tests` project (it already has the ASP.NET framework
  reference for compilation).
- **TS `requestStream` signature change** — replacing the single brace-scan method is a
  snapshot-churning change to both `TypeScriptFetchApiClientExtractor` and
  `TypeScriptAxiosApiClientExtractor`; all streaming TS snapshots regenerate.
- **Per-op inline emit size** — the per-op endpoint files grow for streaming ops; keep
  the actual parse in the emitted client helper and call it from the endpoint to bound
  duplication.
