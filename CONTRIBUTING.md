# Contributing

Thanks for working on `<Suite>`. This project is run deliberately — like a professional
product, not a weekend hack. Please take a few minutes to read this and
[`CLAUDE.md`](CLAUDE.md) before contributing.

## Development environment

- **.NET 10 SDK** — the exact band is pinned in [`global.json`](global.json).
- **MAUI workloads** (`android`, `ios`, `maccatalyst`, `maui-windows`) — only needed once
  native app work begins: `dotnet workload restore`.
- **DocFX** — installed as a local tool: `dotnet tool restore`.
- An IDE with EditorConfig + Roslyn analyzer support (Visual Studio, Rider, or VS Code with
  the C# Dev Kit).

Verify your setup:

```bash
dotnet --version                 # resolves the pinned .NET 10 SDK
dotnet tool restore
dotnet docfx docfx/docfx.json    # docs site builds
dotnet build Suite.slnx          # solution builds
```

## How we work

All work flows through the phases described in
[`docs/process/ways-of-working.md`](docs/process/ways-of-working.md):

> idea → brainstorming → use cases + requirements → architecture decisions (ADRs) →
> feature spec → implementation → tests → deployment

Each phase produces a specific artifact from a template. See the artifact/template map in
[`CLAUDE.md`](CLAUDE.md#creating-each-artifact-which-template-where). New domain terms go in the
[glossary](docs/glossary/) first.

## Branching & commits

Follow [`docs/process/git-conventions.md`](docs/process/git-conventions.md):

- Branch off `main`: `feat/…`, `fix/…`, `docs/…`, `chore/…`.
- **Conventional Commits** (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`).
- Small, focused PRs. Keep code and its docs in the same PR.

## Adding or updating documentation

- Write Markdown in [`docs/`](docs/). Add a `toc.yml` entry and link from the folder `index.md`.
- **Diagrams:** Mermaid inline by default (fenced ` ```mermaid ` blocks). For rigorous C4
  diagrams, author PlantUML in `docs/diagrams/c4/` and commit the **pre-rendered SVG** to
  `docs/diagrams/exported/` (see [ADR-0004](docs/adr/0004-plantuml-prerender.md)).
- Build the site locally to confirm there are **no broken links/xrefs** before opening a PR.

## Definition of Done

Every change must satisfy [`docs/process/definition-of-done.md`](docs/process/definition-of-done.md).
At minimum: builds clean under analyzers, tests cover the acceptance criteria, docs updated,
traceability links closed, PR reviewed.

## Proposing an architecture decision

Copy [`docs/adr/0000-template.md`](docs/adr/0000-template.md), fill it in, add it to the ADR
register in [`docs/adr/index.md`](docs/adr/index.md), and open a PR for discussion. Implement
only after the ADR is **Accepted**.

## Code of Conduct

By participating you agree to the [Code of Conduct](CODE_OF_CONDUCT.md).
