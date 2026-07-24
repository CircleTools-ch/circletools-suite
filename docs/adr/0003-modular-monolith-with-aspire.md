---
status: Accepted
date: 2026-07-24
deciders: [Adrian]
tags: [architecture, aspire]
---

# ADR-0003 — Start as a modular monolith orchestrated by Aspire

## Context and problem statement

The suite will have several app-types (Main shell + Secret-Santa + more), each a bounded
context, plus shared building blocks. Should we start with microservices or a modular monolith,
and how do we run it locally and later in the cloud?

## Decision drivers

- Small team; speed and simplicity matter early.
- Clear bounded-context boundaries must be preserved for future extraction.
- Consistent local dev experience and observability.
- .NET 10 + Aspire are fixed by the product brief.

## Considered options

1. **Modular monolith** (one deployable per app-type API, clear module boundaries) orchestrated by **.NET Aspire**
2. Microservices from day one
3. Single undivided application

## Decision outcome

**Chosen: a modular monolith with strict bounded-context modules, orchestrated by .NET Aspire.**
Each app-type keeps its own `Service.Core` / `Service.Repository` / `Service` projects and can be
extracted into an independent service later without re-architecting the domain. Aspire's AppHost
is the local composition root; `ServiceDefaults` provides OpenTelemetry, health checks,
resilience, and service discovery.

## Consequences

- **Positive:** Low operational overhead now; boundaries enforced by architecture tests; smooth
  path to services later; excellent local observability via the Aspire dashboard.
- **Negative:** Requires discipline to keep modules from coupling; a single deployable is easier
  to accidentally entangle.
- **Follow-ups:** Architecture tests (ArchUnitNET) to guard boundaries; the extraction criteria
  will be its own ADR when needed.

## Links

- arc42: [04 Solution strategy](../arc42/04-solution-strategy.md), [05 Building-block view](../arc42/05-building-block-view.md)
- Guideline: [architecture-guidelines.md](../guidelines/architecture-guidelines.md)
