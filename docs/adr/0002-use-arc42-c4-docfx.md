---
status: Accepted
date: 2026-07-24
deciders: [Adrian]
tags: [documentation, architecture]
---

# ADR-0002 — Document with arc42 + C4 + DocFX

## Context and problem statement

We need a professional, recognizable documentation approach, and a way to publish everything
(architecture, decisions, guidelines, and the C# API reference) as one browsable site. Which
standards and tooling?

## Decision drivers

- Use established standards over invented structure.
- All documentation is Markdown in the repo (docs-as-code).
- One published site combining conceptual docs and the code API reference.
- Multilingual product, but **English-only** documentation.

## Considered options

- **Structure:** arc42 (+ C4 for diagrams) vs a custom outline.
- **Publishing:** DocFX vs MkDocs vs Docusaurus vs none.

## Decision outcome

**Chosen: arc42** for the architecture documentation, **C4** as the diagram model, **ADRs** for
decisions (ADR-0001), and **DocFX** to publish a single static site — DocFX because it is
.NET-native and can generate the **API reference from C# XML doc comments** alongside the
conceptual Markdown, which MkDocs/Docusaurus cannot do natively.

## Consequences

- **Positive:** Familiar structure; API + prose in one site; pure .NET toolchain (`dotnet docfx`).
- **Negative:** DocFX theming/plugins are less rich than Docusaurus; PlantUML needs handling
  (see [ADR-0004](0004-plantuml-prerender.md)).
- **Follow-ups:** DocFX project scaffolded in `docfx/`; arc42 chapters created; docs build gates
  on broken links/xrefs.

## Links

- Related: [ADR-0001](0001-record-architecture-decisions.md), [ADR-0004](0004-plantuml-prerender.md)
- Guideline: [documentation-guidelines.md](../guidelines/documentation-guidelines.md)
