# Working in this repository (AI assistants & new contributors)

This file is the **operating manual** for anyone — human or AI — doing work in this repo.
Read it before generating code or documentation. The guideline documents it points to are
**authoritative**; when in doubt, follow them over habit.

## What this project is

AppSuite is a **.NET 10 / Aspire / DDD** app suite for families and groups of friends
(first app: Secret-Santa). It is **local-first** (works offline with no account) with
**optional account-based sync**. Targets: Web (Blazor) + native Android/iOS (MAUI Blazor
Hybrid), sharing feature code. Swiss-first; product is multilingual (DE/EN/FR); **all code
and docs are English**. See [`README.md`](README.md).

## Golden rules

1. **Docs-as-code.** All documentation is Markdown in [`docs/`](docs/), versioned with the
   code and reviewed in the same PRs. Never put documentation anywhere else.
2. **Follow the process.** Work flows through the phases in
   [`docs/process/ways-of-working.md`](docs/process/ways-of-working.md). Produce the artifact
   each phase expects (use the templates), and keep the traceability chain intact.
3. **Never start from a blank page.** Every artifact type has a template in its folder
   (`_template-*.md`). Copy it; don't improvise structure.
4. **English only** in code, comments, identifiers, DB tables, and docs. The product UI is
   translated (DE/EN/FR) at runtime — that is a localization concern, not a code concern.
5. **Decisions are ADRs.** Any significant architectural or technological choice is recorded
   in [`docs/adr/`](docs/adr/) before it is implemented. Don't silently decide in code.
6. **Respect the layering.** Dependencies point inward (Domain ← Application ← Infrastructure
   ← Hosts). The domain has no I/O, EF, ASP.NET, or MAUI dependencies. See the architecture
   guidelines.
7. **Ubiquitous language.** Names in code must match the [glossary](docs/glossary/). Don't
   invent domain terms; add a glossary entry first if a term is missing.
8. **No git operations without an explicit order.** Never create branches, stage, commit, push,
   merge, rebase, tag, or otherwise change git state — unless the user explicitly tells you to do
   that specific action, in that instance. Prior permission does **not** carry over to later actions.
   Make and leave changes in the working tree; the user drives git.
9. **All knowledge lives in this repo.** Every piece of project information, decision, plan, or
   context must reside in this repository as Markdown under git. Do **not** use global/external
   memory files or any store outside the repo to hold project knowledge. If something needs to be
   remembered, write it into the right in-repo doc (backlog, ADR, glossary, feature spec, arc42, …).

## Placeholder & naming convention

- **AppSuite** is a neutral **working name**, not a brand. It is the solution / project /
  namespace prefix (e.g. `AppSuite.AppHost`, `AppSuite.Main.Service.Core`) and the placeholder used
  in prose. The final brand is **deliberately deferred** (branding may change) — do **not** bake a
  brand into project/namespace names yet. Leading candidate: **Walf** (*We Are Like Family*).
- Individual apps become **subdomains** of the (future) suite domain (e.g. `secretsanta.<suite-domain>`).
  App brand names are decided per app during that app's idea/branding step.
- Candidate names + domain findings live in [`docs/process/naming.md`](docs/process/naming.md).

## Where the guidelines live (authoritative — read before you build)

| Topic | File |
|-------|------|
| C# / .NET best practices + DDD tactical patterns | [`docs/guidelines/coding-guidelines.md`](docs/guidelines/coding-guidelines.md) |
| DDD strategic + layering + Aspire | [`docs/guidelines/architecture-guidelines.md`](docs/guidelines/architecture-guidelines.md) |
| Testing strategy & levels | [`docs/guidelines/testing-guidelines.md`](docs/guidelines/testing-guidelines.md) |
| Localization (DE/EN/FR) | [`docs/guidelines/localization-guidelines.md`](docs/guidelines/localization-guidelines.md) |
| Writing docs & diagrams | [`docs/guidelines/documentation-guidelines.md`](docs/guidelines/documentation-guidelines.md) |
| Security & privacy | [`docs/guidelines/security-privacy-guidelines.md`](docs/guidelines/security-privacy-guidelines.md) |
| Git / branches / commits | [`docs/process/git-conventions.md`](docs/process/git-conventions.md) |
| Definition of Done | [`docs/process/definition-of-done.md`](docs/process/definition-of-done.md) |

## Creating each artifact (which template, where)

| Artifact | Copy template | Destination | ID scheme |
|----------|---------------|-------------|-----------|
| Architecture decision | `docs/adr/_template-adr.md` | `docs/adr/` | `NNNN-kebab-title.md` |
| Use case | `docs/use-cases/_template-use-case.md` | `docs/use-cases/<app>/` | `UC-<APP>-NNN` |
| Requirement | `docs/requirements/_template-requirement.md` | `docs/requirements/functional` or `non-functional` | `FR-NNN` / `NFR-NNN` |
| User story | `docs/requirements/_template-user-story.md` | `docs/requirements/` | `US-NNN` |
| Feature spec | `docs/features/_template-feature-spec.md` | `docs/features/` | `FEAT-NNN` |
| Test plan | `docs/features/_template-test-plan.md` | `docs/features/` | `TP-NNN` |
| Brainstorming | `docs/brainstorming/_template-brainstorming-session.md` | `docs/brainstorming/` | dated |
| Glossary term | `docs/glossary/_template-glossary-entry.md` | `docs/glossary/` | term slug |

After creating an artifact, **update the folder's `index.md` and `toc.yml`** so it appears in
the docs site, and link it into the traceability chain (backward + forward by ID).

## Build & docs commands

```bash
dotnet tool restore                          # install DocFX (local tool)
dotnet docfx docfx/docfx.json                # build the docs site (fails on broken links/xrefs)
dotnet docfx docfx/docfx.json --serve        # build + serve locally
dotnet build AppSuite.slnx                      # build the solution
dotnet test                                  # run tests (once test projects exist)
```

Builds run with `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` — style violations fail
the build. Packages are added **only** via `Directory.Packages.props` (Central Package Mgmt).

## Verify UI work in a real browser

**A green test run is not evidence that a feature works.** Whenever you add or change something
user-facing, run the app and drive it yourself before reporting the work as done — several real
defects here (a silently partial set of reveal links, `Anlass:24.12.2026Zürich` with no separators,
a badge whose text said the opposite of its condition) all passed the unit tests and were only
visible on screen.

```bash
dotnet run --project src/Apps/SecretSanta/AppSuite.SecretSanta.WebApp --launch-profile http
# → http://localhost:5081   (the http profile avoids dev-certificate prompts)
```

The Secret-Santa web app is a **standalone Blazor WASM** app with no backend — it is not part of the
Aspire AppHost, so run its project directly. State lives in browser `localStorage`; clear it with
`localStorage.clear()` for a clean run.

When driving it:

- Walk the whole flow a user would (create → members → exclusions → draw → share → open a reveal
  link), not just the screen you touched.
- Check the **error and empty paths** too, and read the **browser console** — it should stay clean.
- Verify every locale you touched (DE/EN/FR); the language picker is in the sidebar.
- Look at the screenshots. A page that renders is not the same as a page that reads correctly.

## Do NOT

- Don't skip templates or the process phases.
- **Don't call UI work done on a green test run alone** — run the app and look at it (see above).
- Don't invent domain terms outside the glossary.
- Don't add NuGet packages outside `Directory.Packages.props`.
- Don't put domain logic in hosts, or infrastructure concerns in the domain.
- Don't translate the documentation (product strings only, at runtime).
- Don't decide architecture in code without an ADR.
- Don't commit secrets or real personal data.
- **Don't run any git operation** (branch, stage, commit, push, merge, rebase, tag) unless the user
  explicitly orders it in that instance — prior approval never carries over.
- **Don't store project knowledge outside the repo.** No global/external memory files; capture
  everything as Markdown under git.
