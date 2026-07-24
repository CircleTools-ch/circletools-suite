# Walf Documentation

Welcome to the documentation for **Walf** — an app suite for families and groups of
friends. Everything about how this product is conceived, decided, built, tested, and shipped
lives here as Markdown and is published as this site via DocFX.

## Start here

- **[How we work](process/ways-of-working.md)** — the development process, phase by phase.
- **[Architecture (arc42)](arc42/01-introduction-and-goals.md)** — the architecture documentation.
- **[Decisions (ADR log)](adr/index.md)** — why we chose what we chose.
- **[Guidelines](guidelines/coding-guidelines.md)** — coding, architecture, testing, localization.
- **[Glossary](glossary/index.md)** — the ubiquitous language of the domain.
- **[Roadmap & backlog](roadmap/roadmap.md)** — where we are and what's next.

## What is Walf?

A suite of small apps under one brand, one login, and shared configuration, with a global
dashboard over every app. The first app is **Secret-Santa**. Each app runs **offline with no
account** (local SQLite) and can **sync across devices** with an optional account. Native
Android/iOS (MAUI Blazor Hybrid) + Web (Blazor) share the same feature code.

Swiss-first audience; product languages **German, French, English**; code and documentation
are **English**.

> **Note:** Walf is a placeholder brand name. Choosing the final name and domain is the
> first tracked step — see [naming](process/naming.md).

## How this site is organized

| Section | Contents |
|---------|----------|
| **Process** | Ways of working, git conventions, definition of done, naming |
| **Architecture** | The 12 arc42 chapters |
| **Decisions** | Architecture Decision Records (ADRs) |
| **Product** | Roadmap, requirements, use cases, feature specs, brainstorming |
| **Guidelines** | Coding, architecture, testing, localization, documentation, security |
| **Glossary** | Canonical domain terms |
| **API Reference** | Generated from C# XML doc comments (appears once code exists) |
