---
status: Accepted
date: 2026-07-24
deciders: [Adrian]
tags: [documentation, diagrams]
---

# ADR-0004 — Pre-render PlantUML diagrams to SVG

## Context and problem statement

We use Mermaid for most inline diagrams, but want PlantUML / C4-PlantUML for rigorous C4
diagrams. DocFX renders Mermaid natively but has **no built-in PlantUML renderer**. How do we
include PlantUML diagrams reliably in the site?

## Decision drivers

- Deterministic, versionable output; no runtime dependency on a PlantUML server.
- Keep the docs build simple and offline-capable.
- Diagrams must render identically for every reader.

## Considered options

1. **Pre-render `.puml` → SVG** and embed the committed SVG
2. A DocFX markdown-preprocessing plugin that calls a PlantUML server at build time
3. Live client-side rendering against a public PlantUML server

## Decision outcome

**Chosen: pre-render.** PlantUML sources live in `docs/diagrams/c4/`; a build step (PlantUML CLI)
renders them to `docs/diagrams/exported/*.svg`, and Markdown embeds the SVG. The SVG is committed
so the site builds with no external service.

## Consequences

- **Positive:** Deterministic, reviewable diffs (source + output), offline builds, no plugin risk.
- **Negative:** A manual/CI render step must run when `.puml` sources change; the committed SVG
  can drift from source if the step is skipped (mitigate with a CI check later).
- **Follow-ups:** Mermaid remains the default for simple inline diagrams; document the render
  command in the documentation guidelines.

## Links

- Related: [ADR-0002](0002-use-arc42-c4-docfx.md)
- Guideline: [documentation-guidelines.md](../guidelines/documentation-guidelines.md)
- Example: [`docs/diagrams`](../diagrams/index.md)
