---
status: Accepted
date: 2026-07-24
deciders: [Adrian]
tags: [architecture, api, versioning]
---

# ADR-0010 — API style & versioning

## Context and problem statement

The Web API is consumed by the WASM web client and the native apps, and must evolve without breaking
installed apps. What API style and versioning approach do we use?

## Decision drivers

- Multiple long-lived clients (installed mobile apps) → **backward compatibility** matters.
- Low ceremony; good fit with Minimal APIs and .NET 10.
- Consistent error model across the API.

## Considered options

1. **Minimal APIs + `Asp.Versioning` + `ProblemDetails`**, URL-segment versioning
2. MVC Controllers + `Asp.Versioning`
3. GraphQL

## Decision outcome

**Chosen: Minimal APIs** with the `Asp.Versioning` library, **URL-segment versioning** (`/api/v1/…`),
and the standard **`ProblemDetails`** error model. DTOs live in `CircleTools.<App>.Contracts` per bounded
context; endpoints are grouped per resource and map to application commands/queries.

## Consequences

- **Positive:** Lightweight, fast, idiomatic .NET 10; explicit versioning protects installed clients;
  uniform error contract; contracts decoupled from the domain.
- **Negative:** Minimal APIs need deliberate organization (endpoint grouping) to stay tidy at scale;
  versioning requires discipline (deprecation policy).
- **Follow-ups:** Define an endpoint-organization convention and a deprecation policy; consider OpenAPI
  document generation per version (built-in in .NET 10).

## Pros and cons of the options

### 1. Minimal APIs + Asp.Versioning
- 👍 Lean, fast, modern; explicit versioning; clean contracts.
- 👎 Needs structure discipline as it grows.

### 2. Controllers
- 👍 Familiar conventions, attribute routing.
- 👎 More ceremony than needed.

### 3. GraphQL
- 👍 Flexible client-driven queries.
- 👎 Overkill for this domain; heavier client/server; caching/offline complexity.

## Links

- arc42: [05 Building-block view](../arc42/05-building-block-view.md)
- Related: [ADR-0005](0005-web-hosting-render-model.md)
- Guideline: [architecture-guidelines.md](../guidelines/architecture-guidelines.md)
