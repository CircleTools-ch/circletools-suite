---
status: Accepted
date: 2026-10-25
deciders: [Adrian]
tags: [data, migrations, release, safety]
---

# ADR-0087 — Production data is kept: no destructive changes from 2026-10-25

## Context and problem statement

Until now production held test data, and changes were allowed to throw it away: the deployment of
[ADR-0086](0086-one-way-to-be-in-a-group.md) drops the `linking` and `shared` schemas and the group copies in the
account sync (decision D2). From that deployment on (2026-10-25), production holds real people's groups, wishlists
and accounts, and devices hold groups that exist nowhere else.

**How do we make sure no later change — a migration, a format change, a release — destroys data again?**

## Decision drivers

- Data loss is silent: nothing fails, the data is simply gone. It has to be caught before it ships.
- Groups kept on a device have no server copy; the app is their only keeper.
- A single maintainer: the rules must be enforced by the build, not by memory.
- Apps on phones update later than the server, so both sides change at different times.

## Considered options

1. **A written rule only** — a checklist in the release plan.
2. **Rules enforced where they can be, plus a backup before every release** — a build-time guard for migrations,
   set-aside instead of discard on devices, versioned documents, and a backup with a restore drill.
3. **Never change the schema** — freeze it.

## Decision outcome

**Chosen: option 2**, because data loss has to be stopped by the build where possible and recoverable where not.

1. **Migrations never drop or delete data.** Schema changes are *expand, then contract*: add the new shape, move the
   data, switch the code; remove the old shape only in a later release, by its own migration, once nothing reads
   it. A test (`NonDestructiveMigrationTests`) builds every migration of every context and fails on any that drops
   a table, a column or a schema, deletes rows, or runs SQL that does. Its list of exceptions is closed: the four
   migrations from before real data. Adding to it needs a new ADR that says why the data may go and how it is saved
   first.
2. **The group document changes additively.** `GroupDocument` carries a schema version. New fields are optional, so
   older documents still read. A version bump comes with code that reads every older version, and the server is
   deployed before the apps (it refuses documents newer than it knows, so nothing is stored that it would lose).
3. **Devices never discard what they can't read.** A stored document a version can't read is set aside under its
   own key before anything is written (`GroupRecordStore.UnreadableStorageKey`). A change of storage key or format
   comes with an import of the old one, as the ADR-0086 import does for the old local store.
4. **Every release that migrates is preceded by a backup.** The nightly encrypted backup stays
   ([deployment/backups.md](../deployment/backups.md)), and the deploy pipeline runs `backup.sh` right before the
   migrator: a failed backup stops the rollout before anything migrates. The restore drill is run before a release
   whose migrations move data.
5. **Only the people and the rules delete.** The server deletes data only on a person's request (delete a group,
   leave, free a spot, delete the account) or by the documented retention of groups without an account owner
   ([NFR-010](../requirements/non-functional/NFR-010-shared-group-retention-lifecycle.md)).

## Consequences

- **Positive:** the one way data could vanish unnoticed — a migration or a format change — is caught by the build or
  set aside; every release can be rolled back to a backup taken minutes before.
- **Negative:** removing a column or a table takes two releases; the schema keeps unused shapes a little longer.
- **Follow-ups:** the release plan names the backup and the restore drill as steps before every deployment with a
  migration; a recovery path for a set-aside device document (export from Settings → Your data) if one is ever
  found.
- **New risks:** the guard reads EF Core operations and SQL text; a destructive change made outside migrations
  (manual SQL on the server) is not caught — the operator's read-only role stays read-only for that reason.

## Pros and cons of the options

### A written rule only
- 👍 No code.
- 👎 Relies on remembering it under release pressure; ADR-0086's own deployment shows how easy a drop is to write.

### Rules enforced, plus a backup before every release
- 👍 The build fails before a destructive migration ships; devices keep what they can't read.
- 👎 A little more ceremony for schema changes.

### Never change the schema
- 👍 Nothing can be dropped.
- 👎 Stops the product from evolving; unrealistic.

## Links

- Related ADRs: [ADR-0086](0086-one-way-to-be-in-a-group.md) (the last destructive deployment),
  [ADR-0047](0047-versioning-and-release-strategy.md) (releases), [ADR-0003](0003-modular-monolith-with-aspire.md)
  (one migration history per context)
- Requirements: [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md),
  [NFR-008](../requirements/non-functional/NFR-008-sync-convergence-and-data-safety.md),
  [NFR-010](../requirements/non-functional/NFR-010-shared-group-retention-lifecycle.md)
- Operations: [deployment/backups.md](../deployment/backups.md),
  [deployment/production-release-plan.md](../deployment/production-release-plan.md)
