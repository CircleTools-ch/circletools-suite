# CircleTools Documentation

Welcome to the documentation for **CircleTools** — an app suite for families and groups of
friends. Everything about how this product is conceived, decided, built, tested, and shipped
lives here as Markdown and is published as this site via DocFX.

## Start here

- **[How we work](process/ways-of-working.md)** — the development process, phase by phase.
- **[Architecture (arc42)](arc42/01-introduction-and-goals.md)** — the architecture documentation.
- **[Decisions (ADR log)](adr/index.md)** — why we chose what we chose.
- **[Guidelines](guidelines/coding-guidelines.md)** — coding, architecture, testing, localization.
- **[Glossary](glossary/index.md)** — the ubiquitous language of the domain.
- **[Roadmap & backlog](roadmap/roadmap.md)** — where we are and what's next.

## What is CircleTools?

A suite of small apps under one brand, one login, and shared configuration, with a global
dashboard over every app. The first app is **Secret-Santa**. Each app runs **offline with no
account** (a local JSON key-value store, [ADR-0015](adr/0015-mvp-client-offline-storage.md)) and can
**sync across devices** with an optional account. Native Android/iOS (MAUI Blazor Hybrid) + Web
(Blazor) share the same feature code.

Swiss-first audience; product languages **German, French, English**; code and documentation
are **English**.

The service is live at [circletools.ch](https://circletools.ch) and
[secretsanta.circletools.ch](https://secretsanta.circletools.ch). The source code is open under
Apache-2.0 at [github.com/CircleTools-ch/circletools-suite](https://github.com/CircleTools-ch/circletools-suite)
([ADR-0093](adr/0093-publish-the-repository.md)). The brand and domain were chosen on 2026-08-13 —
see [naming](process/naming.md).

## How this site is organized

The top bar has one entry per section; the section's pages are listed in the sidebar on the left,
which has a filter box (useful for the 100-odd ADRs).

| Section | Contents |
|---------|----------|
| **Process** | Ways of working, git conventions, definition of done, naming, brand assets |
| **Architecture** | The 12 arc42 chapters |
| **Decisions** | Architecture Decision Records (ADRs) |
| **Product** | Roadmap & backlog, requirements, use cases, feature specs, brainstorming |
| **Guidelines** | Coding, architecture, testing, localization, documentation, security |
| **Glossary** | Canonical domain terms |
| **Deployment** | Hosting, server setup, backups, the docs site |
| **API** | Generated from the C# XML doc comments in `src/` |
