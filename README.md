# AppSuite — app suite for families & friends

> **AppSuite** is a neutral **working name** — the brand is not finalized (branding may change).
> A leading candidate is **Walf** (*We Are Like Family*; `walf.ch` available). See the naming
> history and candidates in [`docs/process/naming.md`](docs/process/naming.md).

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
| [`src/`](src/) | Production code — Aspire hosts, shared building blocks, and the apps |
| [`tests/`](tests/) | Test projects — unit, integration, component, architecture |
| [`CLAUDE.md`](CLAUDE.md) | How AI assistants (and new humans) work in this repo |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Dev setup, workflow, conventions |

## Status

🟢 **Two slices built; M2 next.** See the [roadmap](docs/roadmap/roadmap.md) and
[backlog](docs/roadmap/backlog.md).

- **Secret-Santa MVP — shipped** ([FEAT-001](docs/features/FEAT-001-secret-santa-mvp.md)). Create a
  group, add members and wishlists, define exclusions, run a fair draw, share self-contained reveal
  links. Fully offline, no account, no backend — a standalone Blazor WASM PWA plus a MAUI head.
- **M1 — Identity in the Main shell — built and closed**
  ([FEAT-002](docs/features/FEAT-002-m1-identity.md)). Sign-up with email verification, password and
  passkey sign-in, refresh rotation with reuse detection, password reset, profile and language,
  email change, data export, account deletion. 281 tests green.
- **Not yet shippable.** M1 runs locally against real Postgres and a real SMTP sink, but production
  needs two decisions that have not been made: a **transactional-email provider** and the
  **hosting / suite domain** (which fixes the passkey RP-ID, the CSP origin, and the data-residency
  target). The brand and domain are still deliberately open — see
  [naming](docs/process/naming.md).
- **Next:** **M2 — Circles**, the shared set of people reused across apps and years, starting with
  the `UC-MAIN-008+` use cases.

## Quickstart

Prerequisites: **.NET 10 SDK** (pinned via [`global.json`](global.json)); **MAUI workloads** for the
native Secret-Santa head; **Docker** for the Aspire stack (Postgres + Mailpit) and the integration
tests, which use Testcontainers.

```bash
# Restore local tools (DocFX)
dotnet tool restore

# Build & serve the documentation site locally (opens on http://localhost:8080)
dotnet docfx docfx/docfx.json --serve

# Build the solution
dotnet build AppSuite.slnx
```

## Where to find things

- **How we work:** [`docs/process/ways-of-working.md`](docs/process/ways-of-working.md)
- **Architecture (arc42):** [`docs/arc42/`](docs/arc42/)
- **Decisions (ADR log):** [`docs/adr/`](docs/adr/)
- **Guidelines:** [`docs/guidelines/`](docs/guidelines/)
- **Glossary (ubiquitous language):** [`docs/glossary/`](docs/glossary/)

## License

Proprietary — see [`LICENSE`](LICENSE). Private, pre-release project.
