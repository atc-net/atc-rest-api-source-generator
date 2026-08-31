# Eloverblik.ThirdPartyApi.Client

Typed-client sample generated from the Danish Eloverblik ThirdParty API specification.

Upstream API documentation: <https://docs.eloverblik.dk/docs/api/thirdparty#description/introduction>

## What this sample demonstrates

### Namespace resolution without a pinned `namespace`

The marker file (`.atc-rest-api-client`) deliberately does **not** set `namespace`. The specification file is named `api-1.yaml`, which is not a usable C# identifier, so this sample exercises the namespace resolution chain:

1. `namespace` in the marker file - not set here.
2. `info.title` from the specification - `Eloverblik.Api.ThirdPartyApi`, which is already a valid dotted identifier and is therefore used.
3. The specification file name - would yield `api-1`, and is only reached as a last resort.

The resulting namespace is `Eloverblik.Api.ThirdPartyApi`.

> Note that the namespace comes from `info.title`, not from the project name. The project is named `Eloverblik.ThirdPartyApi.Client` to describe what it produces, while the generated namespace continues to mirror the specification.

### Client granularity and type naming

This sample sets `clientGranularity` to `Single`, so the whole specification is emitted as **one** client type instead of one client per path area.

`clientName` is set to `ElOverblikThirdPartyApiClient`. An explicit `clientName` is taken as the author stating the full type name, so it is used **verbatim** and `clientSuffix` is *not* appended - no `ClientClient` is produced. When `clientName` is omitted, the type name is instead derived from the namespace, and a trailing segment equal to the suffix is dropped before the suffix is appended.

Under `Single` granularity the generated types are laid out flat:

| Type | Namespace |
| --- | --- |
| Client | `Eloverblik.Api.ThirdPartyApi.Generated` |
| Parameter records | `Eloverblik.Api.ThirdPartyApi.Generated` |
| Models | `Eloverblik.Api.ThirdPartyApi.Generated.Models` |

With the default `PerArea` granularity these would instead be nested per area under `.Generated.<Area>.Client` and `.Generated.<Area>.Models`.

### Versioning

The API version comes from `info.version` in the specification, never from the `api-1.yaml` file name.

## Building

```shell
dotnet build
```

The client is produced at build time by the `Atc.Rest.Api.SourceGenerator` analyzer; there are no checked-in generated files.
