---
status: Accepted
date: 2026-10-16
deciders: [Adrian]
tags: [architecture, sync, sharing, secret-santa, ux]
---

# ADR-0081 — A signed-in organizer's shared group stays in their account

## Context and problem statement

[ADR-0058](0058-account-free-link-shared-groups.md) gives a group one of three storage modes — *On this
device*, *Shared by link*, *My account* — and the [2026-10-16 two-account test](../brainstorming/2026-10-16-secret-santa-two-account-device-test.md)
showed what that means for the most common real case, a signed-in organizer inviting their family (Q31):

- A *My account* group can't be shared at all: its *Sharing & sync* offers no way to invite anyone.
- Choosing *Shared by link* moves the group **out of** the account: it vanished from the organizer's other
  devices (QA-Familie did not appear in their second browser). The organizer has to know to "claim admin"
  afterwards to get it back into the account — an action named in jargon (Q15).

Families want both at once: the organizer's devices in sync **and** the family invited. The pieces exist —
claim-admin turns a shared group into one *owned* by the account (synced to all its devices through the
owner path, join link kept; ADR-0058, ADR-0078) — but nothing puts them together.

**What does sharing mean for a signed-in organizer, and how does the app present it?**

## Decision drivers

- One sentence a family organizer understands: *"it's in my account, and my family joins with a link"*.
- No dead ends: a *My account* group can always invite people.
- Reuse the owner path (claim-admin, owned reads/writes, discovery on other devices) — no new storage tier.
- Signed out, nothing changes: *Shared by link* stays the account-free tier of ADR-0058.
- Existing groups keep working; no silent migration of someone's link-only group.

## Considered options

1. **Keep the three modes; explain better.** Tell the user that sharing leaves the account and offer
   claim-admin afterwards.
2. **A signed-in organizer's share is owned by their account.** Sharing while signed in runs share +
   claim-admin in one step; a *My account* group offers "Invite people", which does the same after leaving the
   account store. Signed out, *Shared by link* is unchanged.
3. **A fourth, separate mode "My account + link"** with its own server representation.

## Decision outcome

**Chosen: option 2.**

### What a signed-in person is offered

| Signed out | Signed in |
|---|---|
| **On this device** | **On this device** |
| **Shared by link** — online, account-free, admin link + join link | **My account** — synced across their devices |
| **My account** — "sign in needed" | **My account · shared** — synced across their devices, and others join with a link |

- Creating a group as *shared* while signed in, or switching an *On this device* group to *shared*, shares it
  (`POST /shared`) and claims admin into the account in the same step. The group is then an **admin-claimed
  shared group** (ADR-0078): owned by the account, its join link kept, its admin link retired.
- A *My account* group (account store) offers **Invite people**: it leaves the account store (the un-claim
  of ADR-0073 Amendment 2 — the account's other devices drop their copy) and is shared and claimed as above;
  the other devices then discover it again through the owner path, now as *My account · shared*.
- *My account · shared* → *My account* (**stop sharing**) deletes the online copy — the join link stops
  working and joined people lose the group — and puts the group back into the account store.
- *My account · shared* → *On this device* is unchanged (release, then remove the online copy).
- **Release to a link** (hand the group over without an account, so co-organizers can use an admin link)
  stays available as an explicit action on the join-link card, with plain wording (Q15).
- The **owner** can release a joined person (`DELETE /shared/{groupId}/owned/claims/{memberId}`), as an
  admin-link holder already can — the only recovery when the wrong person joined.
- A signed-in device holding a **link-only** shared group (an admin token, no owner) is shown as shared, with
  an offer to **keep it in my account** (claim-admin). Nothing is claimed silently.

### Presentation

The list chip, the group header chip and *Sharing & sync* name the kind: *On this device*, *Shared by link*
(signed out / link-only), *My account*, *My account · shared*. The prompts say what happens in plain words —
"Invite people? You get a join link for your family. The group stays in your account." (Q15) — and avoid
"admin link" in favour of "management link (to co-organize)".

## Consequences

- **Positive:** the common case is one step and never loses the organizer's other devices; *My account*
  groups can invite; no new storage tier and no new server representation.
- **Negative / trade-offs:** a signed-in organizer's shared group has no admin link while owned, so
  co-organizing needs an explicit release to a link (as before). Stop-sharing removes joined people's access
  — said in its prompt.
- **Follow-ups:** FR-038 (mode transitions) reads with this table; glossary *Shared-by-link group* notes the
  owned variant; TP for the transitions (FEAT-025).
- **New risks:** the two-step share + claim can fail in between (offline after the share) — the group is
  then a link-only shared group on this device, shown with the "keep it in my account" offer, so nothing is
  lost.

## Pros and cons of the options

### 1. Explain the three modes better
- 👍 No behaviour change.
- 👎 Keeps the trap: the family case still needs two steps and jargon.

### 2. Owned share for signed-in organizers *(chosen)*
- 👍 Matches the mental model; reuses claim-admin and the owner path.
- 👎 Two server calls for one user action.

### 3. A fourth storage mode
- 👍 Explicit.
- 👎 A new server representation for what claim-admin already is.

## Links

- Related ADRs: [0058](0058-account-free-link-shared-groups.md), [0073](0073-storage-mode-is-authoritative.md),
  [0078](0078-my-account-groups-record-their-account.md), [0080](0080-participant-access-to-shared-groups.md).
- Requirements: [FR-038](../requirements/functional/FR-038-per-group-storage-mode.md).
- Brainstorming: [2026-10-16 two-account test](../brainstorming/2026-10-16-secret-santa-two-account-device-test.md) (Q15, Q31), backlog WP-2.
