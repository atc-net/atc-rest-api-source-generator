# Security Policy

Thank you for helping keep `Atc.Rest.Api.SourceGenerator` and the code it generates safe.

## Supported versions

Security fixes are applied to the latest released **major** version on the [`main`](https://github.com/atc-net/atc-rest-api-source-generator) branch. Older major versions are not maintained for security fixes; please upgrade to the latest version when reporting an issue.

| Version line | Supported |
|---|---|
| Latest `main` / latest published NuGet | ✅ |
| Older major versions | ❌ |

## Reporting a vulnerability

**Please do not file a public GitHub issue for security-sensitive reports.**

Use one of these private channels instead:

1. **GitHub private vulnerability reporting** - open the repository's [Security tab](https://github.com/atc-net/atc-rest-api-source-generator/security) and choose **Report a vulnerability**. This routes the report directly to the maintainers and is the preferred path.
2. **Direct contact** - if private reporting is unavailable, contact the ATC.Net maintainers via [atc-net.github.io](https://atc-net.github.io) and request a private channel.

When reporting, please include:

- A description of the issue and the impact you have assessed.
- A minimal reproduction: the OpenAPI specification (cut down, without anything confidential), the marker file, and the generated code or CLI command involved.
- The affected version(s) of the packages (`Atc.Rest.Api.SourceGenerator`, `atc-rest-api-gen`, `Atc.Rest.Api.Client.Testing`) and the .NET SDK.
- Any relevant logs, stack traces or build diagnostics.

## What to expect

- An acknowledgement within a reasonable timeframe (typically a few business days).
- A coordinated disclosure timeline if the report is confirmed.
- Credit in the release notes once the fix ships, unless you prefer to remain anonymous.

## Scope

This policy covers the packages this repository produces and the code they generate (servers, C# and TypeScript clients). Issues in the runtime libraries the generated code uses (`Atc.Rest.MinimalApi`, `Atc.Rest.Client`) or in other dependencies should be reported to those projects directly; if you are unsure, send the report here and we will help route it.
