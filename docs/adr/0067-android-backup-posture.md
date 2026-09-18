---
status: Accepted
date: 2026-09-18
deciders: [Adrian]
tags: [mobile, android, maui, privacy, backup, security]
---

# ADR-0067 — Android backup posture: disable OS auto-backup, backup is the app's own export/sync

## Context and problem statement

The Android head shipped with the MAUI template default `android:allowBackup="true"`. That opts the
app into **Android Auto Backup for Apps**: the OS automatically copies the app's local files (for
Secret-Santa, the file-per-key store holding **member names, exclusions and assignments** — ADR-0015)
to the user's **Google Drive** backup, roughly once a day, and restores them on reinstall or a new
device. It is silent and on by default.

That sits awkwardly against the product's local-first, data-minimised posture
([NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md)): the promise is
that personal data **stays on the device unless the user opts into account sync**. Auto Backup copies
that same personal data off-device — to the user's own encrypted Google account, so not a leak, but
without the user ever choosing it.

The flag is baked at build time and cannot be flipped from an in-app setting. So the question, forced
before the first store release: **should the Android head enable OS auto-backup, disable it, or gate it
behind an in-app toggle?**

## Decision drivers

- **Privacy posture / data minimisation** (NFR-004) — nothing leaves the device unless the user chooses
  it.
- **Cross-platform consistency** — the heads are MAUI (Android + iOS); a backup story that only exists
  on Android is inconsistent UX.
- **Reuse what exists** — the app already has **manual export** and **opt-in account sync**
  ([ADR-0015](0015-mvp-client-offline-storage.md), [ADR-0016](0016-sync-and-conflict-resolution.md)),
  which are exactly a user-controlled backup/restore path.
- **Simplicity & maintenance** — avoid Android-only native machinery if the same goal is met at the app
  layer.
- **User data recovery** — a user changing device should have *a* way to keep their data.

## Considered options

1. **Keep `allowBackup="true"`** — OS Auto Backup on by default.
2. **`allowBackup="false"`** — no OS backup; the app's own export + opt-in sync are the backup path.
3. **`allowBackup="true"` + a custom `BackupAgent`** gated by an in-app preference (off by default), so
   the OS backup only runs when the user enables it.

## Decision outcome

**Chosen: Option 2 — `allowBackup="false"`.** It is the only option that keeps personal data on the
device by default, matches the local-first posture on **both** platforms, and reuses the export/sync the
app already has instead of adding platform-specific code. The user still gets the choice they asked for
— "off by default, opt in to move my data" — but expressed as an **app feature** (export / account
sync), not as a gated OS setting.

Option 3 was rejected: a `BackupAgent` is Android-only native code, so the resulting "Backup" toggle
would silently do nothing on iOS (iCloud backup is a separate mechanism), which is worse UX than a
consistent app-level model — for meaningfully more surface to build and maintain.

## Consequences

- **Positive:** no silent off-device copying of member/assignment data; posture is consistent across
  Android and iOS; the existing export + opt-in sync become the single, user-controlled backup path; no
  extra native code.
- **Negative:** users **lose automatic restore** on reinstall / new device via Google — they must use
  export or account sync to migrate. `allowBackup="false"` also disables `adb backup` (a dev
  convenience) and the app-data portion of Google's device-to-device transfer.
- **Follow-ups:**
  - Manifest set to `false` (done, with a comment pointing here).
  - Resolve the "open decision" note in [mobile-apps.md](../deployment/mobile-apps.md).
  - Make sure export / account sync is **discoverable** in the UI so the recovery path is obvious (the
    features already exist; this is a findability check, not new work).
  - **iOS counterpart is a separate, still-open call:** iCloud backup similarly uploads app data to the
    user's Apple account. Symmetric strictness would mark the data files
    `NSURLIsExcludedFromBackupKey`; but iCloud backup is per-Apple-ID and can be end-to-end encrypted,
    and it provides the restore convenience Android is giving up. Not decided here — flagged for a future
    ADR if we want full parity.
- **New risks:** a user who reinstalls without having exported or enabled sync **loses their data**
  silently. Mitigated by keeping export/sync easy to find; revisit if this becomes a real support
  complaint.

## Pros and cons of the options

### Option 1 — keep `allowBackup="true"`
- 👍 Zero work; automatic restore on reinstall / new device.
- 👎 Silently uploads personal data to Google Drive by default — against NFR-004 and the local-first
  promise; the user never chose it.

### Option 2 — `allowBackup="false"` (chosen)
- 👍 Nothing off-device by default; consistent on Android + iOS; reuses existing export/sync; no native
  code.
- 👎 No automatic Google restore; migration relies on the user using export/sync; also drops `adb backup`
  and D2D app-data transfer.

### Option 3 — `allowBackup="true"` + gated `BackupAgent`
- 👍 A genuine in-app toggle over the OS backup, off by default.
- 👎 Android-only native code; the toggle does nothing on iOS; more surface to build/test/maintain for a
  worse cross-platform story.

## Links

- Related ADRs: [ADR-0015](0015-mvp-client-offline-storage.md) (offline storage the flag would back up),
  [ADR-0016](0016-sync-and-conflict-resolution.md) (opt-in sync = the chosen recovery path),
  [ADR-0053](0053-backend-free-first-mobile-release.md) (offline-first mobile posture)
- Requirements: [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md)
  (data protection & minimisation), [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)
  (store distribution)
- Deployment: [mobile-apps.md](../deployment/mobile-apps.md) (resolves its `allowBackup` open decision)
