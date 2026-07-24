---
status: Accepted
date: 2026-07-24
deciders: [Adrian]
tags: [architecture, application, cqrs]
---

# ADR-0006 — Application layer & mediation

## Context and problem statement

The application layer uses a light **CQRS** style: use cases are commands and queries with handlers,
wrapped by cross-cutting **pipeline behaviors** (validation, logging, transactions/unit-of-work,
domain-event dispatch). MediatR was the usual choice, but as of v13 (July 2025) it is **commercially
licensed** (Lucky Penny Software; free "Community" only for orgs < $5M revenue, under the copyleft
RPL). How do we implement mediation for a product that may later be commercialized?

## Decision drivers

- Avoid licensing risk/copyleft for a potentially commercial product.
- Keep the application layer simple, testable, and dependency-light.
- Preserve a uniform pipeline for cross-cutting concerns.

## Considered options

1. **Hand-rolled minimal dispatcher** (in `AppSuite.Application`)
2. **`Mediator`** (martinothamar) — MIT, source-generated
3. **MediatR 13 Community** — RPL/commercial
4. **No mediator** — call application services directly

## Decision outcome

**Chosen: a hand-rolled minimal dispatcher** in `AppSuite.Application`:

- `ICommand`/`ICommand<TResult>`, `IQuery<TResult>` marker interfaces.
- `ICommandHandler<…>` / `IQueryHandler<…>` implementations.
- A small `ISender` that resolves the handler from DI and runs it through an ordered list of
  `IPipelineBehavior<TRequest,TResponse>` (validation → logging → transaction/UoW → domain-event dispatch).

This is ~100 lines, has **no external dependency or license entanglement**, and is trivial to test.

## Consequences

- **Positive:** Zero licensing concern; full control over the pipeline; easy to understand and debug;
  no source-gen/runtime magic.
- **Negative:** We maintain a small amount of infrastructure ourselves (acceptable; it rarely changes).
- **Follow-ups:** Define the dispatcher + behavior contracts in `AppSuite.Application`; document the pattern
  in the [coding guidelines](../guidelines/coding-guidelines.md).

## Pros and cons of the options

### 1. Hand-rolled
- 👍 No deps/license; minimal, explicit, testable.
- 👎 Small maintenance burden.

### 2. Mediator (source-gen, MIT)
- 👍 Familiar API, permissive license, fast.
- 👎 Source-gen dependency; more than we need.

### 3. MediatR 13 Community
- 👍 Ubiquitous, mature.
- 👎 RPL/commercial licensing risk for a commercial product.

### 4. No mediator
- 👍 Simplest surface.
- 👎 Loses uniform pipeline behaviors; cross-cutting duplicated per service.

## Links

- arc42: [04 Solution strategy](../arc42/04-solution-strategy.md), [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md)
- Guideline: [coding-guidelines.md](../guidelines/coding-guidelines.md)
