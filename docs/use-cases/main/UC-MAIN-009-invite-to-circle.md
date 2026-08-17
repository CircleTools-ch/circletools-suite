---
id: UC-MAIN-009
title: Invite a person to a circle
app: Main
actors: [User, Circle admin]
status: Approved
priority: Must
related: [FR-017, FR-019, NFR-003, NFR-005, NFR-006, ADR-0018, ADR-0043]
---

# UC-MAIN-009 — Invite a person to a circle

## Summary / goal

An owner or admin invites someone into a [Circle](../../glossary/circle.md) by email address —
**without ever learning whether that address already has an account**.

## Primary actor & stakeholders

- **Primary actor:** a circle **owner** or **admin**.
- **Stakeholders & interests:** the recipient, whose address must not become a probe result; the
  operator, for whom this form is the most tempting account-enumeration oracle in the product
  ([NFR-005](../../requirements/non-functional/NFR-005-account-enumeration-resistance.md)); other
  circle members, who will see the new person appear.

## Preconditions

- The actor is signed in, verified, and holds the **owner** or **admin** role in the circle.
- The device is online.

## Postconditions

- **Success:** an [Invitation](../../glossary/invitation.md) exists for that address with an expiry,
  it appears in the circle's **pending invitations** list, and a mail has been queued. The actor is
  told *"invitation sent"* — and nothing else.
- **Failure:** no invitation is created; the actor is told the send failed, in wording that is
  identical whether or not the address has an account.

## Trigger

The actor selects **Invite someone** on the circle page.

## Main success scenario

1. Actor enters an email address.
2. System validates the format, applies the per-inviter rate limit, and creates a pending invitation.
3. System responds **"invitation sent"**.
4. System queues a mail to the address, in the language chosen per the localization note below.
5. The invitation appears in the circle's pending list with the address and its expiry, where it can
   be **resent** or **revoked**.
6. The recipient accepts or declines ([UC-MAIN-010](UC-MAIN-010-respond-to-invitation.md)); on
   acceptance a **member** membership is created and the pending entry becomes a person in the list.

## Extensions / alternate flows

- **2a. The address already has an account** → the invitation is created exactly as in step 2 and the
  response in step 3 is byte-for-byte identical. Which of the two paths applies is resolved **on the
  recipient's side** — they see an in-app join request instead of a sign-up link
  ([ADR-0018](../../adr/0018-circle-and-sharing-model.md)).
- **2b. The address already belongs to someone in this circle** → still identical. The recipient is
  told *"you are already in this circle"* when they open it; no duplicate membership is possible. The
  inviter gets no *"already a member"* hint, because member email addresses are not shown in the
  people list and such a hint would confirm that this address is that person's.
- **2c. An invitation for the same address is already pending** → the existing one is **resent**
  rather than duplicated, and the response is unchanged.
- **5a. Actor revokes a pending invitation** → it can no longer be accepted; an accept attempt is told
  plainly that the invitation is no longer valid.
- **5b. Actor resends** → the same invitation is re-delivered with a refreshed expiry. Resend is
  rate-limited too, or it becomes a mail-bombing tool aimed at a third party.
- **6a. The invitation expires unaccepted** → it leaves the pending list with no reason given, and the
  address can be invited again.
- **1a. Actor invites an *offline person* (managed member) to join with their own account** → the actor
  uses **Invite to join** on that person's row, or picks them under *"this invitation is for"*. The
  invitation is aimed at that [managed member](../../glossary/managed-member.md); on acceptance the
  existing profile is **promoted** — linked to the account rather than duplicated, keeping the person's
  place and history ([FR-019](../../requirements/functional/FR-019-manage-managed-members.md)). The
  pending entry shows *"as <name>"* so the aimed invitation is told apart from an ordinary one. If the
  actor does **not** aim it, the recipient may still pick who they are on their side
  ([UC-MAIN-010](UC-MAIN-010-respond-to-invitation.md), [ADR-0043](../../adr/0043-invitee-chosen-managed-member-promotion.md)).

## Exceptions

- **The actor holds only the `member` role** → the action is not offered, and the endpoint rejects it
  server-side. The UI hiding a button is not the control
  ([NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)).
- **The address is malformed** → rejected as invalid format. This is a syntactic check on input the
  actor typed and reveals nothing about who exists.
- **Rate limit exceeded** → the actor is told they have sent too many invitations and when they may
  continue. The limit is per inviter, and it is what stops the identical response in step 3 from being
  enumerated by volume instead of by content.
- **Mail delivery fails** → the actor is **not** told. A delivery error surfaced to the inviter would
  distinguish deliverable from undeliverable addresses, and the invitation stays pending and resendable
  either way.
- **The device is offline** → inviting is unavailable; nothing is queued locally, because an
  invitation the server has not accepted has no expiry and no rate-limit accounting.

## Non-functional notes

- **Offline/local-first:** online-only.
- **Localization:** the mail is sent in the **recipient's** preferred language when the address has an
  account, and in the **inviter's** language otherwise — the inviter knows the person and is the best
  available guess. Only the recipient ever sees the mail, so the difference leaks nothing
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
- **Privacy:** the mail names the circle and the inviter, because a stranger's invitation with neither
  is indistinguishable from phishing. It never names other members.
- **Performance:** the mail is queued, not awaited — both for responsiveness and because a path that
  waits for SMTP while another does not is a measurable timing oracle (NFR-005).

## Open questions

- **Invitation lifetime and the rate-limit numbers** (per inviter, per address, per circle) → fixed in
  the M2 feature spec. [ADR-0018](../../adr/0018-circle-and-sharing-model.md) requires them to exist
  but deliberately leaves the values open.
- **May someone be invited straight as an admin?** Recommendation: **no** — invitations always create a
  `member` membership, and promotion is a separate, deliberate act by the owner
  ([UC-MAIN-012](UC-MAIN-012-manage-circle-people.md)). It keeps the accept step free of privilege
  negotiation.

## Traceability

- Requirement: [FR-017](../../requirements/functional/FR-017-invite-to-circle.md); constrained by
  [NFR-005](../../requirements/non-functional/NFR-005-account-enumeration-resistance.md),
  [NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md),
  [NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)
- ADR: [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (decision D5 — never reveal existence)
- Glossary: [Invitation](../../glossary/invitation.md), [Circle](../../glossary/circle.md),
  [Circle membership](../../glossary/circle-membership.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md), decision D5
