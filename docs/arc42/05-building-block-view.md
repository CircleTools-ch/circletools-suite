# 5. Building Block View

> The technology choices are now **ratified** ([ADR-0005–0012](09-architecture-decisions.md)); the
> **projects below are not created yet** — they are scaffolded in the walking-skeleton milestone.

## 5.1 Level 1 — solution structure

Single solution; physical folders mirror solution folders. Projects are prefixed `Suite.`
(placeholder), app-specific ones `Suite.<AppType>.`.

```mermaid
flowchart TD
    subgraph Aspire
        AppHost[Suite.AppHost]
        Defaults[Suite.ServiceDefaults]
    end
    subgraph BuildingBlocks
        SK[SharedKernel]
        App[Application]
        Infra[Infrastructure]
        Id[Identity]
        Loc[Localization]
        ApiC[ApiClient]
    end
    subgraph UI
        UiShared[Ui.Shared - RCL]
        UiAbs[Ui.Abstractions]
    end
    subgraph AppType["Per app-type (Main, SecretSanta, ...)"]
        Contracts
        Core[Service.Core - domain+application]
        Repo[Service.Repository - Postgres]
        Svc[Service - API host]
        AppUi[App.Ui - RCL]
        WebApp[WebApp - Blazor host]
        Native[App - MAUI host]
        Local[LocalStore - SQLite + sync]
    end

    Svc --> Core
    Repo --> Core
    Local --> Core
    Core --> SK
    App --> SK
    WebApp --> AppUi --> UiShared
    Native --> AppUi
    Native --> Local
    WebApp --> ApiC
    Svc --> Defaults
```

## 5.2 Projects & responsibilities

| Project | Responsibility |
|---------|----------------|
| `Suite.AppHost` | Aspire orchestrator; local F5 entrypoint; wires DB, services, web hosts. |
| `Suite.ServiceDefaults` | OpenTelemetry, health checks, resilience, service discovery. |
| `Suite.SharedKernel` | DDD tactical base types (entity, value object, aggregate root, domain event). No I/O deps. |
| `Suite.Application` | CQRS abstractions, pipeline behaviors, `Result`, unit-of-work. |
| `Suite.Infrastructure` | Shared EF conventions, outbox, clock, ID generation. |
| `Suite.Identity` | Identity model, JWT, WebAuthn/passkeys. |
| `Suite.Localization` | DE/EN/FR resources & culture services. |
| `Suite.ApiClient` | Shared typed HttpClient + auth handler. |
| `Suite.Ui.Shared` | RCL: layout, theming, branding, shared components. |
| `Suite.Ui.Abstractions` | Platform-service interfaces (`IDataStore`, `IConnectivity`, `ISecureStorage`, `ICultureProvider`, …). |
| `Suite.<App>.Contracts` | API DTOs for the app-type (bounded-context boundary). |
| `Suite.<App>.Service.Core` | Domain + application layer; defines repository **ports**. |
| `Suite.<App>.Service.Repository` | EF Core + Postgres adapter. |
| `Suite.<App>.Service` | API host / composition root. |
| `Suite.<App>.Ui` | RCL: the app's pages & components. |
| `Suite.<App>.WebApp` | Standalone **Blazor WebAssembly PWA** host ([ADR-0005](../adr/0005-web-hosting-render-model.md)). |
| `Suite.<App>.App` | MAUI Blazor Hybrid host (Android/iOS). |
| `Suite.<App>.LocalStore` | SQLite adapter + client sync engine. |
| `Suite.<App>.*.Test` | Unit/integration/architecture tests co-located with the subject. |

**App-types now:** `Main` (shell: branding, login, dashboard, shared config) and `SecretSanta`.
More follow the same template.

> **Web offline-storage caveat:** on native, `LocalStore` uses on-device SQLite. On the standalone
> WASM web client, `LocalStore` cannot use device SQLite directly; its offline persistence
> (WASM SQLite/OPFS vs IndexedDB) is decided together with the sync strategy (deferred — see the
> [ADR register](../adr/index.md)).

## 5.3 Dependency rule

Dependencies point **inward**; the domain (`Service.Core` / `SharedKernel`) has **no** dependency
on EF, ASP.NET, HTTP, or MAUI. Enforced by an **architecture test** (ArchUnitNET). Full reference
rules are in the [architecture guidelines](../guidelines/architecture-guidelines.md).
