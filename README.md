# CircleTools — app suite for families & friends

> **CircleTools** is the **final** brand, at **`circletools.ch`** (registered at Infomaniak,
> **2026-08-13**). It is also the solution / project / namespace prefix. See the naming history in
> [`docs/process/naming.md`](docs/process/naming.md).

A suite of small, delightful apps for **families and groups of friends** — one brand,
one login, shared configuration, and a global dashboard over every app. The first app is
a **Secret-Santa** app.

Every app works two ways:

- **Standalone & offline** — no account, no backend; all data stored locally in a JSON key-value
  store (web `localStorage`, MAUI file-per-key — not SQLite; see [ADR-0015](docs/adr/0015-mvp-client-offline-storage.md)).
- **Connected** — create an account (email + password, optional passkeys) to **sync across
  devices** via a Postgres-backed API.

**Targets:** native **Android & iOS** (phone + tablet, .NET MAUI Blazor Hybrid) and a
responsive **Web** app (Blazor) — sharing the same feature code.

**Audience:** Switzerland-first. **Product languages:** German, French, English (`de-CH`
default). **Code & documentation:** English only.

## Tech stack

- **.NET 10**, **.NET Aspire** (orchestration)
- **Domain-Driven Design** (strategic + tactical)
- **PostgreSQL** (backend) · **JSON key-value store** (on-device, per [ADR-0015](docs/adr/0015-mvp-client-offline-storage.md))
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

🟢 **M1–M5 built and closed; deployment (M8) is the only open milestone.** See the
[roadmap](docs/roadmap/roadmap.md) and [backlog](docs/roadmap/backlog.md).

- **Secret-Santa MVP — shipped** ([FEAT-001](docs/features/FEAT-001-secret-santa-mvp.md)). Create a
  group, add members and wishlists, define exclusions, run a fair draw, share self-contained reveal
  links. Fully offline, no account, no backend — a standalone Blazor WASM PWA plus a MAUI head.
- **M1 — Identity in the Main shell — built and closed**
  ([FEAT-002](docs/features/FEAT-002-m1-identity.md)). Sign-up with email verification, password and
  passkey sign-in, refresh rotation with reuse detection, password reset, profile and language,
  email change, data export, account deletion.
- **M2 — Circles — built and closed** ([FEAT-003](docs/features/FEAT-003-m2-circles.md)). A circle is
  the standing set of people apps reuse: create one with a label you choose, invite by email without
  the form revealing whether an address already has an account, accept or decline from the mail link
  or in the app, keep managed members for people who will never sign up, administer roles and
  ownership, leave, delete. Online-only for now — offline circles are M3's decision, not a patch.
  **537 tests green across 12 projects.**
- **M3 — Sync & claim, M4 — Linking, M5 — Cross-app SSO, and Events per group — all built and
  closed.** App↔backend reconciliation with the anonymous→account claim, member↔user linking with
  consent, the redirect-based PKCE single-sign-on hand-off across suite origins, and one-or-more
  events per group each with its own draw history — see the [roadmap](docs/roadmap/roadmap.md).
- **Milestone 7 — Testing & CI — complete.** GitHub Actions runs build/test, link-checked docs and
  the MAUI heads on every push/PR ([ADR-0038](docs/adr/0038-ci-cd-pipeline.md)), and the WebAuthn
  ceremony and invitation sign-up journey are automated over a real-Chromium E2E host.
- **Not yet deployed — but not blocked on undecided decisions.** Hosting
  ([ADR-0036](docs/adr/0036-cloud-provider-and-hosting.md)) and transactional email
  ([ADR-0037](docs/adr/0037-transactional-email-provider.md)) are both **Accepted (2026-08-13)**, and
  the brand and `circletools.ch` domain are final. What remains for **M8 — Deployment** is
  **operational provisioning** (VPS, secrets, DNS, backups) and the **legal pages** (privacy / imprint
  / account-deletion, DE/EN/FR) — execution, not decisions. See the
  [launch-readiness assessment](docs/deployment/launch-readiness.md).

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
dotnet build CircleTools.slnx

# Run the full local stack (Postgres, Mailpit, Main API, and both WASM clients —
# main-web and secretsanta-web) and open the Aspire dashboard
dotnet run --project src/Aspire/CircleTools.AppHost
```

## Where to find things

- **How we work:** [`docs/process/ways-of-working.md`](docs/process/ways-of-working.md)
- **Architecture (arc42):** [`docs/arc42/`](docs/arc42/)
- **Decisions (ADR log):** [`docs/adr/`](docs/adr/)
- **Guidelines:** [`docs/guidelines/`](docs/guidelines/)
- **Glossary (ubiquitous language):** [`docs/glossary/`](docs/glossary/)

## License

Open source under the **Apache License 2.0**. See [`LICENSE`](LICENSE) and [`NOTICE`](NOTICE)
(third-party components), and [ADR-0099](docs/adr/0099-apache-2-license.md) for why. Contributions are
accepted under the same license (see [`CONTRIBUTING.md`](CONTRIBUTING.md)). The CircleTools name and logos
are not covered by the license — see the [trademark policy](TRADEMARKS.md).
