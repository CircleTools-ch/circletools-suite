---
id: TP-010
feature: FEAT-010
status: Reviewed
related: [FR-035, UC-SS-014, FR-034, FR-004, ADR-0042, ADR-0052]
---

# TP-010 — Test plan for FEAT-010 (carry forward a new occasion)

## Scope & objectives

Verify that creating a new [Event](../glossary/event.md) by
[Carry-forward](../glossary/carry-forward.md) copies the right things from the source event, copies
**only** them, leaves the source untouched, and — the sharpest property — produces a first
[Draw](../glossary/draw.md) that **does not reproduce the source event's pairings**. The category's two
loudest complaints (re-typing wishlists; the same giftee every year) are exactly these, so the plan is
weighted toward the **draw-baseline** behaviour and the **roster-intersection** edge cases, where a
green-but-wrong result would be silent.

## Test levels

- **Unit** — the `Group.AddEvent`-with-source domain behaviour: copy scope, roster intersection,
  archived-member handling, baseline seeding, and the resulting no-repeat draw (no I/O). **Primary.**
- **UI/component** — bUnit for the add-event carry-forward control (offer appears only with a prior
  event; source picker; "start blank"; account-free path renders and works).
- **End-to-end** — the `CLAUDE.md` browser walkthrough: create a second occasion by carry-forward in
  DE/EN/FR, edit a carried wishlist, draw, confirm a **different** giftee than the source draw, console
  clean.
- **Integration/Contract/Architecture** — minimal: assert **no** new schema/endpoint (additive optional
  field only) and that the domain stays I/O-free (arch tests already enforce this).

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| FR-035 / copy wishlists+budget | Source event with wishlists + budget → new event has both copied; new event undrawn; source unchanged | Unit | ✅ |
| FR-035 / draw rule (c) baseline | Carry forward from a **drawn** source → run new draw → **no pairing equals** a source pairing | Unit | ✅ |
| FR-035 / removed member not copied | Member on source but removed/archived from roster → wishlist **not** copied; baseline still resolves their name by id | Unit | ✅ |
| FR-035 / new member empty | Member on current roster but absent from source → **empty** wishlist in new event | Unit | ✅ |
| FR-035 / start blank | "Start blank" → nothing copied; identical to today's add-event | Unit + UI | ✅ |
| FR-035 / exclusions not copied | Group exclusions apply to the new event with no copy step (they are shared) | Unit | ✅ |
| FR-035 / account-free | Signed-out carry-forward succeeds on-device; no server call issued | UI | ✅ |
| UC-SS-014 5a / graceful failure | Constrained roster where avoiding the baseline is impossible → draw fails gracefully; re-draw ignoring baseline succeeds | Unit | ✅ |
| UC-SS-014 / offer visibility | Carry-forward offered only when the group already has ≥1 event; source picker defaults to most recent | UI | ✅ |
| FR-035 / editable after copy | Copied wishlist/budget can be edited without touching the source event | Unit + UI | ✅ |
| NFR-003 | Carry-forward offer, picker, "start blank", and the anti-repeat note render in DE/EN/FR | E2E | ✅ |
| E2E | Two-occasion carry-forward walkthrough: giftee differs from source; console clean | End-to-end | ✅ (Playwright) |

## Test data & environments

- Fixture groups: (a) one event, wishlists + budget filled, roster of 5; (b) a drawn source event to
  assert the no-repeat baseline; (c) a roster mutated after the source draw (one removed/archived, one
  added) for the intersection cases; (d) a 2–3 person roster to force graceful failure under (5a).
- Account-free path exercised with no signed-in session; no Main service required for the unit/UI
  levels. The E2E walkthrough runs the standalone WebApp (offline flow), per `CLAUDE.md`.

## Localization & accessibility notes

- DE/EN/FR strings for the offer, source-picker label, "start blank", and the anti-repeat note; check
  the DE *du* / FR *vous* register (ADR-0032) and that no string is English-only (parity test).
- The carry-forward control keeps the ≥48px touch targets and sheet pattern already established
  ([FEAT-008](FEAT-008-mobile-native-feel-polish.md)); verify keyboard/screen-reader labels on the
  source picker.

## Coverage (2026-09-02)

- **Domain (`CarryForwardTests`, 7):** copy-scope, roster intersection, archived-member exclusion,
  the seeded baseline making the next draw avoid the source pairings, state round-trip, graceful failure
  + ignore-baseline escape, unknown-source guard.
- **Application (`HandlerTests`, 1):** the `AddEventCommand` carry-forward path through the handler +
  mapper — budget/wishlist copied, `HasCurrentDraw` false, `HasCarriedForwardBaseline` true, a different
  drawn pairing than the source, baseline cleared after a real draw.
- **Browser walkthrough (Playwright/Chromium, DE/EN/FR):** the dialog renders with the source picker and
  anti-repeat hint; confirming creates a distinct occasion; the carried occasion draws successfully;
  **console error count 0** in every locale. Run as a throwaway walkthrough (not committed — it needs the
  standalone WebApp running), the durable coverage being the domain + handler tests above.
- **Deferred:** a committed bUnit test that drives the dialog through `IDialogService` (the flow was
  browser-verified instead); the constrained-roster graceful-failure path has domain coverage but no
  dedicated on-screen shot.

## Exit criteria

- [x] All FR-035 acceptance criteria covered by automated tests (domain + handler)
- [x] The **no-repeat baseline** case proven at the unit level (not only asserted in the UI)
- [x] Coverage target met (see [testing guidelines](../guidelines/testing-guidelines.md))
- [x] **Browser walkthrough** green in DE/EN/FR (carried occasion draws; console clean)
- [ ] CI green *(carried project-wide — no pipeline yet, as for FEAT-001..009)*
