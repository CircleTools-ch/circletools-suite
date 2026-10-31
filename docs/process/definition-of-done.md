# Definition of Done

A change is **Done** only when all of the following hold. Phase-specific DoDs in
[ways-of-working](ways-of-working.md) add to this baseline.

## Every change
- [ ] Builds clean: `dotnet build` with `TreatWarningsAsErrors` (no warnings).
- [ ] Style passes: `EnforceCodeStyleInBuild` (analyzer/editorconfig rules).
- [ ] Documentation site builds: `dotnet docfx docfx/docfx.json` with no broken links/xrefs.
- [ ] Relevant docs updated (arc42/ADR/guidelines/glossary as applicable).
- [ ] Traceability links closed (backward + forward by ID).
- [ ] No secrets or real personal data added.
- [ ] PR follows [git conventions](git-conventions.md) and is reviewed.

## Code changes (additionally)
- [ ] Layering/boundaries respected (architecture tests green).
- [ ] Tests cover the acceptance criteria; coverage target met; CI green.
- [ ] Public APIs have XML doc comments.
- [ ] New user-facing strings localized (DE/EN/FR).
- [ ] Packages added only via `Directory.Packages.props`.

## Server changes (additionally)
- [ ] Still works with **every app version released through a store** ([ADR-0090](../adr/0090-backend-stays-compatible-with-released-apps.md)):
      no changed or removed endpoint, field, status or error code; documents from older apps still read;
      the review answers "what happens when the oldest app does this?".
- [ ] A new server-side deletion or rejection path has a test, including a request from an older or stale copy.

## Decisions (additionally)
- [ ] Recorded as an ADR; status **Accepted**; arc42 updated.
