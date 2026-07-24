---
id: TP-000
feature: FEAT-000
status: Draft         # Draft | Reviewed | Approved
related: []           # UC-…, FR-…, NFR-…
---

# {{id}} — Test plan for {{feature}}

## Scope & objectives

What is being verified and why.

## Test levels

- **Unit** — domain logic, value objects, use-case handlers (no I/O).
- **Integration** — repositories & API against real Postgres/SQLite via Aspire/Testcontainers.
- **Architecture** — dependency-direction / boundary rules (ArchUnitNET).
- **Contract** — API DTOs and client compatibility.
- **UI/component** — Blazor components (bUnit).
- **End-to-end** — critical user journeys across Web/native.
- **Exploratory** — <areas for manual exploration>

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| FR-…/AC-1 | <case> | Unit | ✅ |

## Test data & environments

- <fixtures, seed data, environment notes>

## Localization & accessibility notes

- <DE/EN/FR checks, a11y checks>

## Exit criteria

- [ ] All acceptance criteria covered by automated tests
- [ ] Coverage target met (see testing guidelines)
- [ ] CI green
