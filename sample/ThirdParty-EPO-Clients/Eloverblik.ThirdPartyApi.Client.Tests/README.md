# Eloverblik.ThirdPartyApi.Client.Tests

Showcase test project for the **`EndpointPerOperation`** generation mode.

## Which style should I use?

| Your goal | Seam | Files here |
|-----------|------|------------|
| Test **your own** service that calls the API | 🔌 Contract — mock `IXEndpoint` | `InterfaceLevel/MeteringPointReportServiceTests.cs` |
| Verify a **generated endpoint** builds URLs, serializes and maps status codes | 🚚 Transport — fake `HttpMessageHandler` | `EndpointLevel/GetTokenEndpointTests.cs` |
| Verify every endpoint is registered | 🧩 DI — build a real container | `DependencyInjection/ServiceRegistrationTests.cs` |

**Rule of thumb:** if the class under test is *yours*, mock the endpoint interface. In this mode failures arrive as **data** (`IsOk == false`), not exceptions — so always cover the failure branch.

## Layout

```
TestDoubles/
  FakeHttpMessageHandler.cs   # records requests, replays canned responses
  StubHttpClientFactory.cs    # endpoints resolve transport via IHttpClientFactory
  EndpointResultFactory.cs    # builds concrete endpoint results (see below)
  ModelBuilder.cs             # hides the 23-argument positional records
Scenario/
  MeteringPointReportService.cs
```

## Gotchas worth knowing

- **`ExecuteAsync` returns the *concrete* result type**, not `IXEndpointResult`. Because that type is sealed and only constructible from an `Atc.Rest.Client` `EndpointResponse`, a mocked endpoint cannot return a substituted result — it must hand back a real one. That is the sole reason `EndpointResultFactory` exists; it disappears once generated result factories ship (item 2.1 / 10.9 in `issues/client-testing.md`).
- **Endpoints need `IHttpMessageFactory`.** These tests resolve it from a container built with the generated `AddEloverblikApiThirdPartyApiEndpoints()`, then pair it with a `StubHttpClientFactory`.
- **`OutputType=Exe` is required** for `xunit.v3`, and `sample/` projects inherit no shared props or `.editorconfig`.

## Running

```pwsh
dotnet test sample/ThirdParty-EPO-Clients/Eloverblik.ThirdPartyApi.Client.Tests
```

## See also

- `sample/ThirdParty-Typed-Clients/Eloverblik.ThirdPartyApi.Client.Tests` — the same scenarios in `TypedClient` mode
- Wiki: **Working with C# Client Testing**
