# OpenAPI 3.2 Streaming Wire-Framing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make generated server/client streaming emit the real wire format declared by the response media type (`text/event-stream`, `application/jsonl`, `application/json-seq`, `multipart/mixed`) instead of always a JSON array, keyed on a single `GetStreamingFraming()` seam.

**Architecture:** A new media-type classifier (`StreamingFraming`) drives every emitter. Server uses first-party `TypedResults.ServerSentEvents` for SSE and emitted `IResult` writers for jsonl/json-seq/multipart. C# clients use first-party `SseParser` (SSE) + `DeserializeAsyncEnumerable(topLevelValues:true)` (jsonl) and emitted readers (json-seq/multipart), all collected into one emitted helper file per generated client so the per-op client (whose `StreamingEndpointResponse<T>` ctor is public) can build its async enumerable inline without touching the external `Atc.Rest.Client`. The legacy `application/json` + `x-return-async-enumerable` path stays JSON-array (zero churn). Correctness is proven by a wire-byte + round-trip test that compiles the generated helpers, loads them, and exercises write→read over a `MemoryStream`.

**Tech Stack:** .NET 10 / C# 14, Roslyn source generators (netstandard2.0 libs), `Microsoft.OpenApi` 3.6.0, `System.Net.ServerSentEvents`, `System.Text.Json`, xUnit (Microsoft Testing Platform), TypeScript emitters.

**Spec:** [`docs/superpowers/specs/2026-06-08-openapi32-streaming-wire-framing-design.md`](../specs/2026-06-08-openapi32-streaming-wire-framing-design.md)

---

## Conventions used by this plan

- **Build:** `dotnet build Atc.Rest.Api.SourceGenerator.slnx`
- **Unit tests (Microsoft Testing Platform):** filter by method with `--filter-method "*MethodName"`, e.g.
  `dotnet run --project test/Atc.Rest.Api.Generator.Tests/Atc.Rest.Api.Generator.Tests.csproj -- --filter-method "*GetStreamingFraming*"`
  (If the repo's existing scripts use a different runner, mirror whatever the green tests already use — do not invent a new runner.)
- **Integration snapshots:** generated output is compared to `.verified.cs` / `.verified.ts`; a `.received.*` file is written on mismatch. Accept by copying `.received.*` over `.verified.*`. The framework reports **one mismatch per scenario per run**, so expect iterative accept-and-rerun cycles.
- **PowerShell note:** this machine lacks `Get-Content -Raw`; use `[System.IO.File]::ReadAllText()`. Avoid piping PowerShell through bash (special chars mangle) — write a `.ps1` and run `powershell -ExecutionPolicy Bypass -File`.
- **"Target generated output"** blocks are the contract for an emit change: write extractor code until the integration snapshot equals that text. The exact `StringBuilder`/`MethodParameters` calls are discovered against the snapshot — the generated text is the spec.

---

## File structure

**Create:**
- `src/Atc.OpenApi/StreamingFraming.cs` — the `StreamingFraming` enum + `StreamingMediaType` classifier helper.
- `test/Atc.Rest.Api.SourceGenerator.Tests/Generators/StreamingWireFramingTests.cs` — wire-byte + round-trip harness (compiles a scenario, loads the emitted helpers, exercises write→read).
- Emitted (by the generator, captured as snapshots): `Streaming/StreamReaders.cs` (each generated C# client) and `Streaming/SequentialResults.cs` (generated server). Not source files in the repo — they appear as `.verified.cs` snapshots under the scenario.

**Modify:**
- `src/Atc.OpenApi/Extensions/OpenApiOperationExtensions.cs` — add `GetStreamingResponse()`, `GetStreamingFraming()`; keep `GetStreamingItemSchema()`/`IsStreamingResponse()`.
- `src/Atc.Rest.Api.Generator/Extractors/ResultClassExtractor.cs` — server: branch the `Ok(IAsyncEnumerable<T>)` factory by framing.
- `src/Atc.Rest.Api.Generator/Extractors/EndpointDefinitionExtractor.cs` — server: emit the `SequentialResults.cs` helper + `Produces` content-type metadata when a non-array framing is used. (Confirm which extractor owns the per-segment file set during Task 3.)
- `src/Atc.Rest.Api.Generator/Extractors/HttpClientExtractor.cs` — typed client: branch the streaming read by framing; emit `StreamReaders.cs`.
- `src/Atc.Rest.Api.Generator/Extractors/EndpointPerOperationExtractor.cs` — per-op client: build `StreamingEndpointResponse<T>` inline via `StreamReaders` for non-array framings.
- `src/Atc.Rest.Api.Generator.Cli/Extractors/TypeScript/TypeScriptFetchApiClientExtractor.cs` and `TypeScriptAxiosApiClientExtractor.cs` — framing-aware `requestStream`.
- `src/Atc.Rest.Api.Generator.Cli/Extractors/TypeScript/TypeScriptClientExtractor.cs` — pass the operation's framing to `requestStream`.
- `test/Scenarios/StreamingItemSchema/StreamingItemSchema.yaml` — add SSE / json-seq / multipart operations.
- `test/Atc.Rest.Api.SourceGenerator.Tests/Generators/CompilationVerificationTests.cs` — (already includes `StreamingItemSchema`) ensure new ops compile.
- `docs/roadmap-openapi32-support.md` — flip Phase 2 to ✅ once the round-trip bar is met.

---

## Task 1: Core seam — `StreamingFraming` classifier

**Files:**
- Create: `src/Atc.OpenApi/StreamingFraming.cs`
- Modify: `src/Atc.OpenApi/Extensions/OpenApiOperationExtensions.cs`
- Test: `test/Atc.Rest.Api.Generator.Tests/Extensions/OpenApiOperationExtensionsTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `OpenApiOperationExtensionsTests.cs` (mirror the existing test style/usings in that file; build the `OpenApiOperation` the same way the existing `GetStreamingItemSchema` tests do):

```csharp
[Theory]
[InlineData("text/event-stream", StreamingFraming.ServerSentEvents)]
[InlineData("application/jsonl", StreamingFraming.JsonLines)]
[InlineData("application/x-ndjson", StreamingFraming.JsonLines)]
[InlineData("application/x-jsonlines", StreamingFraming.JsonLines)]
[InlineData("application/json-seq", StreamingFraming.JsonSequence)]
[InlineData("multipart/mixed", StreamingFraming.MultipartMixed)]
[InlineData("application/json", StreamingFraming.JsonArray)]
[InlineData("text/event-stream; charset=utf-8", StreamingFraming.ServerSentEvents)]
public void GetStreamingFraming_ItemSchemaMediaType_ReturnsExpectedFraming(
    string mediaType,
    StreamingFraming expected)
{
    var operation = BuildStreamingOperation(mediaType); // 200 response, given media type, with itemSchema

    Assert.Equal(expected, operation.GetStreamingFraming());
}

[Fact]
public void GetStreamingFraming_LegacyAnnotationOnly_ReturnsJsonArray()
{
    var operation = BuildAsyncEnumerableAnnotatedOperation(); // x-return-async-enumerable on application/json, no itemSchema

    Assert.Equal(StreamingFraming.JsonArray, operation.GetStreamingFraming());
}

[Fact]
public void GetStreamingResponse_ItemSchemaPresent_ReturnsMediaTypeAndSchema()
{
    var operation = BuildStreamingOperation("application/jsonl");

    var result = operation.GetStreamingResponse();

    Assert.NotNull(result);
    Assert.Equal("application/jsonl", result.Value.MediaType);
    Assert.NotNull(result.Value.ItemSchema);
}
```

Add small builders next to the existing helpers in this test file (reuse the pattern the file already uses to construct `OpenApiOperation` with a `200` response media type carrying `ItemSchema` for the streaming case, and `Extensions["x-return-async-enumerable"]` for the annotation case).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project test/Atc.Rest.Api.Generator.Tests/Atc.Rest.Api.Generator.Tests.csproj -- --filter-method "*GetStreamingFraming*"`
Expected: FAIL — `StreamingFraming` / `GetStreamingFraming` do not exist (compile error or test failure).

- [ ] **Step 3: Create the enum + classifier**

Create `src/Atc.OpenApi/StreamingFraming.cs`:

```csharp
namespace Atc.OpenApi;

/// <summary>
/// Wire framing for a streamed (sequential) response, derived from the declared
/// response media type. Drives how each generator layer reads/writes the stream.
/// </summary>
public enum StreamingFraming
{
    /// <summary>JSON array (<c>[{…},{…}]</c>). Legacy default; also the
    /// <c>application/json</c> + <c>x-return-async-enumerable</c> path.</summary>
    JsonArray,

    /// <summary>Server-Sent Events (<c>text/event-stream</c>): <c>data: &lt;json&gt;\n\n</c>.</summary>
    ServerSentEvents,

    /// <summary>JSON Lines / NDJSON (<c>application/jsonl</c>): <c>&lt;json&gt;\n</c>.</summary>
    JsonLines,

    /// <summary>JSON Text Sequence, RFC 7464 (<c>application/json-seq</c>): <c>\x1e&lt;json&gt;\n</c>.</summary>
    JsonSequence,

    /// <summary>Multipart mixed (<c>multipart/mixed</c>): boundary-delimited JSON parts.</summary>
    MultipartMixed,
}

/// <summary>Maps response media types to <see cref="StreamingFraming"/>.</summary>
public static class StreamingMediaType
{
    /// <summary>
    /// Classifies a declared response media type. Media-type parameters
    /// (e.g. <c>; charset=utf-8</c>) are ignored. Anything not recognized as a
    /// sequential framing maps to <see cref="StreamingFraming.JsonArray"/>.
    /// </summary>
    public static StreamingFraming Classify(string mediaType)
    {
        if (string.IsNullOrEmpty(mediaType))
        {
            return StreamingFraming.JsonArray;
        }

        var baseType = mediaType;
        var semicolon = baseType.IndexOf(';');
        if (semicolon >= 0)
        {
            baseType = baseType.Substring(0, semicolon);
        }

        baseType = baseType.Trim();

        if (baseType.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            return StreamingFraming.ServerSentEvents;
        }

        if (baseType.Equals("application/jsonl", StringComparison.OrdinalIgnoreCase) ||
            baseType.Equals("application/x-ndjson", StringComparison.OrdinalIgnoreCase) ||
            baseType.Equals("application/x-jsonlines", StringComparison.OrdinalIgnoreCase))
        {
            return StreamingFraming.JsonLines;
        }

        if (baseType.Equals("application/json-seq", StringComparison.OrdinalIgnoreCase))
        {
            return StreamingFraming.JsonSequence;
        }

        if (baseType.Equals("multipart/mixed", StringComparison.OrdinalIgnoreCase))
        {
            return StreamingFraming.MultipartMixed;
        }

        return StreamingFraming.JsonArray;
    }
}
```

- [ ] **Step 4: Add `GetStreamingResponse` + `GetStreamingFraming`**

In `OpenApiOperationExtensions.cs`, inside the `extension(OpenApiOperation operation)` block, add (place next to `GetStreamingItemSchema` ~line 254):

```csharp
/// <summary>
/// Gets the streaming 2xx response media type and its per-element <c>itemSchema</c>,
/// or <c>null</c> when no response media type declares one.
/// </summary>
public (string MediaType, IOpenApiSchema ItemSchema)? GetStreamingResponse()
{
    if (operation.Responses == null)
    {
        return null;
    }

    foreach (var statusCode in new[] { "200", "201" })
    {
        if (!operation.Responses.TryGetValue(statusCode, out var response) ||
            response.Content == null)
        {
            continue;
        }

        foreach (var kvp in response.Content)
        {
            if (kvp.Value.ItemSchema != null)
            {
                return (kvp.Key, kvp.Value.ItemSchema);
            }
        }
    }

    return null;
}

/// <summary>
/// Classifies the streaming response's wire framing from its declared media type.
/// Returns <see cref="StreamingFraming.JsonArray"/> when the only streaming signal is the
/// legacy <c>x-return-async-enumerable</c> annotation on <c>application/json</c>.
/// </summary>
public StreamingFraming GetStreamingFraming()
{
    var streaming = operation.GetStreamingResponse();
    return streaming is { } s
        ? StreamingMediaType.Classify(s.MediaType)
        : StreamingFraming.JsonArray;
}
```

Refactor `GetStreamingItemSchema()` to delegate (keeps existing callers working):

```csharp
public IOpenApiSchema? GetStreamingItemSchema()
    => operation.GetStreamingResponse()?.ItemSchema;
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet run --project test/Atc.Rest.Api.Generator.Tests/Atc.Rest.Api.Generator.Tests.csproj -- --filter-method "*GetStreamingFraming*"`
Then the existing streaming tests: `--filter-method "*GetStreamingItemSchema*"` and `--filter-method "*IsStreamingResponse*"`.
Expected: PASS (new + existing).

- [ ] **Step 6: Full build (no snapshot churn expected yet)**

Run: `dotnet build Atc.Rest.Api.SourceGenerator.slnx`
Expected: 0 errors. No emit changed, so integration snapshots are untouched.

- [ ] **Step 7: Commit**

```bash
git add src/Atc.OpenApi/StreamingFraming.cs src/Atc.OpenApi/Extensions/OpenApiOperationExtensions.cs test/Atc.Rest.Api.Generator.Tests/Extensions/OpenApiOperationExtensionsTests.cs
git commit -m "feat(openapi32): add StreamingFraming classifier seam (Phase 2 wire-framing)"
```

---

## Task 2: SSE end-to-end (server, both C# clients, TS) + wire-byte harness

**Files:**
- Modify: `ResultClassExtractor.cs`, `HttpClientExtractor.cs`, `EndpointPerOperationExtractor.cs`, `TypeScriptFetchApiClientExtractor.cs`, `TypeScriptAxiosApiClientExtractor.cs`, `TypeScriptClientExtractor.cs`
- Modify: `test/Scenarios/StreamingItemSchema/StreamingItemSchema.yaml`
- Create: `test/Atc.Rest.Api.SourceGenerator.Tests/Generators/StreamingWireFramingTests.cs`
- Snapshots: new/updated `.verified.cs` / `.verified.ts` under `test/Scenarios/StreamingItemSchema/`

### 2a — Scenario operation

- [ ] **Step 1: Add an SSE operation to the scenario spec**

In `StreamingItemSchema.yaml`, add a second path (keep the existing `/events` jsonl op untouched for now):

```yaml
  /events-sse:
    get:
      operationId: streamEventsSse
      summary: Stream events as server-sent events
      tags:
        - events
      responses:
        "200":
          description: A stream of events
          content:
            text/event-stream:
              itemSchema:
                $ref: "#/components/schemas/Event"
```

### 2b — Client read helper (`StreamReaders.cs`) — SSE reader

The C# clients gain one emitted helper file. Introduce it now with the SSE reader; later tasks add the other readers to the same file.

- [ ] **Step 2: Decide the emitted helper contract (target generated output)**

The generated client must emit `Streaming/StreamReaders.cs` with this content (namespace = the client's generated root + `.Streaming`). This is the contract the snapshot must match:

```csharp
// <auto-generated />
#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;

namespace <Root>.Streaming;

[GeneratedCode("Atc.Rest.Api.SourceGenerator", "1.0.0")]
internal static class StreamReaders
{
    public static async IAsyncEnumerable<T?> ReadServerSentEventsAsync<T>(
        Stream stream,
        JsonSerializerOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var parser = SseParser.Create(
            stream,
            (eventType, bytes) => JsonSerializer.Deserialize<T>(bytes, options));

        await foreach (var item in parser.EnumerateAsync(cancellationToken))
        {
            yield return item.Data;
        }
    }
}
```

> Confirm the `SseParser.Create<T>` item-parser delegate signature against the .NET 10 SDK during implementation (`(string eventType, ReadOnlySpan<byte> data) => T`). Adjust the lambda if the SDK differs; the round-trip test (Step 6) is the arbiter.

- [ ] **Step 3: Emit the helper from the client extractors**

In `HttpClientExtractor.cs` (typed client) emit `Streaming/StreamReaders.cs` once when the client has **any** operation with a non-`JsonArray` framing. Gate with:
`operations.Any(o => o.GetStreamingFraming() != StreamingFraming.JsonArray)`.
Add the same emission to the per-op client generator path. (Find the place each generator assembles its file set; follow how the existing common files — e.g. the typed client class, DI extensions — are added.)

- [ ] **Step 4: Branch the typed-client GET streaming body by framing**

In `HttpClientExtractor.GenerateGetMethodBody` (line ~688), replace the single JSON-array block with a framing switch. Target generated output for the SSE method (`StreamEventsSseAsync`):

```csharp
var url = "/events-sse";
using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
await EnsureSuccessAsync(response, cancellationToken);

var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

await foreach (var item in StreamReaders.ReadServerSentEventsAsync<Event>(stream, jsonSerializerOptions, cancellationToken))
{
    if (item != null)
    {
        yield return item;
    }
}
```

For `StreamingFraming.JsonArray`, keep the existing `JsonSerializer.DeserializeAsyncEnumerable<{T}>(stream, …)` block verbatim (no churn to the legacy path). Add `using <Root>.Streaming;` to the client's header when the helper is referenced.

- [ ] **Step 5: Branch the per-op client streaming body by framing (inline, no Atc.Rest.Client change)**

In `EndpointPerOperationExtractor.cs` (streaming branch ~line 1094), for non-`JsonArray` framings, build the `StreamingEndpointResponse<T>` inline instead of calling `BuildStreamingEndpointResponseAsync`. Target generated output:

```csharp
var response = await httpClient.SendAsync(requestBuilder.Build(HttpMethod.Get), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
if (!response.IsSuccessStatusCode)
{
    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
    return new StreamingEndpointResponse<Event>(false, response.StatusCode, content: null, errorContent, response);
}

var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
var content = StreamReaders.ReadServerSentEventsAsync<Event>(stream, jsonSerializerOptions, cancellationToken);
return new StreamingEndpointResponse<Event>(true, response.StatusCode, content, errorContent: null, response);
```

> The `StreamingEndpointResponse<T>` ctor is **public** (`isSuccess, statusCode, IAsyncEnumerable<T?>? content, errorContent, httpResponse`) — verified against `atc-net/atc-rest-client`. Match the per-op client's existing request-build/serializer-options idiom (it already has a `jsonSerializerOptions`/serializer in scope for other calls; reuse it). Keep the legacy `JsonArray` branch on `BuildStreamingEndpointResponseAsync<T>`.

- [ ] **Step 6: Wire-byte + round-trip harness (the real proof)**

Create `test/Atc.Rest.Api.SourceGenerator.Tests/Generators/StreamingWireFramingTests.cs`. It compiles the `StreamingItemSchema` **client** output, loads it, and invokes the emitted `StreamReaders` over a `MemoryStream` of known framing bytes:

```csharp
namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

public class StreamingWireFramingTests
{
    [Fact]
    public async Task StreamReaders_ServerSentEvents_ReadsItems()
    {
        // Arrange — compile + load the generated client, get the StreamReaders type.
        var streamReaders = LoadGeneratedType("StreamReaders");
        var read = streamReaders.GetMethod("ReadServerSentEventsAsync")!.MakeGenericMethod(typeof(JsonElement));

        const string sse = "data: {\"id\":\"a\",\"type\":\"x\"}\n\ndata: {\"id\":\"b\",\"type\":\"y\"}\n\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sse));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act — enumerate the emitted reader.
        var items = await EnumerateAsync<JsonElement>(read, stream, options);

        // Assert — exact item sequence parsed from SSE framing.
        Assert.Equal(2, items.Count);
        Assert.Equal("a", items[0].GetProperty("id").GetString());
        Assert.Equal("b", items[1].GetProperty("id").GetString());
    }

    // Compiles StreamingItemSchema Client-Typed, emits an in-memory assembly, loads it,
    // and returns the requested generated type by simple name.
    private static Type LoadGeneratedType(string simpleTypeName)
    {
        var (_, sources) = CompilationVerificationHarness.RunClient("StreamingItemSchema", "StreamingItemSchema.yaml");
        var asm = CompilationVerificationHarness.EmitAndLoad(sources);
        return asm.GetTypes().Single(t => t.Name == simpleTypeName);
    }

    private static async Task<List<T>> EnumerateAsync<T>(MethodInfo reader, Stream stream, JsonSerializerOptions options)
    {
        var enumerable = (IAsyncEnumerable<T?>)reader.Invoke(null, new object?[] { stream, options, CancellationToken.None })!;
        var result = new List<T>();
        await foreach (var item in enumerable)
        {
            if (item is not null) result.Add(item);
        }

        return result;
    }
}
```

> This requires two reusable helpers — `CompilationVerificationHarness.RunClient(...)` and `EmitAndLoad(...)`. Extract them from the private `RunGenerator` / compilation logic already in `CompilationVerificationTests.cs` into an internal static `CompilationVerificationHarness` so both test classes share it. `EmitAndLoad` = compile the sources (same references/ImplicitUsings as `CompileGeneratedSources`) to a `MemoryStream` via `compilation.Emit(ms)`, assert success, `Assembly.Load(ms.ToArray())`. This is the leaner, tractable realization of the spec's "round-trip" bar: it proves the emitted reader parses real SSE bytes (and, in Tasks 3–5, that writer bytes round-trip through the reader) without bootstrapping a Kestrel/TestHost server.

- [ ] **Step 7: Server SSE emit**

In `ResultClassExtractor.cs` `GenerateOkMethods` (the `isAsyncEnumerable` branch, ~line 492–517), branch the factory `Content` by `operation.GetStreamingFraming()`. For SSE the result wraps the stream with first-party SSE:

Target generated `Ok` factory for the SSE result class:

```csharp
/// <summary>
/// 200 OK - A stream of events.
/// </summary>
public static StreamEventsSseResult Ok(IAsyncEnumerable<Event> response)
    => new(TypedResults.ServerSentEvents(response));
```

(The result class threads `operation` through to `GenerateOkMethods` — it already receives enough context to know `isAsyncEnumerable`; pass the framing alongside. For `JsonArray`, keep `TypedResults.Ok(response)`.)

- [ ] **Step 8: Server SSE response metadata**

Ensure the endpoint advertises the content type. In the server endpoint emit (`EndpointDefinitionExtractor` / `EndpointMapHelper`), add `.Produces(StatusCodes.Status200OK, contentType: "text/event-stream")` (or the project's existing `Produces` shape) for SSE ops. Match the existing `.Produces(StatusCodes.Status200OK)` call already emitted (EventsEndpoints.verified.cs:46) and add the content type argument when framing ≠ JsonArray.

### 2c — TypeScript SSE

- [ ] **Step 9: Make `requestStream` framing-aware (Fetch + Axios)**

In `TypeScriptFetchApiClientExtractor.cs` and `TypeScriptAxiosApiClientExtractor.cs`, change `requestStream<T>(method, path, options?)` to accept a framing discriminator and dispatch. Target generated TS (Fetch):

```typescript
async *requestStream<T>(method: string, path: string, options?: RequestOptions, framing: StreamFraming = 'json-array'): AsyncGenerator<T> {
  // ... existing fetch + !response.ok handling unchanged ...
  const reader = response.body?.getReader();
  if (!reader) {
    throw new ApiError(0, 'NoBody', 'Response body is empty', response);
  }
  const decoder = new TextDecoder();
  let buffer = '';

  if (framing === 'sse') {
    // SSE: events separated by blank line; data: lines concatenated per event.
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      let sep: number;
      while ((sep = buffer.indexOf('\n\n')) !== -1) {
        const rawEvent = buffer.substring(0, sep);
        buffer = buffer.substring(sep + 2);
        const data = rawEvent
          .split('\n')
          .filter((l) => l.startsWith('data:'))
          .map((l) => l.slice(5).trimStart())
          .join('\n');
        if (data.length > 0) {
          yield JSON.parse(data) as T;
        }
      }
    }
    return;
  }

  // framing === 'json-array' | 'json-lines' | 'json-seq': existing brace-scan path (unchanged).
  // ... existing while/brace-matching loop ...
}
```

Add a `StreamFraming` TS type alias (`'json-array' | 'sse' | 'json-lines' | 'json-seq' | 'multipart'`) to the generated client base. The existing brace-scan already tolerates json-array/json-lines/json-seq objects; SSE and multipart get explicit branches (multipart in Task 5).

- [ ] **Step 10: Pass framing from `TypeScriptClientExtractor`**

In `TypeScriptClientExtractor.cs` (the `requestStream<…>('GET', …)` call sites ~line 783/799), append the framing argument derived from `operation.GetStreamingFraming()` mapped to the TS literal (`ServerSentEvents` → `'sse'`, etc.). Default (`JsonArray`) may omit the arg to minimize churn.

### 2d — Verify + snapshot

- [ ] **Step 11: Build + run wire-byte test**

Run: `dotnet build Atc.Rest.Api.SourceGenerator.slnx`
Then: `dotnet run --project test/Atc.Rest.Api.SourceGenerator.Tests/Atc.Rest.Api.SourceGenerator.Tests.csproj -- --filter-method "*StreamReaders_ServerSentEvents*"`
Expected: PASS (emitted SSE reader parses real `data:` framing).

- [ ] **Step 12: Regenerate + accept snapshots**

Run the integration test suite; for each reported mismatch under `StreamingItemSchema` (new `streamEventsSse` files across Server / Client-Typed / Client-Operation / TS-Client-Axios / TS-Client-Fetch / TS-Hooks-ReactQuery, the new `StreamReaders.cs`, and the SSE `Produces` change), inspect the `.received.cs`/`.received.ts`, confirm it matches the target outputs above, then copy `.received` → `.verified`. Re-run until green (one mismatch per run).

- [ ] **Step 13: Confirm generated code still compiles**

Run: `dotnet run --project test/Atc.Rest.Api.SourceGenerator.Tests/Atc.Rest.Api.SourceGenerator.Tests.csproj -- --filter-method "*GeneratedCode_CompilesWithoutErrors*"`
Expected: PASS for `StreamingItemSchema` (client + server) with the new SSE op + `StreamReaders.cs`.

- [ ] **Step 14: Commit**

```bash
git add -A
git commit -m "feat(openapi32): SSE wire-framing (server, C# clients, TS) + wire-byte harness"
```

---

## Task 3: JSON Lines (`application/jsonl`) end-to-end

**Files:** same emit sites as Task 2; the existing `/events` op (already `application/jsonl`) flips from JSON-array to real jsonl, so its snapshots churn.

- [ ] **Step 1: Add the jsonl writer + reader to the emitted helpers (target output)**

Append to the emitted server `Streaming/SequentialResults.cs` (introduced here — server-side helper; namespace `<Root>.Streaming`). It contains stream writers (write to a plain `Stream` so they are unit-testable) plus thin `IResult` wrappers:

```csharp
// <auto-generated />
#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace <Root>.Streaming;

[GeneratedCode("Atc.Rest.Api.SourceGenerator", "1.0.0")]
public static class SequentialStreamWriter
{
    public static async Task WriteJsonLinesAsync<T>(
        IAsyncEnumerable<T> items,
        Stream stream,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            await JsonSerializer.SerializeAsync(stream, item, options, cancellationToken);
            stream.WriteByte((byte)'\n');
            await stream.FlushAsync(cancellationToken);
        }
    }
}

[GeneratedCode("Atc.Rest.Api.SourceGenerator", "1.0.0")]
public sealed class JsonLinesResult<T> : IResult
{
    private readonly IAsyncEnumerable<T> items;

    public JsonLinesResult(IAsyncEnumerable<T> items)
        => this.items = items;

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = "application/jsonl";
        var options = httpContext.RequestServices
            .GetService(typeof(Microsoft.AspNetCore.Http.Json.JsonOptions)) is Microsoft.AspNetCore.Http.Json.JsonOptions jsonOptions
            ? jsonOptions.SerializerOptions
            : new JsonSerializerOptions();
        await SequentialStreamWriter.WriteJsonLinesAsync(items, httpContext.Response.Body, options, httpContext.RequestAborted);
    }
}
```

Append to the client `Streaming/StreamReaders.cs`:

```csharp
public static IAsyncEnumerable<T?> ReadJsonLinesAsync<T>(
    Stream stream,
    JsonSerializerOptions options,
    CancellationToken cancellationToken)
    => JsonSerializer.DeserializeAsyncEnumerable<T>(stream, topLevelValues: true, options, cancellationToken);
```

> `DeserializeAsyncEnumerable(stream, topLevelValues: true, …)` reads whitespace-separated top-level JSON values; `\n`-separated objects qualify. Verify the overload exists in the SDK in use (it is a .NET 9+ API).

- [ ] **Step 2: Write the failing round-trip test**

Add to `StreamingWireFramingTests.cs`:

```csharp
[Fact]
public async Task JsonLines_WriteThenRead_RoundTrips()
{
    var writerType = LoadGeneratedServerType("SequentialStreamWriter");
    var readerType = LoadGeneratedType("StreamReaders");
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    var source = new[]
    {
        JsonSerializer.SerializeToElement(new { id = "a", type = "x" }),
        JsonSerializer.SerializeToElement(new { id = "b", type = "y" }),
    };

    // Write
    using var ms = new MemoryStream();
    await InvokeWriteAsync(writerType, "WriteJsonLinesAsync", source, ms, options);
    var bytes = ms.ToArray();

    // Assert exact framing: one JSON object per line, newline-terminated.
    var text = Encoding.UTF8.GetString(bytes);
    Assert.EndsWith("\n", text);
    Assert.Equal(2, text.TrimEnd('\n').Split('\n').Length);
    Assert.DoesNotContain("[", text); // not a JSON array

    // Read back
    using var readStream = new MemoryStream(bytes);
    var read = readerType.GetMethod("ReadJsonLinesAsync")!.MakeGenericMethod(typeof(JsonElement));
    var items = await EnumerateAsync<JsonElement>(read, readStream, options);
    Assert.Equal(2, items.Count);
    Assert.Equal("a", items[0].GetProperty("id").GetString());
}
```

(Add `LoadGeneratedServerType` — same as `LoadGeneratedType` but `CompilationVerificationHarness.RunServer(... useFullReferences:true)`; and `InvokeWriteAsync` reflection helper that builds an `IAsyncEnumerable<JsonElement>` from the array and awaits the returned `Task`.)

- [ ] **Step 3: Run — expect FAIL** (helpers/methods not emitted yet).

Run: `dotnet run --project test/Atc.Rest.Api.SourceGenerator.Tests/Atc.Rest.Api.SourceGenerator.Tests.csproj -- --filter-method "*JsonLines_WriteThenRead*"`

- [ ] **Step 4: Emit `SequentialResults.cs` from the server generator**

Mirror Task 2 Step 3: emit the server helper file once when any server operation has framing ∈ {JsonLines, JsonSequence, MultipartMixed}. SSE needs no helper.

- [ ] **Step 5: Server jsonl factory**

In `ResultClassExtractor` framing switch, for `JsonLines` emit:

```csharp
public static StreamEventsResult Ok(IAsyncEnumerable<Event> response)
    => new(new <Root>.Streaming.JsonLinesResult<Event>(response));
```

- [ ] **Step 6: Client jsonl read branch** — in `HttpClientExtractor.GenerateGetMethodBody` and `EndpointPerOperationExtractor`, for `JsonLines` call `StreamReaders.ReadJsonLinesAsync<Event>(stream, jsonSerializerOptions, cancellationToken)` (typed client `await foreach … yield`; per-op client wraps in `StreamingEndpointResponse<Event>`).

- [ ] **Step 7: TS jsonl** — the existing brace-scan already handles `{…}\n{…}`; pass `'json-lines'` from `TypeScriptClientExtractor`. No new TS parse branch needed (the brace-scan path covers it).

- [ ] **Step 8: Run round-trip test — expect PASS.**

- [ ] **Step 9: Regenerate + accept snapshots.** The existing `/events` (jsonl) files now show `JsonLinesResult<Event>` (server) and `ReadJsonLinesAsync` (clients) instead of `TypedResults.Ok` / `DeserializeAsyncEnumerable`. Accept across all five output folders + new `SequentialResults.cs`.

- [ ] **Step 10: Compile check** (`*GeneratedCode_CompilesWithoutErrors*`) — PASS.

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "feat(openapi32): JSON Lines wire-framing (real jsonl on /events)"
```

---

## Task 4: JSON Text Sequence (`application/json-seq`, RFC 7464)

**Files:** same emit sites; add a `/events-seq` op.

- [ ] **Step 1: Add `/events-seq` op to the spec** (copy the `/events-sse` block; `operationId: streamEventsSeq`, media type `application/json-seq`).

- [ ] **Step 2: Add writer + reader to the helpers (target output)**

Server `SequentialStreamWriter`:

```csharp
public static async Task WriteJsonSequenceAsync<T>(
    IAsyncEnumerable<T> items,
    Stream stream,
    JsonSerializerOptions options,
    CancellationToken cancellationToken)
{
    await foreach (var item in items.WithCancellation(cancellationToken))
    {
        stream.WriteByte(0x1E); // RS
        await JsonSerializer.SerializeAsync(stream, item, options, cancellationToken);
        stream.WriteByte((byte)'\n');
        await stream.FlushAsync(cancellationToken);
    }
}
```

Plus a `JsonSequenceResult<T>` IResult (same shape as `JsonLinesResult<T>`, `ContentType = "application/json-seq"`, calls `WriteJsonSequenceAsync`).

Client `StreamReaders`:

```csharp
public static async IAsyncEnumerable<T?> ReadJsonSequenceAsync<T>(
    Stream stream,
    JsonSerializerOptions options,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
    var sb = new StringBuilder();

    while (true)
    {
        var ch = reader.Read();
        if (ch == -1)
        {
            break;
        }

        if (ch == 0x1E)
        {
            if (sb.Length > 0)
            {
                yield return JsonSerializer.Deserialize<T>(sb.ToString().Trim(), options);
                sb.Clear();
            }

            continue;
        }

        sb.Append((char)ch);
    }

    if (sb.Length > 0)
    {
        yield return JsonSerializer.Deserialize<T>(sb.ToString().Trim(), options);
    }
}
```

> RFC 7464 records are `RS <json> LF`. Splitting on the leading `0x1E` and trimming trailing whitespace is robust to the trailing `\n`. (A `Pipe`/`Utf8JsonReader` version is a possible optimization; the StreamReader version is correct and testable — keep it simple.)

- [ ] **Step 3: Failing round-trip test** — add `JsonSequence_WriteThenRead_RoundTrips` mirroring Task 3 Step 2, asserting each record starts with `\x1e` (`Assert.Contains("", text)`) and reads back 2 items.

- [ ] **Step 4: Run — FAIL.**
- [ ] **Step 5: Emit writer/reader + server factory (`JsonSequenceResult<Event>`) + client read branch (`ReadJsonSequenceAsync`).**
- [ ] **Step 6: TS json-seq** — the brace-scan tolerates the leading `0x1E` (it seeks `{`); pass `'json-seq'`. No new branch required. (If a snapshot shows the RS leaking into output, add a `buffer = buffer.replace(/\x1e/g, '')` guard in the non-SSE path.)
- [ ] **Step 7: Run round-trip — PASS.**
- [ ] **Step 8: Regenerate + accept snapshots** (new `/events-seq` files across all folders).
- [ ] **Step 9: Compile check — PASS.**
- [ ] **Step 10: Commit** `feat(openapi32): JSON Text Sequence (RFC 7464) wire-framing`.

---

## Task 5: Multipart mixed (`multipart/mixed`) — the heavy one

**Files:** same emit sites; add `/events-multipart`; the only task needing a hand-rolled boundary parser on the client and a new TS branch.

- [ ] **Step 1: Add `/events-multipart` op** (`operationId: streamEventsMultipart`, media type `multipart/mixed`).

- [ ] **Step 2: Server writer + IResult (target output)**

`SequentialStreamWriter`:

```csharp
public const string MultipartBoundary = "atc-stream-boundary";

public static async Task WriteMultipartMixedAsync<T>(
    IAsyncEnumerable<T> items,
    Stream stream,
    string boundary,
    JsonSerializerOptions options,
    CancellationToken cancellationToken)
{
    await foreach (var item in items.WithCancellation(cancellationToken))
    {
        var header = $"--{boundary}\r\nContent-Type: application/json\r\n\r\n";
        await WriteAsciiAsync(stream, header, cancellationToken);
        await JsonSerializer.SerializeAsync(stream, item, options, cancellationToken);
        await WriteAsciiAsync(stream, "\r\n", cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    await WriteAsciiAsync(stream, $"--{boundary}--\r\n", cancellationToken);
    await stream.FlushAsync(cancellationToken);

    static Task WriteAsciiAsync(Stream s, string text, CancellationToken ct)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        return s.WriteAsync(bytes, 0, bytes.Length, ct);
    }
}
```

`MultipartMixedResult<T>` IResult: `ContentType = $"multipart/mixed; boundary={SequentialStreamWriter.MultipartBoundary}"`, calls `WriteMultipartMixedAsync(items, Body, MultipartBoundary, options, RequestAborted)`.

- [ ] **Step 3: Client boundary reader (target output)** in `StreamReaders`:

```csharp
public static async IAsyncEnumerable<T?> ReadMultipartMixedAsync<T>(
    Stream stream,
    string boundary,
    JsonSerializerOptions options,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    var delimiter = "--" + boundary;
    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

    var inPart = false;     // have we seen the first boundary yet
    var inHeaders = false;  // currently skipping a part's headers
    var body = new StringBuilder();

    string? line;
    while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
    {
        if (line.StartsWith(delimiter, StringComparison.Ordinal))
        {
            // A boundary: flush the part we just finished, then open/close.
            if (inPart && body.Length > 0)
            {
                yield return JsonSerializer.Deserialize<T>(body.ToString(), options);
                body.Clear();
            }

            if (line.StartsWith(delimiter + "--", StringComparison.Ordinal))
            {
                yield break; // closing delimiter
            }

            inPart = true;
            inHeaders = true;
            continue;
        }

        if (!inPart)
        {
            continue; // preamble before the first boundary
        }

        if (inHeaders)
        {
            if (line.Length == 0)
            {
                inHeaders = false; // blank line terminates this part's headers
            }

            continue;
        }

        body.Append(line);
    }

    if (inPart && body.Length > 0)
    {
        yield return JsonSerializer.Deserialize<T>(body.ToString(), options);
    }
}
```

> The boundary reader is fiddly. The round-trip test (Step 5) is the contract — adjust the loop until write→read yields the exact item sequence. Keep the parser entirely inside `StreamReaders` so the ~40 lines exist once. The generated per-op/typed client extracts the boundary from the response: `var boundary = response.Content.Headers.ContentType?.Parameters.FirstOrDefault(p => p.Name == "boundary")?.Value?.Trim('"') ?? "atc-stream-boundary";`

- [ ] **Step 4: Failing round-trip test** — `MultipartMixed_WriteThenRead_RoundTrips`: write 2 items with boundary `"atc-stream-boundary"`, assert bytes contain `--atc-stream-boundary` and end with `--atc-stream-boundary--\r\n`, read back 2 items.

- [ ] **Step 5: Run — FAIL; implement emit; iterate the reader until the round-trip test PASSES.** This is where to spend debugging time — the byte test is the arbiter.

- [ ] **Step 6: Server factory** (`MultipartMixedResult<Event>`) + **client read branch** (extract boundary, call `ReadMultipartMixedAsync<Event>(stream, boundary, …)`).

- [ ] **Step 7: TS multipart branch** in `requestStream` (`framing === 'multipart'`): read the `content-type` response header for the boundary, split the decoded buffer on `--<boundary>`, for each part strip headers up to the blank line and `JSON.parse` the body; stop at `--<boundary>--`. Pass `'multipart'` from `TypeScriptClientExtractor`, and have the TS client read the boundary from `response.headers.get('content-type')`.

- [ ] **Step 8: Regenerate + accept snapshots** (new `/events-multipart` files everywhere + the multipart helper additions).
- [ ] **Step 9: Compile check — PASS.**
- [ ] **Step 10: Commit** `feat(openapi32): multipart/mixed wire-framing`.

---

## Task 6: Finalize — full suite, roadmap, verification

- [ ] **Step 1: Run the entire test suite** (unit + integration + compile + wire-byte). Expected: all green; integration byte-identical.

- [ ] **Step 2: Release build** `dotnet build Atc.Rest.Api.SourceGenerator.slnx -c Release` — 0 warnings / 0 errors (TreatWarningsAsErrors on).

- [ ] **Step 3: Update `docs/roadmap-openapi32-support.md`** — flip Phase 2 row + summary from 🟡 to ✅, replace the "Known limitation (JSON-array framing)" note with the shipped media-type framing matrix, and note the wire-byte round-trip test as the proof. Remove the "must not be marked ✅ off snapshot-green alone" caveat now that the byte test exists.

- [ ] **Step 4: Commit** `docs(openapi32): mark Phase 2 streaming wire-framing complete`.

---

## Notes / risks carried from the spec

- **`SseParser.Create<T>` delegate shape** — confirm against the .NET 10 SDK in Task 2 Step 2; the round-trip test catches a wrong signature.
- **`JsonOptions` source on the server** — `JsonLinesResult`/etc. read `Microsoft.AspNetCore.Http.Json.JsonOptions` from DI to match the rest of the API's serialization; fall back to `new()` if absent.
- **TS `requestStream` signature churn** — all streaming TS snapshots regenerate in Task 2 Step 9; that's expected.
- **No `Atc.Rest.Client` change required** — the per-op client builds `StreamingEndpointResponse<T>` inline via the **public** ctor. If a future cleanup prefers a framing-aware runtime helper instead, that is an optional enhancement documented separately (see the accompanying `atc-rest-client` note), not a dependency of this plan.
