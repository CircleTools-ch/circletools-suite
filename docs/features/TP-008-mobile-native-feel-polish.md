---
id: TP-008
title: Test plan — Mobile native-feel polish pass
context: SecretSanta
status: Draft
related: [FEAT-008, ADR-0054, ADR-0040, ADR-0028, NFR-007]
---

# TP-008 — Test plan — Mobile native-feel polish pass

Covers [FEAT-008](FEAT-008-mobile-native-feel-polish.md) under
[ADR-0054](../adr/0054-mobile-native-feel-strategy.md). These are **UX-and-motion** changes, so the
weight of this plan is on **on-device verification**, not automated tests — per `CLAUDE.md`'s rule that a
green test run is not evidence a user-facing feature works. Several of the real defects this repo has
shipped (a partial reveal-link set, a badge whose text contradicted its condition) passed unit tests and
were only visible on screen; tap-target size, haptic firing, and pull-to-refresh feel are the same kind
of thing.

The load-bearing properties, one per workstream:

- **W1 — every interactive control clears a ~48 px tap target on a phone.** The failure mode (a control
  that is merely *hard* to tap) is invisible to a passing render test; it needs a real finger.
- **W2 — the shell shows exactly one menu entry on narrow, and never a one-item bottom bar.** Both are
  cheap to assert in a component test *and* must read right on device.
- **W3 — motion is present and correct: haptics fire on the three actions and nowhere else; the web head
  is a silent no-op; pull-to-refresh triggers a real sync and settles without jank.**
- **W4 — destructive confirmations and the primary create action use sheets/FAB and still behave
  correctly** (the confirm still deletes; Cancel still cancels; the FAB creates).

Because the feature is a set of four independent workstreams, each row below can be verified and signed
off on its own PR — a green suite is **not** sufficient sign-off for any of them.

## Planned automated coverage (bUnit / unit — cheap checks only)

Component/unit tests are worth writing only where they are cheap and catch a regression a later change
could reintroduce. They are the floor, not the sign-off.

| Workstream | Where (planned) | What it asserts |
|------------|-----------------|-----------------|
| W2 · one menu entry | `SecretSantaShellTests` (bUnit) | under the narrow breakpoint the app-bar hamburger is not rendered; on wide it is |
| W2 · no degenerate bottom bar | `SecretSantaShellTests` (bUnit) | signed out, the bottom bar does not render a single-tab state |
| W2 · FAB vs inline | `GroupsTests` / shell (bUnit) | the FAB renders on narrow, the top-of-page inline create button on wide (not both) |
| W3 · haptic seam invoked | `DrawTests`, `RevealTests`, `GroupsTests` (bUnit, fake `IHapticFeedback`) | running a draw, revealing an assignment, and pin/delete each call the seam **once**; unrelated taps do not |
| W3 · web head is a no-op | `Ui.Web` unit test | the browser `IHapticFeedback` implementation performs no interop and never throws |
| W4 · confirm still acts | `GroupsTests`, `MembersTests`, `DrawTests` (bUnit) | the sheet's confirm still deletes/redraws; Cancel leaves state unchanged (parity with the old inline-`MudAlert` behaviour) |
| Architecture | `ArchitectureTests` | `IHapticFeedback` lives in `Ui.Abstractions`; the MAUI/web impls sit in their heads; no `SecretSanta.Ui` dependency on MAUI (native behaviour stays behind the port — [ADR-0028](../adr/0028-browser-platform-adapters-block.md)) |

Naming above is indicative; the exact classes land with the build. The bar for "done" on the automated
side is that these assertions exist and are green **and** the full existing suite (Core, LocalStore, UI,
Main-service, Architecture) stays green with no regression.

## On-device verification (required by `CLAUDE.md` — the primary sign-off)

Deploy the MAUI Android head to the **Pixel** (USB deploy, see
[device testing](../deployment/device-testing.md)) and drive the whole organizer loop —
create → members → exclusions → draw → share → open a reveal link — in **DE / EN / FR**, watching the
WebView console (it must stay clean). Confirm each property under a real finger:

1. **W1 — tap targets.** Every icon button, kebab menu item, list-row action and the from-circle
   checkboxes are comfortably tappable (no mis-hits); nothing that was `Size.Small`/`Dense` is now a
   sub-target control on the phone.
2. **W2 — navigation.** On the phone there is a **single** way into the drawer (no redundant app-bar
   hamburger alongside the bottom bar's "More"); **signed out**, there is no one-item bottom bar (either
   real destinations or no bar, per the decision taken on device); on a wide window the hamburger + drawer
   return and the bottom bar is gone.
3. **W3 — haptics.** A light buzz fires on **running a draw**, on **revealing an assignment**, and on
   **pin/delete** — and on nothing else. Confirm the same build in a **browser** produces no error and no
   attempt to vibrate (web no-op).
4. **W3 — pull-to-refresh.** Pulling the Groups/event list triggers a sync (a signed-in change made on a
   second device appears), the spinner settles cleanly, and the gesture does **not** fight the WebView's
   native overscroll/scroll-to-top. **Signed out / nothing to refresh**, the gesture is inert and
   non-janky.
5. **W4 — sheets & FAB.** Delete group/member and redraw open a bottom/action sheet (not an inline banner
   that shoves the layout down); the sheet's confirm actually deletes/redraws and Cancel cancels. The
   primary **create group / add member** action is a FAB on the phone and still creates.
6. **Localization.** Every new or changed string reads correctly in **DE / EN / FR** (DE `du` / FR `vous`,
   [ADR-0032](../adr/0032-ui-tone-of-voice-and-gender-neutral-terms.md)); sheet buttons reuse existing
   `Delete` / `Cancel` / `ConfirmYes` copy where possible.

## Exit criteria

- [ ] Planned automated checks above exist and are green; the full existing suite stays green (no regression)
- [ ] **On-device (Pixel) pass completed in DE/EN/FR, console clean** — the primary sign-off
- [ ] Haptics fire on exactly the three actions; web head verified as a silent no-op
- [ ] Pull-to-refresh triggers a real sync and settles without jank; inert when there is nothing to refresh
- [ ] Architecture boundaries hold (native behaviour behind `IHapticFeedback`; no MAUI leak into `SecretSanta.Ui`)
- [ ] Traceability closed (FEAT-008 ↔ TP-008 ↔ ADR-0054)
