---
id: TP-021
feature: FEAT-021
status: Draft
related: [FR-048, FR-038, ADR-0071]
---

# TP-021 — Test plan for FEAT-021

## Scope & objectives

Verify the device-local default storage mode, the one-time onboarding, the phone-FAB seeding, the
group-list storage indicator, and the Settings control — without regressing FR-038's per-group modes or
the local-first default (NFR-001).

## Test levels

- **Unit** — `StorageModePreferences` (default value, round-trip, onboarded flag, best-effort on store
  failure) and the "default = Account while signed out → On this device" clamp helper.
- **UI/component** (bUnit) — onboarding shows when not onboarded and not after; Settings control writes
  the default; the list renders the right icon per mode.
- **Exploratory / manual** — the full first-run flow and the phone FAB on web (narrow) and both device
  heads, in DE/EN/FR.

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| FR-048 (default starts local) | `GetDefaultAsync` returns `OnThisDevice` on a fresh store | Unit | ✅ |
| FR-048 (round-trip) | `SetDefaultAsync(x)` then `GetDefaultAsync` returns `x` for each mode | Unit | ✅ |
| FR-048 (onboarded flag) | `IsOnboardedAsync` false initially, true after `MarkOnboardedAsync` | Unit | ✅ |
| FR-048 (best-effort) | a throwing store ⇒ `GetDefaultAsync` = `OnThisDevice`, no exception | Unit | ✅ |
| FR-048 (signed-out clamp) | default `Account` + signed out ⇒ effective create mode `OnThisDevice` | Unit | ✅ |
| FR-048 (onboard once) | not onboarded ⇒ dialog shown; onboarded ⇒ not shown | UI/component | ✅ |
| FR-048 (Settings) | changing the Settings default persists and pre-selects next create | UI/component | ✅ |
| FR-048 (list icon) | a group per mode renders device/link/cloud icon + tooltip | UI/component | ✅ |
| FR-048 (phone FAB seeds) | default `SharedByLink` ⇒ FAB create produces a SharedByLink group | Exploratory | manual |
| FR-038 / NFR-001 | never changing the default ⇒ created group is `OnThisDevice`, nothing sent | Unit + Exploratory | ✅ / manual |

## Test data & environments

- Fake `IKeyValueStore` (in-memory + a throwing variant) for the unit tests.
- Component tests drive `StorageModePicker` / `OnboardingStorageDialog` with a stub preferences service
  and auth state (signed in / out).
- Manual: a fresh browser profile (`localStorage.clear()`) and a fresh install on iOS/Android.

## Localization & accessibility notes

- DE (informal "du") / EN / FR (formal "vous") strings present and fitting for the onboarding dialog,
  the Settings label/hint, and the icon tooltips.
- Icons carry an accessible label/tooltip (not colour-only); dialog is keyboard-dismissible and
  focus-trapped (MudDialog defaults).

## Exit criteria

- [ ] All FR-048 acceptance criteria covered by automated tests where feasible
- [ ] Coverage target met (see testing guidelines)
- [ ] CI green
- [ ] Manual first-run + phone-FAB + list-icon pass recorded on web and at least one device head
