---
id: TP-011
feature: FEAT-011
status: Reviewed
related: [FR-036, UC-SS-015, FR-034, ADR-0014, ADR-0042, ADR-0056]
---

# TP-011 — Test plan for FEAT-011 (budget minimum as well as maximum)

## Scope & objectives

Verify that an [Event](../glossary/event.md)'s [Budget](../glossary/budget.md) can carry an optional
**minimum** alongside the required maximum, that the `0 ≤ min ≤ max` invariant holds, that the budget
displays as a **range** when a minimum is set and as a bare maximum otherwise, and — the property most
easily broken silently — that an **unset minimum is omitted from the encoded reveal link**, so max-only
and pre-existing links are unchanged. The plan is weighted toward the **invariant** and the
**link-compatibility** behaviour, where a green-but-wrong result would not show on screen.

## Test levels

- **Unit** — the `Budget` value object invariant (non-negative min, `min ≤ max`, `min = max` allowed,
  equality distinguishes the minimum), and the reveal codec omitting `bm` when null. **Primary.**
- **Application** — the `SetBudgetCommand` handler: a min round-trips through persistence and the mapper;
  a min greater than the max is rejected.
- **UI/component** — bUnit on the reveal page: a min+max renders "10–30 CHF"; a max-only renders
  "30 CHF" with no range dash.
- **End-to-end** — the `CLAUDE.md` browser walkthrough in DE/EN/FR: set a 10–30 budget, see the range on
  every reveal link; a max-only budget shows no range; the min-exceeds-max save is rejected inline;
  console clean.
- **Integration/Contract/Architecture** — minimal: assert **no** new schema/endpoint (additive optional
  field) and that the domain stays I/O-free (arch tests already enforce this).

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| FR-036 / min stored | Set max 30 + min 10 → budget reads 10–30; both persist and round-trip | Unit + App | ✅ |
| FR-036 / invariant min ≤ max | `Budget.Create(20, min:30)` throws; handler rejects; UI shows inline error | Unit + App | ✅ |
| FR-036 / non-negative min | `Budget.Create(30, min:-1)` throws | Unit | ✅ |
| FR-036 / min = max allowed | `Budget.Create(20, min:20)` is valid | Unit | ✅ |
| FR-036 / equality | Budgets differing only in the minimum are unequal; identical ones equal | Unit | ✅ |
| FR-036 / bare maximum | No minimum → budget is a bare maximum; behaviour unchanged | Unit + UI | ✅ |
| FR-036 / zero-or-blank min | UI treats a zero/blank minimum as "no minimum", not an error | UI/E2E | ✅ (E2E) |
| FR-036 / clear budget | Clearing the maximum removes the whole budget, minimum included | App | ✅ |
| FR-036 / link omits unset min | A max-only payload encodes with **no** `bm` key; decodes to null; size unchanged | Unit | ✅ |
| FR-036 / range display | Reveal + my-assignments show "10–30 CHF" with a min, "30 CHF" without | UI | ✅ |
| FR-036 / back-compat load | A group snapshot saved before `MinAmount` loads as a bare maximum | Unit (LocalStore) | ➖ (covered by additive-field default) |
| NFR-003 | Minimum/Maximum labels, min-exceeds-max error, and the range render in DE/EN/FR | E2E | ✅ |
| E2E | 10–30 budget walkthrough: range on the reveal link; console clean | End-to-end | ✅ (Playwright) |

## Test data & environments

- Fixture events: (a) max-only budget (the prior behaviour); (b) min+max budget for the range and
  round-trip; (c) an invalid min > max for the rejection path.
- Account-free path exercised with no signed-in session; no Main service required for the unit/UI levels.
  The E2E walkthrough runs the standalone WebApp (offline flow), per `CLAUDE.md`.

## Localization & accessibility notes

- DE/EN/FR strings for **Minimum** / **Maximum** and the min-exceeds-max error; the range separator is a
  culture-neutral en-dash rendered in code (no localized format string). Amounts format for the active
  region ([ADR-0056](../adr/0056-decouple-ui-language-from-formatting-region.md)); the parity test guards
  against an English-only string.
- The Minimum field inherits the event editor's dense layout and touch targets; verify the numeric field
  has an accessible label and that the inline error is announced (`role="alert"`, as the existing budget
  error already is).

## Coverage (2026-09-03)

- **Domain (`BudgetTests`, 6):** max-only, min+max, min = max, min > max rejected, negative min rejected,
  equality distinguishes the minimum.
- **Application (`ApplicationHandlerTests`, 2):** `SetBudget` round-trips the minimum through persistence
  and the mapper (and still clears the whole budget); a min greater than the max is rejected.
- **Reveal codec (`RevealLinkTests`, +1 and sample extended):** the min round-trips through the codec and
  builder; a max-only payload encodes with no `bm` key and decodes to null.
- **UI (`RevealPageTests`, 2):** a min+max reveal renders "10–30 CHF"; a max-only reveal renders "30 CHF"
  with no range dash.
- **Browser walkthrough (Playwright/Chromium, DE/EN/FR):** set a 10–30 budget → each reveal link shows
  the range; a max-only budget shows the bare maximum; the min-exceeds-max save shows the localized inline
  error; **console error count 0** in every locale. Run as a throwaway walkthrough (not committed — needs
  the standalone WebApp running), the durable coverage being the tests above.

## Exit criteria

- [x] All FR-036 acceptance criteria covered by automated tests (domain + handler + codec + UI)
- [x] The **link-compatibility** property proven at the unit level (unset min omitted from the link)
- [x] Coverage target met (see [testing guidelines](../guidelines/testing-guidelines.md))
- [x] **Browser walkthrough** green in DE/EN/FR (range shown; max-only unchanged; min-exceeds-max rejected)
- [ ] CI green *(carried project-wide — no pipeline yet, as for FEAT-001..010)*
