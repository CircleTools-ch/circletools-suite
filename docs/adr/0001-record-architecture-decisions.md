---
status: Accepted
date: 2026-07-24
deciders: [Adrian]
tags: [process, documentation]
---

# ADR-0001 — Record architecture decisions

## Context and problem statement

This project is run as a professional product, not ad-hoc. Significant technical and
architectural choices need to be captured with their context and consequences so that future
contributors (human or AI) understand *why*, not just *what*. How should we record decisions?

## Decision drivers

- Docs-as-code: decisions must live in the repo, versioned with the code.
- Low ceremony, high signal — easy to write, easy to read.
- Traceability to requirements, use cases, and arc42.

## Considered options

1. **Lightweight ADRs in Markdown (MADR-style)**
2. A wiki / external tool
3. No formal record (rely on commit messages & memory)

## Decision outcome

**Chosen: Lightweight ADRs in Markdown (MADR-style)**, stored in `docs/adr/`, one file per
decision, numbered sequentially, with a status lifecycle (Proposed → Accepted → Deprecated /
Superseded). This is the classic Nygard/MADR practice.

## Consequences

- **Positive:** Durable, reviewable rationale; decisions diff and travel with the code; new
  contributors can read the history.
- **Negative:** Small discipline cost to write one per significant decision.
- **Follow-ups:** ADR template and register created; arc42 ch. 9 links to this log.

## Links

- arc42 chapter: [09 Architecture Decisions](../arc42/09-architecture-decisions.md)
- Template: [`_template-adr.md`](_template-adr.md)
