# 5. Building Block View

> The technology choices are **ratified** ([ADR-0005–0015](09-architecture-decisions.md)). The
> building blocks (`SharedKernel`, `Application`) and the **Main** app-type projects **exist**
> (walking skeleton, milestone 5); a throwaway `Heartbeat` sample exercises the full pipeline
> (domain → dispatcher → repository → EF Core → Postgres → Minimal API → WASM). The **SecretSanta**
> app-type is being implemented in milestone 6a as the **offline MVP — no server tier**
> (`Contracts`/`Core`/`LocalStore`/`Ui`/`WebApp` + a MAUI head, per
> [FEAT-001](../features/FEAT-001-secret-santa-mvp.md)); the dispatcher + domain run **client-side**
> with local storage ([ADR-0015](../adr/0015-mvp-client-offline-storage.md)). `Ui.Shared`,
> `Identity`, `Localization`, and the server/sync tier follow the same template when needed.

## 5.1 Level 1 — solution structure

Single solution; physical folders mirror solution folders. Projects are prefixed `AppSuite.`
(placeholder), app-specific ones `AppSuite.<AppType>.`.

```mermaid
flowchart TD
    subgraph Aspire
        AppHost[AppSuite.AppHost]
        Defaults[AppSuite.ServiceDefaults]
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
| `AppSuite.AppHost` | Aspire orchestrator; local F5 entrypoint; wires DB, services, web hosts. |
| `AppSuite.ServiceDefaults` | OpenTelemetry, health checks, resilience, service discovery. |
| `AppSuite.SharedKernel` | DDD tactical base types (entity, value object, aggregate root, domain event). No I/O deps. |
| `AppSuite.Application` | CQRS abstractions, pipeline behaviors, `Result`, unit-of-work. |
| `AppSuite.Infrastructure` | Shared EF conventions, outbox, clock, ID generation. |
| `AppSuite.Identity` | Identity model, JWT, WebAuthn/passkeys. |
| `AppSuite.Localization` | DE/EN/FR resources & culture services. |
| `AppSuite.ApiClient` | Shared typed HttpClient + auth handler. |
| `AppSuite.Ui.Shared` | RCL: layout, theming, branding, shared components. |
| `AppSuite.Ui.Abstractions` | Platform-service interfaces (`IDataStore`, `IConnectivity`, `ISecureStorage`, `ICultureProvider`, …). |
| `AppSuite.<App>.Contracts` | API DTOs for the app-type (bounded-context boundary). |
| `AppSuite.<App>.Service.Core` | Domain + application layer; defines repository **ports**. |
| `AppSuite.<App>.Service.Repository` | EF Core + Postgres adapter. |
| `AppSuite.<App>.Service` | API host / composition root. |
| `AppSuite.<App>.Ui` | RCL: the app's pages & components. |
| `AppSuite.<App>.WebApp` | Standalone **Blazor WebAssembly PWA** host ([ADR-0005](../adr/0005-web-hosting-render-model.md)). |
| `AppSuite.<App>.App` | MAUI Blazor Hybrid host (Android/iOS). |
| `AppSuite.<App>.LocalStore` | SQLite adapter + client sync engine. |
| `AppSuite.<App>.*.Test` | Unit/integration/architecture tests co-located with the subject. |

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
