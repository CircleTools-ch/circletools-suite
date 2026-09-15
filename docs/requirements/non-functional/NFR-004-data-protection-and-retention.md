---
id: NFR-004
title: Data protection & retention
type: non-functional
status: Approved
priority: Must
source: UC-MAIN-001, UC-MAIN-006, ADR-0022
verification: review
related: [UC-MAIN-001, UC-MAIN-006, FR-007, FR-014, FR-015, NFR-002, ADR-0020, ADR-0022]
---

# NFR-004 — Data protection & retention

## Description

v2 is the first version to store **personal data server-side** — display name, email address, circle
memberships and [Assignments](../../glossary/assignment.md). That creates obligations under the Swiss
**revDSG** and the **GDPR**, and this requirement states them as testable properties.

**Data minimisation.** The suite stores only what a stated requirement needs. Specifically: no
date of birth, no postal address, no phone number, no payment data, and no assignment data beyond
giver, recipient and group ([NFR-002](NFR-002-assignment-privacy.md)).

**Credential storage.** Passwords are stored only as hashes via ASP.NET Core Identity. For
[Passkeys](../../glossary/passkey.md), only the public key and metadata are stored — never a private
key, never biometric data.

**Retention.**

| Data | Retention |
|------|-----------|
| Pending (unverified) accounts | purged **30 days** after creation |
| Consumed / expired verification, reset and email-change links | invalid on use, purged with the account |
| Export archives delivered by link | short-lived and **single-use** |
| Refresh tokens | 30-day sliding window; revoked on password reset and on account deletion |
| Deleted accounts | **immediate** hard delete, no soft-delete state ([ADR-0022](../../adr/0022-account-deletion-and-data-export.md)) |

**Subject rights.** Export ([FR-014](../functional/FR-014-export-personal-data.md)) and deletion
([FR-015](../functional/FR-015-delete-account.md)) are self-service, require no support contact, and
complete without manual intervention.

**Local data is out of scope of deletion.** Data on the user's own device is theirs; signing out and
deleting the account both leave it intact ([ADR-0017](../../adr/0017-anonymous-to-account-claim.md)).

**Data residency.** Personal data should be hosted in Switzerland or the EU/EEA. The concrete target
is now **decided**: personal data is hosted in **Switzerland** — the Infomaniak VPS in **Geneva**
([ADR-0036](../../adr/0036-cloud-provider-and-hosting.md)) — and mail in transit goes through **Brevo
(EU)** ([ADR-0037](../../adr/0037-transactional-email-provider.md)).

## Rationale

The MVP stored nothing server-side, so data protection could be satisfied by architecture alone —
"the link is the secret". v2 gives that up deliberately
([ADR-0020](../../adr/0020-server-side-assignment-privacy.md)) in exchange for sync and in-app reveal,
which means the guarantee now has to be **stated and verified** rather than being a property of having
no server. Writing it as a requirement is also what makes the obligations checkable in review instead
of remembered in someone's head.

## Acceptance criteria

```gherkin
Given the persisted data model
When it is reviewed against this requirement
Then no field stores a date of birth, postal address, phone number or payment data
And no assignment field beyond giver, recipient and group exists

Given a user account
When credential storage is inspected
Then the password is present only as a hash
And no passkey private key or biometric data is stored

Given a pending-verification account created more than 30 days ago
When the retention job runs
Then the account and its personal data are purged

Given a user who wants their data
When they use export and deletion
Then both complete self-service, without contacting support and without manual intervention

Given a user who deletes their account
When deletion completes
Then no soft-deleted copy of the account remains in the data store

Given a user who deletes their account
When they reopen the app on their own device
Then their local data is intact
```

## Quality attribute *(NFR only)*

- **Attribute:** privacy / data protection (revDSG, GDPR).
- **Metric & target:**
  - **zero** stored personal-data fields beyond those enumerated by an FR;
  - **zero** plaintext passwords, private keys or biometric data at rest;
  - unverified accounts purged within **30 days** (target: 100 % of eligible accounts per run);
  - export and deletion available **self-service**, 0 manual steps;
  - **zero** soft-deleted account rows remaining after a deletion.
- **Measurement approach:** the data-model check is **automated**, not reviewed —
  `PersistedModelReviewTests` scans every persisted property of the whole EF model for the forbidden
  field kinds on each build. A review that runs once per change is exactly how the first breach got
  in: `IdentityUser` ships `PhoneNumber` and `PhoneNumberConfirmed`, nobody chose to add them, and
  the first Identity migration created both columns (dropped 2026-07-28). Also automated: the
  retention job and the post-deletion absence of account rows. The residency target becomes
  measurable once the hosting ADR lands.

## Dependencies & related

- Derived from: [UC-MAIN-001](../../use-cases/main/UC-MAIN-001-create-account.md),
  [UC-MAIN-006](../../use-cases/main/UC-MAIN-006-delete-account-and-export-data.md)
- Related: [FR-007](../functional/FR-007-create-suite-account.md),
  [FR-014](../functional/FR-014-export-personal-data.md),
  [FR-015](../functional/FR-015-delete-account.md), [NFR-002](NFR-002-assignment-privacy.md),
  [NFR-005](NFR-005-account-enumeration-resistance.md)
- Governing ADRs: [ADR-0022](../../adr/0022-account-deletion-and-data-export.md),
  [ADR-0020](../../adr/0020-server-side-assignment-privacy.md),
  [ADR-0017](../../adr/0017-anonymous-to-account-claim.md)
- Guideline: [security & privacy guidelines](../../guidelines/security-privacy-guidelines.md)
- **Data residency:** decided — Switzerland (Infomaniak Geneva,
  [ADR-0036](../../adr/0036-cloud-provider-and-hosting.md)); mail in transit via Brevo EU
  ([ADR-0037](../../adr/0037-transactional-email-provider.md)).
- Glossary: [User](../../glossary/user.md), [Passkey](../../glossary/passkey.md),
  [Assignment](../../glossary/assignment.md)
