---
term: Invitation
contexts: [Circles]
status: Draft
---

# Invitation

## Definition

A pending offer to join a [Circle](circle.md), issued by an owner or admin and addressed to an email
address. Accepting it creates a [Circle membership](circle-membership.md); declining, revoking or
letting it expire leaves no trace in the circle.

An invitation resolves down one of two paths, decided on the **recipient's** side:

- the address has no account → the recipient signs up, and lands in the circle;
- the address already has a [User](user.md) → the recipient sees a **join request** in the app and
  accepts or declines it.

An invitation has an expiry, and can be resent or revoked by an admin. Issuing invitations is
rate-limited per inviter.

## Privacy constraint

The inviter is **never** told which of the two paths applied — the UI says *"invitation sent"* either
way. Reporting "this address already has an account" would turn the invite form into an
account-enumeration oracle, which
[security & privacy guidelines](../guidelines/security-privacy-guidelines.md) §1 rules out. This is a
deliberate UX cost, accepted in decision D5.

## Bounded context(s)

- **Circles:** an entity within the circle aggregate, distinct from the membership it may become.
  [Managed members](managed-member.md) are added directly by an admin and never go through an
  invitation.

## Synonyms / do-not-use

- **Synonyms:** invite (informal), join request (the same invitation seen from the recipient's side
  when they already have an account).
- **Do NOT use:** "request" alone (ambiguous with HTTP requests in code); "referral"; "share link" —
  invite-by-link is a parked idea, not this concept.

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Einladung |
| EN | Invitation |
| FR | Invitation |

## Related terms

- [Circle](circle.md), [Circle membership](circle-membership.md), [User](user.md), [Managed member](managed-member.md)

## First appearance

- [Brainstorming — Accounts, families & sync (v2)](../brainstorming/2026-07-24-accounts-families-sync.md), decision D5
