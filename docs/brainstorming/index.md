# Brainstorming

Idea-gathering and selection sessions. Divergent (generate) then convergent (select) — the
selected ideas feed [use cases](../use-cases/index.md) and [requirements](../requirements/index.md);
rejected ideas are kept for the record.

- New session → copy [`_template-brainstorming-session.md`](_template-brainstorming-session.md),
  name it `YYYY-MM-DD-topic.md`.

## Sessions

| Date | Topic | Outcome |
|------|-------|---------|
| [2026-07-24](2026-07-24-secret-santa-scope.md) | Secret-Santa MVP scope | Concluded — offline, account-free MVP with self-contained reveal links |
| [2026-07-24](2026-07-24-accounts-families-sync.md) | Accounts, families & sync (v2) | Concluded (2026-07-27) — all eight pivotal decisions D1–D8 resolved and recorded as ADR-0016..0022; sliced into M1–M4, all built and closed |
| [2026-09-01](2026-09-01-secret-santa-mobile.md) | Secret-Santa mobile (Android) features | Draft — native share sheet built (ADR-0028 seam); QR code, add-to-calendar, share-all selected for a next pass |
| [2026-09-02](2026-09-02-secret-santa-competitor-research.md) | Secret-Santa competitor & app-store review research (DE/EN/FR) | Draft — validates the no-account/offline/link-QR/non-spoiling-reveal strategy; promotes recurring-list-carryover, budget min+max, do-not-gift, Schrottwichteln; raises MNF-8 reminders |
| [2026-09-13](2026-09-13-storage-and-sharing-tiers.md) | Storage & sharing tiers (local / account-free backend / account) | Concluded — net-new middle tier (account-free, backend-stored, link-shared); admin link + join link, trust-based first-claim-locks w/ admin release, mode chosen at creation; needs an ADR (reopens ADR-0016/0020/0053, NFR-002, glossary/sync) |
| [2026-10-14](2026-10-14-secret-santa-android-ux-review.md) | Secret-Santa Android UX review (on-device run, Pixel 9 Pro) | Concluded — 11 bugs + 17 usability/polish findings, all fixed except B1 (not reproducible) and P9 (ADR-0070); ADR-0077 + ADR-0044/0073 amendments |
| [2026-10-15](2026-10-15-secret-santa-sync-device-test.md) | Secret-Santa sync & storage-mode test (Pixel 9 Pro + browser, production) | Concluded — sync works in the main paths; 10 sync bugs (S1–S10) + 9 UI findings (D7 withdrawn); all selected for fixing. ADR-0073 Am. 2 (un-claim is a move), ADR-0078 Am. 3, ADR-0044 amendment (sheet prompt, resume sync), ADR-0079 (sign out this device, change password) |
| [2026-10-16](2026-10-16-secret-santa-two-account-device-test.md) | Secret-Santa full test, two accounts (Pixel 9 Pro + browser, production) | Concluded — S1/S7/S9 fixes hold on device; participants can still break the secret (Q1–Q4) and can't edit their own wishlist (Q5); proposals for circles button, drawer, sync status and configurable reminders (T1–T4); all 49 items selected (incl. Q37), sliced into backlog WP-1…WP-8 |
| [2026-10-20](2026-10-20-secret-santa-release-device-test.md) | Secret-Santa release check of WP-1…WP-8 (Pixel 9 Pro + browser, production) | Draft — most WP-1…WP-8 fixes hold on device; linked givers' *My assignments* freezes at draw time (R1, server), page end hidden behind the bottom nav (R2), privacy text vs organizer view (R3); 18 findings awaiting triage; part 2 (full run): all automated suites green, App Links, offline, reminders, passkey, account deletion verified; new: shown reminders vanish on next sync (R12), /join not an App Link (R10), import drops sharing (R11) |
| [2026-10-22](2026-10-22-database-and-write-path-review.md) | Database design and write-path review (schema, write paths, use-case coverage) | Draft — two authorization gaps in linking/sync (S1, S2), account deletion leaves sync data (S3), two separate own-entry tables behind R16, shared-store concurrency, no body schema version |
| [2026-10-22](2026-10-22-one-way-to-be-in-a-group.md) | One way to be part of a group — analysis and target model | Draft — two places (device / online), one group store, participants for every route, derived views; D1 keep account-free organizing, D2 start empty, D3 fold *My assignments*/*My wishlists* into the group list → ADR-0086 |
