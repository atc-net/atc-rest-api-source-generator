# Eloverblik.ThirdPartyApi.Client.Tests

Showcase test project for the **`TypedClient`** generation mode.

It demonstrates how a consumer unit-tests code that depends on a generated client, and doubles as an acceptance test for the generator itself — it was this project that caught the two-constructor DI defect that source-text assertions could not see.

## Which style should I use?

Pick the seam that matches what you are actually verifying.

| Your goal | Seam | Files here |
|-----------|------|------------|
| Test **your own** service that calls the API | 🔌 Contract — mock `I{ClientName}` | `MeteringPointReportServiceTests.cs` |
| Verify the **generated client** builds URLs, serializes and maps status codes | 🚚 Transport — fake `HttpMessageHandler` | `ElOverblikThirdPartyApiClientTests.cs` |
| Verify DI wiring actually resolves | 🧩 DI — build a real container | `ServiceCollectionExtensionsTests.cs` |

**Rule of thumb:** if the class under test is *yours*, mock the interface. Only drop to the transport seam when the generated client is the thing being verified — those tests are slower to write and break on unrelated spec changes, so keep one or two per operation.

## Layout

```
TestDoubles/
  FakeHttpMessageHandler.cs      # records requests, replays canned responses
  HttpResponseMessageFactory.cs  # builds JSON responses concisely
  ModelBuilder.cs                # hides the 23-argument positional records
MeteringPointReportService.cs    # stand-in for real consumer domain code
```

## Gotchas worth knowing

- **`OutputType=Exe` is required.** `xunit.v3` reports *"zero tests ran"* without it.
- **No inherited config.** Projects under `sample/` inherit neither `test/Directory.Build.props` nor `test/.editorconfig`, so this project declares its own package versions and analyzer relaxations.
- **Generated models are positional records with no defaults.** A test touching two fields must still pass all 23 arguments — hence `ModelBuilder`. Tracked as a Phase 2 follow-up in `issues/client-testing.md`.

## Running

```pwsh
dotnet test sample/ThirdParty-Typed-Clients/Eloverblik.ThirdPartyApi.Client.Tests
```

## See also

- `sample/ThirdParty-EPO-Clients/Eloverblik.ThirdPartyApi.Client.Tests` — the same scenarios in `EndpointPerOperation` mode
- Wiki: **Working with C# Client Testing**
