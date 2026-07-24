# 6. Runtime View

> _Proposed_ — key runtime scenarios. Detailed sequence diagrams are added per feature spec.

## 6.1 Offline use (no account)

```mermaid
sequenceDiagram
    actor U as User
    participant UI as App UI (RCL)
    participant Core as Service.Core (use case)
    participant Local as LocalStore (SQLite)
    U->>UI: Create Secret-Santa group
    UI->>Core: CreateGroup command
    Core->>Local: persist aggregate (local)
    Local-->>Core: ok (version token)
    Core-->>UI: result
    UI-->>U: group created (works offline)
```

## 6.2 Signing in and first sync ("claim" local data)

```mermaid
sequenceDiagram
    actor U as User
    participant UI as App UI
    participant Id as Identity
    participant Sync as Sync engine (LocalStore)
    participant API as Web API
    U->>UI: Create account / sign in
    UI->>Id: authenticate (email+pw / passkey)
    Id-->>UI: token
    UI->>Sync: start sync (adopt local data)
    Sync->>API: push local changes since cursor
    API-->>Sync: server changes since cursor
    Sync->>Sync: reconcile (per aggregate)
    Sync-->>UI: synced
```

## 6.3 Ongoing sync

Delta pull/push per aggregate using a version/cursor; conflict handling per the (to-be-decided)
sync ADR. The domain stays sync-ignorant; reconciliation lives in an anti-corruption boundary.
