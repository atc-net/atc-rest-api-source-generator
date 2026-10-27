# Contributing to Atc.Rest.Api.SourceGenerator

Thanks for your interest in contributing! This repository is part of the [ATC.Net](https://atc-net.github.io) family of open-source libraries; the general contribution and coding guidelines live at the ATC umbrella site.

## 📚 Guidelines

- **[ATC contribution guidelines](https://atc-net.github.io/introduction/about-atc#how-to-contribute)** - what to expect from PRs, branch naming, etc.
- **[ATC coding guidelines](https://atc-net.github.io/introduction/about-atc#coding-guidelines)** - code style, naming, testing.
- **[Security policy](SECURITY.md)** - how to report a vulnerability (privately, not as an issue).
- **[Wiki](https://github.com/atc-net/atc-rest-api-source-generator/wiki)** - the documentation; [Development Notes](https://github.com/atc-net/atc-rest-api-source-generator/wiki/Development-Notes) describes the architecture and the test suites.

## 🛠️ Getting started

```bash
git clone https://github.com/atc-net/atc-rest-api-source-generator.git
cd atc-rest-api-source-generator
dotnet build Atc.Rest.Api.SourceGenerator.slnx
dotnet test --solution Atc.Rest.Api.SourceGenerator.slnx
```

> Requires the **.NET 10 SDK**. The TypeScript gates need **Node.js 24**; without it they skip themselves.

## ✅ Before opening a PR

1. **Write the test first.** A bug fix starts with a test that fails for the bug; a generator change usually shows in the scenario snapshots under `test/Scenarios` - review the `*.received.*` files before accepting them.
2. **Build clean in Release.** Release builds treat warnings as errors, the test projects included:
   ```bash
   dotnet build Atc.Rest.Api.SourceGenerator.slnx -c Release
   ```
3. **Run all the test projects**, `Atc.Rest.Api.Generator.IntegrationTests` included - it holds the TypeScript snapshots and the package-install test.
4. **Update the documentation**: the [wiki](https://github.com/atc-net/atc-rest-api-source-generator/wiki), the README and the samples when the behavior or the generated code changes.
5. **Use Conventional Commits** (`fix(client): …`, `feat(server): …`, `docs: …`). The changelog and the release notes are written from them by release-please, so do not edit `CHANGELOG.md` by hand.

## 📦 Updating dependencies

Dependencies live in three places - NuGet (`*.csproj`, `Directory.Build.props`), npm (the Showcase React apps, the Playwright tests and the TypeScript gates' `test/NodeWorkspace`) and the GitHub Actions workflows. List what is outdated in all three, majors included:

```powershell
./scripts/check-outdated.ps1          # or: NuGet, Npm, Actions
```

```bash
scripts/check-outdated.sh             # or: nuget, npm, actions
```

`npm outdated` and `npm update` stay inside each `package.json` range (`^6.1.0` never shows 7.x), so they miss the majors. Before a release, take the minor and patch updates, and either take each major or keep it pinned with an `ignore` and its reason in `.github/dependabot.yml`. The majors of `test/NodeWorkspace` are the versions the generated TypeScript is checked against, so each is a PR of its own.

Repository automation such as `.github/dependabot.yml` only runs from `main`: change it in a small PR of its own, not on a long feature branch.
