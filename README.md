# Walf — app suite for families & friends

> **Walf** — *We Are Like Family*. The suite brand (chosen 2026-07-24); domain `walf.ch`.
> See the decision and the naming history in [`docs/process/naming.md`](docs/process/naming.md).

A suite of small, delightful apps for **families and groups of friends** — one brand,
one login, shared configuration, and a global dashboard over every app. The first app is
a **Secret-Santa** app.

Every app works two ways:

- **Standalone & offline** — no account, no backend; all data stored locally (SQLite).
- **Connected** — create an account (email + password, optional passkeys) to **sync across
  devices** via a Postgres-backed API.

**Targets:** native **Android & iOS** (phone + tablet, .NET MAUI Blazor Hybrid) and a
responsive **Web** app (Blazor) — sharing the same feature code.

**Audience:** Switzerland-first. **Product languages:** German, French, English (`de-CH`
default). **Code & documentation:** English only.

## Tech stack

- **.NET 10**, **.NET Aspire** (orchestration)
- **Domain-Driven Design** (strategic + tactical)
- **PostgreSQL** (backend) · **SQLite** (on-device)
- **Blazor** (Web) + **MAUI Blazor Hybrid** (native), shared via Razor Class Libraries
- **DocFX** for the documentation site

## Repository map

| Path | What |
|------|------|
| [`docs/`](docs/) | **All** documentation (Markdown): arc42, ADRs, requirements, use cases, guidelines, process |
| [`docfx/`](docfx/) | The **Documentation** project — builds the static docs site |
| `src/` | Production code (added in later phases — empty for now) |
| `tests/` | Test projects (added in later phases — empty for now) |
| [`CLAUDE.md`](CLAUDE.md) | How AI assistants (and new humans) work in this repo |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Dev setup, workflow, conventions |

## Status

🟢 **Harness phase.** The repository, documentation framework, and ways-of-working are being
established *before* feature code. The next milestones are naming, brainstorming, requirements,
and the first architecture decisions. See the [roadmap](docs/roadmap/roadmap.md).

## Quickstart

Prerequisites: **.NET 10 SDK** (pinned via [`global.json`](global.json); MAUI workloads for
mobile work later).

```bash
# Restore local tools (DocFX)
dotnet tool restore

# Build & serve the documentation site locally (opens on http://localhost:8080)
dotnet docfx docfx/docfx.json --serve

# Build the solution (near-empty during the harness phase)
dotnet build Walf.slnx
```

## Where to find things

- **How we work:** [`docs/process/ways-of-working.md`](docs/process/ways-of-working.md)
- **Architecture (arc42):** [`docs/arc42/`](docs/arc42/)
- **Decisions (ADR log):** [`docs/adr/`](docs/adr/)
- **Guidelines:** [`docs/guidelines/`](docs/guidelines/)
- **Glossary (ubiquitous language):** [`docs/glossary/`](docs/glossary/)

## License

Proprietary — see [`LICENSE`](LICENSE). Private, pre-release project.
