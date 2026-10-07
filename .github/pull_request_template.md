## Description

<!-- What changes and why, if the linked issues do not say it. Breaking changes: say what a consumer must do. -->

## Checklist

- [ ] The PR targets `main` and links the related issues, if any.
- [ ] A test covers the change, and it failed before the fix.
- [ ] Changed snapshots (`*.verified.*`) were reviewed, not just accepted.
- [ ] `dotnet build Atc.Rest.Api.SourceGenerator.slnx -c Release` is clean.
- [ ] All test projects pass, `Atc.Rest.Api.Generator.IntegrationTests` included.
- [ ] The wiki, README and samples are updated where the behavior or the generated code changed.
- [ ] Commit messages follow Conventional Commits (`fix:`, `feat:`, `feat!:` for a breaking change).
