---
id: TP-012
feature: FEAT-012
status: Reviewed
related: [FR-037, UC-SS-016, FR-036, FR-029, ADR-0014, ADR-0020, ADR-0042]
---

# TP-012 — Test plan for FEAT-012 (member gift note)

## Scope & objectives

Verify that a [Member](../glossary/member.md) can carry a stable [Gift note](../glossary/gift-note.md),
that it is bounded and stable across occasions, that the **recipient's** note reaches the **giver** (and
only via their own link/read), and — the properties most easily broken silently — that an unset note is
**omitted from the encoded reveal link** and that the note **and** the budget minimum survive the
**server assignment projection** (the CR-2 gap this feature closes). The plan is weighted toward the
**cap**, the **giver-sees-recipient** direction, and the **server round-trip**.

## Test levels

- **Unit (domain)** — `Member.GiftNote` / `Group.SetMemberGiftNote`: set, trim, clear, the 500-char cap,
  rename-independence, state round-trip. **Primary.**
- **Application** — the `SetMemberGiftNoteCommand` handler (round-trip via the read model, survives a
  rename); the `GetRevealLinks` handler carrying each recipient's note to their giver's link; the codec
  omitting `gn` when unset.
- **UI/component** — bUnit: the reveal page shows the note only **after** the reveal (behind the spoiler
  gate).
- **Integration (server)** — a Main assignment-read test: publish with a budget minimum and a gift note,
  read my-assignments, assert both survive (runs under the Testcontainers/Docker suite).
- **End-to-end** — the `CLAUDE.md` browser walkthrough in DE/EN/FR.
- **Architecture** — no new schema in the account-free path; the domain stays I/O-free (arch tests
  already enforce this).

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| FR-037 / set + trim | Set a note → stored trimmed on the member | Unit | ✅ |
| FR-037 / clear | Empty/whitespace note → cleared | Unit + App | ✅ |
| FR-037 / cap | Note over 500 chars → rejected; exactly 500 accepted | Unit | ✅ |
| FR-037 / rename independence | Renaming a member does not wipe the note | Unit + App | ✅ |
| FR-037 / stable across occasions | One note shows on every occasion's reveal (per-member, not per-event) | Unit (state) + E2E | ✅ |
| FR-037 / giver sees recipient's note | Give one member a note → exactly one link (their giver's) decodes to it | App | ✅ |
| FR-037 / omitted when unset | A payload with no note carries no `gn` key; decodes to null | Unit | ✅ |
| FR-037 / spoiler gate | The note is hidden until the reveal button is pressed | UI | ✅ |
| FR-037 + FR-036 / server projection | Publish min budget + note → my-assignments returns both | Integration | ✅ (Docker) |
| FR-037 / archived member | ~~A member archived after a draw keeps their note on the past reveal~~ — superseded 2026-10-31: removing a member deletes their note (ADR-0052 amendment) | Unit | ✅ `GroupTests.RemoveMember_InADraw_KeepsOnlyTheName` |
| NFR-003 | Label, helper, "Good to know" render in DE/EN/FR | E2E | ✅ |
| E2E | Organizer sets a note → giver's reveal shows "Good to know: …"; no note shows nothing; console clean | End-to-end | ✅ (Playwright) |

## Test data & environments

- Fixture groups: a roster of 3–4 where exactly one member has a note (to prove the giver-sees-recipient
  direction and single-link targeting); a member with a 500- and a 501-char note for the cap.
- Account-free path exercised with no signed-in session (unit/UI/E2E). The server projection test needs
  the Main service + Postgres via Testcontainers (Docker), so it runs in that suite, not the offline run.

## Localization & accessibility notes

- DE/EN/FR strings for the field label, its helper, and the reveal "Good to know" label; DE *du* / FR
  *vous* register (ADR-0032); the parity test guards against an English-only string. The note's own text
  is user content, untranslated.
- The gift-note field is a labelled multiline input with a character counter; the reveal shows the note
  as ordinary text with a bold label. Verify the field label and counter are announced.

## Coverage (2026-09-03)

- **Domain (`GiftNoteTests`, 6):** trim, clear, cap-rejects, cap-accepts-at-limit, rename-independence,
  state round-trip.
- **Application (`ApplicationHandlerTests` +1, `HandlerTests` +1):** the command round-trips the note and
  survives a rename; the reveal-links handler carries the recipient's note to exactly their giver's link.
- **Reveal codec (`RevealLinkTests` +1, sample extended):** the note round-trips; an unset note is omitted.
- **UI (`RevealPageTests` +1):** the note is hidden before the reveal and shown after.
- **Server (`AssignmentReadTests` +1):** publishing a min budget + gift note and reading my-assignments
  returns both (Testcontainers/Docker; not run in the offline session that built this).
- **Browser walkthrough (Playwright/Chromium, DE/EN/FR):** organizer sets a member's note → the giver's
  reveal shows it under "Good to know" after the giftee is revealed; a member with no note shows nothing
  extra; **console error count 0** in every locale. Throwaway walkthrough (not committed — needs the
  standalone WebApp), the durable coverage being the tests above.

## Exit criteria

- [x] All FR-037 acceptance criteria covered by automated tests (domain + app + codec + UI + server)
- [x] The **giver-sees-recipient** and **omitted-when-unset** properties proven at the unit/app level
- [x] The **server projection** round-trip (note + budget min) has an integration test
- [x] Coverage target met (see [testing guidelines](../guidelines/testing-guidelines.md))
- [x] **Browser walkthrough** green in DE/EN/FR (note shown under "Good to know"; no-note shows nothing)
- [ ] CI green *(carried project-wide — no pipeline yet, as for FEAT-001..011)*
