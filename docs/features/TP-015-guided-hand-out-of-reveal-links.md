---
id: TP-015
feature: FEAT-015
status: Reviewed
related: [UC-SS-022, FR-045, ADR-0028, NFR-001, NFR-002, NFR-003]
---

# TP-015 — Test plan for FEAT-015 (Guided hand-out of reveal links)

> **Executed 2026-09-15.** Automated: `HandoutProgressServiceTests` (mark/unmark, persists across a new
> instance on the same store, **re-draw resets**, per-link key stable+distinct) and Share-page assertions
> (button + progress present; a handed-out participant shows the *Handed out* chip) — part of **Ui 72/72**;
> LocalizationParity green. Browser-verified end-to-end: **"Handed out: 0 / 3"** → guided dialog
> (one-at-a-time Share/Copy/Mark + checklist) → **"1 / 3"** and the chip **persist across a full reload**;
> console clean. **Pending the Pixel:** the real OS share sheet under a finger. The one-link-per-payload rule
> is structural (the panel only ever passes a single `RevealLinkDto.Url`).

## Scope & objectives

Verify that [FEAT-015](FEAT-015-guided-hand-out-of-reveal-links.md) guides the Organizer through handing
each participant their **own** reveal link and remembers who is done — **without ever** putting more than
one link in a single share/copy payload. The weight is on three things a green suite could otherwise miss:
the **one-link-per-payload** privacy property, the **persisted progress** (survives reopen, keyed per
event, **reset on re-draw**), and the **degrade-to-Copy** where no share sheet is present. Everything else
is a thin coordinator over the existing per-person Share/Copy actions and the `IShareService` seam.

## Test levels

- **Unit** — the progress store (per-event key, draw-token invalidation, handed-out set) and the count
  logic (no I/O; fake key/value store).
- **UI/component** — bUnit over the hand-out panel with a fake `IShareService` and a fake `IKeyValueStore`.
- **Architecture** — no new seam; hand-out lives in `SecretSanta.Ui` over `IShareService`
  (`Ui.Abstractions`); existing layering tests apply.
- **End-to-end / platform** — Android share sheet and web Copy fallback where automatable; on-device Pixel
  pass for the real share sheet.
- **Exploratory** — a full hand-out over a 6+ person roster on the Pixel: skip, resume after reopen,
  re-draw reset.

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| NFR-002 / one link per payload | Every `IShareService.ShareAsync` (and clipboard) call carries exactly one `RevealLinkDto.Url`; no call concatenates links | Unit + UI | ✅ |
| FR-045 / action offered | Share page of a **drawn** event shows "Hand out links"; not shown when undrawn (no links) | UI (bUnit) | ✅ |
| FR-045 / initial count | Fresh pass shows "0 of N done · N left" and focuses the first person | UI (bUnit) | ✅ |
| FR-045 / advance on mark | Marking the focused person handed out updates the count and advances to the next outstanding | UI (bUnit) | ✅ |
| FR-045 / toggle back | Toggling a handed-out person returns them to outstanding and to the count | UI (bUnit) | ✅ |
| FR-045 / persistence | Progress written via `IKeyValueStore` survives a panel/app reopen (fake store returns prior state) | Unit + UI | ✅ |
| FR-045 / per-event isolation | Two events keep independent progress (distinct keys) | Unit | ✅ |
| FR-045 / re-draw resets | Changing the draw token clears the stored progress; the pass starts at "0 of N" | Unit | ✅ |
| FR-045 / degrade to Copy | With `IsSupportedAsync() == false`, each person is delivered by Copy and progress still tracked | UI (bUnit) | ✅ |
| NFR-001 / offline | The pass builds links and records progress with no API/account (component test, no network) | UI (bUnit) | ✅ |
| FR-045 / Android share | Hand-out fires the native share sheet with one person's link; mark advances | E2E (device) | ❌ manual (Pixel) |
| FR-045 / web Copy | Desktop browser: hand-out copies one link per person (no share sheet) | E2E (web) | ⚠️ where automatable |
| NFR-003 / parity | "Hand out links", the count (**singular + plural**), and handed-out/outstanding present in DE/EN/FR (+ neutral); parity test | Unit/Review | ✅ |

## Test data & environments

- Fixtures: a **drawn** event with a 5–6 person roster (to exercise advance/skip/resume and the count),
  an **undrawn** event (action absent), and a **re-drawn** event (progress reset). Include an archived
  member kept in the draw ([ADR-0052](../adr/0052-member-archival-and-permanent-draw-history.md)) to
  confirm the hand-out lists exactly the kept draw's links under the existing "roster changed" warning.
- Fakes: an in-memory `IKeyValueStore` (assert reads/writes and survival across a simulated reopen) and a
  recording `IShareService` (assert every payload has exactly one link; assert Copy path when
  unsupported).
- Environments: unit/bUnit in the standard test host; web E2E via the browser-driving host
  ([ADR-0045](../adr/0045-browser-driving-e2e-test-host.md)); Android on the Pixel per
  [device testing](../deployment/device-testing.md).

## Localization & accessibility notes

- DE/EN/FR (+ neutral) for the action, the **counted** progress string (needs a singular as well as a
  plural — the M2 lesson), and the per-person state.
- The hand-out controls have accessible names and ≥48 px touch targets (inherit the FEAT-008 mobile
  density baseline).

## Exit criteria

- [ ] All acceptance criteria covered by automated tests where a headless environment allows (progress
      store, count, degrade-to-Copy, one-link-per-payload, parity); the Android share path confirmed
      manually on the Pixel
- [ ] Coverage target met (see testing guidelines)
- [ ] CI green
- [ ] Verified in a real browser and on the Pixel per CLAUDE.md (not on a green suite alone)
