---
id: UC-MAIN-008
title: Create a circle
app: Main
actors: [User]
status: Approved
priority: Must
related: [FR-016, NFR-003, NFR-006, ADR-0018]
---

# UC-MAIN-008 — Create a circle

## Summary / goal

A signed-in [User](../../glossary/user.md) creates a [Circle](../../glossary/circle.md) — the
standing set of people they share apps with — names it, and chooses the word the suite will use for
it. They become its **owner**.

## Primary actor & stakeholders

- **Primary actor:** a User with a verified account.
- **Stakeholders & interests:** everyone who will later be invited into the circle (the name and label
  they will see are chosen here); the operator, who now stores a second kind of personal data —
  *who belongs with whom* — alongside accounts.

## Preconditions

- The user is signed in and their email address is **verified**. An unverified account may sign in but
  gets nothing account-scoped ([UC-MAIN-001](UC-MAIN-001-create-account.md)), and a circle is
  account-scoped data.
- The device is online. Circles live server-side and have no offline mode in M2.

## Postconditions

- **Success:** the circle exists with a name and a label; the creator holds the single **owner**
  [membership](../../glossary/circle-membership.md); the circle has no other people yet.
- **Failure:** nothing is created — there is no half-built circle without an owner.

## Trigger

The user selects **Create a circle** from the Main shell.

## Main success scenario

1. User selects **Create a circle**.
2. System asks for a **name** (e.g. *"Müller"*, *"Tuesday climbing crew"*).
3. System offers a **label** — the word the UI will use for this circle — pre-filled with the default
   for the user's current language (*Family* / *Familie* / *Famille*) and freely editable.
4. User confirms.
5. System creates the circle, creates the creator's membership in the **owner** role, and shows the
   circle with an empty people list and the two ways to fill it: **invite someone**
   ([UC-MAIN-009](UC-MAIN-009-invite-to-circle.md)) or **add a managed member**
   ([UC-MAIN-011](UC-MAIN-011-manage-managed-members.md)).

## Extensions / alternate flows

- **1a. The account is unverified** → creation is refused with the reason and a way to resend the
  verification mail. This is the same rule that governs every other account-scoped surface, not a new
  one for circles.
- **2a. The name is empty or whitespace** → required; the circle is the thing people will recognise in
  a list, and an unnamed one is unusable once a user belongs to more than one.
- **3a. The user clears the label** → the default for their language is used. The label is never empty.
- **3b. The user types their own word** (*Household*, *Crew*, *Bande*) → stored verbatim as **user
  data**. It is not translated and does not change when anyone switches language — see the
  localization note below.
- **5a. The user already owns circles** → nothing prevents another. A user may own and belong to
  several circles with a different role in each ([ADR-0018](../../adr/0018-circle-and-sharing-model.md)).

## Exceptions

- **The device is offline** → circle creation is unavailable and says so. Account-free Secret-Santa is
  entirely unaffected ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).
- **The server fails partway** → no circle and no membership are left behind. A circle without an owner
  could never be administered or deleted, so partial creation is worse than none.

## Non-functional notes

- **Offline/local-first:** online-only in M2. Circles are inherently shared data; there is nothing
  meaningful to do with one on a device that cannot reach the other members. A read-only cached view
  arrives with sync in M3.
- **Localization:** the **default** label is resolved from the creator's language at creation time and
  then stored as plain text. A circle labelled *Familie* stays *Familie* for a member browsing in
  French — translating another user's chosen word would be editing their data, and the three defaults
  are only defaults ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
- **Privacy:** the circle name and label are personal data — a list of who belongs together is exactly
  the kind of social graph [NFR-004](../../requirements/non-functional/NFR-004-data-protection-and-retention.md)
  governs. They are visible to circle members only
  ([NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)).
- **Performance:** creation is a single round trip; the circle page opens without a second load.

## Open questions

- **Is there a cap on circles per user?** Not a product limit but an abuse limit — an unbounded
  create endpoint is a cheap way to fill a database. Recommendation: a generous per-user cap and a
  creation rate limit, numbers to be fixed in the M2 feature spec.
- **Can a circle be created from inside Secret-Santa** ("make a circle from this group's members")?
  Attractive, but it needs the Member↔User link — deferred to **M4**
  ([ADR-0019](../../adr/0019-member-user-linking.md)).

## Traceability

- Requirement: [FR-016](../../requirements/functional/FR-016-create-circle.md); constrained by
  [NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md),
  [NFR-004](../../requirements/non-functional/NFR-004-data-protection-and-retention.md),
  [NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)
- ADRs: [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (the aggregate, the canonical name and
  the per-circle label), [ADR-0008](../../adr/0008-entity-id-strategy.md)
- Glossary: [Circle](../../glossary/circle.md), [Circle membership](../../glossary/circle-membership.md),
  [User](../../glossary/user.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md), decision D6
