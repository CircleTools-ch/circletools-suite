# Architecture Guidelines (DDD strategic + Aspire)

> **Status: outline.** Complements [arc42 ch. 4–5](../arc42/04-solution-strategy.md) and the
> [ADR log](../adr/index.md).

## 1. Principles
- Dependencies point inward; the domain is the center and depends on nothing external.
- Standards over invention; decisions are ADRs; boundaries are guarded by tests.

## 2. Strategic DDD
- Subdomains (core / supporting / generic); **bounded context per app-type**; a context map with
  relationship patterns (partnership, customer/supplier, anti-corruption layer, conformist).
- Ubiquitous language owned per context; names match the [glossary](../glossary/index.md).

## 3. Overall style
- **Modular monolith first** ([ADR-0003](../adr/0003-modular-monolith-with-aspire.md)); one module
  per bounded context; extractable to a service later.

## 4. Layering (Clean/Hexagonal)
- Domain → Application → Infrastructure → Presentation/Hosts.
- `Service.Core` defines repository **ports**; `Service.Repository` (Postgres) and `LocalStore`
  (SQLite) are **adapters**. Hosts are the only composition roots.

### Reference dependency rules
| Project | May reference | Must NOT reference |
|---------|---------------|--------------------|
| `SharedKernel` | (nothing) | I/O, EF, ASP.NET |
| `<App>.Service.Core` | SharedKernel, Application, Contracts | EF, HTTP, ASP.NET, MAUI |
| `<App>.Service.Repository` | Service.Core, Infrastructure, EF/Npgsql | ASP.NET host, UI |
| `<App>.Service` | Core, Repository, Contracts, Identity, ServiceDefaults | UI, MAUI |
| `<App>.Ui` (RCL) | Ui.Shared, Ui.Abstractions, Contracts, ApiClient | EF, Repository, MAUI APIs |
| `<App>.WebApp` | Ui, ApiClient, Identity, ServiceDefaults | Core/Repository (uses HTTP) |
| `<App>.App` (MAUI) | Ui, Contracts, LocalStore, Identity | server EF/Repository |
| `<App>.LocalStore` | Service.Core (ports), SQLite | ASP.NET, Postgres |

## 5. Solution & naming
- `Suite.<Context>.<Layer>`; folders mirror solution folders.

## 6. Cross-context communication
- In-process contracts / integration events; no shared database; anti-corruption at boundaries.

## 7. Data & persistence
- Schema/DB per context; EF Core conventions in `Infrastructure`; migrations owned by the context;
  no cross-context joins.

## 8. Aspire
- `AppHost` composition root; `ServiceDefaults` for OTel/health/resilience/discovery; resources
  (Postgres, cache) modeled in AppHost; configuration & secrets via Aspire parameters.

## 9. API design
- REST conventions, versioning, `ProblemDetails`, pagination; contracts in `<App>.Contracts`.

## 10. Cross-cutting
- Auth, localization, validation, caching, resilience — see
  [arc42 ch. 8](../arc42/08-crosscutting-concepts.md).

## 11. Non-functional & Swiss-first
- Data residency, privacy-by-design, GDPR/Swiss DPA, availability targets.

## 12. Governance
- Architecture tests (ArchUnitNET) as fitness functions; keep arc42 in sync; ADR discipline.

## 13. Evolution
- Criteria and steps for extracting a context into an independent service (future ADR).
