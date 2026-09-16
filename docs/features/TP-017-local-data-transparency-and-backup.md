---
id: TP-017
feature: FEAT-017
status: Reviewed
related: [NFR-011, NFR-001, NFR-008, ADR-0025, ADR-0026, ADR-0028, ADR-0048, ADR-0058]
---

> **Executed 2026-09-16.** Automated: `BackupServiceTests` (7 tests — export→import round-trip;
> empty-device export; merge-by-id add/overwrite/keep; non-backup and malformed JSON rejected;
> newer-version refused without writing; single malformed entry skipped) — part of **LocalStore 82/82**.
> Browser-verified (Chromium, DE + EN) against the standalone web head: the Settings **Your data** section
> renders the explainer, the persistence-status line, and the export/restore controls; export produced a
> valid versioned envelope; `localStorage.clear()` then restore brought the groups back through the real
> syncing repository; the drawer refreshed **in place with no full page reload**; an invalid file was
> rejected and left existing data untouched; the DE storage-mode picker wording ("Wichtel-Link", no
> repeated "auf diesem Gerät"), its web-only caveat, and the top-aligned radio layout were confirmed;
> console clean throughout. **Not covered (by design):** the MAUI heads (native storage is durable and not
> evictable, so the whole feature is web-only) and the browser's own grant of `persist()` (best-effort,
> environment-dependent — the status line reports whatever the browser decides).

# TP-017 — Test plan for FEAT-017

## Scope & objectives

Verify [FEAT-017](FEAT-017-local-data-transparency-and-backup.md) and the requirement it realises,
[NFR-011](../requirements/non-functional/NFR-011-local-data-durability-and-portability.md): on the **web
head**, the local-first data (a group in `localStorage`,
[NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)) is made **durable**, **portable**
and **legible** without changing the storage model:

1. **Durability** — persistence is requested at startup (`navigator.storage.persist()`), and the current
   grant is reported to the user (`navigator.storage.persisted()`).
2. **Backup/restore** — every group exports to a self-describing, versioned JSON file and re-imports by
   **merging by id** (add new, overwrite same-id, delete nothing), reusing the `GroupState` memento so a
   backup reads back through the ordinary load-time migrations.
3. **Transparency** — Settings explains where the data lives and whether it is kept; the first-run empty
   state and the "On this device" storage-mode option carry a web-only caveat.

The load-bearing risks are **silent data loss** (a bad import wiping or corrupting good groups) and
**mis-reading a foreign/newer file**; the plan weights the merge/rejection/skip unit cases accordingly.

## Test levels

- **Unit** — `BackupService` over the real `GroupRepository` on an in-memory `IKeyValueStore`: the full
  serialize → file → deserialize → rehydrate round-trip, with no browser and no device.
- **Browser walkthrough (manual + JS-driven)** — the `DataStorageSection` in Settings, the storage-mode
  picker wording/layout, and the silent post-restore refresh, on the standalone web head.
- **Localization** — DE/EN/FR (+ neutral) parity for the new strings.

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| Backup round-trip | Export a group (members, event, budget) → import into an empty device → group restored intact | Unit | ✅ |
| Empty device | Export with no groups → a valid backup whose `groups` is empty | Unit | ✅ |
| Merge — add | Import a group whose id is new → **added**; count reported | Unit | ✅ |
| Merge — overwrite | Import a group whose id already exists → **overwritten** with the backed-up copy; count reported | Unit | ✅ |
| Merge — no delete | A group on the device but absent from the backup **survives** the import | Unit | ✅ |
| Reject foreign file | A non-backup JSON file → `Invalid`; device unchanged | Unit | ✅ |
| Reject malformed | Non-JSON text → `Invalid` | Unit | ✅ |
| Refuse newer version | `version > CurrentVersion` → `UnsupportedVersion`; **nothing written** | Unit | ✅ |
| Skip corrupt entry | A backup with one unreadable (null) entry among valid ones → the bad one **skipped**, the rest restored; counts reported | Unit | ✅ |
| Settings section (web) | On web, Settings shows "Your data": explainer, persistence status, export + restore | Browser | ⚠️ manual |
| Persistence status | The status line reflects `navigator.storage.persisted()` (kept vs may-be-cleared) | Browser | ⚠️ manual |
| Export downloads | Export yields a `secret-santa-backup-YYYY-MM-DD.json` with `format`/`version`/`groups` | Browser | ⚠️ manual |
| Restore after wipe | `localStorage.clear()` → restore the file → groups reappear (written through the syncing repository) | Browser | ⚠️ manual |
| Silent refresh | After restore the drawer refreshes in place; **no full page reload**; list re-reads on navigation | Browser | ⚠️ manual |
| Invalid-file UX | Restoring a non-backup shows the error snackbar and leaves existing groups untouched | Browser | ⚠️ manual |
| Web-only gating | The "Your data" section, the empty-state note and the picker caveat appear on web only (`OperatingSystem.IsBrowser()`) | Browser | ⚠️ manual |
| Picker wording (DE) | "On this device" reads cleanly (no repeated location, app's "Wichtel-Link" term); radio top-aligned to the title | Browser | ⚠️ manual |
| Localization parity | Every new string present in DE/EN/FR (+ neutral) | Unit (resx parity) | ✅ |

## Test data & environments

- **Unit:** `GroupRepository` over `InMemoryKeyValueStore`, seeded with groups at controlled ids/names and
  an event with a budget; malformed inputs built by splicing a `null` entry into a real export and by
  serializing a `BackupDocument` with a bumped `version`. No Postgres, no browser.
- **Browser:** the standalone Secret-Santa web head (`http` launch profile, `localhost:5081`); import
  driven through the framework `InputFile`; export captured by stubbing `circletoolsStorage.download`.

## Localization & accessibility notes

- DE/EN/FR (+ neutral) parity for the "Your data" strings, the export/restore hints and results, the
  first-run note, the storage-mode hint and its web-only caveat.
- The section is keyboard-operable; the restore control is a `<label>`-triggered file input; the
  persistence status uses an alert/status role appropriate to its severity.

## Exit criteria

- [x] `BackupService` behaviour covered by automated unit tests (7/7; LocalStore suite 82/82 green)
- [x] Browser walkthrough (create → export → wipe → restore → silent refresh; invalid path) completed, DE + EN
- [x] Localized strings present in DE/EN/FR (+ neutral)
- [ ] CI green (no pipeline yet — carried, as for the other milestones)
