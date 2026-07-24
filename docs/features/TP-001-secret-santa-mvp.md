---
id: TP-001
feature: FEAT-001
status: Reviewed         # Draft | Reviewed | Approved
related: [UC-SS-001, UC-SS-002, UC-SS-003, UC-SS-004, UC-SS-005, UC-SS-006, FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, NFR-001, NFR-002, NFR-003]
---

# TP-001 — Test plan for FEAT-001 (Secret-Santa MVP)

## Scope & objectives

Verify the offline, local-first Secret-Santa MVP ([FEAT-001](FEAT-001-secret-santa-mvp.md)) against
its covered use cases, functional requirements, and NFRs: group configuration, members & wishlists,
exclusions, the fair draw, reveal-link generation/decoding, local persistence, the anti-spoiler
reveal UX, and DE/EN/FR string parity. This is the milestone-6a testing phase; the server/sync tier
(v2) is out of scope.

## Test levels

- **Unit** — domain aggregate & value objects, `DrawGenerator`, reveal-link codec/builder,
  application command/query handlers (no I/O; in-memory repository fake).
- **Integration (local persistence)** — `GroupRepository` + `FileKeyValueStore` round-trips
  (the MVP has no server DB; the browser `localStorage` adapter is covered indirectly — see notes).
- **Architecture** — `SecretSanta.Core`/`Contracts` free of EF Core, Npgsql, ASP.NET Core, JS interop,
  `LocalStore` and `Ui` (ArchUnitNET fitness functions).
- **UI/component (bUnit)** — the reveal page spoiler gate.
- **Localization** — key parity across `de-CH`, `en`, `fr-CH`.

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| FR-001 create/configure group | `ApplicationHandlerTests.CreateGroup_*`, `SetBudget_*`, `SetEventDetails_*`; `GroupTests` name invariants | Unit | ✅ |
| FR-002 members + wishlists | `ApplicationHandlerTests.AddMember_*`, `EditMember_*`, `RemoveMember_*`; `GroupTests` unique-name | Unit | ✅ |
| FR-003 exclusions | `ApplicationHandlerTests.AddExclusion_*`, `RemoveExclusion_*`; `GroupTests` non-member endpoint | Unit | ✅ |
| FR-004 fair draw (rules + graceful failure) | `DrawGeneratorTests` (exclusions, no-reciprocal, no-repeat, deterministic seed, 2-member & over-constrained failure) | Unit | ✅ |
| FR-005 generate/share reveal links | `HandlerTests.RunDraw_Then_GetRevealLinks_*`; `RevealLinkTests` builder shape/round-trip | Unit | ✅ |
| FR-005 URL-length warn threshold | `RevealLinkLengthTests` (modest within budget; oversized flagged) | Unit | ✅ |
| FR-006 reveal page decode + spoiler gate | `RevealPageTests.RevealPage_HidesRecipientUntilRevealButtonClicked`, `..._MalformedFragment_*` | Component (bUnit) | ✅ |
| NFR-001 local-first/offline persistence | `GroupRepositoryTests` (JSON round-trip, one key, update, delete), `FileKeyValueStoreTests` (disk persistence); `GroupTests` memento round-trip | Unit / Integration | ✅ |
| NFR-002 assignment privacy | `RevealLinkTests` (payload only in fragment, URL-safe blob); `RevealPageTests` (giftee hidden until reveal) | Unit / Component | ✅ |
| NFR-003 multilingual DE/EN/FR | `LocalizationParityTests.EverySupportedCulture_DefinesEveryNeutralKey` | Localization | ✅ |
| Layering (SecretSanta.Core boundary) | `SecretSantaLayeringTests` (Core/Contracts free of EF/Npgsql/ASP.NET/JS interop; Core free of LocalStore/Ui) | Architecture | ✅ |

## Test data & environments

- Deterministic randomness via `SeededRandomSource`; time via injected `TimeProvider` — no wall-clock
  or `Guid.NewGuid()` in production logic (test data may use them).
- Repository fakes: `InMemoryGroupRepository` (Core) and `InMemoryKeyValueStore` (LocalStore).
- `FileKeyValueStore` tests use a unique temp directory per test, removed on dispose.
- All tests run on `net10.0` via `dotnet test`; no external services (Postgres/Testcontainers not
  needed for the offline MVP).

## Localization & accessibility notes

- **Localization:** parity test fails if any `de-CH`/`en`/`fr-CH` key defined in the neutral
  `AppStrings` resources is missing or empty. Runtime culture is currently fixed to `de-CH`; the
  in-app language switcher is a separate MVP-polish item (not covered here).
- **Accessibility:** the reveal spoiler note and warnings use `role="alert"`; full a11y sweeps are
  deferred to the broader testing milestone (7).

## Coverage notes / known gaps (no silent truncation)

- `LocalStorageKeyValueStore` (browser `IJSRuntime` adapter) has no direct unit test — it is a thin
  pass-through exercised via the WASM host and manual smoke; a bUnit `JSInterop`-mock test may be
  added later.
- Full end-to-end (Web + native) journeys and host composition smoke tests are deferred to milestone 7.

## Exit criteria

- [x] All acceptance criteria covered by automated tests (matrix above)
- [x] Coverage: domain, application, reveal, local persistence, reveal-page component, and localization
      parity all exercised; architecture fitness functions green
- [x] CI green (`dotnet build` clean under analyzers; `dotnet test` all green)
